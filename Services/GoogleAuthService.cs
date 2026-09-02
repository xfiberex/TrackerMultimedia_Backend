using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TrackerMultimedia.Infrastructure.Options;

namespace TrackerMultimedia.Services;

/// <summary>
/// Implementación de <see cref="IGoogleAuthService"/> para Google OAuth 2.0.
/// Intercambia el código por un access token y pide el perfil al endpoint
/// <c>userinfo</c> con ese token. <b>No</b> valida un <c>id_token</c>: el comentario
/// anterior lo afirmaba y era falso. Si algún día se quiere esa validación —que
/// ahorraría la segunda llamada de red— hay que implementarla, no darla por hecha.
/// </summary>
public sealed class GoogleAuthService(
    HttpClient httpClient,
    IOptions<OAuthOptions> options,
    ILogger<GoogleAuthService> logger) : IGoogleAuthService
{
    private readonly OAuthProviderOptions _opts = options.Value.Google;

    private static readonly IReadOnlyList<string> BaseScopes = ["openid", "email", "profile"];

    public string BuildAuthorizationUrl(string state)
    {
        var scopes = BaseScopes.Concat(_opts.ExtraScopes).Distinct();
        var query = new Dictionary<string, string>
        {
            ["client_id"] = _opts.ClientId,
            ["redirect_uri"] = _opts.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = string.Join(" ", scopes),
            ["state"] = state,
            ["access_type"] = "online",
        };

        return "https://accounts.google.com/o/oauth2/v2/auth?" +
               string.Join("&", query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
    }

    public async Task<ExternalUserProfile> ExchangeCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        // 1. Intercambiar código por tokens
        var tokenResponse = await ExchangeCodeForTokensAsync(code, cancellationToken);
        // TryGetProperty y no GetProperty: si Google responde 200 sin el campo, lo que
        // salía era KeyNotFoundException, que nadie capturaba y acababa en un 500.
        if (!tokenResponse.TryGetProperty("access_token", out var accessTokenProp))
            throw new InvalidOperationException("Google no devolvió access_token.");

        var accessToken = accessTokenProp.GetString()
            ?? throw new InvalidOperationException("Google no devolvió access_token.");

        // 2. Obtener perfil del usuario con el access token
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v3/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var userResponse = await httpClient.SendAsync(request, cancellationToken);
        userResponse.EnsureSuccessStatusCode();

        var userJson = await userResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);

        var email = userJson.GetProperty("email").GetString()
            ?? throw new InvalidOperationException("Google no devolvió email.");

        // Google garantiza emails verificados en cuentas normales; comprobamos la flag
        var emailVerified = userJson.TryGetProperty("email_verified", out var verifiedProp) &&
                            verifiedProp.GetBoolean();

        if (!emailVerified)
            throw new InvalidOperationException("El email de la cuenta de Google no está verificado.");

        var sub = userJson.GetProperty("sub").GetString()
            ?? throw new InvalidOperationException("Google no devolvió sub.");

        var name = userJson.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
        var picture = userJson.TryGetProperty("picture", out var picProp) ? picProp.GetString() : null;

        logger.LogInformation("Perfil de Google obtenido para sub={Sub}", sub);

        return new ExternalUserProfile(sub, email.ToLowerInvariant(), name, picture);
    }

    // -------------------------------------------------------------------------

    private async Task<JsonElement> ExchangeCodeForTokensAsync(string code, CancellationToken ct)
    {
        var body = new Dictionary<string, string>
        {
            ["client_id"] = _opts.ClientId,
            ["client_secret"] = _opts.ClientSecret,
            ["redirect_uri"] = _opts.RedirectUri,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
        };

        var response = await httpClient.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(body),
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Error al intercambiar código con Google: {Error}", error);
            throw new InvalidOperationException("No se pudo intercambiar el código con Google.");
        }

        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
    }
}
