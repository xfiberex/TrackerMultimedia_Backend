namespace TrackerMultimedia.Infrastructure.Options;

/// <summary>
/// Qué proxies pueden decirle a la aplicación cuál es la IP real del cliente (T1-05).
///
/// El valor por defecto —las dos listas vacías— significa **no confiar en nadie**, y es
/// el correcto para el uso local y por LAN: ahí no hay ningún proxy inverso delante, así
/// que <c>X-Forwarded-For</c> solo puede venir de quien quiera falsearla. Con las listas
/// vacías el middleware ni se registra y <c>RemoteIpAddress</c> es siempre la dirección
/// real del socket.
///
/// Al desplegar detrás de un proxy hay que declarar sus direcciones o sus redes, y solo
/// entonces se atiende a la cabecera. Es deliberado que haya que escribirlo: la versión
/// anterior hacía <c>KnownProxies.Clear()</c> y aceptaba la cabecera de cualquier origen,
/// lo que permitía saltarse el límite de 10 peticiones/minuto que protege login y
/// registro sin más que rotar el valor en cada intento.
/// </summary>
public sealed class ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeaders";

    /// <summary>
    /// Direcciones IP concretas de los proxies de confianza, p. ej. <c>["10.0.0.7"]</c>.
    /// </summary>
    public string[] KnownProxies { get; init; } = [];

    /// <summary>
    /// Redes de confianza en notación CIDR, p. ej. <c>["10.0.0.0/8"]</c>. Es lo habitual
    /// en un PaaS, donde la IP concreta del proxy no es estable.
    /// </summary>
    public string[] KnownNetworks { get; init; } = [];

    /// <summary>
    /// Cuántas entradas de <c>X-Forwarded-For</c> se recorren, de la más cercana hacia
    /// atrás. El valor por defecto de ASP.NET Core es 1, que es lo correcto con un único
    /// proxy: subirlo sin tener esa cantidad de proxies encadenados vuelve a permitir que
    /// el cliente inyecte la dirección que quiera.
    /// </summary>
    public int ForwardLimit { get; init; } = 1;

    /// <summary>
    /// Solo se procesan los <c>X-Forwarded-*</c> si hay al menos un proxy o una red
    /// declarados. Sin eso no hay forma de distinguir la cabecera legítima de la falsa.
    /// </summary>
    public bool IsEnabled => KnownProxies.Length > 0 || KnownNetworks.Length > 0;
}
