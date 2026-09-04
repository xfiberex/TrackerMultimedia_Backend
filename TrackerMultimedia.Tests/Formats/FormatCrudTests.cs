using System.Net;
using System.Net.Http.Json;
using TrackerMultimedia.Contracts.Formats;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.Formats;

/// <summary>
/// Las categorías tenían su `CategoryCrudTests` desde el principio; los formatos, no.
/// `FormatsService.UpdateAsync` y `DeleteAsync` estaban al **0 % de cobertura**, igual
/// que `FormatsController.Update`. Lo destapó la medición de T4-05.
/// </summary>
public class FormatCrudTests(AppFactory factory) : IClassFixture<AppFactory>
{
    [Fact]
    public async Task Create_Update_Delete_RoundTrip()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);

        var creado = await CreateAsync(client, "Audiolibro");
        Assert.Equal("Audiolibro", creado.Name);

        var actualizado = await client.PutAsJsonAsync($"/api/formats/{creado.Id}",
            new UpdateFormatRequest { Name = "Audiolibro largo" });
        actualizado.EnsureSuccessStatusCode();
        Assert.Equal("Audiolibro largo",
            (await actualizado.Content.ReadFromJsonAsync<FormatResponse>())!.Name);

        var borrado = await client.DeleteAsync($"/api/formats/{creado.Id}");
        Assert.Equal(HttpStatusCode.NoContent, borrado.StatusCode);

        var restantes = await client.GetFromJsonAsync<FormatResponse[]>("/api/formats");
        Assert.DoesNotContain(restantes!, formato => formato.Id == creado.Id);
    }

    [Fact]
    public async Task Update_WithADuplicateName_Returns400()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        await CreateAsync(client, "Primero");
        var segundo = await CreateAsync(client, "Segundo");

        var respuesta = await client.PutAsJsonAsync($"/api/formats/{segundo.Id}",
            new UpdateFormatRequest { Name = "  primero  " });

        // El nombre se normaliza recortando y en mayúsculas, así que "  primero  " y
        // "Primero" son el mismo formato. Sin esto, el índice único devolvería un 500.
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Update_KeepingItsOwnName_IsAllowed()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        var formato = await CreateAsync(client, "Novela ligera");

        // La comprobación de duplicado tiene que excluirse a sí misma: si no, renombrar
        // "Novela ligera" a "Novela Ligera" chocaría consigo mismo.
        var respuesta = await client.PutAsJsonAsync($"/api/formats/{formato.Id}",
            new UpdateFormatRequest { Name = "Novela Ligera" });

        respuesta.EnsureSuccessStatusCode();
        Assert.Equal("Novela Ligera",
            (await respuesta.Content.ReadFromJsonAsync<FormatResponse>())!.Name);
    }

    [Fact]
    public async Task UpdateAndDelete_NeverReachAnotherUsersFormats()
    {
        var (clienteA, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        var (clienteB, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        var deA = await CreateAsync(clienteA, "Solo de A");

        var intentoDeCambio = await clienteB.PutAsJsonAsync($"/api/formats/{deA.Id}",
            new UpdateFormatRequest { Name = "Secuestrado" });
        var intentoDeBorrado = await clienteB.DeleteAsync($"/api/formats/{deA.Id}");

        // 404 y no 403: decir "existe pero no es tuyo" ya revela que existe.
        Assert.Equal(HttpStatusCode.NotFound, intentoDeCambio.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, intentoDeBorrado.StatusCode);

        var deAsigueAhi = await clienteA.GetFromJsonAsync<FormatResponse[]>("/api/formats");
        Assert.Contains(deAsigueAhi!, formato => formato.Id == deA.Id && formato.Name == "Solo de A");
    }

    [Fact]
    public async Task Delete_AFormatInUse_LeavesTheItemWithoutFormat()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        var formato = await CreateAsync(client, "En uso");
        var elemento = await MediaItemTestHelpers.CreateMediaItemAsync(client, request =>
        {
            request.Title = "Con formato";
            request.UserFormatId = formato.Id;
        });
        Assert.Equal(formato.Id, elemento.FormatId);

        var borrado = await client.DeleteAsync($"/api/formats/{formato.Id}");
        Assert.Equal(HttpStatusCode.NoContent, borrado.StatusCode);

        // La relación es SetNull, no Cascade: borrar un formato no puede llevarse por
        // delante los elementos que lo usaban.
        var trasBorrar = await MediaItemTestHelpers.GetMediaItemByIdAsync(client, elemento.Id);
        Assert.Null(trasBorrar.FormatId);
        Assert.Null(trasBorrar.FormatName);
    }

    [Fact]
    public async Task Update_WithAnEmptyName_Returns400()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        var formato = await CreateAsync(client, "Con nombre");

        var respuesta = await client.PutAsJsonAsync($"/api/formats/{formato.Id}",
            new UpdateFormatRequest { Name = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    private static async Task<FormatResponse> CreateAsync(HttpClient client, string name)
    {
        var respuesta = await client.PostAsJsonAsync("/api/formats", new CreateFormatRequest { Name = name });
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<FormatResponse>())!;
    }
}
