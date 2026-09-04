using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrackerMultimedia.Data;
using TrackerMultimedia.Domain.Entities;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.Auth;

/// <summary>
/// T4-08. Una exportación de datos personales vale por dos cosas opuestas: que esté
/// todo lo que es de la persona y que no esté nada que no le corresponda. Los tests
/// cubren las dos, y miran el JSON crudo además del deserializado, porque lo que no
/// debe salir del servidor no aparece en ninguna propiedad que se pueda leer.
/// </summary>
public class PersonalDataExportTests(AppFactory factory) : IClassFixture<AppFactory>
{
    [Fact]
    public async Task Export_IncludesAccountFieldsThatTheLibraryExportLeavesOut()
    {
        var (client, email, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);

        var export = await GetExportAsync(client);
        var account = export.GetProperty("account");

        Assert.Equal(email, account.GetProperty("email").GetString());
        Assert.True(account.GetProperty("emailConfirmed").GetBoolean());
        Assert.True(account.GetProperty("hasPassword").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(account.GetProperty("displayName").GetString()));
        Assert.True(account.GetProperty("createdAtUtc").GetDateTime() > DateTime.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task Export_IncludesLibraryCategoriesAndFormats()
    {
        var (client, email, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        var userId = await GetUserIdAsync(email);

        var category = await CategoryTestHelpers.CreateCategoryAsync(client, request => request.Name = "Exportable");
        await MediaItemTestHelpers.CreateMediaItemAsync(client, request =>
        {
            request.Title = "Serie exportada";
            request.CategoryIds = [category.Id];
        });
        await SeedFormatAsync(userId, "Formato exportado");

        var export = await GetExportAsync(client);

        var library = export.GetProperty("library");
        Assert.Contains(
            library.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("title").GetString() == "Serie exportada");
        Assert.Contains(
            library.GetProperty("categories").EnumerateArray(),
            item => item.GetProperty("name").GetString() == "Exportable");

        // Los formatos no viajan en el archivo de biblioteca —la importación no sabría
        // leerlos— así que si no estuvieran aquí no estarían en ninguna exportación.
        Assert.Contains(
            export.GetProperty("formats").EnumerateArray(),
            item => item.GetProperty("name").GetString() == "Formato exportado");
    }

    [Fact]
    public async Task Export_ListsSessionsWithoutTheTokenThatOpensThem()
    {
        var (client, email, auth) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        var userId = await GetUserIdAsync(email);

        var response = await client.GetAsync("/api/auth/account/export");
        var raw = await response.Content.ReadAsStringAsync();
        var export = JsonDocument.Parse(raw).RootElement;

        // La sesión con la que se pide la exportación tiene que figurar…
        var sessions = export.GetProperty("sessions").EnumerateArray().ToList();
        Assert.Single(sessions);
        Assert.False(sessions[0].GetProperty("isRevoked").GetBoolean());

        // …pero ni el token de refresco en claro ni su hash pueden aparecer en el archivo:
        // el primero abre la cuenta y el segundo es lo que se compara contra él.
        var tokenHash = await GetRefreshTokenHashAsync(userId);
        Assert.DoesNotContain(auth.RefreshToken, raw, StringComparison.Ordinal);
        Assert.DoesNotContain(tokenHash, raw, StringComparison.Ordinal);
        Assert.DoesNotContain(auth.AccessToken, raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_ContainsNothingFromAnotherAccount()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        var (otherClient, otherEmail, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        await MediaItemTestHelpers.CreateMediaItemAsync(otherClient, request => request.Title = "Serie del vecino");

        var response = await client.GetAsync("/api/auth/account/export");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Serie del vecino", raw, StringComparison.Ordinal);
        Assert.DoesNotContain(otherEmail, raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Export_IsDownloadedAsAJsonFile()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);

        var response = await client.GetAsync("/api/auth/account/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("tracker-datos-personales-", response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
    }

    [Fact]
    public async Task Export_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/account/export");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------------------------------------------------------------------------

    private static async Task<JsonElement> GetExportAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/account/export");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    private async Task<Guid> GetUserIdAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Users.Where(user => user.Email == email).Select(user => user.Id).SingleAsync();
    }

    private async Task<string> GetRefreshTokenHashAsync(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.RefreshTokens
            .Where(token => token.UserId == userId)
            .Select(token => token.TokenHash)
            .FirstAsync();
    }

    private async Task SeedFormatAsync(Guid userId, string name)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.UserFormats.Add(new UserFormat
        {
            UserId = userId,
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            Order = 1,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }
}
