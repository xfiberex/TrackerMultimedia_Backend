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

        var linkedProviders = await GetLinkedProvidersAsync(user.Id, cancellationToken);

        return new AuthResponse(
            accessToken,
            rawToken,
            tokenService.GetAccessTokenLifetimeMinutes() * 60,
            ToUserResponse(user, linkedProviders));
    }

    /// <summary>
    /// Devuelve en minúsculas los proveedores externos vinculados a un usuario.
    /// Se consulta la tabla directamente en lugar de usar la navegación porque
    /// <c>UserManager.GetLoginsAsync</c> proyecta a <c>UserLoginInfo</c> y no
    /// materializa entidades rastreadas, así que no rellena la colección.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetLinkedProvidersAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var providers = await dbContext.UserLogins
            .AsNoTracking()
            .Where(login => login.UserId == userId)
            .Select(login => login.LoginProvider)
            .ToListAsync(cancellationToken);

        return providers
            .Select(provider => provider.ToLowerInvariant())
            .ToList();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <param name="linkedProviders">
    /// Proveedores externos ya cargados. Si es null se usa la navegación, que solo
    /// está poblada cuando el llamador hizo un Include explícito.
    /// </param>
    public static UserResponse ToUserResponse(
        ApplicationUser user,
        IReadOnlyList<string>? linkedProviders = null)
    {
        var logins = linkedProviders
            ?? user.ExternalLogins?
                .Select(login => login.LoginProvider.ToLowerInvariant())
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
