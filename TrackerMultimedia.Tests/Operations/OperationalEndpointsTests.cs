using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrackerMultimedia.Data;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.Operations;

/// <summary>
/// T4-06 y T4-07: las dos sondas y la especificación OpenAPI.
/// </summary>
public class OperationalEndpointsTests(AppFactory factory) : IClassFixture<AppFactory>
{
    [Fact]
    public async Task Health_ReportsHealthy()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Ready_ReportsHealthy_WhenTheDatabaseAnswers()
    {
        var response = await factory.CreateClient().GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Este es el test que justifica T4-06. Con la base de datos inalcanzable, el
    /// <c>/health</c> anterior seguía respondiendo «Healthy»: no comprobaba nada.
    /// </summary>
    [Fact]
    public async Task Ready_ReportsUnhealthy_WhenTheDatabaseIsUnreachable()
    {
        using var broken = new AppFactory(configureAdditionalTestServices: services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDatabaseProvider>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(
                "Host=127.0.0.1;Port=1;Database=no_existe;Username=nadie;Password=nada;" +
                "Timeout=2;Command Timeout=2;Pooling=false"));
        });

        var client = broken.CreateClient();

        // La sonda de vida sigue respondiendo: el proceso está en pie y reiniciarlo no
        // arreglaría una base caída. Esa distinción es justamente el motivo de tener dos.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);

        var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal("Unhealthy", await ready.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Ready_DoesNotLeakConnectionDetails()
    {
        using var broken = new AppFactory(configureAdditionalTestServices: services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDatabaseProvider>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(
                "Host=servidor-secreto.interno;Port=1;Database=trackerMultimedia;" +
                "Username=usuario_secreto;Password=nada;Timeout=2;Pooling=false"));
        });

        var response = await broken.CreateClient().GetAsync("/health/ready");
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("servidor-secreto", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("usuario_secreto", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// La otra mitad del interruptor: sin activarlo y fuera de desarrollo, la
    /// especificación no se sirve. Sin este test, el anterior pasaría igual aunque
    /// el documento se publicara siempre.
    /// </summary>
    [Fact]
    public async Task OpenApi_IsNotPublished_OutsideDevelopment()
    {
        using var production = new AppFactory(environment: "Production");

        var response = await production.CreateClient().GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OpenApi_IsPublished_WhenExplicitlyExposed()
    {
        using var exposed = new AppFactory(
            new Dictionary<string, string?> { ["OpenApi:Exposed"] = "true" },
            environment: "Production");

        var response = await exposed.CreateClient().GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var paths = document.GetProperty("paths");

        // Un documento que se genera pero no describe las rutas no documenta nada.
        Assert.True(paths.TryGetProperty("/api/auth/login", out _));
        Assert.True(paths.TryGetProperty("/api/media-items", out _));
        Assert.True(paths.TryGetProperty("/api/auth/account/export", out _));
    }
}
