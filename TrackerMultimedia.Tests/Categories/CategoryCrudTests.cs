using System.Net;
using System.Net.Http.Json;
using TrackerMultimedia.Contracts.Categories;
using TrackerMultimedia.Contracts.MediaItems;
using TrackerMultimedia.Domain.Enums;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.Categories;

public class CategoryCrudTests(AppFactory factory) : IClassFixture<AppFactory>
{
    [Fact]
    public async Task CategoryCrud_FlowsThroughCreateListUpdateAndDelete()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);

        var createResponse = await client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest
        {
            Name = "  Backlog  ",
            Color = "#12abef",
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<CategoryResponse>(MediaItemTestHelpers.JsonOpts);
        Assert.NotNull(created);
        Assert.Equal("Backlog", created!.Name);
        Assert.Equal("#12ABEF", created.Color);

        var list = await client.GetFromJsonAsync<List<CategoryResponse>>("/api/categories", MediaItemTestHelpers.JsonOpts);
        Assert.NotNull(list);
        Assert.Single(list!);
        Assert.Equal(created.Id, list[0].Id);

        var updateResponse = await client.PutAsJsonAsync($"/api/categories/{created.Id}", new UpdateCategoryRequest
        {
            Name = "  Favoritos  ",
            Color = "",
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content.ReadFromJsonAsync<CategoryResponse>(MediaItemTestHelpers.JsonOpts);
        Assert.NotNull(updated);
        Assert.Equal("Favoritos", updated!.Name);
        Assert.Null(updated.Color);

        var deleteResponse = await client.DeleteAsync($"/api/categories/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var listAfterDelete = await client.GetFromJsonAsync<List<CategoryResponse>>("/api/categories", MediaItemTestHelpers.JsonOpts);
        Assert.NotNull(listAfterDelete);
        Assert.Empty(listAfterDelete!);
    }

    [Fact]
    public async Task Create_DuplicateNameIgnoringCase_Returns400()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        await CategoryTestHelpers.CreateCategoryAsync(client, request => request.Name = "Backlog");

        var response = await client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest
        {
            Name = "  backlog  ",
            Color = "#445566",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Category_RemovesItemAssignments()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        var category = await CategoryTestHelpers.CreateCategoryAsync(client, request => request.Name = "Library");

        var item = await MediaItemTestHelpers.CreateMediaItemAsync(client, request =>
        {
            request.Title = "Assigned item";
            request.Type = MediaType.Anime;
            request.CategoryIds = [category.Id];
        });

        var deleteResponse = await client.DeleteAsync($"/api/categories/{category.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var reloadedItem = await client.GetFromJsonAsync<MediaItemResponse>($"/api/media-items/{item.Id}", MediaItemTestHelpers.JsonOpts);
        Assert.NotNull(reloadedItem);
        Assert.Empty(reloadedItem!.Categories);
    }
}
