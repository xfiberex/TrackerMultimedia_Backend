using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrackerMultimedia.Infrastructure.Options;
using TrackerMultimedia.Services;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.Services;

public class GitHubAuthServiceTests
{
    [Fact]
    public void BuildAuthorizationUrl_IncludesStateAndScopes()
    {
        var service = CreateService((_, _) => throw new NotSupportedException(), new OAuthProviderOptions
        {
            ClientId = "github-client",
            ClientSecret = "secret",
            RedirectUri = "http://localhost/github-callback",
            ExtraScopes = ["repo"]
        });

        var url = service.BuildAuthorizationUrl("state-abc");

        Assert.Contains("client_id=github-client", url, StringComparison.Ordinal);
        Assert.Contains("state=state-abc", url, StringComparison.Ordinal);
        Assert.Contains("repo", url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExchangeCodeAsync_ReturnsPrimaryVerifiedEmail()
    {
        var service = CreateService((request, _) =>
        {
            var uri = request.RequestUri!.ToString();
            if (uri.Contains("github.com/login/oauth/access_token", StringComparison.Ordinal))
            {
                return Task.FromResult(DelegateHttpMessageHandler.Json("""{ "access_token": "github-token" }"""));
            }

            if (uri.Contains("api.github.com/user/emails", StringComparison.Ordinal))
            {
                return Task.FromResult(DelegateHttpMessageHandler.Json(
                    """
                    [
                      { "email": "PRIMARY@Example.com", "primary": true, "verified": true },
                      { "email": "other@example.com", "primary": false, "verified": true }
                    ]
                    """));
            }

            return Task.FromResult(DelegateHttpMessageHandler.Json(
                """
                {
                  "id": 42,
                  "login": "github-user",
                  "name": "GitHub User",
                  "avatar_url": "https://img.test/github.png"
                }
                """));
        });

        var profile = await service.ExchangeCodeAsync("code-123");

        Assert.Equal("42", profile.ProviderUserId);
        Assert.Equal("primary@example.com", profile.Email);
        Assert.Equal("GitHub User", profile.DisplayName);
        Assert.Equal("https://img.test/github.png", profile.AvatarUrl);
    }

    [Fact]
    public async Task ExchangeCodeAsync_WithoutVerifiedPrimaryEmail_Throws()
    {
        var service = CreateService((request, _) =>
        {
            var uri = request.RequestUri!.ToString();
            if (uri.Contains("github.com/login/oauth/access_token", StringComparison.Ordinal))
            {
                return Task.FromResult(DelegateHttpMessageHandler.Json("""{ "access_token": "github-token" }"""));
            }

            if (uri.Contains("api.github.com/user/emails", StringComparison.Ordinal))
            {
                return Task.FromResult(DelegateHttpMessageHandler.Json(
                    """
                    [
                      { "email": "user@example.com", "primary": true, "verified": false }
                    ]
                    """));
            }

            return Task.FromResult(DelegateHttpMessageHandler.Json(
                """
                {
                  "id": 42,
                  "login": "github-user"
                }
                """));
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExchangeCodeAsync("code-123"));
    }

    /// <summary>
    /// T2-06. Con un código caducado o ya usado, GitHub responde <b>200</b> con
    /// <c>{"error":"bad_verification_code"}</c>: el estado HTTP no delata nada y
    /// <c>GetProperty("access_token")</c> lanzaba <c>KeyNotFoundException</c>, que
    /// nadie capturaba. Ahora se traduce a <c>InvalidOperationException</c>, que es
    /// lo que el callback sabe convertir en una vuelta al login.
    /// </summary>
    [Fact]
    public async Task ExchangeCodeAsync_WhenGitHubReturns200WithAnErrorBody_ThrowsInvalidOperation()
    {
        var service = CreateService((_, _) => Task.FromResult(DelegateHttpMessageHandler.Json(
            """
            {
              "error": "bad_verification_code",
              "error_description": "The code passed is incorrect or expired."
            }
            """)));

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExchangeCodeAsync("code-caducado"));

        // El texto del proveedor se queda en el log; al usuario no le llega.
        Assert.DoesNotContain("bad_verification_code", excepcion.Message, StringComparison.Ordinal);
    }

    private static GitHubAuthService CreateService(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler,
        OAuthProviderOptions? githubOptions = null)
    {
        var httpClient = new HttpClient(new DelegateHttpMessageHandler(handler));
        var options = Options.Create(new OAuthOptions
        {
            GitHub = githubOptions ?? new OAuthProviderOptions
            {
                ClientId = "github-client",
                ClientSecret = "secret",
                RedirectUri = "http://localhost/github-callback"
            }
        });

        // IConfiguration vacía — el servicio sólo la usa para leer dev-credentials,
        // que en tests se sobreescriben vía OAuthProviderOptions directamente.
        var configuration = new ConfigurationBuilder().Build();

        // IHostEnvironment apuntando a Production para que no se apliquen sobreescrituras dev.
        var env = new FakeHostEnvironment("Production");

        return new GitHubAuthService(httpClient, options, configuration, env, NullLogger<GitHubAuthService>.Instance);
    }

    // ---------------------------------------------------------------------------
    // Fake mínimo de IHostEnvironment (no necesita NSubstitute ni Moq)
    // ---------------------------------------------------------------------------
    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "TrackerMultimedia.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}