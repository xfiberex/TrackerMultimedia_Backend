namespace TrackerMultimedia.Contracts.Auth;

/// <summary>
/// Respuesta de login y refresh. El frontend guarda accessToken en memoria
/// y refreshToken en localStorage para persistir la sesión entre recargas.
/// </summary>
public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    /// <summary>Vida útil del access token en segundos.</summary>
    int ExpiresIn,
    UserResponse User);
