using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TrackerMultimedia.Contracts.Auth;
using TrackerMultimedia.Data;
using TrackerMultimedia.Domain.Entities;
using TrackerMultimedia.Infrastructure.Options;
using TrackerMultimedia.Services;

namespace TrackerMultimedia.Controllers;

[ApiController]
[Route("api/auth")]
public class OAuthController(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    AuthSessionService sessionService,
    IOptions<OAuthOptions> oauthOptions,
    ILogger<OAuthController> logger,
    IGoogleAuthService? googleAuth = null,
    IGitHubAuthService? githubAuth = null) : ControllerBase
{
    // -------------------------------------------------------------------------
    // GET /api/auth/{provider}/init   — inicia el flujo OAuth
    // -------------------------------------------------------------------------

    [HttpGet("{provider}/init")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<OAuthInitResponse>> Init(
        string provider,
        [FromQuery] string? returnPath,
        CancellationToken cancellationToken)
    {
        var opts = oauthOptions.Value;
        var normalizedProvider = provider.ToLowerInvariant();

        if (normalizedProvider == "google" && !opts.Google.Enabled)
            return NotFound("El proveedor Google no está habilitado.");
        if (normalizedProvider == "github" && !opts.GitHub.Enabled)
            return NotFound("El proveedor GitHub no está habilitado.");
        if (normalizedProvider != "google" && normalizedProvider != "github")
            return BadRequest("Proveedor no soportado.");

        // Generar state anti-CSRF
        var stateValue = GenerateSecureToken();
        var ttl = normalizedProvider == "google"
            ? opts.Google.StateTtlMinutes
            : opts.GitHub.StateTtlMinutes;

        dbContext.OAuthStates.Add(new OAuthState
        {
            StateValue = stateValue,
            Provider = normalizedProvider,
            ReturnPath = returnPath,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(ttl),
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        var authUrl = normalizedProvider switch
        {
            "google" => googleAuth!.BuildAuthorizationUrl(stateValue),
            "github" => githubAuth!.BuildAuthorizationUrl(stateValue),
            _ => throw new InvalidOperationException(),
        };

        return Ok(new OAuthInitResponse(authUrl));
    }

    // -------------------------------------------------------------------------
    // GET /api/auth/{provider}/callback   — callback del proveedor
    // -------------------------------------------------------------------------

    [HttpGet("{provider}/callback")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Callback(
        string provider,
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken cancellationToken)
    {
        var frontendBase = HttpContext.RequestServices
            .GetRequiredService<IConfiguration>()["App:FrontendBaseUrl"] ?? "http://localhost:5173";

        if (!string.IsNullOrEmpty(error))
        {
            logger.LogWarning("OAuth callback con error del proveedor {Provider}: {Error}", provider, error);
            return Redirect(BuildErrorRedirect(frontendBase, "access_denied"));
        }

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return Redirect(BuildErrorRedirect(frontendBase, "invalid_callback"));

        // Validar state
        var normalizedProvider = provider.ToLowerInvariant();
        var storedState = await dbContext.OAuthStates
            .FirstOrDefaultAsync(s =>
                s.StateValue == state &&
                s.Provider == normalizedProvider &&
                !s.IsUsed &&
                s.ExpiresAtUtc > DateTime.UtcNow,
                cancellationToken);

        if (storedState is null)
        {
            logger.LogWarning("State OAuth inválido o expirado para proveedor {Provider}", normalizedProvider);
            return Redirect(BuildErrorRedirect(frontendBase, "state_mismatch"));
        }

        // Marcar el state como usado (no se puede reutilizar)
        storedState.IsUsed = true;
        await dbContext.SaveChangesAsync(cancellationToken);

        // Intercambiar código por perfil externo
        ExternalUserProfile profile;
        try
        {
            profile = normalizedProvider switch
            {
                "google" => await googleAuth!.ExchangeCodeAsync(code, cancellationToken),
                "github" => await githubAuth!.ExchangeCodeAsync(code, cancellationToken),
                _ => throw new InvalidOperationException("Proveedor no soportado."),
            };
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Error al obtener perfil de {Provider}: {Message}", normalizedProvider, ex.Message);
            return Redirect(BuildErrorRedirect(frontendBase, "profile_error", ex.Message));
        }

        // Resolver cuenta local
        var loginProvider = CapitalizeProvider(normalizedProvider);
        var existingLoginUser = await userManager.FindByLoginAsync(loginProvider, profile.ProviderUserId);

        if (existingLoginUser is not null)
        {
            // Caso A: login externo ya vinculado → emitir sesión
            logger.LogInformation("Login OAuth {Provider}: usuario {UserId} ya vinculado", normalizedProvider, existingLoginUser.Id);
            var session = await sessionService.CreateSessionAsync(existingLoginUser, cancellationToken);
            return Redirect(BuildSuccessRedirect(frontendBase, storedState.ReturnPath, session));
        }

        var emailUser = await userManager.FindByEmailAsync(profile.Email);

        if (emailUser is null)
        {
            // Caso B: email no existe → crear cuenta nueva sin contraseña
            var newUser = new ApplicationUser
            {
                Email = profile.Email,
                UserName = profile.Email,
                DisplayName = profile.DisplayName ?? profile.Email.Split('@')[0],
                EmailConfirmed = true, // email ya verificado por el proveedor
            };

            var createResult = await userManager.CreateAsync(newUser);
            if (!createResult.Succeeded)
            {
                logger.LogError("Error al crear usuario OAuth {Email}: {Errors}",
                    profile.Email, string.Join(", ", createResult.Errors.Select(e => e.Code)));
                return Redirect(BuildErrorRedirect(frontendBase, "create_failed"));
            }

            await userManager.AddLoginAsync(newUser, new UserLoginInfo(loginProvider, profile.ProviderUserId, loginProvider));
            logger.LogInformation("Cuenta creada vía OAuth {Provider}: {UserId}", normalizedProvider, newUser.Id);

            var session = await sessionService.CreateSessionAsync(newUser, cancellationToken);
            return Redirect(BuildSuccessRedirect(frontendBase, storedState.ReturnPath, session));
        }

        // Caso C: email ya existe en cuenta manual → vinculación explícita requerida
        var linkToken = GenerateSecureToken();
        storedState.LinkToken = linkToken;
        storedState.PendingProviderKey = profile.ProviderUserId;
        storedState.PendingEmail = profile.Email;
        storedState.PendingDisplayName = profile.DisplayName;
        // Extender el TTL para dar tiempo al usuario a completar la vinculación
        storedState.IsUsed = false; // se reutiliza como registro de vinculación
        storedState.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Vinculación requerida para {Email} vía {Provider}", profile.Email, normalizedProvider);
        return Redirect(
            $"{frontendBase}/link-account" +
            $"?link_token={Uri.EscapeDataString(linkToken)}" +
            $"&provider={Uri.EscapeDataString(normalizedProvider)}" +
            $"&email={Uri.EscapeDataString(profile.Email)}");
    }

    // -------------------------------------------------------------------------
    // POST /api/auth/link-confirm   — confirma la vinculación explícita
    // -------------------------------------------------------------------------

    [HttpPost("link-confirm")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> LinkConfirm(
        [FromBody] OAuthLinkConfirmRequest request,
        CancellationToken cancellationToken)
    {
        var linkState = await dbContext.OAuthStates
            .FirstOrDefaultAsync(s =>
                s.LinkToken == request.LinkToken &&
                s.Provider == request.Provider.ToLowerInvariant() &&
                s.PendingEmail == request.Email.ToLowerInvariant() &&
                !s.IsUsed &&
                s.ExpiresAtUtc > DateTime.UtcNow,
                cancellationToken);

        if (linkState is null)
            return BadRequest("El token de vinculación no es válido o ha expirado.");

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null) return BadRequest("Usuario no encontrado.");

        if (await userManager.IsLockedOutAsync(user))
            return Unauthorized("Cuenta bloqueada temporalmente.");

        var passwordOk = await userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordOk)
        {
            await userManager.AccessFailedAsync(user);
            return Unauthorized("Credenciales inválidas.");
        }

        await userManager.ResetAccessFailedCountAsync(user);

        // Comprobar que el login externo no esté ya vinculado a otra cuenta
        var loginProvider = CapitalizeProvider(request.Provider);
        var existingUser = await userManager.FindByLoginAsync(loginProvider, linkState.PendingProviderKey!);
        if (existingUser is not null && existingUser.Id != user.Id)
            return Conflict($"Este proveedor ya está vinculado a otra cuenta.");

        if (existingUser is null)
        {
            var addResult = await userManager.AddLoginAsync(
                user, new UserLoginInfo(loginProvider, linkState.PendingProviderKey!, loginProvider));

            if (!addResult.Succeeded)
            {
                logger.LogError("Error al vincular {Provider} a usuario {UserId}: {Errors}",
                    request.Provider, user.Id, string.Join(", ", addResult.Errors.Select(e => e.Code)));
                return StatusCode(500, "No se pudo completar la vinculación.");
            }
        }

        // Marcar el registro de vinculación como consumido
        linkState.IsUsed = true;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Cuenta {UserId} vinculada a {Provider}", user.Id, request.Provider);
        var session = await sessionService.CreateSessionAsync(user, cancellationToken);
        return Ok(session);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string GenerateSecureToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static string CapitalizeProvider(string provider) =>
        char.ToUpper(provider[0]) + provider[1..];

    /// <summary>
    /// Construye la URL de redirección al frontend con los tokens de sesión.
    /// Los tokens viajan en el fragment (#) para que no queden en server logs.
    /// El frontend los lee desde window.location.hash al montar el callback.
    /// </summary>
    private static string BuildSuccessRedirect(string frontendBase, string? returnPath, AuthResponse session)
    {
        var target = string.IsNullOrEmpty(returnPath) ? "/library" : returnPath;
         var user = Uri.EscapeDataString(JsonSerializer.Serialize(session.User));

        return $"{frontendBase}/oauth-callback" +
               $"#access_token={Uri.EscapeDataString(session.AccessToken)}" +
               $"&refresh_token={Uri.EscapeDataString(session.RefreshToken)}" +
               $"&expires_in={session.ExpiresIn}" +
             $"&return_path={Uri.EscapeDataString(target)}" +
             $"&user={user}";
    }

    private static string BuildErrorRedirect(string frontendBase, string errorCode, string? errorMessage = null)
    {
        var query = new List<string>
        {
            $"oauth_error={Uri.EscapeDataString(errorCode)}"
        };

        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            query.Add($"oauth_error_message={Uri.EscapeDataString(errorMessage)}");
        }

        return $"{frontendBase}/login?{string.Join("&", query)}";
    }
}
