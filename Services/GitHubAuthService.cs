using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TrackerMultimedia.Infrastructure.Options;

namespace TrackerMultimedia.Services;

/// <summary>
/// Implementación de <see cref="IGitHubAuthService"/> para GitHub OAuth 2.0.
/// Consulta /user y /user/emails para obtener el email primario verificado,
/// que es obligatorio para crear o vincular la cuenta local.
/// </summary>
public sealed class GitHubAuthService(
    HttpClient httpClient,
    IOptions<OAuthOptions> options,
    ILogger<GitHubAuthService> logger) : IGitHubAuthService
{
    private readonly OAuthProviderOptions _opts = options.Value.GitHub;

    private static readonly IReadOnlyList<string> BaseScopes = ["read:user", "user:email"];

    public string BuildAuthorizationUrl(string state)
    {
        var scopes = BaseScopes.Concat(_opts.ExtraScopes).Distinct();
        var query = new Dictionary<string, string>
        {
            ["client_id"] = _opts.ClientId,
            ["redirect_uri"] = _opts.RedirectUri,
            ["scope"] = string.Join(" ", scopes),
            ["state"] = state,
        };

        return "https://github.com/login/oauth/authorize?" +
               string.Join("&", query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
    }

    public async Task<ExternalUserProfile> ExchangeCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        // 1. Intercambiar código por access token
        var accessToken = await ExchangeCodeForAccessTokenAsync(code, cancellationToken);

        // 2. Obtener perfil básico del usuario
        using var userRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
        AddGitHubHeaders(userRequest, accessToken);
        var userResponse = await httpClient.SendAsync(userRequest, cancellationToken);
        userResponse.EnsureSuccessStatusCode();
        var userJson = await userResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);

        var githubId = userJson.GetProperty("id").GetInt64().ToString();
        var login = userJson.TryGetProperty("login", out var loginProp) ? loginProp.GetString() : null;
        var name = userJson.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
        var avatar = userJson.TryGetProperty("avatar_url", out var avProp) ? avProp.GetString() : null;

        // 3. Obtener emails y exigir uno primario verificado
        using var emailRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/emails");
        AddGitHubHeaders(emailRequest, accessToken);
        var emailResponse = await httpClient.SendAsync(emailRequest, cancellationToken);
        emailResponse.EnsureSuccessStatusCode();
        var emails = await emailResponse.Content.ReadFromJsonAsync<JsonElement[]>(cancellationToken: cancellationToken) ?? [];

        var primaryEmail = emails
            .Where(e =>
                e.TryGetProperty("primary", out var p) && p.GetBoolean() &&
                e.TryGetProperty("verified", out var v) && v.GetBoolean())
            .Select(e => e.TryGetProperty("email", out var em) ? em.GetString() : null)
            .FirstOrDefault(e => e is not null);

        if (primaryEmail is null)
        {
            logger.LogWarning("GitHub id={GitHubId} no tiene email primario verificado", githubId);
            throw new InvalidOperationException(
                "Tu cuenta de GitHub no tiene un email primario verificado. " +
                "Verifica tu email en GitHub e inténtalo de nuevo.");
        }

        logger.LogInformation("Perfil de GitHub obtenido para id={GitHubId}", githubId);
        return new ExternalUserProfile(githubId, primaryEmail.ToLowerInvariant(), name ?? login, avatar);
    }

    // -------------------------------------------------------------------------

    private async Task<string> ExchangeCodeForAccessTokenAsync(string code, CancellationToken ct)
    {
        var body = new Dictionary<string, string>
        {
            ["client_id"] = _opts.ClientId,
            ["client_secret"] = _opts.ClientSecret,
            ["redirect_uri"] = _opts.RedirectUri,
            ["code"] = code,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
        {
            Content = new FormUrlEncodedContent(body),
        };
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Error al intercambiar código con GitHub: {Error}", err);
            throw new InvalidOperationException("No se pudo intercambiar el código con GitHub.");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        return json.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("GitHub no devolvió access_token.");
    }

    private static void AddGitHubHeaders(HttpRequestMessage request, string accessToken)
    {
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.ParseAdd("TrackerMultimedia/1.0");
    }
}
