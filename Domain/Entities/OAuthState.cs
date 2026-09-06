namespace TrackerMultimedia.Domain.Entities;

/// <summary>
/// Almacena el parámetro "state" anti-CSRF de un flujo OAuth pendiente.
/// Se persiste en base de datos hasta que el callback llega o hasta que expira.
/// Permite asociar la respuesta del proveedor con la solicitud original.
/// </summary>
public class OAuthState
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Valor del parámetro "state" enviado al proveedor.</summary>
    public string StateValue { get; set; } = string.Empty;

    /// <summary>Proveedor al que corresponde este estado, p. ej. "google" o "github".</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Ruta o URL relativa del frontend a la que redirigir tras completar el flujo.
    /// Permite recuperar el "from" de la navegación.
    /// </summary>
    public string? ReturnPath { get; set; }

    /// <summary>
    /// Verificador PKCE (RFC 7636) de este flujo, si el proveedor lo usa (T4-02).
    ///
    /// Se guarda aquí y no en el navegador a propósito: el hash viaja al proveedor en la
    /// autorización y el verificador nunca sale del servidor, así que quien intercepte el
    /// código no puede canjearlo. Null cuando el proveedor no tiene PKCE activado.
    /// </summary>
    public string? CodeVerifier { get; set; }

    /// <summary>Momento en que expira este estado (UTC).</summary>
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>Indica si este estado ya fue utilizado (solo se puede consumir una vez).</summary>
    public bool IsUsed { get; set; } = false;

    // ── Campos para el flujo de vinculación explícita ─────────────────────────

    /// <summary>
    /// Cuando el email del proveedor ya existe en una cuenta manual, se genera un
    /// token de vinculación (valor aleatorio) que el frontend debe devolver junto
    /// con las credenciales del usuario para confirmar la unión.
    /// Null si no se está en un flujo de vinculación.
    /// </summary>
    public string? LinkToken { get; set; }

    /// <summary>ProviderKey del login externo pendiente de vincular.</summary>
    public string? PendingProviderKey { get; set; }

    /// <summary>Email normalizado del perfil externo (para crear o vincular la cuenta).</summary>
    public string? PendingEmail { get; set; }

    /// <summary>DisplayName del perfil externo (se usa al crear una cuenta nueva).</summary>
    public string? PendingDisplayName { get; set; }

    /// <summary>Momento en que se creó (UTC).</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
