using Microsoft.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using TrackerMultimedia.Infrastructure.Http;

namespace TrackerMultimedia.Tests.Helpers;

/// <summary>
/// Ayudas para los dos endpoints que se autentican con la cookie de refresco.
///
/// Existen porque desde T4-01 una petición a <c>/auth/refresh</c> tiene dos requisitos que
/// no se ven en la URL —la cookie y la cabecera <c>X-TM-Client</c>—, y repetirlos a mano en
/// cada test invita a olvidar uno y a que el test pase por el motivo equivocado.
/// </summary>
public static class SessionCookies
{
    public const string CookieName = "tm_refresh";

    /// <summary>
    /// Cliente que guarda cookies solo, sin la cabecera puesta por defecto: se añade en
    /// cada llamada. Así el test de «sin cabecera devuelve 401» no depende de acordarse
    /// de quitarla.
    /// </summary>
    public static HttpClient CreateClient(AppFactory factory)
        => factory.CreateClient();

    /// <summary>
    /// Cliente que **no** gestiona cookies. Para los tests que necesitan presentar un
    /// valor concreto —el token viejo de una rotación, por ejemplo—, donde dejar que el
    /// contenedor añadiera también el suyo mandaría dos valores para la misma cookie.
    /// </summary>
    public static HttpClient CreateClientWithoutCookies(AppFactory factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    public static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string? cookieValue = null)
        => SendAsync(client, "/api/auth/refresh", cookieValue);

    public static Task<HttpResponseMessage> LogoutAsync(HttpClient client, string? cookieValue = null)
        => SendAsync(client, "/api/auth/logout", cookieValue);

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string path, string? cookieValue)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add(RequireClientHeaderAttribute.HeaderName, "tests");

        if (cookieValue is not null)
            request.Headers.Add("Cookie", $"{CookieName}={cookieValue}");

        return client.SendAsync(request);
    }

    /// <summary>Valor de la cookie escrita en esta respuesta. Falla si no hay ninguna.</summary>
    public static string Read(HttpResponseMessage response)
        => TryRead(response, out var value)
            ? value
            : throw new InvalidOperationException(
                $"La respuesta no trae la cookie '{CookieName}'. Set-Cookie: " +
                string.Join(" | ", SetCookieHeaders(response)));

    public static bool TryRead(HttpResponseMessage response, out string value)
    {
        foreach (var header in SetCookieHeaders(response))
        {
            var parsed = ParseValue(header);
            // Una cookie con valor vacío es un borrado, no una emisión.
            if (parsed is { Length: > 0 })
            {
                value = parsed;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    /// <summary>
    /// Si la respuesta borra la cookie. El borrado es un Set-Cookie con valor vacío y una
    /// caducidad en el pasado; comprobar solo «hay Set-Cookie» daría igual de verde con
    /// una cookie recién emitida.
    /// </summary>
    public static bool WasCleared(HttpResponseMessage response)
        => SetCookieHeaders(response).Any(header =>
            header.StartsWith($"{CookieName}=", StringComparison.Ordinal) &&
            ParseValue(header) is { Length: 0 });

    public static IEnumerable<string> SetCookieHeaders(HttpResponseMessage response)
        => response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];

    /// <summary>Atributos de la cookie tal y como llegan, para comprobarlos literalmente.</summary>
    public static string Attributes(HttpResponseMessage response)
        => SetCookieHeaders(response)
               .FirstOrDefault(header => header.StartsWith($"{CookieName}=", StringComparison.Ordinal))
           ?? throw new InvalidOperationException($"No hay Set-Cookie para '{CookieName}'.");

    private static string? ParseValue(string setCookieHeader)
    {
        if (!SetCookieHeaderValue.TryParse(setCookieHeader, out var parsed)) return null;
        return parsed.Name.Equals(CookieName, StringComparison.Ordinal) ? parsed.Value.ToString() : null;
    }
}
