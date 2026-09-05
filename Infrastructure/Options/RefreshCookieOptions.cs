namespace TrackerMultimedia.Infrastructure.Options;

/// <summary>
/// Atributos de la cookie que transporta el token de refresco.
///
/// Son configurables porque la respuesta correcta depende de si el frontend y el API
/// comparten sitio. En local lo comparten: el navegador carga la aplicación desde el
/// servidor de Vite y este hace de proxy de <c>/api</c>, así que la cookie es
/// first-party y <c>SameSite=Strict</c> vale. En un despliegue con el frontend y el
/// backend en dominios distintos haría falta <c>SameSite=None</c> con <c>Secure</c>,
/// y entonces la única defensa de CSRF que queda es la cabecera obligatoria.
/// </summary>
public sealed class RefreshCookieOptions
{
    public const string SectionName = "Auth:RefreshCookie";

    /// <summary>Nombre de la cookie.</summary>
    public string Name { get; init; } = "tm_refresh";

    /// <summary>
    /// Ruta a la que se limita la cookie. Acotarla a los endpoints de sesión evita
    /// que el navegador la adjunte a todas las demás peticiones del API, que se
    /// autentican con el Bearer en memoria y no la necesitan para nada.
    /// </summary>
    public string Path { get; init; } = "/api/auth";

    /// <summary>
    /// <c>Strict</c>, <c>Lax</c> o <c>None</c>. Con <c>None</c> el navegador exige
    /// además <c>Secure</c>.
    /// </summary>
    public string SameSite { get; init; } = "Strict";

    /// <summary>
    /// Si es null, se decide por entorno: <c>false</c> en Development —donde el
    /// servidor habla HTTP y una cookie <c>Secure</c> sería descartada sin aviso— y
    /// <c>true</c> en cualquier otro.
    /// </summary>
    public bool? Secure { get; init; }
}
