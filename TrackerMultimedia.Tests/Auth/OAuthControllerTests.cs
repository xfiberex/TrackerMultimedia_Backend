using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrackerMultimedia.Contracts.Auth;
using TrackerMultimedia.Data;
using TrackerMultimedia.Domain.Entities;
using TrackerMultimedia.Services;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.Auth;

public class OAuthControllerTests
{
    [Fact]
    public async Task Init_UnsupportedProvider_Returns400()
    {
        using var factory = await CreateGoogleFactoryAsync();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/discord/init");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Init_DisabledProvider_Returns404()
    {
        using var factory = new AppFactory();
        await factory.InitializeDatabaseAsync();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/google/init");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Init_EnabledGoogle_ReturnsAuthorizationUrlAndPersistsState()
    {
        var googleService = new StubGoogleAuthService();
        using var factory = await CreateGoogleFactoryAsync(googleService);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/google/init?returnPath=%2Fdashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<OAuthInitResponse>();
        Assert.NotNull(body);
        Assert.StartsWith("https://google.test/auth?state=", body!.AuthorizationUrl, StringComparison.Ordinal);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var state = dbContext.OAuthStates.Single();

        Assert.Equal("google", state.Provider);
        Assert.Equal("/dashboard", state.ReturnPath);
        Assert.False(state.IsUsed);
        Assert.Contains(state.StateValue, body.AuthorizationUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Callback_WithProviderError_RedirectsToFrontendError()
    {
        using var factory = await CreateGoogleFactoryAsync();
        var client = NewNoRedirectClient(factory);

        var response = await client.GetAsync("/api/auth/google/callback?error=access_denied");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("http://frontend.test/login?oauth_error=access_denied", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Callback_WithInvalidState_RedirectsToStateMismatch()
    {
        using var factory = await CreateGoogleFactoryAsync();
        var client = NewNoRedirectClient(factory);

        var response = await client.GetAsync("/api/auth/google/callback?code=ok&state=missing-state");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("http://frontend.test/login?oauth_error=state_mismatch", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Callback_WithExistingLinkedUser_RedirectsWithSessionTokens()
    {
        var email = $"oauthlinked_{Guid.NewGuid():N}@test.com";
        var googleService = new StubGoogleAuthService
        {
            Profile = new ExternalUserProfile("google-linked", email, "Linked User", null)
        };

        using var factory = await CreateGoogleFactoryAsync(googleService);
        await SeedStateAsync(factory, "state-linked", "/library");
        await CreateLinkedUserAsync(factory.Services, email, "Google", "google-linked");
        var client = NewNoRedirectClient(factory);

        var response = await client.GetAsync("/api/auth/google/callback?code=ok&state=state-linked");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var location = response.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.StartsWith("http://frontend.test/oauth-callback#", location, StringComparison.Ordinal);
        Assert.Contains("access_token=", location, StringComparison.Ordinal);
        Assert.Contains("refresh_token=", location, StringComparison.Ordinal);
        Assert.Contains("return_path=%2Flibrary", location, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Callback_WithNewUser_CreatesAccountAndRedirectsToRequestedPath()
    {
        var email = $"oauthnew_{Guid.NewGuid():N}@test.com";
        var googleService = new StubGoogleAuthService
        {
            Profile = new ExternalUserProfile("google-new", email, "New OAuth User", null)
        };

        using var factory = await CreateGoogleFactoryAsync(googleService);
        await SeedStateAsync(factory, "state-new", "/profile");
        var client = NewNoRedirectClient(factory);

        var response = await client.GetAsync("/api/auth/google/callback?code=ok&state=state-new");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var location = response.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.Contains("return_path=%2Fprofile", location, StringComparison.Ordinal);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.True(user!.EmailConfirmed);
    }

    [Fact]
    public async Task Callback_WithExistingManualUser_RedirectsToLinkAccount()
    {
        var email = $"oauthmanual_{Guid.NewGuid():N}@test.com";
        var googleService = new StubGoogleAuthService
        {
            Profile = new ExternalUserProfile("google-manual", email, "Manual User", null)
        };

        using var factory = await CreateGoogleFactoryAsync(googleService);
        await SeedStateAsync(factory, "state-link", "/ignored");
        await AuthHelpers.CreateConfirmedUserAsync(factory.Services, email);
        var client = NewNoRedirectClient(factory);

        var response = await client.GetAsync("/api/auth/google/callback?code=ok&state=state-link");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var location = response.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.StartsWith("http://frontend.test/link-account?link_token=", location, StringComparison.Ordinal);
        Assert.Contains("provider=google", location, StringComparison.Ordinal);
        Assert.Contains($"email={Uri.EscapeDataString(email)}", location, StringComparison.Ordinal);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // El state original queda consumido y así se queda. Antes se resucitaba
        // poniéndole IsUsed=false y ampliando su caducidad, con lo que el mismo
        // valor de state seguía sirviendo para un segundo callback durante 15
        // minutos más, en contra del "solo se consume una vez" del propio modelo.
        var stateOriginal = dbContext.OAuthStates.Single(s => s.StateValue == "state-link");
        Assert.True(stateOriginal.IsUsed);
        Assert.Null(stateOriginal.LinkToken);

        // La vinculación pendiente vive en una fila aparte.
        var linkState = dbContext.OAuthStates.Single(s => s.LinkToken != null);
        Assert.NotEqual("state-link", linkState.StateValue);
        Assert.False(linkState.IsUsed);
        Assert.Equal("google-manual", linkState.PendingProviderKey);
        Assert.Equal(email, linkState.PendingEmail);
        Assert.False(string.IsNullOrWhiteSpace(linkState.LinkToken));
    }

    [Fact]
    public async Task LinkConfirm_ValidCredentials_LinksAccountAndReturnsSession()
    {
        var email = $"linkconfirm_{Guid.NewGuid():N}@test.com";
        using var factory = await CreateGoogleFactoryAsync();
        await AuthHelpers.CreateConfirmedUserAsync(factory.Services, email);
        await SeedLinkStateAsync(factory, "link-token", email, "google-link");
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/link-confirm",
            new OAuthLinkConfirmRequest("link-token", "google", email, AuthHelpers.DefaultPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth!.AccessToken));

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var linkedUser = await userManager.FindByLoginAsync("Google", "google-link");
        Assert.NotNull(linkedUser);
        Assert.Equal(email, linkedUser!.Email);
    }

    [Fact]
    public async Task LinkConfirm_WrongPassword_Returns401()
    {
        var email = $"linkwrong_{Guid.NewGuid():N}@test.com";
        using var factory = await CreateGoogleFactoryAsync();
        await AuthHelpers.CreateConfirmedUserAsync(factory.Services, email);
        await SeedLinkStateAsync(factory, "wrong-password-token", email, "google-wrong");
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/link-confirm",
            new OAuthLinkConfirmRequest("wrong-password-token", "google", email, "bad-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Callback_WithLockedOutLinkedUser_DoesNotIssueASession()
    {
        var email = $"oauthlocked_{Guid.NewGuid():N}@test.com";
        var googleService = new StubGoogleAuthService
        {
            Profile = new ExternalUserProfile("google-locked", email, "Locked User", null)
        };

        using var factory = await CreateGoogleFactoryAsync(googleService);
        await SeedStateAsync(factory, "state-locked", "/library");
        await CreateLinkedUserAsync(factory.Services, email, "Google", "google-locked");

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            await userManager.SetLockoutEndDateAsync(user!, DateTimeOffset.UtcNow.AddHours(1));
        }

        var client = NewNoRedirectClient(factory);
        var response = await client.GetAsync("/api/auth/google/callback?code=ok&state=state-locked");

        // El bloqueo por intentos fallidos protege el login con contraseña. Si el
        // callback de OAuth no lo comprueba, la cuenta queda cerrada por un camino
        // y abierta por el otro.
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var location = response.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.Contains("oauth_error=account_unavailable", location, StringComparison.Ordinal);
        Assert.DoesNotContain("access_token=", location, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Callback_WhenTheProfileFails_DoesNotLeakTheExceptionMessage()
    {
        var googleService = new StubGoogleAuthService
        {
            FailureMessage = "Npgsql no pudo conectar con db-interna:5432 usando el rol admin",
        };

        using var factory = await CreateGoogleFactoryAsync(googleService);
        await SeedStateAsync(factory, "state-fail", "/library");
        var client = NewNoRedirectClient(factory);

        var response = await client.GetAsync("/api/auth/google/callback?code=ok&state=state-fail");

        var location = response.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.Contains("oauth_error=profile_error", location, StringComparison.Ordinal);

        // El mensaje de la excepción acababa en la barra de direcciones, y de ahí
        // al historial del navegador y a cualquier sitio donde se pegue la URL.
        Assert.DoesNotContain("oauth_error_message", location, StringComparison.Ordinal);
        Assert.DoesNotContain("db-interna", location, StringComparison.Ordinal);
        Assert.DoesNotContain("Npgsql", location, StringComparison.Ordinal);
    }

    private static HttpClient NewNoRedirectClient(AppFactory factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

    private static async Task<AppFactory> CreateGoogleFactoryAsync(StubGoogleAuthService? googleService = null)
    {
        googleService ??= new StubGoogleAuthService();

        var factory = new AppFactory(
            configurationOverrides: new Dictionary<string, string?>
            {
                ["OAuth:Google:Enabled"] = "true",
                ["App:FrontendBaseUrl"] = "http://frontend.test"
            },
            configureAdditionalTestServices: services =>
            {
                services.RemoveAll<IGoogleAuthService>();
                services.AddSingleton<IGoogleAuthService>(googleService);
            });

        await factory.InitializeDatabaseAsync();
        return factory;
    }

    private static async Task SeedStateAsync(AppFactory factory, string stateValue, string? returnPath)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.OAuthStates.Add(new OAuthState
        {
            StateValue = stateValue,
            Provider = "google",
            ReturnPath = returnPath,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedLinkStateAsync(AppFactory factory, string linkToken, string email, string providerKey)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.OAuthStates.Add(new OAuthState
        {
            StateValue = $"state_{Guid.NewGuid():N}",
            Provider = "google",
            LinkToken = linkToken,
            PendingEmail = email.ToLowerInvariant(),
            PendingProviderKey = providerKey,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task CreateLinkedUserAsync(IServiceProvider services, string email, string provider, string providerKey)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await AuthHelpers.CreateConfirmedUserAsync(services, email);
        var user = await userManager.FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"No se encontró el usuario {email} para vincular el login externo.");
        var addLoginResult = await userManager.AddLoginAsync(user, new UserLoginInfo(provider, providerKey, provider));
        if (!addLoginResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"No se pudo vincular el login externo: {string.Join(", ", addLoginResult.Errors.Select(e => e.Description))}");
        }
    }

    private sealed class StubGoogleAuthService : IGoogleAuthService
    {
        public ExternalUserProfile Profile { get; set; } =
            new("google-user", "google@test.com", "Google User", null);

        public string BuildAuthorizationUrl(string state) => $"https://google.test/auth?state={state}";

        /// <summary>Si se rellena, el intercambio falla con este mensaje.</summary>
        public string? FailureMessage { get; set; }

        public Task<ExternalUserProfile> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default)
            => FailureMessage is null
                ? Task.FromResult(Profile)
                : throw new InvalidOperationException(FailureMessage);
    }
}