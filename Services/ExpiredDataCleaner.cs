using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TrackerMultimedia.Data;
using TrackerMultimedia.Infrastructure.Options;

namespace TrackerMultimedia.Services;

/// <summary>Filas eliminadas en una pasada de purga.</summary>
public readonly record struct CleanupResult(int RefreshTokens, int OAuthStates)
{
    public int Total => RefreshTokens + OAuthStates;
}

public interface IExpiredDataCleaner
{
    Task<CleanupResult> CleanAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Borra tokens de refresco y estados OAuth que ya no sirven para nada.
///
/// Va separado del <see cref="ExpiredDataCleanupService"/> que lo programa para
/// poder ejecutarlo —y comprobarlo— sin depender de un temporizador.
/// </summary>
public sealed class ExpiredDataCleaner(
    ApplicationDbContext dbContext,
    IOptions<CleanupOptions> options,
    ILogger<ExpiredDataCleaner> logger) : IExpiredDataCleaner
{
    public async Task<CleanupResult> CleanAsync(CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var now = DateTime.UtcNow;

        var refreshTokenCutoff = now.AddDays(-Math.Max(0, settings.RefreshTokenRetentionDays));
        var oauthStateCutoff = now.AddDays(-Math.Max(0, settings.OAuthStateRetentionDays));

        // Se borra por caducidad, no por revocación: un token revocado pero aún
        // vigente sigue sirviendo para rechazar explícitamente su reutilización.
        var deletedRefreshTokens = await dbContext.RefreshTokens
            .Where(token => token.ExpiresAtUtc < refreshTokenCutoff)
            .ExecuteDeleteAsync(cancellationToken);

        var deletedOAuthStates = await dbContext.OAuthStates
            .Where(state => state.ExpiresAtUtc < oauthStateCutoff)
            .ExecuteDeleteAsync(cancellationToken);

        var result = new CleanupResult(deletedRefreshTokens, deletedOAuthStates);

        if (result.Total > 0)
        {
            logger.LogInformation(
                "Purga de datos caducados: {RefreshTokens} token(s) de refresco y {OAuthStates} estado(s) OAuth eliminados.",
                result.RefreshTokens,
                result.OAuthStates);
        }

        return result;
    }
}
