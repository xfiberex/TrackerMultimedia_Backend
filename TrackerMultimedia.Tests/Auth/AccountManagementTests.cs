using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using TrackerMultimedia.Contracts.Auth;
using TrackerMultimedia.Domain.Entities;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.Auth;

public class AccountManagementTests(AppFactory factory) : IClassFixture<AppFactory>
{
    private HttpClient NewClient() => factory.CreateClient();

    [Fact]
    public async Task Me_ReturnsLinkedExternalProviders()
    {
        var email = $"linked_{Guid.NewGuid():N}@test.com";
        var user = await AuthHelpers.CreateConfirmedUserAsync(factory.Services, email);

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            var stored = await userManager.FindByEmailAsync(email);
            var result = await userManager.AddLoginAsync(
                stored!, new UserLoginInfo("Google", $"google-{Guid.NewGuid():N}", "Google"));
            Assert.True(result.Succeeded);
        }

        var client = NewClient();
        var auth = await AuthHelpers.LoginAsync(client, user.Email!);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await client.GetAsync("/api/auth/me");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();

        // Antes esto llegaba siempre vacío: la navegación ExternalLogins colgaba de una
        // FK sombra (ApplicationUserId) que AddLoginAsync nunca rellena.
        Assert.NotNull(body);
        Assert.Contains("google", body.LinkedProviders);

