namespace TrackerMultimedia.Contracts.Auth;

/// <summary>
/// Respuesta de login, refresh y vinculación OAuth.
///
/// **No lleva el token de refresco.** Viaja en una cookie <c>HttpOnly</c> que el cliente
/// no puede leer ni necesita leer, así que exponerlo aquí sería devolver por el cuerpo
/// justo lo que la cookie existe para esconder. El access token sí viene: vive en memoria
/// del módulo durante la sesión y nunca se persiste.
/// </summary>
public record AuthResponse(
    string AccessToken,
    /// <summary>Vida útil del access token en segundos.</summary>
    int ExpiresIn,
    UserResponse User);
