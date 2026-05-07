using Microsoft.AspNetCore.Identity;

namespace TrackerMultimedia.Domain.Entities;

/// <summary>
/// Usuario de la aplicación. Extiende IdentityUser con campos personalizados.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>Nombre visible del usuario en la interfaz.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Fecha de creación de la cuenta (UTC).</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Tokens de refresco activos para este usuario.</summary>
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];

    /// <summary>Logins externos (Google, GitHub…) vinculados. Propiedad de navegación de IdentityUserLogin.</summary>
    public ICollection<IdentityUserLogin<Guid>>? ExternalLogins { get; set; }
}
