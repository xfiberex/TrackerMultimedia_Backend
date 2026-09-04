using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TrackerMultimedia.Data;
using TrackerMultimedia.Services;

namespace TrackerMultimedia.Tests.Helpers;

/// <summary>
/// WebApplicationFactory sobre una base **PostgreSQL desechable**, propia de cada clase
/// de test, y con el envío de correos desactivado.
///
/// Antes esto usaba SQLite en memoria, y por tanto no comprobaba nada específico del
/// proveedor real: la búsqueda de la biblioteca devolvía 500 en la suite porque
/// `EF.Functions.ILike` solo existe en Npgsql. Ver <see cref="TestDatabase"/> para el
/// porqué y el cómo.
/// </summary>
public sealed class AppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _connectionString = TestDatabase.CreateForTestClass();
    private readonly IReadOnlyDictionary<string, string?> _configurationOverrides;
    private bool _databaseDropped;
    private readonly Action<IServiceCollection>? _configureAdditionalTestServices;

    public AppFactory()
        : this(null, null)
    {
    }

    internal AppFactory(
        IReadOnlyDictionary<string, string?>? configurationOverrides = null,
        Action<IServiceCollection>? configureAdditionalTestServices = null)
    {
        _configurationOverrides = configurationOverrides ?? new Dictionary<string, string?>();
        _configureAdditionalTestServices = configureAdditionalTestServices;
        EmailInbox = new TestEmailInbox();
    }

    public TestEmailInbox EmailInbox { get; }

    async Task IAsyncLifetime.InitializeAsync()
        => await InitializeDatabaseAsync();

    /// <summary>
    /// El esquema ya viene aplicado: la base se copia de una plantilla a la que se le
    /// pasaron las migraciones una vez por ejecución. Esto solo fuerza la construcción
    /// del host y comprueba que se puede hablar con la base.
    /// </summary>
    public async Task InitializeDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.CanConnectAsync();
    }

    Task IAsyncLifetime.DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// La base se borra aquí y no en <c>DisposeAsync</c> porque hay dos formas de usar
    /// esta factoría: como <c>IClassFixture</c>, donde xUnit llama a
    /// <c>IAsyncLifetime.DisposeAsync</c>, y con un <c>using</c> local en los tests que
    /// necesitan una configuración propia, donde solo se llama a <c>Dispose</c>. Ponerlo
    /// solo en el primero dejaba una base huérfana por cada factoría del segundo tipo:
    /// 23 tras tres ejecuciones.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && !_databaseDropped)
        {
            _databaseDropped = true;
            TestDatabase.Drop(_connectionString);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // --- Configuración de test (anula appsettings y user-secrets) ---
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                // JWT con valores de test
                ["Jwt:Secret"] = "clave-secreta-para-tests-32chars!!",
                ["Jwt:Issuer"] = "TrackerMultimedia.Tests",
                ["Jwt:Audience"] = "TrackerMultimedia.Tests",
                ["Jwt:AccessTokenLifetimeMinutes"] = "15",
                ["Jwt:RefreshTokenLifetimeDays"] = "7",
                // CORS (requerido por startup)
                ["Cors:AllowedOrigins:0"] = "http://localhost:5173",
                // Deshabilitar OAuth para no registrar HttpClients externos
                ["OAuth:Google:Enabled"] = "false",
                ["OAuth:GitHub:Enabled"] = "false",
                // Sin temporizador de purga en los tests: se ejecuta a mano
                // resolviendo IExpiredDataCleaner cuando hace falta comprobarla.
                ["Cleanup:Enabled"] = "false",
                // SMTP vacío (el servicio se reemplaza por no-op)
                ["Smtp:Host"] = "localhost",
                ["Smtp:FromAddress"] = "noreply@test.local",
            };

            foreach (var (key, value) in _configurationOverrides)
                settings[key] = value;

            config.AddInMemoryCollection(settings);
        });

        // --- Servicios de test (se ejecutan DESPUÉS de los servicios de la app) ---
        builder.ConfigureTestServices(services =>
        {
            // **No basta con sobrescribir la cadena de conexión en la configuración.**
            // `Program.cs` la lee de `builder.Configuration` al componer los servicios,
            // antes de que se apliquen las fuentes que añade la factoría de tests, así
            // que el `AddDbContext` de la aplicación se queda con la de los user-secrets:
            // **la base real**. Quitar este bloque hizo que una tanda de tests escribiera
            // 199 usuarios en la base de desarrollo. El registro hay que reemplazarlo.
            //
            // En EF Core 7+, AddDbContext registra IDbContextOptionsConfiguration<T> que
            // se aplica acumulativamente: sin eliminarlo, las dos cadenas quedan activas.
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDatabaseProvider>();

            services.AddDbContext<ApplicationDbContext>(opts => opts.UseNpgsql(_connectionString));

            // Reemplazar el servicio de email por una implementación vacía
            var emailDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IEmailService));
            if (emailDescriptor is not null)
                services.Remove(emailDescriptor);

            services.AddSingleton(EmailInbox);
            services.AddScoped<IEmailService, RecordingEmailService>();

            // ── JWT: corregir IssuerSigningKey ─────────────────────────────────
            // Problema de timing en WebApplicationFactory: jwtSecret se lee como
            // variable local en Program.cs ANTES de que el factory aplique sus
            // overrides de configuración, por lo que IssuerSigningKey queda con
            // el secreto de desarrollo (user-secrets) en lugar del secreto de test.
            // La solución es reemplazarlo aquí, después de que todos los Configure
            // han corrido y ya se tienen los valores correctos.
            const string testSecret = "clave-secreta-para-tests-32chars!!";
            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.TokenValidationParameters.IssuerSigningKey =
                        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(testSecret));
                });

            // ── Rate limiter: deshabilitar en tests ────────────────────────────
            // El límite "auth" (10 req/min, key="unknown") se agota rápidamente
            // porque TestServer no tiene RemoteIpAddress. Se eliminan las
            // configuraciones originales y se registran políticas sin límite.
            services.RemoveAll<IConfigureOptions<RateLimiterOptions>>();
            services.Configure<RateLimiterOptions>(options =>
            {
                options.AddPolicy("auth", _ => RateLimitPartition.GetNoLimiter<string>("test"));
                options.AddPolicy("user", _ => RateLimitPartition.GetNoLimiter<string>("test"));
                options.AddPolicy("search", _ => RateLimitPartition.GetNoLimiter<string>("test"));
            });

            _configureAdditionalTestServices?.Invoke(services);
        });
    }
}

