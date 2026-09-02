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

    /// <summary>
    /// Valida que returnPath sea una ruta relativa simple y segura (e.g. "/dashboard").
    /// Rechaza rutas absolutas, protocolo-relativas o cadenas excesivamente largas.
    /// </summary>
    private static bool IsValidReturnPath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return true; // null/vacío es válido (se usa la ruta por defecto)
        if (path.Length > 200) return false;
        // Debe empezar con '/' pero no con '//' (protocolo-relativo)
        if (!path.StartsWith('/') || path.StartsWith("//")) return false;
        // No permitir caracteres de control ni secuencias de escape de URL para protocolos
        if (path.Contains('\n') || path.Contains('\r') || path.Contains('\0')) return false;
        return true;
    }

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

        if (!IsValidReturnPath(returnPath))
            return BadRequest("returnPath no es una ruta relativa válida.");

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
            // El detalle se queda en el log. Lo que llega al usuario es un código:
            // el mensaje de una excepción puede describir la infraestructura interna
            // y acababa en la barra de direcciones, en el historial del navegador y
            // en cualquier sitio donde se pegue esa URL.
            logger.LogWarning(ex, "Error al obtener el perfil de {Provider}.", normalizedProvider);
            return Redirect(BuildErrorRedirect(frontendBase, "profile_error"));
        }

        // Resolver cuenta local
        var loginProvider = CapitalizeProvider(normalizedProvider);
        var existingLoginUser = await userManager.FindByLoginAsync(loginProvider, profile.ProviderUserId);

        if (existingLoginUser is not null)
        {
            // Caso A: login externo ya vinculado → emitir sesión.
            // Las mismas condiciones que exige el login con contraseña. Sin esto,
            // entrar con Google o GitHub rodeaba el bloqueo por intentos fallidos:
            // la cuenta quedaba bloqueada para la contraseña y abierta para OAuth.
            if (await userManager.IsLockedOutAsync(existingLoginUser) || !existingLoginUser.EmailConfirmed)
            {
                logger.LogWarning(
                    "Login OAuth rechazado por estado de la cuenta {UserId} (bloqueada o correo sin confirmar).",
                    existingLoginUser.Id);
                return Redirect(BuildErrorRedirect(frontendBase, "account_unavailable"));
            }

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
        // El `state` original se queda consumido. Antes se resucitaba poniendo
        // IsUsed=false y ampliando su caducidad, lo que contradecía el "solo se
        // puede consumir una vez" que documenta la propia entidad y dejaba el
        // valor del state válido otros 15 minutos para un segundo callback.
        // La vinculación pendiente va en una fila nueva, con su propio token.
        dbContext.OAuthStates.Add(new OAuthState
        {
            // Valor irrepetible y sin relación con el state original: esta fila se
            // busca por LinkToken, no por StateValue.
            StateValue = GenerateSecureToken(),
            Provider = normalizedProvider,
            ReturnPath = storedState.ReturnPath,
            IsUsed = false,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15),
            LinkToken = linkToken,
            PendingProviderKey = profile.ProviderUserId,
            PendingEmail = profile.Email,
            PendingDisplayName = profile.DisplayName,
        });
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

        if (await userManager.IsLockedOutAsync(user) || !user.EmailConfirmed)
        {
            // Igual que en login y en el callback: bloqueo y correo sin confirmar
            // se comprueban en los tres caminos, o la regla se rodea por el más débil.
            logger.LogWarning(
                "Vinculación rechazada por estado de la cuenta {UserId} (bloqueada o correo sin confirmar).",
                user.Id);
            return Unauthorized("No se pudo completar la vinculación.");
        }

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

    /// <summary>
    /// Construye la redirección de error al frontend. Solo viaja un **código**:
    /// el parámetro de mensaje libre se eliminó porque se usaba para propagar
    /// mensajes de excepción internos hasta la URL del navegador.
    /// </summary>
    private static string BuildErrorRedirect(string frontendBase, string errorCode)
        => $"{frontendBase}/login?oauth_error={Uri.EscapeDataString(errorCode)}";
}
