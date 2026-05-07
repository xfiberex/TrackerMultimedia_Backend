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
    // -------------------------------------------------------------------------
    // POST /api/auth/register
    // -------------------------------------------------------------------------

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<UserResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

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
            logger.LogWarning("Registro fallido para {Email}: {Errors}",
                email, string.Join(", ", result.Errors.Select(e => e.Code)));
            foreach (var error in result.Errors)
                ModelState.AddModelError(error.Code, error.Description);
            return ValidationProblem(ModelState);
        }

        logger.LogInformation("Nuevo usuario registrado: {UserId} ({Email})", user.Id, email);
        await SendConfirmationEmailAsync(user, cancellationToken);
        return CreatedAtAction(nameof(Me), AuthSessionService.ToUserResponse(user));
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

        // No diferenciamos entre "usuario no existe" y "contraseña incorrecta"
        // para no revelar si un email está registrado.
        if (user is null)
        {
            logger.LogWarning("Intento de login con email desconocido: {Email}", email);
            return Unauthorized("Credenciales inválidas.");
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            logger.LogWarning("Login bloqueado para usuario {UserId} ({Email})", user.Id, email);
            return Unauthorized("Cuenta bloqueada temporalmente. Inténtalo más tarde.");
        }

        // El usuario debe confirmar su email antes de poder iniciar sesión.
        if (!user.EmailConfirmed)
        {
            logger.LogWarning("Login denegado: email no confirmado para {UserId} ({Email})", user.Id, email);
            return Unauthorized("Debes confirmar tu dirección de correo antes de iniciar sesión.");
        }

        var passwordOk = await userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordOk)
        {
            await userManager.AccessFailedAsync(user);
            logger.LogWarning("Contraseña incorrecta para usuario {UserId} ({Email}). Intentos: {Count}",
                user.Id, email, await userManager.GetAccessFailedCountAsync(user));
            return Unauthorized("Credenciales inválidas.");
        }

        await userManager.ResetAccessFailedCountAsync(user);
        logger.LogInformation("Login exitoso: {UserId} ({Email})", user.Id, email);

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

        if (storedToken is null || storedToken.IsRevoked || storedToken.ExpiresAtUtc < DateTime.UtcNow)
        {
            logger.LogWarning("Refresh token inválido o expirado. Hash: {Hash}", tokenHash[..8]);
            return Unauthorized("Refresh token inválido o expirado.");
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

        if (userId is null || !Guid.TryParse(userId, out _))
            return Unauthorized();

        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return Unauthorized();
        await userManager.GetLoginsAsync(user); // precarga por navegación no cargada en este path
        return Ok(AuthSessionService.ToUserResponse(user));
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
        if (userId is null) return Unauthorized();

        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return Unauthorized();

        if (!string.IsNullOrWhiteSpace(request.DisplayName))
            user.DisplayName = request.DisplayName.Trim();

        await userManager.UpdateAsync(user);
        return Ok(AuthSessionService.ToUserResponse(user));
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
            logger.LogWarning("Solicitud de recuperación para email no registrado: {Email}", email);
            return Ok(new { message = "Si ese correo está registrado, recibirás las instrucciones." });
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        logger.LogInformation("Token de recuperación generado para usuario {UserId}", user.Id);

        var frontendBase = configuration["App:FrontendBaseUrl"] ?? "http://localhost:5173";
        var resetUrl = $"{frontendBase}/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";

        await emailService.SendAsync(
            to: email,
            subject: "Restablecer contraseña – TrackerMultimedia",
            htmlBody: EmailTemplates.ResetPassword(user.DisplayName ?? email.Split('@')[0], resetUrl),
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

    private async Task SendConfirmationEmailAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var frontendBase = configuration["App:FrontendBaseUrl"] ?? "http://localhost:5173";
        var confirmUrl = $"{frontendBase}/confirm-email?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}";

        await emailService.SendAsync(
            to: user.Email!,
            subject: "Confirma tu cuenta – TrackerMultimedia",
            htmlBody: EmailTemplates.ConfirmEmail(user.DisplayName ?? user.Email!.Split('@')[0], confirmUrl),
            cancellationToken);

        logger.LogInformation("Email de confirmación enviado a {UserId} ({Email})", user.Id, user.Email);
    }
}
