using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Web;
using Microsoft.AspNetCore.Authorization;
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
public class AuthController(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext dbContext,
    TokenService tokenService,
    AuthSessionService sessionService,
    IEmailService emailService,
    IOptions<OAuthOptions> oauthOptions,
    IConfiguration configuration,
    ILogger<AuthController> logger) : ControllerBase
{
    /// <summary>
    /// Respuesta única del registro. Es idéntica exista o no el correo, para que la
    /// petición no sirva para averiguar qué direcciones están dadas de alta.
    /// </summary>
    private const string RegisterAcknowledgement =
        "Si el correo no estaba registrado, recibirás un enlace para confirmar la cuenta.";

    /// <summary>
    /// Respuesta única de todos los fallos de login. Cubre credenciales incorrectas,
    /// cuenta inexistente, correo sin confirmar y bloqueo temporal sin distinguirlos:
    /// cualquier diferencia entre esos casos revela si una dirección tiene cuenta.
    /// </summary>
    /// <summary>
    /// Igual que en login: una sola respuesta para todos los motivos. Distinguir
    /// «token desconocido» de «token caducado» o de «cuenta bloqueada» le diría a
    /// quien tenga un token robado en qué estado está la cuenta.
    /// </summary>
    private const string RefreshFailureMessage = "Sesión no válida. Vuelve a iniciar sesión.";

    private const string LoginFailureMessage =
        "No se pudo iniciar sesión. Comprueba el correo y la contraseña, confirma tu cuenta " +
        "si acabas de registrarte, o espera unos minutos si has fallado varias veces.";

    // -------------------------------------------------------------------------
    // POST /api/auth/register
    // -------------------------------------------------------------------------

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            // A quien envía la petición se le responde exactamente igual que en un alta
            // correcta. Es al titular real de la dirección a quien se le avisa, por correo.
            logger.LogWarning("Intento de registro sobre un correo ya dado de alta: {UserId}", existingUser.Id);
            await SendAccountExistsEmailAsync(existingUser, cancellationToken);
            return Accepted(new { message = RegisterAcknowledgement });
        }

        var user = new ApplicationUser
        {
            Email = email,
            UserName = email,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? email.Split('@')[0]
                : request.DisplayName.Trim(),
        };

        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            // Si lo único que falló fue el duplicado, es una carrera con otra petición
            // simultánea: se responde como si el alta hubiera ido bien, igual que arriba.
            if (result.Errors.All(IsDuplicateError))
                return Accepted(new { message = RegisterAcknowledgement });

            logger.LogWarning("Registro fallido: {Errors}",
                string.Join(", ", result.Errors.Select(error => error.Code)));

            foreach (var error in result.Errors.Where(error => !IsDuplicateError(error)))
                ModelState.AddModelError(error.Code, error.Description);

            return ValidationProblem(ModelState);
        }

        logger.LogInformation("Nuevo usuario registrado: {UserId}", user.Id);
        await SendConfirmationEmailAsync(user, cancellationToken);
        return Accepted(new { message = RegisterAcknowledgement });
    }

    // -------------------------------------------------------------------------
    // POST /api/auth/login
    // -------------------------------------------------------------------------

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(email);

        // Todos los motivos de rechazo devuelven el MISMO mensaje y el mismo código.
        // Distinguir "cuenta bloqueada" o "correo sin confirmar" de "credenciales
        // inválidas" permitiría averiguar qué direcciones tienen cuenta. El enlace de
        // reenvío de confirmación está siempre visible en la pantalla de login, así que
        // el usuario legítimo tiene salida sin necesidad de un mensaje específico.
        if (user is null)
        {
            logger.LogWarning("Intento de login con un correo sin cuenta.");
            return Unauthorized(LoginFailureMessage);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            logger.LogWarning("Login rechazado por bloqueo temporal: {UserId}", user.Id);
            return Unauthorized(LoginFailureMessage);
        }

        var passwordOk = await userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordOk)
        {
            await userManager.AccessFailedAsync(user);
            logger.LogWarning("Contraseña incorrecta para el usuario {UserId}. Intentos: {Count}",
                user.Id, await userManager.GetAccessFailedCountAsync(user));
            return Unauthorized(LoginFailureMessage);
        }

        // El correo debe estar confirmado. Se comprueba después de la contraseña para que
        // un fallo de credenciales y una cuenta sin confirmar consuman el mismo trabajo.
        if (!user.EmailConfirmed)
        {
            logger.LogWarning("Login rechazado: correo sin confirmar para {UserId}", user.Id);
            return Unauthorized(LoginFailureMessage);
        }

        await userManager.ResetAccessFailedCountAsync(user);
        logger.LogInformation("Login correcto: {UserId}", user.Id);

        var authResponse = await sessionService.CreateSessionAsync(user, cancellationToken);
        return Ok(authResponse);
    }

    // -------------------------------------------------------------------------
    // POST /api/auth/refresh
    // -------------------------------------------------------------------------

    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken)
    {
        var tokenHash = tokenService.ComputeHash(request.RefreshToken);

        var storedToken = await dbContext.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null)
        {
            logger.LogWarning("Refresh rechazado: token desconocido.");
            return Unauthorized(RefreshFailureMessage);
        }

        // Detección de reutilización. Los tokens rotan: al usar uno se revoca en el
        // acto, así que presentar uno ya revocado significa que existen dos copias
        // del mismo token y una de ellas no está en manos del usuario legítimo. No
        // se puede saber cuál, así que se cierran todas las sesiones y quien de
        // verdad sea el dueño vuelve a entrar con su contraseña.
        if (storedToken.IsRevoked)
        {
            var revokedCount = await dbContext.RefreshTokens
                .Where(rt => rt.UserId == storedToken.UserId && !rt.IsRevoked)
                .ExecuteUpdateAsync(setters => setters.SetProperty(rt => rt.IsRevoked, true), cancellationToken);

            logger.LogWarning(
                "Reutilización de token de refresco detectada para el usuario {UserId}. " +
                "Se han revocado {Count} sesión(es) activa(s).",
                storedToken.UserId,
                revokedCount);

            return Unauthorized(RefreshFailureMessage);
        }

        if (storedToken.ExpiresAtUtc < DateTime.UtcNow)
        {
            logger.LogInformation("Refresh rechazado: token caducado para el usuario {UserId}.", storedToken.UserId);
            return Unauthorized(RefreshFailureMessage);
        }

        // El estado de la cuenta se comprueba también aquí. Sin esto, bloquear a un
        // usuario o revocarle la confirmación del correo no tenía efecto hasta que
        // caducase su token de refresco: podía seguir renovando la sesión durante días.
        if (await userManager.IsLockedOutAsync(storedToken.User) || !storedToken.User.EmailConfirmed)
        {
            storedToken.IsRevoked = true;
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogWarning(
                "Refresh rechazado por estado de la cuenta {UserId} (bloqueada o correo sin confirmar).",
                storedToken.UserId);
            return Unauthorized(RefreshFailureMessage);
        }

        // Rotación: invalidar el token usado y emitir uno nuevo
        storedToken.IsRevoked = true;
        logger.LogInformation("Token refrescado para usuario {UserId}", storedToken.User.Id);

        var authResponse = await sessionService.CreateSessionAsync(storedToken.User, cancellationToken);
        return Ok(authResponse);
    }

    // -------------------------------------------------------------------------
    // POST /api/auth/logout
    // -------------------------------------------------------------------------

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody] RefreshRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is not null && !string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var tokenHash = tokenService.ComputeHash(request.RefreshToken);
            var storedToken = await dbContext.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

            if (storedToken is not null)
            {
                storedToken.IsRevoked = true;
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Logout: sesión revocada para usuario {UserId}", storedToken.UserId);
            }
        }

        return NoContent();
    }

    // -------------------------------------------------------------------------
    // POST /api/auth/logout-all  (requiere autenticación)
    // -------------------------------------------------------------------------

    [HttpPost("logout-all")]
    [Authorize]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (userId is null || !Guid.TryParse(userId, out var parsedId))
            return Unauthorized();

        var revoked = await dbContext.RefreshTokens
            .Where(rt => rt.UserId == parsedId && !rt.IsRevoked)
            .ExecuteUpdateAsync(setters => setters.SetProperty(rt => rt.IsRevoked, true), cancellationToken);

        logger.LogInformation("Logout-all: {Count} sesiones revocadas para usuario {UserId}", revoked, parsedId);
        return NoContent();
    }

    // -------------------------------------------------------------------------
    // GET /api/auth/me  (requiere autenticación)
    // -------------------------------------------------------------------------

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (userId is null || !Guid.TryParse(userId, out var parsedUserId))
            return Unauthorized();

        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return Unauthorized();

        var linkedProviders = await sessionService.GetLinkedProvidersAsync(parsedUserId, cancellationToken);
        return Ok(AuthSessionService.ToUserResponse(user, linkedProviders));
    }

    // -------------------------------------------------------------------------
    // POST /api/auth/change-password  (requiere autenticación)
    // -------------------------------------------------------------------------

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (userId is null) return Unauthorized();

        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return Unauthorized();

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            logger.LogWarning("Cambio de contraseña fallido para usuario {UserId}", userId);
            foreach (var error in result.Errors)
                ModelState.AddModelError(error.Code, error.Description);
            return ValidationProblem(ModelState);
        }

        logger.LogInformation("Contraseña cambiada correctamente para usuario {UserId}", userId);
        return NoContent();
    }

    // -------------------------------------------------------------------------
    // PUT /api/auth/profile  (requiere autenticación)
    // -------------------------------------------------------------------------

    [HttpPut("profile")]
    [Authorize]
    public async Task<ActionResult<UserResponse>> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (userId is null || !Guid.TryParse(userId, out var parsedUserId))
            return Unauthorized();

        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return Unauthorized();

        if (!string.IsNullOrWhiteSpace(request.DisplayName))
            user.DisplayName = request.DisplayName.Trim();

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            logger.LogWarning("Actualización de perfil fallida para usuario {UserId}: {Errors}",
                parsedUserId, string.Join(", ", updateResult.Errors.Select(error => error.Code)));
            foreach (var error in updateResult.Errors)
                ModelState.AddModelError(error.Code, error.Description);
            return ValidationProblem(ModelState);
        }

        var linkedProviders = await sessionService.GetLinkedProvidersAsync(parsedUserId, cancellationToken);
        return Ok(AuthSessionService.ToUserResponse(user, linkedProviders));
    }

    // -------------------------------------------------------------------------
    // POST /api/auth/forgot-password
    // -------------------------------------------------------------------------

    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(email);

        // Respuesta idéntica tanto si el usuario existe como si no (evita enumeración de emails)
        if (user is null)
        {
            logger.LogWarning("Solicitud de recuperación para un correo sin cuenta.");
            return Ok(new { message = "Si ese correo está registrado, recibirás las instrucciones." });
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        logger.LogInformation("Token de recuperación generado para usuario {UserId}", user.Id);

        var resetUrl = $"{FrontendBaseUrl}/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";

        await SendEmailSafelyAsync(
            user,
            "Restablecer contraseña – TrackerMultimedia",
            EmailTemplates.ResetPassword(user.DisplayName ?? email.Split('@')[0], resetUrl),
            "recuperación de contraseña",
            cancellationToken);

        return Ok(new { message = "Si ese correo está registrado, recibirás las instrucciones." });
    }

    // -------------------------------------------------------------------------
    // POST /api/auth/reset-password
    // -------------------------------------------------------------------------

    [HttpPost("reset-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
            return BadRequest("El enlace de recuperación no es válido o ha expirado.");

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            logger.LogWarning("Reset de contraseña fallido para usuario {UserId}: {Errors}",
                user.Id, string.Join(", ", result.Errors.Select(e => e.Code)));
            foreach (var error in result.Errors)
                ModelState.AddModelError(error.Code, error.Description);
            return ValidationProblem(ModelState);
        }

        // Revocar todas las sesiones activas por seguridad
        await dbContext.RefreshTokens
            .Where(rt => rt.UserId == user.Id && !rt.IsRevoked)
            .ExecuteUpdateAsync(setters => setters.SetProperty(rt => rt.IsRevoked, true), cancellationToken);

        logger.LogInformation("Contraseña restablecida y sesiones revocadas para usuario {UserId}", user.Id);
        return NoContent();
    }

    // -------------------------------------------------------------------------
    // POST /api/auth/confirm-email
    // -------------------------------------------------------------------------

    [HttpPost("confirm-email")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ConfirmEmail(
        [FromBody] ConfirmEmailRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
            return BadRequest("El enlace de confirmación no es válido o ha expirado.");

        if (user.EmailConfirmed)
            return Ok(new { message = "El correo ya estaba confirmado. Puedes iniciar sesión." });

        var result = await userManager.ConfirmEmailAsync(user, request.Token);
        if (!result.Succeeded)
        {
            logger.LogWarning("Confirmación de email fallida para {UserId}: {Errors}",
                user.Id, string.Join(", ", result.Errors.Select(e => e.Code)));
            return BadRequest("El enlace de confirmación no es válido o ha expirado.");
        }

        logger.LogInformation("Email confirmado para usuario {UserId}", user.Id);
        return Ok(new { message = "Cuenta activada correctamente. Ya puedes iniciar sesión." });
    }

    // -------------------------------------------------------------------------
    // POST /api/auth/resend-confirmation
    // -------------------------------------------------------------------------

    [HttpPost("resend-confirmation")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResendConfirmation(
        [FromBody] ResendConfirmationRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(email);

        // Respuesta genérica para no revelar si el email existe.
        if (user is null || user.EmailConfirmed)
        {
            return Ok(new { message = "Si ese correo está pendiente de confirmación, recibirás un nuevo enlace." });
        }

        await SendConfirmationEmailAsync(user, cancellationToken);
        return Ok(new { message = "Si ese correo está pendiente de confirmación, recibirás un nuevo enlace." });
    }

    // -------------------------------------------------------------------------
    // GET /api/auth/methods  — no requiere autenticación
    // -------------------------------------------------------------------------

    [HttpGet("methods")]
    public IActionResult GetAuthMethods()
    {
        var oauth = oauthOptions.Value;
        return Ok(new AuthMethodsResponse(
            ManualEnabled: true,
            GoogleEnabled: oauth.Google.Enabled,
            GitHubEnabled: oauth.GitHub.Enabled));
    }

    // -------------------------------------------------------------------------
    // Helpers privados
    // -------------------------------------------------------------------------

    private static bool IsDuplicateError(IdentityError error) =>
        error.Code is "DuplicateEmail" or "DuplicateUserName";

    private string FrontendBaseUrl =>
        configuration["App:FrontendBaseUrl"] ?? "http://localhost:5173";

    private async Task SendConfirmationEmailAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmUrl = $"{FrontendBaseUrl}/confirm-email?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}";

        await SendEmailSafelyAsync(
            user,
            "Confirma tu cuenta – TrackerMultimedia",
            EmailTemplates.ConfirmEmail(user.DisplayName ?? user.Email!.Split('@')[0], confirmUrl),
            "confirmación de cuenta",
            cancellationToken);
    }

    private async Task SendAccountExistsEmailAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        await SendEmailSafelyAsync(
            user,
            "Ya tienes una cuenta – TrackerMultimedia",
            EmailTemplates.AccountAlreadyExists(
                user.DisplayName ?? user.Email!.Split('@')[0],
                $"{FrontendBaseUrl}/login",
                $"{FrontendBaseUrl}/forgot-password"),
            "aviso de cuenta existente",
            cancellationToken);
    }

    /// <summary>
    /// Envía un correo sin dejar que un fallo del servidor SMTP tumbe la petición.
    /// Importa sobre todo en el registro: el usuario ya está creado cuando se manda la
    /// confirmación, así que propagar la excepción devolvía un 500 y dejaba una cuenta
    /// existente, sin confirmar y sin forma de continuar.
    /// </summary>
    private async Task SendEmailSafelyAsync(
        ApplicationUser user,
        string subject,
        string htmlBody,
        string purpose,
        CancellationToken cancellationToken)
    {
        try
        {
            await emailService.SendAsync(user.Email!, subject, htmlBody, cancellationToken);
            logger.LogInformation("Correo de {Purpose} enviado al usuario {UserId}", purpose, user.Id);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception,
                "No se pudo enviar el correo de {Purpose} al usuario {UserId}", purpose, user.Id);
        }
    }
}
