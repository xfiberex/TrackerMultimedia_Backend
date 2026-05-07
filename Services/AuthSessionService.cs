using Microsoft.EntityFrameworkCore;
using TrackerMultimedia.Contracts.Auth;
using TrackerMultimedia.Data;
using TrackerMultimedia.Domain.Entities;

namespace TrackerMultimedia.Services;

/// <summary>
/// Centraliza la emisión de una sesión (JWT + RefreshToken) para cualquier
/// camino de acceso: login manual, OAuth de Google, OAuth de GitHub.
/// </summary>
public class AuthSessionService(
    TokenService tokenService,
    ApplicationDbContext dbContext)
{
    /// <summary>
    /// Genera un par de tokens, persiste el refresh token hasheado y devuelve
    /// el <see cref="AuthResponse"/> listo para enviar al cliente.
    /// </summary>
    public async Task<AuthResponse> CreateSessionAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default)
    {
        var accessToken = tokenService.GenerateAccessToken(user);
        var (rawToken, tokenHash) = tokenService.GenerateRefreshToken();

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            TokenHash = tokenHash,
            UserId = user.Id,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(tokenService.GetRefreshTokenLifetimeDays()),
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new AuthResponse(
            accessToken,
            rawToken,
            tokenService.GetAccessTokenLifetimeMinutes() * 60,
            ToUserResponse(user));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    public static UserResponse ToUserResponse(ApplicationUser user)
    {
        var logins = user.ExternalLogins?
            .Select(l => l.LoginProvider.ToLowerInvariant())
            .ToList()
            ?? [];

        return new UserResponse(
            user.Id,
            user.Email!,
            user.DisplayName ?? user.Email!.Split('@')[0],
            user.EmailConfirmed,
            !string.IsNullOrEmpty(user.PasswordHash),
            logins);
    }
}
