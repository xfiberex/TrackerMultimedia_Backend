using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using TrackerMultimedia.Infrastructure.Logging;
using TrackerMultimedia.Infrastructure.Options;

namespace TrackerMultimedia.Services;

/// <summary>
/// Implementación SMTP de <see cref="IEmailService"/> usando MailKit.
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
            // Solo loguea remitente y asunto — nunca el body (puede contener URLs de reset).
            logger.LogWarning(
                "[DEV - SMTP desactivado] Correo para {MaskedTo} | Asunto: {Subject}",
                PersonalData.MaskEmail(to), subject);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_opts.FromName, _opts.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        await client.ConnectAsync(_opts.Host, _opts.Port, SecureSocketOptions.StartTls, cancellationToken);
        await client.AuthenticateAsync(_opts.Username, _opts.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        logger.LogInformation("Correo enviado a {MaskedTo} | Asunto: {Subject}", PersonalData.MaskEmail(to), subject);
    }
}
