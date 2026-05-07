using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using TrackerMultimedia.Infrastructure.Options;

namespace TrackerMultimedia.Services;

/// <summary>
/// Implementación SMTP de <see cref="IEmailService"/>.
/// Usa las credenciales de Mailtrap (sandbox) en desarrollo y cualquier
/// servidor SMTP compatible en producción.
/// Cuando <see cref="SmtpOptions.Enabled"/> es false, no envía nada y
/// registra el correo en el log para facilitar el desarrollo.
/// </summary>
public sealed class SmtpEmailService(
    IOptions<SmtpOptions> options,
    ILogger<SmtpEmailService> logger) : IEmailService
{
    private readonly SmtpOptions _opts = options.Value;

    public async Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        if (!_opts.Enabled)
        {
            logger.LogWarning(
                "[DEV - SMTP desactivado] Correo para {To} | Asunto: {Subject} | Body (truncado): {Body}",
                to, subject, htmlBody[..Math.Min(200, htmlBody.Length)]);
            return;
        }

        using var client = new SmtpClient(_opts.Host, _opts.Port)
        {
            Credentials = new NetworkCredential(_opts.Username, _opts.Password),
            EnableSsl = true,
        };

        using var message = new MailMessage
        {
            From = new MailAddress(_opts.FromAddress, _opts.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
        };
        message.To.Add(to);

        await client.SendMailAsync(message, cancellationToken);
        logger.LogInformation("Correo enviado a {To} | Asunto: {Subject}", to, subject);
    }
}
