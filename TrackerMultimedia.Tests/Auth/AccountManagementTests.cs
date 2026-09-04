using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

        var refreshA = await loginClientA.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(authA.RefreshToken));
        var refreshB = await loginClientB.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(authB.RefreshToken));

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

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var oldLogin = await NewClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(email, AuthHelpers.DefaultPassword));
        var newLogin = await NewClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(email, newPassword));

        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
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
