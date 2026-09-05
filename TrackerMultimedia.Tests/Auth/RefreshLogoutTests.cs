using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrackerMultimedia.Contracts.Auth;
using TrackerMultimedia.Data;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.Auth;

public class RefreshLogoutTests(AppFactory factory) : IClassFixture<AppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Login_WritesTheRefreshCookieWithItsProtectiveAttributes()
    {
        var email = $"cookie_{Guid.NewGuid():N}@test.com";
        await AuthHelpers.CreateConfirmedUserAsync(factory.Services, email);

        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email, password = AuthHelpers.DefaultPassword });

        // Se leen los atributos de la cabecera, no se confía en que el cliente la guarde:
        // un HttpClient acepta igual de bien una cookie sin HttpOnly, que es justo el
        // descuido que este test tiene que detectar.
        var setCookie = SessionCookies.Attributes(response);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_DoesNotReturnTheRefreshTokenInTheBody()
    {
        var email = $"body_{Guid.NewGuid():N}@test.com";
        await AuthHelpers.CreateConfirmedUserAsync(factory.Services, email);

        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email, password = AuthHelpers.DefaultPassword });

        var cookieToken = SessionCookies.Read(response);
        var raw = await response.Content.ReadAsStringAsync();

        // Sobre el JSON crudo y no sobre propiedades deserializadas: lo que no debe salir
        // no puede aparecer en ningún campo, se llame como se llame.
        Assert.DoesNotContain(cookieToken, raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refresh_WithTheCookie_Returns200AndRotatesIt()
    {
        var (_, _) = await AuthHelpers.CreateAndLoginAsync(_client, factory.Services);

        var response = await SessionCookies.RefreshAsync(_client);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var newAuth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(newAuth);
        Assert.False(string.IsNullOrEmpty(newAuth.AccessToken));

        // La rotación tiene que llegar al navegador: sin cookie nueva, la siguiente
        // renovación mandaría el token que este mismo refresh acaba de revocar.
        Assert.True(SessionCookies.TryRead(response, out var rotated));
        Assert.False(string.IsNullOrEmpty(rotated));
    }

    [Fact]
    public async Task Refresh_AfterRotation_TheOldCookieIsRejected()
    {
        var client = SessionCookies.CreateClientWithoutCookies(factory);
        var email = $"rot_{Guid.NewGuid():N}@test.com";
        await AuthHelpers.CreateConfirmedUserAsync(factory.Services, email);

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email, password = AuthHelpers.DefaultPassword });
        var original = SessionCookies.Read(login);

        var first = await SessionCookies.RefreshAsync(client, original);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await SessionCookies.RefreshAsync(client, original);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithoutTheClientHeader_IsRejectedEvenWithAValidCookie()
    {
        await AuthHelpers.CreateAndLoginAsync(_client, factory.Services);

        // Misma petición que la del test anterior salvo por la cabecera. Es la defensa
        // contra CSRF del endpoint: sin ella, una página de otro sitio podría provocar
        // la rotación de la sesión de quien la visite.
        var response = await _client.PostAsync("/api/auth/refresh", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithoutCookie_Returns401()
    {
        var client = SessionCookies.CreateClientWithoutCookies(factory);

        var response = await SessionCookies.RefreshAsync(client);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithAnUnknownToken_ClearsTheCookie()
    {
        var client = SessionCookies.CreateClientWithoutCookies(factory);

        var response = await SessionCookies.RefreshAsync(client, "este-token-no-existe-en-bd");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        // Si no se borrara, el navegador seguiría mandando un token muerto en cada
        // arranque y cada intento gastaría cupo del rate limiter.
        Assert.True(SessionCookies.WasCleared(response));
    }

    [Fact]
    public async Task Logout_RevokesTheTokenAndClearsTheCookie()
    {
        var (email, _) = await AuthHelpers.CreateAndLoginAsync(_client, factory.Services);

        var response = await SessionCookies.LogoutAsync(_client);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(SessionCookies.WasCleared(response));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stillActive = await db.RefreshTokens.AsNoTracking()
            .AnyAsync(token => !token.IsRevoked && token.User.Email == email);
        Assert.False(stillActive);
    }

    [Fact]
    public async Task Logout_ThenRefresh_Returns401()
    {
        await AuthHelpers.CreateAndLoginAsync(_client, factory.Services);

        await SessionCookies.LogoutAsync(_client);
        var refreshResponse = await SessionCookies.RefreshAsync(_client);

        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutCookie_Returns204()
    {
        // Cerrar sesión es idempotente: quien ya no tiene sesión no necesita un error,
        // necesita que el cliente pueda limpiar su estado local sin ramas especiales.
        var client = SessionCookies.CreateClientWithoutCookies(factory);

        var response = await SessionCookies.LogoutAsync(client);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
