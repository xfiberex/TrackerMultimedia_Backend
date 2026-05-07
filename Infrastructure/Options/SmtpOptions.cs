namespace TrackerMultimedia.Infrastructure.Options;

/// <summary>
/// Configuración SMTP para el envío de correos (Mailtrap en desarrollo,
/// cualquier servidor compatible en producción).
/// Todos los valores sensibles deben cargarse desde User Secrets o
/// variables de entorno, NUNCA en appsettings.json en texto claro.
/// </summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    /// <summary>Servidor SMTP, p. ej. "sandbox.smtp.mailtrap.io".</summary>
    public string Host { get; init; } = string.Empty;

    /// <summary>Puerto SMTP, p. ej. 587.</summary>
    public int Port { get; init; } = 587;

    /// <summary>Usuario de autenticación.</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>Contraseña de autenticación (cargar desde secretos).</summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>Dirección de remitente, p. ej. "noreply@trackermultimedia.local".</summary>
    public string FromAddress { get; init; } = string.Empty;

    /// <summary>Nombre del remitente que verá el destinatario.</summary>
    public string FromName { get; init; } = "TrackerMultimedia";

    /// <summary>Habilita el envío real. Si false, el email se registra en el log.</summary>
    public bool Enabled { get; init; } = false;
}
