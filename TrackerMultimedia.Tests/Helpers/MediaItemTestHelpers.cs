using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrackerMultimedia.Contracts.Auth;
using TrackerMultimedia.Contracts.Common;
using TrackerMultimedia.Contracts.MediaItems;
using TrackerMultimedia.Data;
using TrackerMultimedia.Domain.Enums;

namespace TrackerMultimedia.Tests.Helpers;

public static class MediaItemTestHelpers
{
    public static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<(HttpClient Client, string Email, AuthResponse Auth)> CreateAuthenticatedClientAsync(
        AppFactory factory,
        string? email = null,
        string? password = null)
    {
        var client = factory.CreateClient();
        var (createdEmail, auth) = await AuthHelpers.CreateAndLoginAsync(client, factory.Services, email, password);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, createdEmail, auth);
    }

    public static async Task<MediaItemResponse> CreateMediaItemAsync(
        HttpClient client,
        Action<CreateMediaItemRequest>? configure = null)
    {
        var request = new CreateMediaItemRequest
        {
            Title = $"Item_{Guid.NewGuid():N}",
            Type = MediaType.Anime,
            Status = MediaTrackingStatus.Planned,
            CurrentSeason = 1,
        };

        configure?.Invoke(request);

        var response = await client.PostAsJsonAsync("/api/media-items", request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<MediaItemResponse>(JsonOpts)
            ?? throw new InvalidOperationException("No se pudo deserializar MediaItemResponse.");
    }

    public static async Task<PagedResponse<MediaItemResponse>> GetMediaItemsAsync(HttpClient client, string queryString = "")
    {
        var response = await client.GetFromJsonAsync<PagedResponse<MediaItemResponse>>(
            $"/api/media-items{queryString}",
            JsonOpts);

        return response ?? throw new InvalidOperationException("No se pudo deserializar la lista de media items.");
    }

    public static async Task<MediaItemResponse> GetMediaItemByIdAsync(HttpClient client, Guid itemId)
    {
        var response = await client.GetFromJsonAsync<MediaItemResponse>($"/api/media-items/{itemId}", JsonOpts);
        return response ?? throw new InvalidOperationException("No se pudo deserializar MediaItemResponse.");
    }

    public static async Task SetCreatedAtUtcAsync(AppFactory factory, Guid itemId, DateTime createdAtUtc)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await dbContext.MediaItems.FirstAsync(mediaItem => mediaItem.Id == itemId);
        item.CreatedAtUtc = createdAtUtc;
        await dbContext.SaveChangesAsync();
    }

    public static async Task SetUpdatedAtUtcAsync(AppFactory factory, Guid itemId, DateTime updatedAtUtc)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await dbContext.MediaItems.FirstAsync(mediaItem => mediaItem.Id == itemId);
        item.UpdatedAtUtc = updatedAtUtc;
        await dbContext.SaveChangesAsync();
    }
}