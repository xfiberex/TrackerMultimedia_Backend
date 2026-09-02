using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TrackerMultimedia.Contracts.Auth;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.Authorization;

public class AuthorizationTests(AppFactory factory) : IClassFixture<AppFactory>
{
    // Cada test crea su propio HttpClient para no compartir headers entre tests
    private HttpClient NewClient() => factory.CreateClient();

    // -------------------------------------------------------------------------
    // Endpoints protegidos sin token → 401
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("GET", "/api/media-items")]
    [InlineData("GET", "/api/auth/me")]
    [InlineData("POST", "/api/auth/logout-all")]
    public async Task ProtectedEndpoints_WithoutToken_Return401(string method, string url)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        var response = await NewClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // Endpoints públicos sin token → no 401
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("/health")]
    [InlineData("/api/auth/login")]   // POST sin body → 400, no 401
    [InlineData("/api/auth/register")]
    public async Task PublicEndpoints_WithoutToken_DoNotReturn401(string url)
    {
        // Usamos POST vacío para los endpoints de auth (devolverán 400, pero NO 401)
        var response = url.Contains("/api/auth")
            ? await NewClient().PostAsync(url, null)
            : await NewClient().GetAsync(url);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // Endpoints protegidos con token válido → respuesta correcta
    // -------------------------------------------------------------------------

    [Fact]
    public async Task MediaItems_WithValidToken_Returns200()
    {
        var client = NewClient();
        var (_, auth) = await AuthHelpers.CreateAndLoginAsync(client, factory.Services);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await client.GetAsync("/api/media-items");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsAuthenticatedUser()
    {
        var client = NewClient();
        var (email, auth) = await AuthHelpers.CreateAndLoginAsync(client, factory.Services);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(user);
        Assert.Equal(email, user.Email);
        Assert.True(user.EmailConfirmed);
    }

    // -------------------------------------------------------------------------
    // Token manipulado → 401
    // -------------------------------------------------------------------------

    [Fact]
    public async Task MediaItems_WithTamperedToken_Returns401()
    {
        var client = NewClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "eyJhbGciOiJIUzI1NiJ9.este.token.esta.manipulado");

        var response = await client.GetAsync("/api/media-items");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
