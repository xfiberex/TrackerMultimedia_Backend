using System.Net;
using System.Net.Http.Json;
using TrackerMultimedia.Contracts.Categories;
using TrackerMultimedia.Contracts.Formats;
using TrackerMultimedia.Contracts.MediaItems;
using TrackerMultimedia.Domain.Enums;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.MediaItems;

public class MediaItemValidationTests(AppFactory factory) : IClassFixture<AppFactory>
{
    /// <summary>
    /// T2-19. Un UserFormatId ajeno o inexistente se descartaba en silencio: el
    /// elemento se guardaba sin formato y la interfaz mostraba un guardado correcto.
    /// </summary>
    [Fact]
    public async Task Create_WithForeignUserFormatId_Returns400()
    {
        var (ownerClient, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        var created = await ownerClient.PostAsJsonAsync("/api/formats", new CreateFormatRequest { Name = "Formato ajeno" });
        created.EnsureSuccessStatusCode();
        var foreignFormat = await created.Content.ReadFromJsonAsync<FormatResponse>();

        var (intruderClient, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        var response = await intruderClient.PostAsJsonAsync("/api/media-items", new CreateMediaItemRequest
        {
            Title = "Con formato ajeno",
            Type = MediaType.Anime,
            Status = MediaTrackingStatus.Planned,
            UserFormatId = foreignFormat!.Id,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(nameof(CreateMediaItemRequest.UserFormatId), body);
    }

    [Fact]
    public async Task Create_WithUnknownUserFormatId_Returns400()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/media-items", new CreateMediaItemRequest
        {
            Title = "Formato inexistente",
            Type = MediaType.Anime,
            Status = MediaTrackingStatus.Planned,
            UserFormatId = Guid.NewGuid(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_JikanWithoutExternalId_Returns400()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/media-items", new CreateMediaItemRequest
        {
            Title = "Jikan missing id",
            Type = MediaType.Anime,
            Status = MediaTrackingStatus.Planned,
            SourceType = MediaItemSourceType.Jikan,
            ExternalMediaKind = ExternalMediaKind.Anime,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_JikanWithoutExternalMediaKind_Returns400()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/media-items", new CreateMediaItemRequest
        {
            Title = "Jikan missing kind",
            Type = MediaType.Anime,
            Status = MediaTrackingStatus.Planned,
            SourceType = MediaItemSourceType.Jikan,
            ExternalId = 3001,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateExternalSourceForSameUser_Returns400()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);

        await MediaItemTestHelpers.CreateMediaItemAsync(client, request =>
        {
            request.Title = "Original";
            request.SourceType = MediaItemSourceType.Jikan;
            request.ExternalId = 3002;
            request.ExternalMediaKind = ExternalMediaKind.Anime;
        });

        var response = await client.PostAsJsonAsync("/api/media-items", new CreateMediaItemRequest
        {
            Title = "Duplicate",
            Type = MediaType.Anime,
            Status = MediaTrackingStatus.Completed,
            SourceType = MediaItemSourceType.Jikan,
            ExternalId = 3002,
            ExternalMediaKind = ExternalMediaKind.Anime,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_ManualSource_ClearsExternalFields()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);

        var item = await MediaItemTestHelpers.CreateMediaItemAsync(client, request =>
        {
            request.Title = "Manual item";
            request.SourceType = MediaItemSourceType.Manual;
            request.ExternalId = 4001;
            request.ExternalMediaKind = ExternalMediaKind.Anime;
            request.ExternalStatusLabel = "Activo";
            request.ExternalScore = 8.5;
        });

        Assert.Equal(MediaItemSourceType.Manual, item.SourceType);
        Assert.Null(item.ExternalId);
        Assert.Null(item.ExternalMediaKind);
        Assert.Null(item.ExternalStatusLabel);
        Assert.Null(item.ExternalScore);
    }

    [Fact]
    public async Task Create_NewDomainPayloadWithoutLegacyType_PopulatesNeutralFields()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        var startedAtUtc = new DateTime(2024, 03, 01, 0, 0, 0, DateTimeKind.Utc);
        var completedAtUtc = startedAtUtc.AddDays(10);

        var response = await client.PostAsJsonAsync("/api/media-items", new CreateMediaItemRequest
        {
            Title = "  Interstellar  ",
            Description = "  Sci-fi movie  ",
            ContentKind = ContentKind.Movie,
            Status = MediaTrackingStatus.Completed,
            SourceType = MediaItemSourceType.Manual,
            ProgressUnit = ProgressUnit.None,
            ProgressCurrent = 1,
            ProgressTotal = 1,
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = completedAtUtc,
            Notes = "  Favorite  ",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var item = await response.Content.ReadFromJsonAsync<MediaItemResponse>(MediaItemTestHelpers.JsonOpts);
        Assert.NotNull(item);
        Assert.Null(item!.Type);
        Assert.Equal(ContentKind.Movie, item.ContentKind);
        Assert.Equal(ProgressUnit.None, item.ProgressUnit);
        Assert.Equal(1, item.ProgressCurrent);
        Assert.Equal(1, item.ProgressCount);
        Assert.Equal(1, item.ProgressTotal);
        Assert.Equal("Sci-fi movie", item.Description);
        Assert.Equal("Favorite", item.Notes);
        Assert.Equal(startedAtUtc, item.StartedAtUtc);
        Assert.Equal(completedAtUtc, item.CompletedAtUtc);
        Assert.True(item.UpdatedAtUtc >= item.CreatedAtUtc);
    }

    [Fact]
    public async Task Create_LegacyType_DerivesNeutralFields()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);

        var item = await MediaItemTestHelpers.CreateMediaItemAsync(client, request =>
        {
            request.Title = "Derived comic";
            request.Type = MediaType.Manhwa;
            request.ProgressCount = 42;
        });

        Assert.Equal(MediaType.Manhwa, item.Type);
        Assert.Equal(ContentKind.Comic, item.ContentKind);
        Assert.Equal(ProgressUnit.Chapters, item.ProgressUnit);
        Assert.Equal(42, item.ProgressCurrent);
        Assert.Equal(42, item.ProgressCount);
    }

    [Fact]
    public async Task Create_InvalidCategoryIds_Returns400()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/media-items", new CreateMediaItemRequest
        {
            Title = "Invalid category payload",
            Type = MediaType.Anime,
            Status = MediaTrackingStatus.Planned,
            CategoryIds = [Guid.NewGuid()],
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_ManualSource_ClearsExternalFields()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        var created = await MediaItemTestHelpers.CreateMediaItemAsync(client, request =>
        {
            request.Title = "Jikan item";
            request.SourceType = MediaItemSourceType.Jikan;
            request.ExternalId = 4002;
            request.ExternalMediaKind = ExternalMediaKind.Anime;
            request.ExternalStatusLabel = "Activo";
            request.ExternalScore = 7.5;
        });

        var response = await client.PutAsJsonAsync($"/api/media-items/{created.Id}", new UpdateMediaItemRequest
        {
            Title = "Manualized item",
            Type = MediaType.Anime,
            Status = MediaTrackingStatus.Completed,
            SourceType = MediaItemSourceType.Manual,
            ExternalId = 9999,
            ExternalMediaKind = ExternalMediaKind.Anime,
            ExternalStatusLabel = "Should clear",
            ExternalScore = 9.9,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<MediaItemResponse>(MediaItemTestHelpers.JsonOpts);
        Assert.NotNull(updated);
        Assert.Equal(MediaItemSourceType.Manual, updated!.SourceType);
        Assert.Null(updated.ExternalId);
        Assert.Null(updated.ExternalMediaKind);
        Assert.Null(updated.ExternalStatusLabel);
        Assert.Null(updated.ExternalScore);
    }

    [Fact]
    public async Task Update_NewDomainPayload_RefreshesUpdatedAtUtc()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        var created = await MediaItemTestHelpers.CreateMediaItemAsync(client, request =>
        {
            request.Title = "Legacy item";
            request.Type = MediaType.Anime;
            request.ProgressCount = 3;
        });

        var staleUpdatedAtUtc = new DateTime(2020, 01, 01, 0, 0, 0, DateTimeKind.Utc);
        await MediaItemTestHelpers.SetUpdatedAtUtcAsync(factory, created.Id, staleUpdatedAtUtc);

        var response = await client.PutAsJsonAsync($"/api/media-items/{created.Id}", new UpdateMediaItemRequest
        {
            Title = "Hades",
            Description = "  Roguelike  ",
            ContentKind = ContentKind.Game,
            Status = MediaTrackingStatus.InProgress,
            SourceType = MediaItemSourceType.Manual,
            ProgressUnit = ProgressUnit.Hours,
            ProgressCurrent = 14,
            ProgressTotal = 40,
            StartedAtUtc = new DateTime(2024, 02, 10, 0, 0, 0, DateTimeKind.Utc),
            Notes = "  Ongoing  ",
            PersonalScore = 9,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<MediaItemResponse>(MediaItemTestHelpers.JsonOpts);
        Assert.NotNull(updated);
        Assert.Null(updated!.Type);
        Assert.Equal(ContentKind.Game, updated.ContentKind);
        Assert.Equal(ProgressUnit.Hours, updated.ProgressUnit);
        Assert.Equal(14, updated.ProgressCurrent);
        Assert.Equal(14, updated.ProgressCount);
        Assert.Equal(40, updated.ProgressTotal);
        Assert.Equal("Roguelike", updated.Description);
        Assert.Equal("Ongoing", updated.Notes);
        Assert.True(updated.UpdatedAtUtc > staleUpdatedAtUtc);
    }

    [Fact]
    public async Task Update_CategoryOwnedByAnotherUser_Returns400()
    {
        var (clientA, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        var (clientB, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        var categoryOfB = await CategoryTestHelpers.CreateCategoryAsync(clientB, request => request.Name = "Private");
        var created = await MediaItemTestHelpers.CreateMediaItemAsync(clientA);

        var response = await clientA.PutAsJsonAsync($"/api/media-items/{created.Id}", new UpdateMediaItemRequest
        {
            Title = "Invalid update",
            Type = MediaType.Anime,
            Status = MediaTrackingStatus.Completed,
            CategoryIds = [categoryOfB.Id],
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_MismatchedLegacyTypeAndContentKind_Returns400()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/media-items", new CreateMediaItemRequest
        {
            Title = "Invalid payload",
            Type = MediaType.Manga,
            ContentKind = ContentKind.Movie,
            Status = MediaTrackingStatus.Planned,
            SourceType = MediaItemSourceType.Manual,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_JikanWithoutExternalId_Returns400()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        var created = await MediaItemTestHelpers.CreateMediaItemAsync(client);

        var response = await client.PutAsJsonAsync($"/api/media-items/{created.Id}", new UpdateMediaItemRequest
        {
            Title = "Invalid update",
            Type = MediaType.Anime,
            Status = MediaTrackingStatus.Completed,
            SourceType = MediaItemSourceType.Jikan,
            ExternalMediaKind = ExternalMediaKind.Anime,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_DuplicateExternalSourceForSameUser_Returns400()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);

        await MediaItemTestHelpers.CreateMediaItemAsync(client, request =>
        {
            request.Title = "Original";
            request.SourceType = MediaItemSourceType.Jikan;
            request.ExternalId = 7001;
            request.ExternalMediaKind = ExternalMediaKind.Anime;
        });

        var second = await MediaItemTestHelpers.CreateMediaItemAsync(client, request =>
        {
            request.Title = "Second";
            request.SourceType = MediaItemSourceType.Jikan;
            request.ExternalId = 7002;
            request.ExternalMediaKind = ExternalMediaKind.Anime;
        });

        var response = await client.PutAsJsonAsync($"/api/media-items/{second.Id}", new UpdateMediaItemRequest
        {
            Title = "Duplicated",
            Type = MediaType.Anime,
            Status = MediaTrackingStatus.Completed,
            SourceType = MediaItemSourceType.Jikan,
            ExternalId = 7001,
            ExternalMediaKind = ExternalMediaKind.Anime,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}