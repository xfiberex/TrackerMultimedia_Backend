using Microsoft.Extensions.Options;
using TrackerMultimedia.Infrastructure.Options;

namespace TrackerMultimedia.Services;

/// <summary>
/// Ejecuta la purga al arrancar y luego cada <c>Cleanup:IntervalHours</c>.
///
/// Un fallo aquí no debe tumbar la aplicación: se registra y se reintenta en la
/// siguiente vuelta. Que la base no esté disponible un momento no es motivo para
/// dejar de servir peticiones.
/// </summary>
public sealed class ExpiredDataCleanupService(
    IServiceProvider serviceProvider,
    IOptions<CleanupOptions> options,
    ILogger<ExpiredDataCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        var interval = TimeSpan.FromHours(Math.Max(1, settings.IntervalHours));

        logger.LogInformation(
            "Purga de datos caducados activa: cada {Horas} h. Retención: {DiasToken} d para tokens de refresco, {DiasEstado} d para estados OAuth.",
            interval.TotalHours,
            settings.RefreshTokenRetentionDays,
            settings.OAuthStateRetentionDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var cleaner = scope.ServiceProvider.GetRequiredService<IExpiredDataCleaner>();
                await cleaner.CleanAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Falló la purga de datos caducados. Se reintentará en la siguiente vuelta.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
