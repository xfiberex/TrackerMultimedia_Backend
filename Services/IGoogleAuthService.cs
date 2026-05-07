namespace TrackerMultimedia.Services;

/// <summary>
/// Contrato para el servicio de autenticación de Google OAuth 2.0.
/// </summary>
public interface IGoogleAuthService
{
    /// <summary>Construye la URL de autorización de Google con el state anti-CSRF.</summary>
    string BuildAuthorizationUrl(string state);

    /// <summary>
    /// Intercambia el código de autorización por tokens de Google y
    /// devuelve el perfil del usuario verificado.
    /// </summary>
    Task<ExternalUserProfile> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default);
}
