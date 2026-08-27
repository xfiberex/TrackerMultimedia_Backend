using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrackerMultimedia.Infrastructure.Options;
using TrackerMultimedia.Services;

namespace TrackerMultimedia.Tests.Services;

public class EmailServiceTests
{
    [Fact]
    public void ConfirmEmailTemplate_ContainsDisplayNameAndUrl()
    {
        var html = EmailTemplates.ConfirmEmail("Ada", "https://frontend.test/confirm");

        Assert.Contains("Ada", html, StringComparison.Ordinal);
        Assert.Contains("https://frontend.test/confirm", html, StringComparison.Ordinal);
        Assert.Contains("Confirmar cuenta", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ResetPasswordTemplate_ContainsDisplayNameAndUrl()
    {
        var html = EmailTemplates.ResetPassword("Lin", "https://frontend.test/reset");

        Assert.Contains("Lin", html, StringComparison.Ordinal);
        Assert.Contains("https://frontend.test/reset", html, StringComparison.Ordinal);
        Assert.Contains("Restablecer contraseña", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SmtpEmailService_WhenDisabled_DoesNotThrow()
    {
        var service = new SmtpEmailService(
            Options.Create(new SmtpOptions
            {
                Enabled = false,
                FromAddress = "noreply@test.local",
                Host = "localhost"
            }),
            NullLogger<SmtpEmailService>.Instance);

        await service.SendAsync("user@test.com", "Asunto", "<p>Hola</p>");
    }
}