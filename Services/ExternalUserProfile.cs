namespace TrackerMultimedia.Services;

/// <summary>
/// Perfil de usuario normalizado tal como lo devuelve un proveedor externo.
/// Las implementaciones de cada proveedor convierten su respuesta a este tipo.
/// </summary>
public record ExternalUserProfile(
    /// <summary>ID único del usuario en el proveedor (no exponer al cliente).</summary>
    string ProviderUserId,
    /// <summary>Correo electrónico verificado por el proveedor.</summary>
    string Email,
    /// <summary>Nombre a mostrar, p. ej. displayName de GitHub o name de Google.</summary>
    string? DisplayName,
    /// <summary>URL del avatar del proveedor (opcional, para uso futuro).</summary>
    string? AvatarUrl);
