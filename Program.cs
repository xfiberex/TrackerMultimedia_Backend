using System.IO;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TrackerMultimedia.Data;
using TrackerMultimedia.Domain.Entities;
using TrackerMultimedia.Infrastructure.Health;
using TrackerMultimedia.Infrastructure.Http;
using TrackerMultimedia.Infrastructure.Options;
using TrackerMultimedia.Services;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration
        .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.local.json", optional: true, reloadOnChange: true)
        .AddUserSecrets<Program>(optional: true)
        .AddEnvironmentVariables();
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not found or is empty. " +
        "In development, set it via User Secrets or appsettings.Local.json. In production, use an environment variable.");
}

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? throw new InvalidOperationException("'Cors:AllowedOrigins' configuration section is required.");
var applyMigrationsOnStartup = builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup");
var dataProtectionKeysDirectory = builder.Configuration["DataProtection:KeysDirectory"];

// `AllowCredentials` es imprescindible desde que el token de refresco viaja en cookie:
// sin él el navegador no la adjunta a las peticiones del API. Obliga a que el allowlist
// sea explícito —`AllowAnyOrigin` es incompatible con credenciales—, que es justo lo que
// sostiene la defensa contra CSRF de `RequireClientHeaderAttribute`.
builder.Services.AddCors(options =>
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowCredentials()
              .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")));

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var dataProtectionBuilder = builder.Services
    .AddDataProtection()
    .SetApplicationName("TrackerMultimedia");

if (!string.IsNullOrWhiteSpace(dataProtectionKeysDirectory))
{
    Directory.CreateDirectory(dataProtectionKeysDirectory);
    dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysDirectory));
}

builder.Services.AddRateLimiter(options =>
{
    // Búsqueda externa: 30 req/min por IP
    options.AddPolicy("search", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 30,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // Endpoints de auth (login, register, refresh): 10 req/min por IP — freno de fuerza bruta
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 10,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // Endpoints autenticados: 200 req/min por usuario — evita abuso de cuentas comprometidas
    options.AddPolicy("user", httpContext =>
    {
        var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: userId,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 200,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddScoped<MediaItemsService>();
builder.Services.AddScoped<CategoriesService>();
builder.Services.AddScoped<FormatsService>();
builder.Services.AddScoped<ExternalCatalogSearchService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AuthSessionService>();
builder.Services.Configure<RefreshCookieOptions>(builder.Configuration.GetSection(RefreshCookieOptions.SectionName));
builder.Services.AddSingleton<RefreshTokenCookie>();
builder.Services.AddScoped<PersonalDataExportService>();

// ── SMTP / Email ─────────────────────────────────────────────────────────────
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.AddScoped<IEmailService, SmtpEmailService>();

// ── Respuestas de error ──────────────────────────────────────────────────────
// Da forma RFC 9457 a los errores y añade el identificador de la petición, que es
// lo que permite cruzar lo que ve el usuario con lo que quedó en el log.
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
        context.ProblemDetails.Extensions["traceId"] =
            System.Diagnostics.Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
    };
});

// ── Purga de datos caducados ─────────────────────────────────────────────────
builder.Services.Configure<CleanupOptions>(builder.Configuration.GetSection(CleanupOptions.SectionName));
builder.Services.AddScoped<IExpiredDataCleaner, ExpiredDataCleaner>();

var cleanupConfig = builder.Configuration.GetSection(CleanupOptions.SectionName).Get<CleanupOptions>() ?? new CleanupOptions();
if (cleanupConfig.Enabled)
{
    // El servicio en segundo plano se registra solo si está habilitado, para que
    // los tests no arrastren un temporizador de fondo en cada arranque del host.
    builder.Services.AddHostedService<ExpiredDataCleanupService>();
}

// ── OAuth providers ───────────────────────────────────────────────────────────
builder.Services.Configure<OAuthOptions>(builder.Configuration.GetSection(OAuthOptions.SectionName));
// Los servicios de Google y GitHub se registran solo si están habilitados en configuración.
var oauthConfig = builder.Configuration.GetSection(OAuthOptions.SectionName).Get<OAuthOptions>() ?? new OAuthOptions();
if (oauthConfig.Google.Enabled)
{
    builder.Services.AddHttpClient<IGoogleAuthService, GoogleAuthService>();
}
if (oauthConfig.GitHub.Enabled)
{
    builder.Services.AddHttpClient<IGitHubAuthService, GitHubAuthService>();
}

// ASP.NET Core Identity (sin cookie de autenticación, solo UserManager)
builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = false;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.AllowedForNewUsers = true;
})
.AddRoles<IdentityRole<Guid>>()
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Validar que el secreto JWT esté configurado y tenga longitud segura antes de arrancar
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret))
{
    throw new InvalidOperationException(
        "'Jwt:Secret' no está configurado. " +
        "En desarrollo, configúralo en User Secrets o appsettings.Local.json.");
}
if (Encoding.UTF8.GetByteCount(jwtSecret) < 32)
{
    throw new InvalidOperationException(
        "'Jwt:Secret' debe tener al menos 32 bytes (256 bits) para HMAC-SHA256. " +
        "Usa una cadena aleatoria de 32 o más caracteres.");
}

