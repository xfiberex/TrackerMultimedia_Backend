using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
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
/// WebApplicationFactory que reemplaza PostgreSQL por SQLite en memoria
/// y desactiva el envío de correos reales.
/// Implementa IAsyncLifetime para crear el esquema de BD antes de los tests.
/// </summary>
public sealed class AppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Conexión SQLite persistente: la BD en memoria vive mientras esta conexión esté abierta.
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly IReadOnlyDictionary<string, string?> _configurationOverrides;
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

        // Abrir la conexión en el constructor, antes de que se construya el host,
        // para que el DbContext pueda reutilizarla y la BD no se destruya entre scopes.
        _connection.Open();
    }

    public TestEmailInbox EmailInbox { get; }

    async Task IAsyncLifetime.InitializeAsync()
        => await InitializeDatabaseAsync();

    public async Task InitializeDatabaseAsync()
    {
        // Acceder a Services fuerza la construcción del host.
        // En este punto el DbContext ya usa SQLite con nuestra conexión.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    Task IAsyncLifetime.DisposeAsync()
    {
        _connection.Dispose();
        Dispose();
        return Task.CompletedTask;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // --- Configuración de test (anula appsettings y user-secrets) ---
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var settings = new Dictionary<string, string?>
            {
                // Satisface el check de startup (el DbContext se reemplaza después)
                ["ConnectionStrings:DefaultConnection"] = "DataSource=:memory:",
                // JWT con valores de test
                ["Jwt:Secret"]                      = "clave-secreta-para-tests-32chars!!",
                ["Jwt:Issuer"]                      = "TrackerMultimedia.Tests",
                ["Jwt:Audience"]                    = "TrackerMultimedia.Tests",
                ["Jwt:AccessTokenLifetimeMinutes"]  = "15",
                ["Jwt:RefreshTokenLifetimeDays"]    = "7",
                // CORS (requerido por startup)
                ["Cors:AllowedOrigins:0"]           = "http://localhost:5173",
                // Deshabilitar OAuth para no registrar HttpClients externos
                ["OAuth:Google:Enabled"]            = "false",
                ["OAuth:GitHub:Enabled"]            = "false",
                // Sin temporizador de purga en los tests: se ejecuta a mano
                // resolviendo IExpiredDataCleaner cuando hace falta comprobarla.
                ["Cleanup:Enabled"]                 = "false",
                // SMTP vacío (el servicio se reemplaza por no-op)
                ["Smtp:Host"]                       = "localhost",
                ["Smtp:FromAddress"]                = "noreply@test.local",
            };

            foreach (var (key, value) in _configurationOverrides)
                settings[key] = value;

            config.AddInMemoryCollection(settings);
        });

        // --- Servicios de test (se ejecutan DESPUÉS de los servicios de la app) ---
        builder.ConfigureTestServices(services =>
        {
            // En EF Core 7+, AddDbContext registra IDbContextOptionsConfiguration<T>
            // que se aplica acumulativamente al construir las opciones.
            // Si no se elimina, tanto Npgsql como SQLite quedan activos a la vez.
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDatabaseProvider>();

            // Registrar el DbContext con SQLite y la conexión en memoria
            services.AddDbContext<ApplicationDbContext>(opts =>
                opts.UseSqlite(_connection));

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
                options.AddPolicy("auth",   _ => RateLimitPartition.GetNoLimiter<string>("test"));
                options.AddPolicy("user",   _ => RateLimitPartition.GetNoLimiter<string>("test"));
                options.AddPolicy("search", _ => RateLimitPartition.GetNoLimiter<string>("test"));
            });

            _configureAdditionalTestServices?.Invoke(services);
        });
    }
}