        // Y la respuesta del login tiene que decir lo mismo que /me.
        Assert.Contains("google", auth.User.LinkedProviders);
    }

    private async Task<(HttpClient Client, string Email, AuthResponse Auth)> CreateAuthenticatedClientAsync(
        string? email = null,
        string? password = null)
    {
        var client = NewClient();
        var (createdEmail, auth) = await AuthHelpers.CreateAndLoginAsync(client, factory.Services, email, password);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, createdEmail, auth);
    }

    [Fact]
    public async Task LogoutAll_RevokesAllRefreshTokens()
    {
        var email = $"logoutall_{Guid.NewGuid():N}@test.com";
        await AuthHelpers.CreateConfirmedUserAsync(factory.Services, email);

        var loginClientA = NewClient();
        var loginClientB = NewClient();
        var authA = await AuthHelpers.LoginAsync(loginClientA, email);
        var authB = await AuthHelpers.LoginAsync(loginClientB, email);

        loginClientA.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authA.AccessToken);

        var logoutAllResponse = await loginClientA.PostAsync("/api/auth/logout-all", null);

        Assert.Equal(HttpStatusCode.NoContent, logoutAllResponse.StatusCode);

        var refreshA = await SessionCookies.RefreshAsync(loginClientA);
        var refreshB = await SessionCookies.RefreshAsync(loginClientB);

        Assert.Equal(HttpStatusCode.Unauthorized, refreshA.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshB.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithValidCurrentPassword_UpdatesCredentials()
    {
        var (client, email, _) = await CreateAuthenticatedClientAsync();
        const string newPassword = "NewTest1234!";

        var response = await client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest(
            AuthHelpers.DefaultPassword,
            newPassword));

        // 200 y no 204 desde T6-01: el cambio emite una sesión nueva y hay que devolverla.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var oldLogin = await NewClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(email, AuthHelpers.DefaultPassword));
        var newLogin = await NewClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(email, newPassword));

        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    /// <summary>
    /// T6-01 — Cambiar la contraseña cierra **todas** las sesiones, no solo la del navegador
    /// que la cambió.
    ///
    /// <para>Este test reproduce paso por paso el experimento con el que se encontró el
    /// defecto el 2026-09-06: login, guardar la cookie, cambiar la contraseña, y volver a
    /// refrescar con la cookie de antes. Antes de la corrección devolvía 200 y un token
    /// nuevo, así que el token robado seguía rotando durante siete días justo después de la
    /// acción que existe para cortarlo.</para>
    ///
    /// <para>Se comprueban las dos mitades que importan y que son distintas entre sí: la
    /// cookie **anterior** del propio dispositivo que hizo el cambio, y la de **otro**
    /// dispositivo que ni se enteró. Es esta segunda la que se corresponde con el caso real
    /// —el atacante está en otra máquina—, y la que quedaría verde por accidente si algún día
    /// alguien «arreglara» esto revocando solo el token presentado en la petición.</para>
    /// </summary>
    [Fact]
    public async Task ChangePassword_RevokesEverySessionIncludingOtherDevices()
    {
        var email = $"chpwd_{Guid.NewGuid():N}@test.com";

        // Dispositivo A: el que cambia la contraseña. Se captura su cookie antes de nada.
        var deviceA = NewClient();
        var (_, authA, cookieBeforeChange) =
            await AuthHelpers.CreateAndLoginCapturingCookieAsync(deviceA, factory.Services, email);
        deviceA.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authA.AccessToken);

        // Dispositivo B: una segunda sesión abierta que no participa en el cambio.
        var deviceB = NewClient();
        await AuthHelpers.LoginAsync(deviceB, email);

        var change = await deviceA.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest(
            AuthHelpers.DefaultPassword,
            "NewTest1234!"));
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);

        // La cookie vieja de A se presenta a mano, con un cliente sin contenedor: el de A ya
        // guardó la cookie nueva al recibir la respuesta y mandaría esa en su lugar.
        var refreshWithOldCookie = await SessionCookies.RefreshAsync(
            SessionCookies.CreateClientWithoutCookies(factory), cookieBeforeChange);
        var refreshFromOtherDevice = await SessionCookies.RefreshAsync(deviceB);

        Assert.Equal(HttpStatusCode.Unauthorized, refreshWithOldCookie.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshFromOtherDevice.StatusCode);
    }

    /// <summary>
    /// T6-01, la otra mitad: revocar no puede echar de casa a quien cambió la contraseña.
    ///
    /// <para>Sin esto, la corrección tendría un final previsible: el usuario cambia la
    /// contraseña, ve la pantalla de login al minuto siguiente y aprende a no cambiarla.
    /// De ahí que la respuesta pase de 204 a 200 con el mismo cuerpo que el login.</para>
    ///
    /// <para>No basta con mirar que venga una cookie: se usa para refrescar de verdad, porque
    /// una cookie emitida **antes** de la revocación llegaría igual de bien en la respuesta y
    /// estaría muerta al primer uso. Ese es exactamente el error de orden que el comentario
    /// del controlador advierte.</para>
    /// </summary>
    [Fact]
    public async Task ChangePassword_LeavesTheCallerWithAUsableSession()
    {
        var (client, _, _) = await CreateAuthenticatedClientAsync();

        var change = await client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest(
            AuthHelpers.DefaultPassword,
            "NewTest1234!"));

        Assert.Equal(HttpStatusCode.OK, change.StatusCode);

        // El cuerpo se lee una sola vez y de ahí salen las dos afirmaciones: leerlo dos veces
        // depende de que el contenido esté bufferizado, que es cierto hoy y no es un contrato.
        var body = await change.Content.ReadAsStringAsync();
        var session = JsonSerializer.Deserialize<AuthResponse>(body, JsonSerializerOptions.Web);

        Assert.NotNull(session);
        Assert.False(string.IsNullOrWhiteSpace(session!.AccessToken));

        // El refresco en claro no viaja nunca en el cuerpo (T4-01): solo en la cookie.
        Assert.DoesNotContain(SessionCookies.Read(change), body, StringComparison.Ordinal);

        var refreshed = await SessionCookies.RefreshAsync(client);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_Returns400()
    {
        var (client, _, _) = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest(
            "incorrecta",
            "NewTest1234!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_ReturnsUpdatedUser()
    {
        var (client, email, _) = await CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/auth/profile", new UpdateProfileRequest("  Nuevo Nombre  "));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(user);
        Assert.Equal(email, user!.Email);
        Assert.Equal("Nuevo Nombre", user.DisplayName);
    }

    [Fact]
    public async Task GetAuthMethods_ReturnsConfiguredFlags()
    {
        var response = await NewClient().GetAsync("/api/auth/methods");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AuthMethodsResponse>();
        Assert.NotNull(body);
        Assert.True(body!.ManualEnabled);
        Assert.False(body.GoogleEnabled);
        Assert.False(body.GitHubEnabled);
    }
}
