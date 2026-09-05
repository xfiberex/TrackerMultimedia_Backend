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
    /// Genera un par de tokens, persiste el refresh token hasheado y devuelve las dos
    /// mitades por separado: la que se envía en el cuerpo y la que va a la cookie.
    ///
    /// Van separadas a propósito. El token de refresco en claro solo debe pasar por el
    /// controlador que escribe la cookie; si viviera dentro del <see cref="AuthResponse"/>
    /// bastaría con serializar la respuesta para devolverlo al cliente por accidente, que
    /// es exactamente lo que hacía antes de T4-01.
    /// </summary>
    public async Task<IssuedSession> CreateSessionAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default)
    {
        var accessToken = tokenService.GenerateAccessToken(user);
        var (rawToken, tokenHash) = tokenService.GenerateRefreshToken();
        var refreshLifetime = TimeSpan.FromDays(tokenService.GetRefreshTokenLifetimeDays());

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            TokenHash = tokenHash,
            UserId = user.Id,
            ExpiresAtUtc = DateTime.UtcNow.Add(refreshLifetime),
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        var linkedProviders = await GetLinkedProvidersAsync(user.Id, cancellationToken);

        var response = new AuthResponse(
            accessToken,
            tokenService.GetAccessTokenLifetimeMinutes() * 60,
            ToUserResponse(user, linkedProviders));

        return new IssuedSession(response, rawToken, refreshLifetime);
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

/// <summary>
/// Una sesión recién emitida, con sus dos mitades separadas por destino.
/// </summary>
/// <param name="Response">Lo que se serializa al cliente. No contiene el refresh.</param>
/// <param name="RefreshToken">El valor en claro, solo para escribirlo en la cookie.</param>
/// <param name="RefreshLifetime">
/// Vida del refresh en base de datos. La cookie caduca a la vez: una cookie que sobreviva
/// al token solo consigue que el navegador mande algo que el servidor ya rechaza.
/// </param>
public record IssuedSession(AuthResponse Response, string RefreshToken, TimeSpan RefreshLifetime);
