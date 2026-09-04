using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TrackerMultimedia.Data;

namespace TrackerMultimedia.Infrastructure.Health;

/// <summary>
/// T4-06. <c>AddHealthChecks()</c> sin comprobaciones registradas devuelve siempre
/// «Healthy»: lo único que demuestra es que el proceso responde, que es justo lo que
/// ya se sabe por haber recibido la petición. Con la base de datos caída —la avería
/// que de verdad deja la aplicación inservible— el antiguo <c>/health</c> seguía
/// diciendo que todo iba bien.
/// </summary>
public sealed class DatabaseHealthCheck(ApplicationDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Abre una conexión de verdad. `CanConnectAsync` no lanza si el servidor no
            // está: devuelve false, así que hay que mirar el valor además de capturar.
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("La base de datos responde.")
                : HealthCheckResult.Unhealthy("No se pudo conectar con la base de datos.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // El detalle va al log de health checks, no al cuerpo de la respuesta: el
            // mensaje de una excepción de Npgsql lleva el host, el puerto y el usuario.
            return HealthCheckResult.Unhealthy("No se pudo conectar con la base de datos.", exception);
        }
    }
}
