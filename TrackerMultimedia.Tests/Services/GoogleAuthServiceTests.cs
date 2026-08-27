using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrackerMultimedia.Infrastructure.Options;
using TrackerMultimedia.Services;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.Services;

public class GoogleAuthServiceTests
{
    [Fact]
    public void BuildAuthorizationUrl_IncludesStateAndConfiguredScopes()
    {
        var service = CreateService((_, _) => throw new NotSupportedException(), new OAuthProviderOptions
        {
            ClientId = "google-client",
            ClientSecret = "secret",
            RedirectUri = "http://localhost/google-callback",
            ExtraScopes = ["calendar.readonly"]
        });

        var url = service.BuildAuthorizationUrl("state-123");

        Assert.Contains("client_id=google-client", url, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString("http://localhost/google-callback"), url, StringComparison.Ordinal);
        Assert.Contains("state=state-123", url, StringComparison.Ordinal);
        Assert.Contains("calendar.readonly", url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExchangeCodeAsync_ReturnsNormalizedProfile()
    {
        var service = CreateService((request, _) =>
        {
            if (request.RequestUri!.ToString().Contains("oauth2.googleapis.com/token", StringComparison.Ordinal))
            {
                return Task.FromResult(DelegateHttpMessageHandler.Json("""{ "access_token": "google-token" }"""));
            }

            return Task.FromResult(DelegateHttpMessageHandler.Json(
                """
                {
                  "email": "USER@Example.com",
                  "email_verified": true,
                  "sub": "google-user-1",
                  "name": "Google User",
                  "picture": "https://img.test/google.png"
                }
                """));
        });

        var profile = await service.ExchangeCodeAsync("code-123");

        Assert.Equal("google-user-1", profile.ProviderUserId);
        Assert.Equal("user@example.com", profile.Email);
        Assert.Equal("Google User", profile.DisplayName);
        Assert.Equal("https://img.test/google.png", profile.AvatarUrl);
    }

    [Fact]
    public async Task ExchangeCodeAsync_UnverifiedEmail_Throws()
    {
        var service = CreateService((request, _) =>
        {
            if (request.RequestUri!.ToString().Contains("oauth2.googleapis.com/token", StringComparison.Ordinal))
            {
                return Task.FromResult(DelegateHttpMessageHandler.Json("""{ "access_token": "google-token" }"""));
            }

            return Task.FromResult(DelegateHttpMessageHandler.Json(
                """
                {
                  "email": "user@example.com",
                  "email_verified": false,
                  "sub": "google-user-2"
                }
                """));
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExchangeCodeAsync("code-123"));
    }

    private static GoogleAuthService CreateService(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler,
        OAuthProviderOptions? googleOptions = null)
    {
        var httpClient = new HttpClient(new DelegateHttpMessageHandler(handler));
        var options = Options.Create(new OAuthOptions
        {
            Google = googleOptions ?? new OAuthProviderOptions
            {
                ClientId = "google-client",
                ClientSecret = "secret",
                RedirectUri = "http://localhost/google-callback"
            }
        });

        return new GoogleAuthService(httpClient, options, NullLogger<GoogleAuthService>.Instance);
    }
}