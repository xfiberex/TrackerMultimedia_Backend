using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrackerMultimedia.Data;
using TrackerMultimedia.Domain.Entities;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.Auth;

/// <summary>
/// T1-14. El criterio de aceptación no es que el endpoint devuelva 204, sino que
/// **no quede ninguna fila del usuario en ninguna tabla**. Por eso estos tests
/// cuentan filas en la base en lugar de fiarse del código de respuesta.
/// </summary>
public class DeleteAccountTests(AppFactory factory) : IClassFixture<AppFactory>
{
    [Fact]
    public async Task Delete_WithCorrectPassword_RemovesEveryRowOfTheUser()
    {
        var (client, email, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        var userId = await GetUserIdAsync(factory, email);

        // Datos en las cuatro tablas que cuelgan del usuario, más un elemento con
        // categoría para ejercitar la tabla de enlace.
        var category = await CategoryTestHelpers.CreateCategoryAsync(client, request => request.Name = "Para borrar");
        await MediaItemTestHelpers.CreateMediaItemAsync(client, request => request.CategoryIds = [category.Id]);
        await SeedPendingOAuthStateAsync(factory, email);
        await SeedFormatAsync(factory, userId);

        var antes = await CountRowsAsync(factory, userId, email);
        Assert.True(antes.MediaItems > 0 && antes.Categories > 0 && antes.Formats > 0
            && antes.RefreshTokens > 0 && antes.Links > 0 && antes.OAuthStates > 0,
            $"la siembra no dejó datos en todas las tablas: {antes}");

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/auth/account")
        {
            Content = JsonContent.Create(new { password = AuthHelpers.DefaultPassword }),
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var despues = await CountRowsAsync(factory, userId, email);
        Assert.Equal(0, despues.Users);
        Assert.Equal(0, despues.MediaItems);
        Assert.Equal(0, despues.Categories);
        Assert.Equal(0, despues.Formats);
        Assert.Equal(0, despues.RefreshTokens);
        Assert.Equal(0, despues.Links);
        // Los OAuthStates no tienen UserId, así que la cascada no los alcanza: se borran
        // a mano por PendingEmail. Sin eso, el correo sobrevivía al borrado de la cuenta.
        Assert.Equal(0, despues.OAuthStates);
    }

    [Fact]
    public async Task Delete_WithWrongPassword_KeepsTheAccount()
    {
        var (client, email, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        var userId = await GetUserIdAsync(factory, email);

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/auth/account")
        {
            Content = JsonContent.Create(new { password = "NoEsLaSuya123!" }),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(1, (await CountRowsAsync(factory, userId, email)).Users);
    }

    [Fact]
    public async Task Delete_WithoutAnyConfirmation_KeepsTheAccount()
    {
        var (client, email, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        var userId = await GetUserIdAsync(factory, email);

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/auth/account")
        {
            Content = JsonContent.Create(new { }),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(1, (await CountRowsAsync(factory, userId, email)).Users);
    }

    [Fact]
    public async Task Delete_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/auth/account")
        {
            Content = JsonContent.Create(new { password = AuthHelpers.DefaultPassword }),
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------------------------------------------------------------------------

    private sealed record Counts(
        int Users, int MediaItems, int Categories, int Formats,
        int RefreshTokens, int Links, int OAuthStates);

    private static async Task<Counts> CountRowsAsync(AppFactory factory, Guid userId, string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return new Counts(
            await db.Users.CountAsync(user => user.Id == userId),
            await db.MediaItems.CountAsync(item => item.UserId == userId),
            await db.UserCategories.CountAsync(category => category.UserId == userId),
            await db.UserFormats.CountAsync(format => format.UserId == userId),
            await db.RefreshTokens.CountAsync(token => token.UserId == userId),
            await db.MediaItemCategories.CountAsync(link => link.MediaItem.UserId == userId),
            await db.OAuthStates.CountAsync(state => state.PendingEmail == email));
    }

    private static async Task<Guid> GetUserIdAsync(AppFactory factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Users.Where(user => user.Email == email).Select(user => user.Id).SingleAsync();
    }

    private static async Task SeedPendingOAuthStateAsync(AppFactory factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.OAuthStates.Add(new OAuthState
        {
            StateValue = $"state-{Guid.NewGuid():N}",
            Provider = "google",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
            PendingEmail = email,
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedFormatAsync(AppFactory factory, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.UserFormats.Add(new UserFormat
        {
            UserId = userId,
            Name = "Formato de prueba",
            NormalizedName = "FORMATO DE PRUEBA",
            Order = 1,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }
}
