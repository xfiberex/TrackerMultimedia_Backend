using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using TrackerMultimedia.Contracts.Search;
using TrackerMultimedia.Data;
using TrackerMultimedia.Domain.Enums;
using TrackerMultimedia.Services;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.Operations;

/// <summary>
/// T4-11 — Correlación de peticiones y registro estructurado (separada de T4-06).
///
/// Los tres comportamientos que se comprueban aquí tienen algo en común: **solo
/// existen en el log**. Ninguno cambia el código de estado ni el cuerpo de una
/// respuesta correcta, así que ninguna otra prueba de la suite los rozaría, y los tres
/// estuvieron rotos mucho tiempo sin que se notara.
/// </summary>
public class ObservabilityTests
{
    /// <summary>
    /// El defecto de fondo de T4-11. La búsqueda federada está diseñada para que el
    /// fallo de un proveedor no tumbe al resto, y hasta aquí bien; el problema era que
    /// tampoco quedaba constancia. Con dos proveedores y uno caído, la respuesta es 200
    /// con resultados parciales: idéntica a la de una búsqueda sana. Si eso no se
    /// registra, no hay ninguna forma de enterarse.
    /// </summary>
    [Fact]
    public async Task Search_LogsTheFailure_WhenOneProviderFailsAndAnotherAnswers()
    {
        var recorder = new RecordingLoggerProvider();

        using var factory = new AppFactory(
            configureAdditionalTestServices: services =>
            {
                services.AddSingleton<ILoggerProvider>(recorder);

                services.RemoveAll<JikanSearchService>();
                services.RemoveAll<IExternalCatalogProvider>();

                // Uno responde...
                services.AddSingleton<JikanSearchService>(_ => new JikanSearchService(
                    new HttpClient(new DelegateHttpMessageHandler((_, _) => Task.FromResult(
                        DelegateHttpMessageHandler.Json(
                            """
                            {
                              "data": [
                                {
                                  "mal_id": 20,
                                  "title": "Naruto",
                                  "url": "https://jikan.test/naruto",
                                  "status": "Currently Airing"
                                }
                              ]
                            }
                            """))))
                    {
                        BaseAddress = new Uri("https://api.jikan.moe/v4/"),
                        Timeout = TimeSpan.FromSeconds(10),
                    }));
                services.AddSingleton<IExternalCatalogProvider>(sp => sp.GetRequiredService<JikanSearchService>());

                // ...y el otro se cae.
                services.AddSingleton<IExternalCatalogProvider>(
                    new FailingProvider("mangadex", new HttpRequestException("catálogo inalcanzable")));
            });

        await factory.InitializeDatabaseAsync();
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);

        var response = await client.GetAsync("/api/discover/search?query=naruto&type=2&limit=5");

        // Para quien busca no ha pasado nada: 200 y resultados del proveedor que sí contestó.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<SearchMediaItemResponse>>(MediaItemTestHelpers.JsonOpts);
        Assert.Single(items!);

        // Pero en el log sí consta, con el proveedor concreto y la excepción original.
        var warning = Assert.Single(
            recorder.Entries,
            entry => entry.Level == LogLevel.Warning &&
                     entry.Message.Contains("mangadex", StringComparison.OrdinalIgnoreCase));

        Assert.Contains("incompletos", warning.Message, StringComparison.OrdinalIgnoreCase);
        Assert.IsType<HttpRequestException>(warning.Exception);
    }

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

    private sealed class FailingProvider(string key, Exception failure) : IExternalCatalogProvider
    {
        public string Key => key;

        public string DisplayName => key;

        public MediaItemSourceType SourceType => MediaItemSourceType.MangaDex;

        public IReadOnlyCollection<MediaSearchType> SupportedTypes => [MediaSearchType.All, MediaSearchType.Anime];

        public bool Supports(MediaSearchType type) => true;

        public Task<IReadOnlyCollection<SearchMediaItemResponse>> SearchAsync(
            SearchMediaItemsRequest request,
            CancellationToken cancellationToken) => throw failure;
    }
}
