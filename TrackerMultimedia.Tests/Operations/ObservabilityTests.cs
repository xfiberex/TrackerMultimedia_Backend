using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using TrackerMultimedia.Data;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.Operations;

/// <summary>
/// T4-11 — Correlación de peticiones y registro estructurado (separada de T4-06).
///
/// Los dos comportamientos que se comprueban aquí tienen algo en común: **solo existen
/// en el log**. Ninguno cambia el código de estado ni el cuerpo de una respuesta
/// correcta, así que ninguna otra prueba de la suite los rozaría, y los dos estuvieron
/// rotos mucho tiempo sin que se notara.
///
/// **Aquí hubo una tercera prueba y se retiró el 2026-09-06**, al eliminarse la búsqueda
/// en catálogos externos: comprobaba que el fallo de un proveedor quedara registrado
/// aunque otro respondiera. Fue la que destapó el defecto de fondo de T4-11 —un fallo
/// parcial silencioso es indistinguible de «no hay resultados»—, así que conviene saber
/// que existió: si algún día vuelve una operación en abanico, la lección se aplica igual.
/// Ver el Tier 6 del ROADMAP y la decisión de eliminar «Descubrir».
/// </summary>
public class ObservabilityTests
{
    /// <summary>
    /// La correlación, que es lo que T2-05 quiso montar y no llegó a funcionar: el
    /// identificador que se le da al usuario tiene que ser el mismo que queda escrito.
    /// Antes la respuesta llevaba <c>Activity.Current.Id</c> y el log
    /// <c>HttpContext.TraceIdentifier</c>, así que buscar uno no encontraba el otro.
    /// </summary>
    [Fact]
    public async Task UnhandledError_TheTraceIdGivenToTheUser_IsTheOneWritten()
    {
        var recorder = new RecordingLoggerProvider();
        using var broken = CreateFactoryWithUnreachableDatabase(recorder);

        var response = await broken.CreateClient().PostAsJsonAsync(
            "/api/auth/register",
            new { email = "correlacion@test.com", password = "Prueba123!", displayName = "Correlación" });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var traceId = JsonDocument.Parse(await response.Content.ReadAsStringAsync())
            .RootElement.GetProperty("traceId").GetString();

        Assert.False(string.IsNullOrWhiteSpace(traceId));

        var logged = Assert.Single(
            recorder.Entries,
            entry => entry.Message.Contains("Excepción no controlada", StringComparison.Ordinal));

        // La afirmación central de T4-06: el mismo valor en la pantalla y en el log.
        Assert.Contains(traceId!, logged.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Y el formato, que es lo que hace la correlación utilizable de verdad: el
    /// identificador es el <c>TraceId</c> del W3C —32 caracteres hexadecimales— y no el
    /// <c>RequestId</c> de la conexión, que tiene la forma <c>0HN…:00000001</c>.
    ///
    /// Sin este test, volver a <c>TraceIdentifier</c> en los dos sitios a la vez dejaría
    /// pasar el test anterior y rompería lo que de verdad se buscaba: que el ámbito que
    /// el host abre por petición —el que marca <b>todas</b> sus líneas, no solo la del
    /// error— lleve ese mismo valor.
    /// </summary>
    [Fact]
    public async Task UnhandledError_TheTraceId_IsTheW3CTraceIdCarriedByEveryLine()
    {
        var recorder = new RecordingLoggerProvider();
        using var broken = CreateFactoryWithUnreachableDatabase(recorder);

        var response = await broken.CreateClient().PostAsJsonAsync(
            "/api/auth/register",
            new { email = "formato@test.com", password = "Prueba123!", displayName = "Formato" });

        var traceId = JsonDocument.Parse(await response.Content.ReadAsStringAsync())
            .RootElement.GetProperty("traceId").GetString();

        Assert.Matches("^[0-9a-f]{32}$", traceId!);

        var logged = Assert.Single(
            recorder.Entries,
            entry => entry.Message.Contains("Excepción no controlada", StringComparison.Ordinal));

        Assert.Contains($"TraceId:{traceId}", logged.Scopes, StringComparison.Ordinal);
    }

    private static AppFactory CreateFactoryWithUnreachableDatabase(RecordingLoggerProvider recorder)
        => new(configureAdditionalTestServices: services =>
        {
            services.AddSingleton<ILoggerProvider>(recorder);

            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDatabaseProvider>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(
                "Host=127.0.0.1;Port=1;Database=no_existe;Username=nadie;Password=nada;" +
                "Timeout=2;Command Timeout=2;Pooling=false"));
        });
}
