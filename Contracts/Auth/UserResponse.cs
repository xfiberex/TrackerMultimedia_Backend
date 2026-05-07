namespace TrackerMultimedia.Contracts.Auth;

/// <summary>Datos del usuario devueltos al cliente (sin datos sensibles).</summary>
public record UserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    /// <summary>Si el usuario confirmó su dirección de correo.</summary>
    bool EmailConfirmed,
    /// <summary>Si la cuenta tiene contraseña local (false en cuentas solo-OAuth).</summary>
    bool HasPassword,
    /// <summary>Proveedores OAuth vinculados, p. ej. ["google", "github"].</summary>
    IReadOnlyList<string> LinkedProviders);
