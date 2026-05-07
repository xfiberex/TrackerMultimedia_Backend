namespace TrackerMultimedia.Domain.Entities;

/// <summary>
/// Token de refresco persistido en base de datos.
/// Sólo se almacena el hash SHA-256; nunca el token en claro.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Hash SHA-256 (Base64) del token original.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>Si es true, el token ya no es válido (logout o rotación).</summary>
    public bool IsRevoked { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
