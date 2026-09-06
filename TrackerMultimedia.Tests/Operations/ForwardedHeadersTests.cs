using System.Net;
using System.Net.Http.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TrackerMultimedia.Infrastructure.Options;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.Operations;

/// <summary>
/// T1-05 — De quién se acepta <c>X-Forwarded-For</c>.
///
/// El defecto original: <c>KnownIPNetworks.Clear()</c> y <c>KnownProxies.Clear()</c>
/// hacían que ASP.NET Core aceptase la cabecera de **cualquier origen**. Como el rate
/// limiter particiona por <c>RemoteIpAddress</c> *después* de <c>UseForwardedHeaders</c>,
/// mandar una IP distinta en cada intento colocaba cada petición en su propia partición
/// y el límite de 10/minuto que frena la fuerza bruta contra login no llegaba a contar
/// hasta dos.
///
/// La prueba de fondo es la primera: no comprueba cómo está escrita la configuración,
/// sino que la cabecera **no cambia el resultado**, que es lo único que un atacante
/// puede observar.
/// </summary>
public class ForwardedHeadersTests
{
    /// <summary>
    /// Reemplaza la política "auth" —que <see cref="AppFactory"/> deja sin límite para
    /// que la suite no se agote a sí misma— por una equivalente a la de producción pero
    /// con un cupo pequeño: misma partición por <c>RemoteIpAddress</c>, mismo 429.
    /// </summary>
    private static void UseRealAuthRateLimit(IServiceCollection services, int permitLimit)
    {
        services.RemoveAll<IConfigureOptions<RateLimiterOptions>>();
        services.Configure<RateLimiterOptions>(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("auth", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = permitLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));
            options.AddPolicy("user", _ => RateLimitPartition.GetNoLimiter<string>("test"));
            options.AddPolicy("search", _ => RateLimitPartition.GetNoLimiter<string>("test"));
        });
    }

    /// <summary>
    /// El criterio de cierre de T1-05, tal cual estaba escrito en el ROADMAP: rotar la
    /// cabecera en cada intento acaba devolviendo 429.
    ///
    /// Antes del arreglo este test fallaba con 20 respuestas 401 y ni un solo 429: cada
    /// IP inventada estrenaba su propio cupo.
    /// </summary>
    [Fact]
    public async Task RotatingXForwardedFor_DoesNotEscapeTheAuthRateLimit()
    {
        using var factory = new AppFactory(
            configureAdditionalTestServices: services => UseRealAuthRateLimit(services, permitLimit: 5));
        var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 20; i++)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new { email = "nadie@example.com", password = "NoImporta1!" })
            };
            // Una IP distinta en cada vuelta: es exactamente lo que hacía saltar el límite.
            request.Headers.Add("X-Forwarded-For", $"203.0.113.{i + 1}");

            var response = await client.SendAsync(request);
            statuses.Add(response.StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }

    /// <summary>
    /// La otra mitad: sin proxies declarados el middleware no se registra, así que la
    /// sección de configuración se queda con sus valores por defecto y nadie los toca.
    /// </summary>
    [Fact]
    public void WithoutDeclaredProxies_NothingIsTrusted()
    {
        using var factory = new AppFactory();
        _ = factory.CreateClient();

        var settings = factory.Services.GetRequiredService<IConfiguration>()
            .GetSection(ForwardedHeadersSettings.SectionName)
            .Get<ForwardedHeadersSettings>() ?? new ForwardedHeadersSettings();

        Assert.False(settings.IsEnabled);
        Assert.Empty(settings.KnownProxies);
        Assert.Empty(settings.KnownNetworks);
    }

    /// <summary>
    /// Declarar un proxy sí llega a <c>ForwardedHeadersOptions</c>. Es la vuelta atrás
    /// que necesita un despliegue: la tarea era dejar de confiar en cualquiera, no dejar
    /// de poder confiar en el proxy propio.
    /// </summary>
    [Fact]
    public void DeclaredProxiesAndNetworks_ReachTheMiddlewareOptions()
    {
        using var factory = new AppFactory(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:KnownProxies:0"] = "10.0.0.7",
            ["ForwardedHeaders:KnownNetworks:0"] = "10.0.0.0/8",
            ["ForwardedHeaders:ForwardLimit"] = "2"
        });
        _ = factory.CreateClient();

        var options = factory.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        Assert.Contains(options.KnownProxies, address => address.ToString() == "10.0.0.7");
        Assert.Contains(options.KnownIPNetworks, network => network.ToString() == "10.0.0.0/8");
        Assert.Equal(2, options.ForwardLimit);
        Assert.True(options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedFor));
    }

    /// <summary>
    /// Una red mal escrita para el arranque con un mensaje que dice cuál es y cómo
    /// tendría que ser. El modo silencioso —ignorar el valor inválido— dejaría la
    /// aplicación creyendo que confía en un proxy que en realidad no está en la lista.
    /// </summary>
    [Fact]
    public void AMalformedNetwork_StopsStartupWithAnExplicitMessage()
    {
        using var factory = new AppFactory(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:KnownNetworks:0"] = "no-es-una-red"
        });

        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("no-es-una-red", error.Message);
        Assert.Contains("CIDR", error.Message);
    }
}
