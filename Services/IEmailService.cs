namespace TrackerMultimedia.Services;

/// <summary>
/// Contrato para el envío de correos transaccionales.
/// Permite cambiar la implementación (SMTP, SendGrid…) sin tocar los consumidores.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Envía un correo electrónico.
    /// En desarrollo, cuando SMTP no está configurado, se registra en el log.
    /// </summary>
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
