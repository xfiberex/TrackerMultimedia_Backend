using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrackerMultimedia.Data;
using TrackerMultimedia.Domain.Entities;
using TrackerMultimedia.Services;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.Services;

/// <summary>
/// Ninguna de las dos tablas se limpiaba nunca: crecían sin límite y, en el caso
/// de OAuthStates, guardando el email del perfil externo indefinidamente.
/// </summary>
public class ExpiredDataCleanerTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public ExpiredDataCleanerTests(AppFactory factory) => _factory = factory;

    private static RefreshToken Token(Guid userId, DateTime expiresAtUtc, bool revoked = false) => new()
    {
        TokenHash = Guid.NewGuid().ToString("N"),
        UserId = userId,
        ExpiresAtUtc = expiresAtUtc,
        IsRevoked = revoked,
        CreatedAtUtc = expiresAtUtc.AddDays(-7),
    };

    private static OAuthState State(DateTime expiresAtUtc) => new()
    {
        StateValue = Guid.NewGuid().ToString("N"),
        Provider = "google",
        ExpiresAtUtc = expiresAtUtc,
        PendingEmail = "perfil.externo@example.com",
        CreatedAtUtc = expiresAtUtc.AddMinutes(-10),
    };

    [Fact]
    public async Task Clean_RemovesExpiredRowsAndKeepsTheRest()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cleaner = scope.ServiceProvider.GetRequiredService<IExpiredDataCleaner>();

        var user = await AuthHelpers.CreateUserAsync(_factory.Services, "purga@example.com");
        var now = DateTime.UtcNow;

        // Caducados hace más que la retención: se van.
        var caducadoHaceMucho = Token(user.Id, now.AddDays(-30));
        var revocadoYCaducado = Token(user.Id, now.AddDays(-30), revoked: true);
        var estadoAntiguo = State(now.AddDays(-5));

        // Dentro de la ventana de retención o todavía vigentes: se quedan.
        var caducadoAyer = Token(user.Id, now.AddDays(-1));
        var vigente = Token(user.Id, now.AddDays(7));
        var revocadoPeroVigente = Token(user.Id, now.AddDays(7), revoked: true);
        var estadoReciente = State(now.AddMinutes(-5));

        db.RefreshTokens.AddRange(caducadoHaceMucho, revocadoYCaducado, caducadoAyer, vigente, revocadoPeroVigente);
        db.OAuthStates.AddRange(estadoAntiguo, estadoReciente);
        await db.SaveChangesAsync();

        var result = await cleaner.CleanAsync();

        Assert.Equal(2, result.RefreshTokens);
        Assert.Equal(1, result.OAuthStates);

        var tokensRestantes = await db.RefreshTokens.AsNoTracking()
            .Where(t => t.UserId == user.Id)
            .Select(t => t.Id)
            .ToListAsync();

        Assert.DoesNotContain(caducadoHaceMucho.Id, tokensRestantes);
        Assert.DoesNotContain(revocadoYCaducado.Id, tokensRestantes);
        Assert.Contains(vigente.Id, tokensRestantes);
        Assert.Contains(revocadoPeroVigente.Id, tokensRestantes);

        // Caducado ayer sigue ahí: la retención por defecto es de 7 días, y ese
        // margen existe por si se añade detección de reutilización de tokens.
        Assert.Contains(caducadoAyer.Id, tokensRestantes);

        var estadosRestantes = await db.OAuthStates.AsNoTracking().Select(s => s.Id).ToListAsync();
        Assert.DoesNotContain(estadoAntiguo.Id, estadosRestantes);
        Assert.Contains(estadoReciente.Id, estadosRestantes);
    }

    [Fact]
    public async Task Clean_IsSafeToRunWhenThereIsNothingToDelete()
    {
        using var scope = _factory.Services.CreateScope();
        var cleaner = scope.ServiceProvider.GetRequiredService<IExpiredDataCleaner>();

        var result = await cleaner.CleanAsync();

        Assert.Equal(0, result.Total);
    }
}
