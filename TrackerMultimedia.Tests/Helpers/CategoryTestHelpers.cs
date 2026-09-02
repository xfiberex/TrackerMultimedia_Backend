using System.Net.Http.Json;
using TrackerMultimedia.Contracts.Categories;

namespace TrackerMultimedia.Tests.Helpers;

public static class CategoryTestHelpers
{
    public static async Task<CategoryResponse> CreateCategoryAsync(
        HttpClient client,
        Action<CreateCategoryRequest>? configure = null)
    {
        var request = new CreateCategoryRequest
        {
            Name = $"Category_{Guid.NewGuid():N}",
            Color = "#336699",
        };

        configure?.Invoke(request);

        var response = await client.PostAsJsonAsync("/api/categories", request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<CategoryResponse>(MediaItemTestHelpers.JsonOpts)
            ?? throw new InvalidOperationException("No se pudo deserializar CategoryResponse.");
    }
}
