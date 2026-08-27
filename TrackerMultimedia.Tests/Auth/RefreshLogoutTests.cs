using System.Net;
using System.Net.Http.Json;
using TrackerMultimedia.Contracts.Auth;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.Auth;

public class RefreshLogoutTests(AppFactory factory) : IClassFixture<AppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Refresh_ValidToken_Returns200WithNewTokens()
    {
        var (_, auth) = await AuthHelpers.CreateAndLoginAsync(_client, factory.Services);

        var response = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(auth.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var newAuth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(newAuth);
        Assert.False(string.IsNullOrEmpty(newAuth.AccessToken));
        Assert.False(string.IsNullOrEmpty(newAuth.RefreshToken));
    }

    [Fact]
    public async Task Refresh_ValidToken_RotatesToken()
    {
        var (_, auth) = await AuthHelpers.CreateAndLoginAsync(_client, factory.Services);

        var response = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(auth.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var newAuth = await response.Content.ReadFromJsonAsync<AuthResponse>();

        // El token nuevo debe ser distinto al original (rotación)
        Assert.NotEqual(auth.RefreshToken, newAuth!.RefreshToken);
    }

    [Fact]
    public async Task Refresh_AfterRotation_OldTokenIsRevoked()
    {
        var (_, auth) = await AuthHelpers.CreateAndLoginAsync(_client, factory.Services);

        // Primera rotación: debe funcionar
        await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken));

        // Reintento con el token original ya revocado: debe fallar
        var secondResponse = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(auth.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, secondResponse.StatusCode);
    }

    [Fact]
    public async Task Refresh_InvalidToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest("este-token-no-existe-en-bd"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_WithRefreshToken_Returns204()
    {
        var (_, auth) = await AuthHelpers.CreateAndLoginAsync(_client, factory.Services);

        var response = await _client.PostAsJsonAsync("/api/auth/logout",
            new RefreshRequest(auth.RefreshToken));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesToken_ThenRefreshReturns401()
    {
        var (_, auth) = await AuthHelpers.CreateAndLoginAsync(_client, factory.Services);

        // Hacer logout: revoca el refresh token
        await _client.PostAsJsonAsync("/api/auth/logout", new RefreshRequest(auth.RefreshToken));

        // Intentar usar el token revocado
        var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(auth.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutToken_Returns204()
    {
        // Logout sin body: debe ser tolerante (cerrar sesión desde cliente que no tiene token)
        var response = await _client.PostAsJsonAsync<RefreshRequest?>("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
