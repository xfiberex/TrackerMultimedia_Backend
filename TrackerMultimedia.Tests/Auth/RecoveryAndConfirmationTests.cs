using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using TrackerMultimedia.Contracts.Auth;
using TrackerMultimedia.Domain.Entities;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.Auth;

public class RecoveryAndConfirmationTests(AppFactory factory) : IClassFixture<AppFactory>
{
    private HttpClient NewClient() => factory.CreateClient();

    [Fact]
    public async Task ForgotPassword_ExistingUser_SendsResetEmail()
    {
        var email = $"forgot_{Guid.NewGuid():N}@test.com";
        await AuthHelpers.CreateConfirmedUserAsync(factory.Services, email);
        factory.EmailInbox.Clear();

        var response = await NewClient().PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(factory.EmailInbox.Messages);

        var sentEmail = factory.EmailInbox.Messages.Single();
        Assert.Equal(email, sentEmail.To);
        Assert.Contains("Restablecer contraseña", sentEmail.Subject);
        Assert.Contains("reset-password", sentEmail.HtmlBody);
    }

    [Fact]
    public async Task ForgotPassword_UnknownUser_ReturnsGenericResponseWithoutEmail()
    {
        factory.EmailInbox.Clear();

        var response = await NewClient().PostAsJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest($"missing_{Guid.NewGuid():N}@test.com"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(factory.EmailInbox.Messages);
    }

    [Fact]
    public async Task ResetPassword_ValidToken_ChangesPasswordAndRevokesSessions()
    {
        var email = $"reset_{Guid.NewGuid():N}@test.com";
        await AuthHelpers.CreateConfirmedUserAsync(factory.Services, email);

        var loginClient = NewClient();
        var auth = await AuthHelpers.LoginAsync(loginClient, email);
        var token = await AuthHelpers.GeneratePasswordResetTokenAsync(factory.Services, email);
        const string newPassword = "ResetTest1234!";

        var resetResponse = await NewClient().PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest(email, token, newPassword));

        Assert.Equal(HttpStatusCode.NoContent, resetResponse.StatusCode);

        var refreshResponse = await NewClient().PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken));
        var oldLoginResponse = await NewClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(email, AuthHelpers.DefaultPassword));
        var newLoginResponse = await NewClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(email, newPassword));

        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLoginResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newLoginResponse.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_Returns400()
    {
        var email = $"resetinvalid_{Guid.NewGuid():N}@test.com";
        await AuthHelpers.CreateConfirmedUserAsync(factory.Services, email);

        var response = await NewClient().PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest(email, "token-invalido", "ResetTest1234!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmEmail_ValidToken_ConfirmsUser()
    {
        var email = $"confirmendpoint_{Guid.NewGuid():N}@test.com";
        await AuthHelpers.CreateUserAsync(factory.Services, email, AuthHelpers.DefaultPassword, emailConfirmed: false);
        var token = await AuthHelpers.GenerateEmailConfirmationTokenAsync(factory.Services, email);

        var response = await NewClient().PostAsJsonAsync(
            "/api/auth/confirm-email",
            new ConfirmEmailRequest(email, token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.True(user!.EmailConfirmed);
    }

    [Fact]
    public async Task ConfirmEmail_AlreadyConfirmed_Returns200()
    {
        var email = $"alreadyconfirmed_{Guid.NewGuid():N}@test.com";
        await AuthHelpers.CreateConfirmedUserAsync(factory.Services, email);

        var response = await NewClient().PostAsJsonAsync(
            "/api/auth/confirm-email",
            new ConfirmEmailRequest(email, "cualquier-token"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmEmail_InvalidToken_Returns400()
    {
        var email = $"badconfirm_{Guid.NewGuid():N}@test.com";
        await AuthHelpers.CreateUserAsync(factory.Services, email, AuthHelpers.DefaultPassword, emailConfirmed: false);

        var response = await NewClient().PostAsJsonAsync(
            "/api/auth/confirm-email",
            new ConfirmEmailRequest(email, "token-invalido"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResendConfirmation_PendingUser_SendsEmail()
    {
        var email = $"resend_{Guid.NewGuid():N}@test.com";
        await AuthHelpers.CreateUserAsync(factory.Services, email, AuthHelpers.DefaultPassword, emailConfirmed: false);
        factory.EmailInbox.Clear();

        var response = await NewClient().PostAsJsonAsync(
            "/api/auth/resend-confirmation",
            new ResendConfirmationRequest(email));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(factory.EmailInbox.Messages);

        var sentEmail = factory.EmailInbox.Messages.Single();
        Assert.Equal(email, sentEmail.To);
        Assert.Contains("Confirma tu cuenta", sentEmail.Subject);
        Assert.Contains("confirm-email", sentEmail.HtmlBody);
    }

    [Fact]
    public async Task ResendConfirmation_ConfirmedUser_ReturnsGenericResponseWithoutEmail()
    {
        var email = $"resendconfirmed_{Guid.NewGuid():N}@test.com";
        await AuthHelpers.CreateConfirmedUserAsync(factory.Services, email);
        factory.EmailInbox.Clear();

        var response = await NewClient().PostAsJsonAsync(
            "/api/auth/resend-confirmation",
            new ResendConfirmationRequest(email));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(factory.EmailInbox.Messages);
    }
}