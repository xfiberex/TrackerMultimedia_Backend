using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrackerMultimedia.Contracts.Categories;
using TrackerMultimedia.Contracts.Common;
using TrackerMultimedia.Contracts.MediaItems;
using TrackerMultimedia.Domain.Enums;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.Isolation;

public class UserIsolationTests(AppFactory factory) : IClassFixture<AppFactory>
{
    // El servidor serializa enums como strings (JsonStringEnumConverter en Program.cs).
    // Los clientes HTTP deben usar las mismas opciones para deserializar correctamente.
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // Crea un cliente HTTP ya autenticado como un usuario nuevo
    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = factory.CreateClient();
        var (_, auth) = await AuthHelpers.CreateAndLoginAsync(client, factory.Services);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    // Crea un MediaItem vía la API y devuelve su ID
    private static async Task<Guid> CreateMediaItemAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/media-items", new CreateMediaItemRequest
        {
            Title = $"Item_{Guid.NewGuid():N}",
            Type = MediaType.Anime,
            Status = MediaTrackingStatus.Planned,
        });
        response.EnsureSuccessStatusCode();
        var item = await response.Content.ReadFromJsonAsync<MediaItemResponse>(JsonOpts);

        Assert.NotNull(item);
        Assert.NotEqual(Guid.Empty, item!.Id);

        return item!.Id;
    }

    // -------------------------------------------------------------------------
    // GET by ID
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetById_OwnItem_Returns200()
    {
        var client = await CreateAuthenticatedClientAsync();
        var itemId = await CreateMediaItemAsync(client);

        var response = await client.GetAsync($"/api/media-items/{itemId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetById_OtherUsersItem_Returns404()
    {
        var clientA = await CreateAuthenticatedClientAsync();
        var clientB = await CreateAuthenticatedClientAsync();

        var itemIdOfA = await CreateMediaItemAsync(clientA);

        // B intenta acceder al item de A
        var response = await clientB.GetAsync($"/api/media-items/{itemIdOfA}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // PUT (update)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Update_OwnItem_Returns200()
    {
        var client = await CreateAuthenticatedClientAsync();
        var itemId = await CreateMediaItemAsync(client);

        var response = await client.PutAsJsonAsync($"/api/media-items/{itemId}", new UpdateMediaItemRequest
        {
            Title = "Título actualizado",
            Type = MediaType.Anime,
            Status = MediaTrackingStatus.InProgress,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_OtherUsersItem_Returns404()
    {
        var clientA = await CreateAuthenticatedClientAsync();
        var clientB = await CreateAuthenticatedClientAsync();

        var itemIdOfA = await CreateMediaItemAsync(clientA);

        var response = await clientB.PutAsJsonAsync($"/api/media-items/{itemIdOfA}", new UpdateMediaItemRequest
        {
            Title = "Intento de hackeo",
            Type = MediaType.Anime,
            Status = MediaTrackingStatus.Completed,
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // DELETE
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Delete_OwnItem_Returns204()
    {
        var client = await CreateAuthenticatedClientAsync();
        var itemId = await CreateMediaItemAsync(client);

        var response = await client.DeleteAsync($"/api/media-items/{itemId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_OtherUsersItem_Returns404()
    {
        var clientA = await CreateAuthenticatedClientAsync();
        var clientB = await CreateAuthenticatedClientAsync();

        var itemIdOfA = await CreateMediaItemAsync(clientA);

        var response = await clientB.DeleteAsync($"/api/media-items/{itemIdOfA}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // GET all (lista)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAll_ReturnsOnlyOwnItems()
    {
        var clientA = await CreateAuthenticatedClientAsync();
        var clientB = await CreateAuthenticatedClientAsync();

        // A crea 2 items, B crea 1
        await CreateMediaItemAsync(clientA);
        await CreateMediaItemAsync(clientA);
        await CreateMediaItemAsync(clientB);

        var responseA = await clientA.GetFromJsonAsync<PagedResponse<MediaItemResponse>>("/api/media-items", JsonOpts);
        var responseB = await clientB.GetFromJsonAsync<PagedResponse<MediaItemResponse>>("/api/media-items", JsonOpts);

        Assert.NotNull(responseA);
        Assert.NotNull(responseB);

        // Como los usuarios se crean con GUID único, cada lista solo contiene sus propios items
        Assert.Equal(2, responseA.TotalCount);
        Assert.Equal(1, responseB.TotalCount);
    }

    [Fact]
    public async Task Categories_GetAll_ReturnsOnlyOwnCategories()
    {
        var clientA = await CreateAuthenticatedClientAsync();
        var clientB = await CreateAuthenticatedClientAsync();

        await CategoryTestHelpers.CreateCategoryAsync(clientA, request => request.Name = "A1");
        await CategoryTestHelpers.CreateCategoryAsync(clientA, request => request.Name = "A2");
        await CategoryTestHelpers.CreateCategoryAsync(clientB, request => request.Name = "B1");

        var responseA = await clientA.GetFromJsonAsync<List<CategoryResponse>>("/api/categories", JsonOpts);
        var responseB = await clientB.GetFromJsonAsync<List<CategoryResponse>>("/api/categories", JsonOpts);

        Assert.NotNull(responseA);
        Assert.NotNull(responseB);
        Assert.Equal(2, responseA!.Count);
        Assert.Single(responseB!);
    }

    [Fact]
    public async Task Categories_Delete_OtherUsersCategory_Returns404()
    {
        var clientA = await CreateAuthenticatedClientAsync();
        var clientB = await CreateAuthenticatedClientAsync();
        var categoryOfA = await CategoryTestHelpers.CreateCategoryAsync(clientA, request => request.Name = "Private");

        var response = await clientB.DeleteAsync($"/api/categories/{categoryOfA.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
