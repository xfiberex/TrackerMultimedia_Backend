namespace TrackerMultimedia.Infrastructure.Options;

/// <summary>
/// Purga periódica de filas caducadas. Ninguna de las dos tablas afectadas se
/// limpiaba nunca: cada inicio de sesión dejaba una fila en <c>RefreshTokens</c>
/// que sobrevivía revocada y caducada para siempre, y cada intento de OAuth
/// dejaba un <c>OAuthState</c> con el email del perfil externo.
/// </summary>
public sealed class CleanupOptions
{
    public const string SectionName = "Cleanup";

    /// <summary>Permite desactivar la purga por completo, p. ej. en tests.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Cada cuánto se ejecuta. Por debajo de un minuto se ignora.</summary>
    public int IntervalHours { get; init; } = 6;

    /// <summary>
    /// Días que se conserva un token de refresco después de caducar o de ser
    /// revocado. No es un requisito funcional —un token caducado ya se rechaza
    /// en el momento de usarse—, sino un margen por si más adelante se añade
    /// detección de reutilización, que necesita ver el token revocado.
    /// </summary>
    public int RefreshTokenRetentionDays { get; init; } = 7;

    /// <summary>
    /// Días que se conserva un estado OAuth después de caducar. Es corto a
    /// propósito: la fila guarda el email y el nombre del perfil externo, y su
    /// vida útil real son los diez minutos que dura el flujo.
    /// </summary>
    public int OAuthStateRetentionDays { get; init; } = 1;
}