// Autenticación JWT Bearer
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            // Sin margen de tolerancia en la expiración
            ClockSkew = TimeSpan.Zero,
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);
builder.Services.AddOpenApi();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddHttpClient<JikanSearchService>(client =>
{
    client.BaseAddress = new Uri("https://api.jikan.moe/v4/");
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("TrackerMultimedia/1.0");
});
builder.Services.AddTransient<IExternalCatalogProvider>(sp => sp.GetRequiredService<JikanSearchService>());

builder.Services.AddHttpClient<AniListSearchService>(client =>
{
    client.BaseAddress = new Uri("https://graphql.anilist.co/");
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("TrackerMultimedia/1.0");
});
builder.Services.AddTransient<IExternalCatalogProvider>(sp => sp.GetRequiredService<AniListSearchService>());

builder.Services.AddHttpClient<MangaDexSearchService>(client =>
{
    client.BaseAddress = new Uri("https://api.mangadex.org/");
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("TrackerMultimedia/1.0");
});
builder.Services.AddTransient<IExternalCatalogProvider>(sp => sp.GetRequiredService<MangaDexSearchService>());

var app = builder.Build();

// Las migraciones solo se aplican sobre una conexión PostgreSQL real ("Host=" descarta
// el SQLite en memoria de los tests) y solo en desarrollo o si se habilita explícitamente
// con Database:ApplyMigrationsOnStartup.
var runtimeConnectionString = app.Configuration.GetConnectionString("DefaultConnection");
var shouldApplyMigrations =
    !string.IsNullOrWhiteSpace(runtimeConnectionString) &&
    runtimeConnectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) &&
    (app.Environment.IsDevelopment() || applyMigrationsOnStartup);

if (shouldApplyMigrations)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync()).ToArray();

        if (pendingMigrations.Length == 0)
        {
            startupLogger.LogInformation("Esquema de base de datos al día: no hay migraciones pendientes.");
        }
        else
        {
            startupLogger.LogInformation(
                "Aplicando {Count} migración(es) pendiente(s): {Migrations}",
                pendingMigrations.Length,
                string.Join(", ", pendingMigrations));

            await dbContext.Database.MigrateAsync();

            startupLogger.LogInformation("Migraciones aplicadas correctamente.");
        }
    }
    catch (Exception exception)
    {
        // No se aborta el arranque: un fallo de migración deja la API en pie para poder
        // diagnosticarlo, pero queda registrado como error para que no pase inadvertido.
        startupLogger.LogError(
            exception,
            "No se pudieron aplicar las migraciones al arrancar. El esquema puede no estar actualizado.");
    }
}

// Lo primero del pipeline a propósito: solo captura lo que ocurre por debajo, así
// que cualquier middleware registrado antes se quedaría fuera de su alcance.
// Sin esto, una excepción no controlada salía como un 500 con cuerpo vacío: el
// usuario no veía nada y no había forma de relacionar su incidencia con el log.
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var feature = context.Features.Get<IExceptionHandlerFeature>();
    var handlerLogger = context.RequestServices.GetRequiredService<ILogger<Program>>();

    handlerLogger.LogError(
        feature?.Error,
        "Excepción no controlada en {Method} {Path}. TraceId: {TraceId}",
        context.Request.Method,
        context.Request.Path,
        context.TraceIdentifier);

    // El detalle se queda en el log. Al cliente le llega un texto fijo y el
    // identificador: el mensaje de una excepción describe la infraestructura.
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await Results.Problem(
        title: "Se produjo un error inesperado.",
        detail: "Vuelve a intentarlo. Si el problema persiste, indica el identificador de esta respuesta.",
        statusCode: StatusCodes.Status500InternalServerError,
        instance: context.Request.Path).ExecuteAsync(context);
}));

// Convierte en ProblemDetails las respuestas de error que salen sin cuerpo,
// como un 404 de ruta desconocida o un 401 sin contenido.
app.UseStatusCodePages();

// La especificación se sirve siempre en desarrollo y, fuera de él, solo si se activa
// a propósito con `OpenApi:Exposed`. Publicarla no es un agujero por sí mismo —no
// contiene secretos y no habilita nada—, pero le entrega a cualquiera el mapa completo
// de rutas y parámetros, así que la decisión se toma por despliegue y no por defecto.
// El archivo versionado en `docs/openapi.json` sirve para consultarla sin arrancar nada.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("OpenApi:Exposed"))
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseForwardedHeaders();

var httpsPort = app.Configuration["HTTPS_PORTS"] ?? app.Configuration["ASPNETCORE_HTTPS_PORT"];
if (app.Environment.IsDevelopment() || !string.IsNullOrWhiteSpace(httpsPort))
{
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapMethods("/", new[] { "GET", "HEAD" }, () => Results.Ok(new { status = "ok" }));
// Dos sondas con propósitos distintos. `/health` solo dice que el proceso está en pie
// —es la que debe contestar un reinicio en marcha— y `/health/ready` comprueba además
// que la base de datos responde, que es lo que decide si la aplicación puede atender.
// Ninguna de las dos devuelve el detalle del fallo: eso queda en el log.
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapControllers();

app.Run();

// Expone la clase Program al proyecto de tests (WebApplicationFactory<Program>)
public partial class Program { }
