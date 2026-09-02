using System.Net;
using System.Net.Http.Json;
using TrackerMultimedia.Contracts.Auth;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.Auth;

public class RegisterTests(AppFactory factory) : IClassFixture<AppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Register_ValidData_Returns202WithNeutralAcknowledgement()
    {
        var email = $"new_{Guid.NewGuid():N}@test.com";

        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Test1234!",
            displayName = "Test User",
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        // La respuesta no expone datos del usuario: solo un acuse genérico.
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(email, body);

        // La cuenta sí se ha creado: se puede confirmar e iniciar sesión.
        await AuthHelpers.ConfirmEmailAsync(factory.Services, email);
        var auth = await AuthHelpers.LoginAsync(_client, email);
        Assert.True(auth.User.HasPassword);
    }

    [Fact]
    public async Task Register_DuplicateEmail_IsIndistinguishableFromNewAccount()
    {
        var newEmail = $"new_{Guid.NewGuid():N}@test.com";
        var duplicatedEmail = $"dup_{Guid.NewGuid():N}@test.com";

        var first = await _client.PostAsJsonAsync("/api/auth/register", new { email = duplicatedEmail, password = "Test1234!" });
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);

        // Registrar un correo ya dado de alta debe devolver exactamente lo mismo que
        // registrar uno nuevo: si difieren, la respuesta sirve para averiguar qué
        // direcciones tienen cuenta.
        var duplicate = await _client.PostAsJsonAsync("/api/auth/register", new { email = duplicatedEmail, password = "Test1234!" });
        var fresh = await _client.PostAsJsonAsync("/api/auth/register", new { email = newEmail, password = "Test1234!" });

        Assert.Equal(fresh.StatusCode, duplicate.StatusCode);
        Assert.Equal(
            await fresh.Content.ReadAsStringAsync(),
            await duplicate.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Register_DuplicateEmail_WarnsTheAccountOwnerByEmail()
    {
        var email = $"dupmail_{Guid.NewGuid():N}@test.com";

        await _client.PostAsJsonAsync("/api/auth/register", new { email, password = "Test1234!" });
        await _client.PostAsJsonAsync("/api/auth/register", new { email, password = "Test1234!" });

        // El aviso va al titular de la dirección, que es quien debe enterarse.
        Assert.Contains(
            factory.EmailInbox.Messages,
            message => message.To == email && message.Subject.Contains("Ya tienes una cuenta"));
    }

    [Fact]
    public async Task Register_PasswordTooShort_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"weak_{Guid.NewGuid():N}@test.com",
            password = "Ab1",  // < 8 chars
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_PasswordWithoutDigit_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"nodigit_{Guid.NewGuid():N}@test.com",
            password = "sindigito", // sin número
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidEmailFormat_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "esto-no-es-un-email",
            password = "Test1234!",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_EmailConfirmedAfterConfirmation_IsTrue()
    {
        var email = $"confirm_{Guid.NewGuid():N}@test.com";

        await _client.PostAsJsonAsync("/api/auth/register", new { email, password = "Test1234!" });

        // Confirmar email a través del UserManager (equivalente al flujo de enlace de email)
        await AuthHelpers.ConfirmEmailAsync(factory.Services, email);

        // El login devuelve el usuario: EmailConfirmed debe ser true
        var auth = await AuthHelpers.LoginAsync(_client, email);
        Assert.True(auth.User.EmailConfirmed);
    }
}
