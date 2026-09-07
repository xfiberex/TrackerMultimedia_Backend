using System.Net;
using TrackerMultimedia.Contracts.Categories;
using TrackerMultimedia.Contracts.MediaItems;
using TrackerMultimedia.Domain.Enums;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.MediaItems;

public class MediaItemQueryTests(AppFactory factory) : IClassFixture<AppFactory>
{
    [Fact]
    public async Task GetAll_CanFilterByTypeStatusAndScore()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);

        await MediaItemTestHelpers.CreateMediaItemAsync(client, request =>
        {
            request.Title = "Matching";
            request.Type = MediaType.Manga;
            request.Status = MediaTrackingStatus.Completed;
            request.PersonalScore = 8;
        });

        await MediaItemTestHelpers.CreateMediaItemAsync(client, request =>
        {
            request.Title = "Wrong Score";
            request.Type = MediaType.Manga;
            request.Status = MediaTrackingStatus.Completed;
            request.PersonalScore = 5;
        });

        await MediaItemTestHelpers.CreateMediaItemAsync(client, request =>
        {
            request.Title = "Wrong Type";
            request.Type = MediaType.Anime;
            request.Status = MediaTrackingStatus.Completed;
            request.PersonalScore = 8;
        });

        await MediaItemTestHelpers.CreateMediaItemAsync(client, request =>
        {
            request.Title = "Wrong Status";
            request.Type = MediaType.Manga;
            request.Status = MediaTrackingStatus.Planned;
            request.PersonalScore = 8;
        });

        var response = await MediaItemTestHelpers.GetMediaItemsAsync(
            client,
            "?type=2&status=3&minPersonalScore=7&maxPersonalScore=9");

        Assert.Equal(1, response.TotalCount);
        Assert.Single(response.Items);
        Assert.Equal("Matching", response.Items.Single().Title);
    }

    [Fact]
    public async Task GetAll_CanSortAndPaginateByTitle()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);

        await MediaItemTestHelpers.CreateMediaItemAsync(client, request => request.Title = "Gamma");
        await MediaItemTestHelpers.CreateMediaItemAsync(client, request => request.Title = "Alpha");
        await MediaItemTestHelpers.CreateMediaItemAsync(client, request => request.Title = "Beta");

        var response = await MediaItemTestHelpers.GetMediaItemsAsync(
            client,
            "?sortBy=4&sortDirection=1&page=2&pageSize=1");

        Assert.Equal(3, response.TotalCount);
        Assert.Equal(3, response.TotalPages);
        Assert.Single(response.Items);
        Assert.Equal("Beta", response.Items.Single().Title);
    }

    [Fact]
    public async Task GetAll_CanFilterByCreatedDateRange()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);

        var first = await MediaItemTestHelpers.CreateMediaItemAsync(client, request => request.Title = "Old Item");
        var second = await MediaItemTestHelpers.CreateMediaItemAsync(client, request => request.Title = "January Item");
        var third = await MediaItemTestHelpers.CreateMediaItemAsync(client, request => request.Title = "Recent Item");

        await MediaItemTestHelpers.SetCreatedAtUtcAsync(factory, first.Id, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await MediaItemTestHelpers.SetCreatedAtUtcAsync(factory, second.Id, new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc));
        await MediaItemTestHelpers.SetCreatedAtUtcAsync(factory, third.Id, new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc));

        var response = await MediaItemTestHelpers.GetMediaItemsAsync(
            client,
            "?createdFrom=2025-01-05&createdTo=2025-01-31");

        Assert.Equal(1, response.TotalCount);
        Assert.Single(response.Items);
        Assert.Equal("January Item", response.Items.Single().Title);
    }

    [Fact]
    public async Task GetAll_CanFilterByCategoryIds_AndReturnsAssignedCategories()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        var backlog = await CategoryTestHelpers.CreateCategoryAsync(client, request => request.Name = "Backlog");
        var favorites = await CategoryTestHelpers.CreateCategoryAsync(client, request => request.Name = "Favorites");

        await MediaItemTestHelpers.CreateMediaItemAsync(client, request =>
        {
            request.Title = "Matching item";
            request.Type = MediaType.Anime;
            request.CategoryIds = [backlog.Id];
        });

        await MediaItemTestHelpers.CreateMediaItemAsync(client, request =>
        {
            request.Title = "Second item";
            request.Type = MediaType.Manga;
            request.CategoryIds = [favorites.Id];
        });

        await MediaItemTestHelpers.CreateMediaItemAsync(client, request =>
        {
            request.Title = "Uncategorized";
            request.Type = MediaType.Donghua;
        });

        var response = await MediaItemTestHelpers.GetMediaItemsAsync(client, $"?categoryIds={backlog.Id}");

        Assert.Equal(1, response.TotalCount);
        Assert.Single(response.Items);
        Assert.Equal("Matching item", response.Items.Single().Title);
        Assert.Single(response.Items.Single().Categories);
        Assert.Equal(backlog.Id, response.Items.Single().Categories.Single().Id);
        Assert.Equal("Backlog", response.Items.Single().Categories.Single().Name);
    }
}
