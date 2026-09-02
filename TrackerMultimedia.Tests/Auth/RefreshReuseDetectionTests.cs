using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrackerMultimedia.Contracts.Auth;
using TrackerMultimedia.Data;
using TrackerMultimedia.Domain.Entities;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.Auth;

/// <summary>
/// Los tokens de refresco rotan: usar uno lo revoca en el acto. Presentar uno ya
/// revocado significa que existen dos copias del mismo token, y una no está en
/// manos del usuario legítimo.
/// </summary>
public class RefreshReuseDetectionTests(AppFactory factory) : IClassFixture<AppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Refresh_ReusingARotatedToken_RevokesEveryOtherSession()
    {
        var (email, primeraSesion) = await AuthHelpers.CreateAndLoginAsync(_client, factory.Services);

        // Una segunda sesión del mismo usuario, como si hubiera entrado en otro dispositivo.
        var segundaSesion = await AuthHelpers.LoginAsync(_client, email);

        // Rotación normal de la primera sesión: su token queda revocado.
        var rotacion = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(primeraSesion.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, rotacion.StatusCode);
        var sesionRotada = await rotacion.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(sesionRotada);

        // Alguien vuelve a presentar el token viejo: es la señal de robo.
        var reutilizacion = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(primeraSesion.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reutilizacion.StatusCode);

        // A partir de ahí no vale ninguna sesión del usuario: ni la recién rotada,
        // ni la del otro dispositivo. Volver a entrar exige la contraseña.
        var trasRobo = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(sesionRotada.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, trasRobo.StatusCode);

        var otroDispositivo = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(segundaSesion.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, otroDispositivo.StatusCode);
    }

    [Fact]
    public async Task Refresh_AllFailureReasons_AreIndistinguishable()
    {
        var (_, auth) = await AuthHelpers.CreateAndLoginAsync(_client, factory.Services);

        var desconocido = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest("token-que-no-existe"));

        await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken));
        var revocado = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(auth.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, desconocido.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, revocado.StatusCode);
        Assert.Equal(
            await desconocido.Content.ReadAsStringAsync(),
            await revocado.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Refresh_WhenTheAccountIsLockedOut_IsRejected()
    {
        var (email, auth) = await AuthHelpers.CreateAndLoginAsync(_client, factory.Services);

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            await userManager.SetLockoutEndDateAsync(user!, DateTimeOffset.UtcNow.AddHours(1));
        }

        // Sin esta comprobación, bloquear a alguien no tenía efecto hasta que
        // caducara su token de refresco: seguía renovando la sesión durante días.
        var response = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(auth.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        using var verificacion = factory.Services.CreateScope();
        var db = verificacion.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var quedanActivos = await db.RefreshTokens.AsNoTracking()
            .AnyAsync(t => !t.IsRevoked && t.User.Email == email);
        Assert.False(quedanActivos);
    }
}
