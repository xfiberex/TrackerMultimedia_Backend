using Microsoft.Extensions.Options;
using TrackerMultimedia.Infrastructure.Options;

namespace TrackerMultimedia.Infrastructure.Http;

/// <summary>
/// Único sitio que conoce los atributos de la cookie del token de refresco.
///
/// Se centraliza por la misma razón que <c>AuthSessionService</c> centraliza la emisión
/// de sesiones: hay seis endpoints que la escriben o la borran, y basta con que uno se
/// deje <c>HttpOnly</c> para que el valor vuelva a ser legible desde JavaScript sin que
/// nada falle a la vista.
/// </summary>
public sealed class RefreshTokenCookie(
    IOptions<RefreshCookieOptions> options,
    IWebHostEnvironment environment)
{
    private readonly RefreshCookieOptions _options = options.Value;

    /// <summary>Nombre de la cookie, para los tests y para el borrado.</summary>
    public string Name => _options.Name;

    /// <summary>
    /// Escribe la cookie con el token en claro. <paramref name="lifetime"/> debe ser la
    /// misma vida que tiene el token en base de datos: una cookie que sobreviva al token
    /// solo consigue que el navegador mande algo que el servidor ya rechaza.
    /// </summary>
    public void Write(HttpResponse response, string rawToken, TimeSpan lifetime)
        => response.Cookies.Append(_options.Name, rawToken, BuildOptions(DateTimeOffset.UtcNow.Add(lifetime)));

    /// <summary>Devuelve el token de la cookie, o null si no viene.</summary>
    public string? Read(HttpRequest request)
    {
        var value = request.Cookies[_options.Name];
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// Borra la cookie. Los atributos tienen que coincidir con los de escritura —path y
    /// SameSite incluidos— o el navegador trata el Set-Cookie como el de otra cookie
    /// distinta y deja la original donde estaba.
    /// </summary>
    public void Delete(HttpResponse response)
        => response.Cookies.Delete(_options.Name, BuildOptions(DateTimeOffset.UnixEpoch));

    private CookieOptions BuildOptions(DateTimeOffset expiresAt) => new()
    {
        HttpOnly = true,
        Secure = _options.Secure ?? !environment.IsDevelopment(),
        SameSite = ParseSameSite(_options.SameSite),
        Path = _options.Path,
        Expires = expiresAt,
        IsEssential = true,
    };

    private static SameSiteMode ParseSameSite(string value) => value.ToLowerInvariant() switch
    {
        "none" => SameSiteMode.None,
        "lax" => SameSiteMode.Lax,
        "strict" => SameSiteMode.Strict,
        _ => throw new InvalidOperationException(
            $"'{RefreshCookieOptions.SectionName}:SameSite' debe ser Strict, Lax o None; se recibió '{value}'."),
    };
}
