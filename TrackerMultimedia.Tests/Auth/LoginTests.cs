using System.Net;
using System.Net.Http.Json;
using TrackerMultimedia.Contracts.Auth;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.Auth;

public class LoginTests(AppFactory factory) : IClassFixture<AppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithTokens()
    {
        var user = await AuthHelpers.CreateConfirmedUserAsync(factory.Services);

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = user.Email,
            password = AuthHelpers.DefaultPassword,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrEmpty(body.AccessToken));
        Assert.True(body.ExpiresIn > 0);
        // El refresco ya no viene en el cuerpo: va en la cookie HttpOnly (T4-01).
        Assert.True(SessionCookies.TryRead(response, out _));
        Assert.Equal(user.Email, body.User.Email);
        Assert.True(body.User.EmailConfirmed);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var user = await AuthHelpers.CreateConfirmedUserAsync(factory.Services);

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = user.Email,
            password = "ContrasenaIncorrecta99!",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "nobody@nowhere.com",
            password = "Test1234!",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_EmailNotConfirmed_Returns401()
    {
        var email = $"unconfirmed_{Guid.NewGuid():N}@test.com";

        // Registrar pero NO confirmar el email
        await _client.PostAsJsonAsync("/api/auth/register", new { email, password = "Test1234!" });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "Test1234!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_AllFailureReasons_AreIndistinguishable()
    {
        // Cuenta existente con contraseña incorrecta
        var existingUser = await AuthHelpers.CreateConfirmedUserAsync(factory.Services);
        var wrongPassword = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = existingUser.Email,
            password = "ContrasenaIncorrecta99!",
        });

        // Cuenta inexistente
        var unknownEmail = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = $"nadie_{Guid.NewGuid():N}@test.com",
            password = "Test1234!",
        });

        // Cuenta existente sin confirmar, con la contraseña correcta
        var unconfirmedEmail = $"sinconfirmar_{Guid.NewGuid():N}@test.com";
        await _client.PostAsJsonAsync("/api/auth/register", new { email = unconfirmedEmail, password = "Test1234!" });
        var unconfirmed = await _client.PostAsJsonAsync("/api/auth/login", new { email = unconfirmedEmail, password = "Test1234!" });

        // Los tres motivos deben responder exactamente igual: cualquier diferencia
        // permite averiguar si una dirección tiene cuenta y en qué estado está.
        var wrongPasswordBody = await wrongPassword.Content.ReadAsStringAsync();
        var unknownEmailBody = await unknownEmail.Content.ReadAsStringAsync();
        var unconfirmedBody = await unconfirmed.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(wrongPassword.StatusCode, unknownEmail.StatusCode);
        Assert.Equal(wrongPassword.StatusCode, unconfirmed.StatusCode);
        Assert.Equal(wrongPasswordBody, unknownEmailBody);
        Assert.Equal(wrongPasswordBody, unconfirmedBody);
    }

    [Fact]
    public async Task Login_EmailIsCaseInsensitive()
    {
        var email = $"case_{Guid.NewGuid():N}@test.com";
        await AuthHelpers.CreateConfirmedUserAsync(factory.Services, email);

        // Login con email en mayúsculas
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = email.ToUpperInvariant(),
            password = AuthHelpers.DefaultPassword,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// T3-20. `Password` no tenía longitud máxima, así que una cadena enorme llegaba
    /// hasta CheckPasswordAsync y gastaba CPU en el hashing PBKDF2 antes de fallar.
    /// Ahora la rechaza el modelo, sin tocar la base de datos.
    /// </summary>
    [Fact]
    public async Task Login_WithAnAbsurdlyLongPassword_Returns400()
    {
        var client = factory.CreateClient();
        var user = await AuthHelpers.CreateConfirmedUserAsync(factory.Services);

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = user.Email,
            password = new string('a', 5_000),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
