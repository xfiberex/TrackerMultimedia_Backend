namespace TrackerMultimedia.Services;

/// <summary>
/// Contrato para el servicio de autenticación de GitHub OAuth 2.0.
/// </summary>
public interface IGitHubAuthService
{
    /// <summary>Construye la URL de autorización de GitHub con el state anti-CSRF.</summary>
    string BuildAuthorizationUrl(string state);

    /// <summary>
    /// Intercambia el código de autorización por tokens de GitHub y
    /// devuelve el perfil del usuario verificado.
    /// Lanza <see cref="InvalidOperationException"/> si el usuario no tiene
    /// un email primario verificado en su cuenta de GitHub.
    /// </summary>
    Task<ExternalUserProfile> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default);
}
