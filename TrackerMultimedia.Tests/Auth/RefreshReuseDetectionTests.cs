using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrackerMultimedia.Data;
using TrackerMultimedia.Domain.Entities;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.Auth;

/// <summary>
/// Los tokens de refresco rotan: usar uno lo revoca en el acto. Presentar uno ya
/// revocado significa que existen dos copias del mismo token, y una no está en
/// manos del usuario legítimo.
///
/// Desde T4-01 el token viaja en cookie, así que estos tests manejan las cookies a mano:
/// necesitan presentar un valor viejo concreto, que es justo lo que un contenedor de
/// cookies se encarga de que nunca ocurra.
/// </summary>
public class RefreshReuseDetectionTests(AppFactory factory) : IClassFixture<AppFactory>
{
    private readonly HttpClient _client = SessionCookies.CreateClientWithoutCookies(factory);

    private async Task<string> LoginCapturingCookieAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email, password = AuthHelpers.DefaultPassword });
        response.EnsureSuccessStatusCode();
        return SessionCookies.Read(response);
    }

    [Fact]
    public async Task Refresh_ReusingARotatedToken_RevokesEveryOtherSession()
    {
        var email = $"reuse_{Guid.NewGuid():N}@test.com";
        await AuthHelpers.CreateConfirmedUserAsync(factory.Services, email);

        var primeraSesion = await LoginCapturingCookieAsync(email);
        // Una segunda sesión del mismo usuario, como si hubiera entrado en otro dispositivo.
        var segundaSesion = await LoginCapturingCookieAsync(email);

        // Rotación normal de la primera sesión: su token queda revocado.
        var rotacion = await SessionCookies.RefreshAsync(_client, primeraSesion);
        Assert.Equal(HttpStatusCode.OK, rotacion.StatusCode);
        var sesionRotada = SessionCookies.Read(rotacion);

        // Alguien vuelve a presentar el token viejo: es la señal de robo.
        var reutilizacion = await SessionCookies.RefreshAsync(_client, primeraSesion);
        Assert.Equal(HttpStatusCode.Unauthorized, reutilizacion.StatusCode);

        // A partir de ahí no vale ninguna sesión del usuario: ni la recién rotada,
        // ni la del otro dispositivo. Volver a entrar exige la contraseña.
        var trasRobo = await SessionCookies.RefreshAsync(_client, sesionRotada);
        Assert.Equal(HttpStatusCode.Unauthorized, trasRobo.StatusCode);

        var otroDispositivo = await SessionCookies.RefreshAsync(_client, segundaSesion);
        Assert.Equal(HttpStatusCode.Unauthorized, otroDispositivo.StatusCode);
    }

    [Fact]
    public async Task Refresh_AllFailureReasons_AreIndistinguishable()
    {
        var email = $"indist_{Guid.NewGuid():N}@test.com";
        await AuthHelpers.CreateConfirmedUserAsync(factory.Services, email);
        var sesion = await LoginCapturingCookieAsync(email);

        var desconocido = await SessionCookies.RefreshAsync(_client, "token-que-no-existe");

        await SessionCookies.RefreshAsync(_client, sesion);
        var revocado = await SessionCookies.RefreshAsync(_client, sesion);

        Assert.Equal(HttpStatusCode.Unauthorized, desconocido.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, revocado.StatusCode);
        Assert.Equal(
            await desconocido.Content.ReadAsStringAsync(),
            await revocado.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Refresh_WhenTheAccountIsLockedOut_IsRejected()
    {
        var email = $"locked_{Guid.NewGuid():N}@test.com";
        await AuthHelpers.CreateConfirmedUserAsync(factory.Services, email);
        var sesion = await LoginCapturingCookieAsync(email);

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            await userManager.SetLockoutEndDateAsync(user!, DateTimeOffset.UtcNow.AddHours(1));
        }

        // Sin esta comprobación, bloquear a alguien no tenía efecto hasta que
        // caducara su token de refresco: seguía renovando la sesión durante días.
        var response = await SessionCookies.RefreshAsync(_client, sesion);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        using var verificacion = factory.Services.CreateScope();
        var db = verificacion.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var quedanActivos = await db.RefreshTokens.AsNoTracking()
            .AnyAsync(t => !t.IsRevoked && t.User.Email == email);
        Assert.False(quedanActivos);
    }
}
