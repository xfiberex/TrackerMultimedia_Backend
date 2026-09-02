using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrackerMultimedia.Contracts.Formats;
using TrackerMultimedia.Data;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.Formats;

/// <summary>
/// T2-20. `GET /api/formats` sembraba los formatos por defecto cuando el usuario
/// no tenía ninguno. Un GET que escribe rompe la semántica HTTP y, con dos
/// peticiones simultáneas de una cuenta nueva, la segunda violaba el índice único.
/// El sembrado pasa a hacerse al dar de alta la cuenta.
/// </summary>
public class FormatSeedingTests(AppFactory factory) : IClassFixture<AppFactory>
{
    [Fact]
    public async Task Register_CreatesDefaultFormats()
    {
        var client = factory.CreateClient();
        var email = $"formats_{Guid.NewGuid():N}@test.com";

        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = AuthHelpers.DefaultPassword,
            displayName = "Formatos",
        });
        Assert.Equal(HttpStatusCode.Accepted, register.StatusCode);

        await AuthHelpers.ConfirmEmailAsync(factory.Services, email);
        var auth = await AuthHelpers.LoginAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);

        var formats = await client.GetFromJsonAsync<FormatResponse[]>("/api/formats");

        Assert.NotNull(formats);
        Assert.Equal(10, formats!.Length);
        Assert.Contains(formats, f => f.Name == "Anime");
    }

    [Fact]
    public async Task GetFormats_DoesNotWrite()
    {
        // Este usuario se crea por debajo del endpoint de registro, así que no
        // tiene formatos: sirve justamente para comprobar que el GET no los inventa.
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        var userId = await GetUserIdAsync(client);

        var formats = await client.GetFromJsonAsync<FormatResponse[]>("/api/formats");
        Assert.NotNull(formats);
        Assert.Empty(formats!);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rowCount = await db.UserFormats.CountAsync(f => f.UserId == userId);

        Assert.Equal(0, rowCount);
    }

    private static async Task<Guid> GetUserIdAsync(HttpClient client)
    {
        var me = await client.GetFromJsonAsync<MeResponse>("/api/auth/me");
        return me!.Id;
    }

    private sealed record MeResponse(Guid Id);
}
