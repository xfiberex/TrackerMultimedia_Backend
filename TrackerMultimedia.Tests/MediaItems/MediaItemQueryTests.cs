using System.Net;
using TrackerMultimedia.Contracts.Categories;
using TrackerMultimedia.Contracts.MediaItems;
using TrackerMultimedia.Domain.Enums;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.MediaItems;

public class MediaItemQueryTests(AppFactory factory) : IClassFixture<AppFactory>
{
    [Fact]
    public async Task GetStats_ReturnsAggregatedCountsAndAverageForCurrentUser()
    {
        var (clientA, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        var (clientB, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        var backlog = await CategoryTestHelpers.CreateCategoryAsync(clientA, request => request.Name = "Backlog");
        var favorites = await CategoryTestHelpers.CreateCategoryAsync(clientA, request => request.Name = "Favorites");
        var currentMonthDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 6, 0, 0, 0, DateTimeKind.Utc);
        var previousMonthDate = currentMonthDate.AddMonths(-1);

        await MediaItemTestHelpers.CreateMediaItemAsync(clientA, request =>
        {
            request.Title = "Manual Planned";
            request.Type = MediaType.Anime;
            request.Status = MediaTrackingStatus.Planned;
            request.PersonalScore = 8;
            request.CategoryIds = [backlog.Id];
        });

        await MediaItemTestHelpers.CreateMediaItemAsync(clientA, request =>
        {
            request.Title = "Jikan Completed";
            request.Type = MediaType.Manga;
            request.ContentKind = ContentKind.Comic;
            request.Status = MediaTrackingStatus.Completed;
            request.SourceType = MediaItemSourceType.Jikan;
            request.ExternalId = 1001;
            request.ExternalMediaKind = ExternalMediaKind.Manga;
            request.PersonalScore = 6;
            request.StartedAtUtc = previousMonthDate;
            request.CompletedAtUtc = currentMonthDate;
            request.CategoryIds = [favorites.Id];
        });

        await MediaItemTestHelpers.CreateMediaItemAsync(clientA, request =>
        {
            request.Title = "Manual InProgress";
            request.Type = null;
            request.ContentKind = ContentKind.Game;
            request.Status = MediaTrackingStatus.InProgress;
            request.ProgressUnit = ProgressUnit.Hours;
            request.StartedAtUtc = currentMonthDate;
            request.CategoryIds = [backlog.Id];
        });

        await MediaItemTestHelpers.CreateMediaItemAsync(clientA, request =>
        {
            request.Title = "Jikan Dropped";
            request.Type = MediaType.Donghua;
            request.Status = MediaTrackingStatus.Dropped;
            request.SourceType = MediaItemSourceType.Jikan;
            request.ExternalId = 1002;
            request.ExternalMediaKind = ExternalMediaKind.Anime;
            request.PersonalScore = 9;
            request.StartedAtUtc = previousMonthDate;
            request.CompletedAtUtc = previousMonthDate;
        });

        await MediaItemTestHelpers.CreateMediaItemAsync(clientB, request =>
        {
            request.Title = "Other User";
            request.Status = MediaTrackingStatus.Completed;
            request.PersonalScore = 10;
        });

        var stats = await MediaItemTestHelpers.GetStatsAsync(clientA);

        Assert.Equal(4, stats.TotalCount);
        Assert.Equal(1, stats.PlannedCount);
        Assert.Equal(1, stats.InProgressCount);
        Assert.Equal(1, stats.CompletedCount);
        Assert.Equal(0, stats.OnHoldCount);
        Assert.Equal(1, stats.DroppedCount);
        Assert.Equal(1, stats.StartedThisMonthCount);
        Assert.Equal(1, stats.CompletedThisMonthCount);
        Assert.Equal(1, stats.BacklogWithoutStartCount);
        Assert.Equal(7.7, stats.AveragePersonalScore);
        Assert.Equal(3, stats.ScoredItemsCount);
        Assert.Contains(stats.ContentKindBreakdown, item => item.ContentKind == ContentKind.Series && item.Count == 2);
        Assert.Contains(stats.ContentKindBreakdown, item => item.ContentKind == ContentKind.Comic && item.Count == 1);
        Assert.Contains(stats.ContentKindBreakdown, item => item.ContentKind == ContentKind.Game && item.Count == 1);
        Assert.Contains(stats.SourceBreakdown, item => item.SourceType == MediaItemSourceType.Manual && item.Count == 2);
        Assert.Contains(stats.SourceBreakdown, item => item.SourceType == MediaItemSourceType.Jikan && item.Count == 2);
        Assert.Contains(stats.CategoryBreakdown, item => item.CategoryId == backlog.Id && item.Count == 2);
        Assert.Contains(stats.CategoryBreakdown, item => item.CategoryId == favorites.Id && item.Count == 1);
        Assert.Contains(stats.AverageScoreByContentKind, item => item.ContentKind == ContentKind.Series && item.AveragePersonalScore == 8.5 && item.ScoredItemsCount == 2);
        Assert.Contains(stats.AverageScoreByContentKind, item => item.ContentKind == ContentKind.Comic && item.AveragePersonalScore == 6 && item.ScoredItemsCount == 1);
    }

    [Fact]
    public async Task GetAll_CanFilterByTypeStatusSourceAndScore()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);

        await MediaItemTestHelpers.CreateMediaItemAsync(client, request =>
        {
            request.Title = "Matching";
            request.Type = MediaType.Manga;
            request.Status = MediaTrackingStatus.Completed;
            request.SourceType = MediaItemSourceType.Jikan;
            request.ExternalId = 2001;
            request.ExternalMediaKind = ExternalMediaKind.Manga;
            request.PersonalScore = 8;
        });

        await MediaItemTestHelpers.CreateMediaItemAsync(client, request =>
        {
            request.Title = "Wrong Score";
            request.Type = MediaType.Manga;
            request.Status = MediaTrackingStatus.Completed;
            request.SourceType = MediaItemSourceType.Jikan;
            request.ExternalId = 2002;
            request.ExternalMediaKind = ExternalMediaKind.Manga;
            request.PersonalScore = 5;
        });

        await MediaItemTestHelpers.CreateMediaItemAsync(client, request =>
        {
            request.Title = "Wrong Type";
            request.Type = MediaType.Anime;
            request.Status = MediaTrackingStatus.Completed;
            request.SourceType = MediaItemSourceType.Jikan;
            request.ExternalId = 2003;
            request.ExternalMediaKind = ExternalMediaKind.Anime;
            request.PersonalScore = 8;
        });

        await MediaItemTestHelpers.CreateMediaItemAsync(client, request =>
        {
            request.Title = "Wrong Status";
            request.Type = MediaType.Manga;
            request.Status = MediaTrackingStatus.Planned;
            request.SourceType = MediaItemSourceType.Jikan;
            request.ExternalId = 2004;
            request.ExternalMediaKind = ExternalMediaKind.Manga;
            request.PersonalScore = 8;
        });

        var response = await MediaItemTestHelpers.GetMediaItemsAsync(
            client,
            "?type=2&status=3&sourceType=2&minPersonalScore=7&maxPersonalScore=9");

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