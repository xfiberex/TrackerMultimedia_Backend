namespace TrackerMultimedia.Infrastructure.Options;

/// <summary>
/// Configuración OAuth para un proveedor externo (Google, GitHub…).
/// Todos los valores secretos deben cargarse desde User Secrets o
/// variables de entorno, NUNCA en appsettings.json en texto claro.
/// </summary>
public sealed class OAuthProviderOptions
{
    /// <summary>Habilita este proveedor. Por defecto false.</summary>
    public bool Enabled { get; init; } = false;

    /// <summary>Client ID de la aplicación registrada en el proveedor.</summary>
    public string ClientId { get; init; } = string.Empty;

    /// <summary>Client Secret (cargar desde secretos).</summary>
    public string ClientSecret { get; init; } = string.Empty;

    /// <summary>Client ID alternativo para desarrollo local.</summary>
    public string DevClientId { get; init; } = string.Empty;

    /// <summary>Client Secret alternativo para desarrollo local.</summary>
    public string DevClientSecret { get; init; } = string.Empty;

    /// <summary>
    /// URI de callback que el proveedor redirigirá tras autorizar.
    /// Debe coincidir exactamente con la URL registrada en la consola del proveedor.
    /// P. ej. "https://localhost:5001/api/auth/google/callback"
    /// </summary>
    public string RedirectUri { get; init; } = string.Empty;

    /// <summary>Scopes adicionales. Los mínimos (openid email profile) se añaden siempre.</summary>
    public IReadOnlyList<string> ExtraScopes { get; init; } = [];

    /// <summary>Tiempo de vida del parámetro state anti-CSRF (minutos).</summary>
    public int StateTtlMinutes { get; init; } = 10;
}

/// <summary>
/// Raíz de la sección "OAuth" en la configuración.
/// </summary>
public sealed class OAuthOptions
{
    public const string SectionName = "OAuth";

    public OAuthProviderOptions Google { get; init; } = new();
    public OAuthProviderOptions GitHub { get; init; } = new();
}
