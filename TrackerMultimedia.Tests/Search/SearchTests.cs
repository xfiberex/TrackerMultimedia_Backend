using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrackerMultimedia.Contracts.Search;
using TrackerMultimedia.Domain.Enums;
using TrackerMultimedia.Services;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.Search;

public class SearchTests
{
    [Fact]
    public async Task Search_ReturnsMappedResults()
    {
        using var factory = await CreateFactoryAsync((request, _) =>
        {
            Assert.Contains("anime?q=naruto", request.RequestUri!.ToString(), StringComparison.OrdinalIgnoreCase);

            return Task.FromResult(DelegateHttpMessageHandler.Json(
                """
                {
                  "data": [
                    {
                      "mal_id": 20,
                      "title": "Naruto",
                      "title_english": "Naruto",
                      "url": "https://jikan.test/naruto",
                      "status": "Currently Airing",
                      "score": 8.4,
                      "year": 2002,
                      "images": { "jpg": { "image_url": "https://img.test/naruto.jpg" } }
                    }
                  ]
                }
                """));
        });

        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);

        var response = await client.GetAsync("/api/discover/search?query=naruto&type=2&limit=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<SearchMediaItemResponse>>(MediaItemTestHelpers.JsonOpts);
        Assert.NotNull(items);
        var item = Assert.Single(items!);
        Assert.Equal(20, item.ExternalId);
        Assert.Equal("Naruto", item.Title);
        Assert.Equal("Activo", item.ExternalStatusLabel);
    }

    [Fact]
    public async Task Search_WhenExternalRequestFails_Returns502()
    {
        using var factory = await CreateFactoryAsync((_, _) => throw new HttpRequestException("boom"));
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);

        var response = await client.GetAsync("/api/discover/search?query=naruto&type=2&limit=5");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task Search_WhenExternalResponseIsInvalid_Returns502()
    {
        using var factory = await CreateFactoryAsync((_, _) =>
            Task.FromResult(DelegateHttpMessageHandler.Json("{ invalid json")));
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);

        var response = await client.GetAsync("/api/discover/search?query=naruto&type=2&limit=5");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task Search_WhenExternalRequestTimesOut_Returns504()
    {
        using var factory = await CreateFactoryAsync((_, _) => throw new TaskCanceledException("timeout"));
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);

        var response = await client.GetAsync("/api/discover/search?query=naruto&type=2&limit=5");

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
    }

    [Fact]
    public async Task GetProviders_ReturnsRegisteredProviderMetadata()
    {
        using var factory = await CreateFactoryAsync((_, _) => Task.FromResult(DelegateHttpMessageHandler.Json("{\"data\":[]}")));
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);

        var response = await client.GetAsync("/api/discover/providers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var providers = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<ExternalCatalogProviderResponse>>(MediaItemTestHelpers.JsonOpts);
        Assert.NotNull(providers);

        var provider = Assert.Single(providers!);
        Assert.Equal("jikan", provider.Key);
        Assert.Equal("Jikan", provider.DisplayName);
        Assert.Equal(MediaItemSourceType.Jikan, provider.SourceType);
        Assert.Contains(MediaSearchType.All, provider.SupportedTypes);
        Assert.Contains(MediaSearchType.Anime, provider.SupportedTypes);
    }

    private static async Task<AppFactory> CreateFactoryAsync(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        var factory = new AppFactory(
            configureAdditionalTestServices: services =>
            {
                services.RemoveAll<JikanSearchService>();
                services.RemoveAll<IExternalCatalogProvider>();

                services.AddSingleton<JikanSearchService>(_ =>
                {
                    var httpClient = new HttpClient(new DelegateHttpMessageHandler(handler))
                    {
                        BaseAddress = new Uri("https://api.jikan.moe/v4/"),
                        Timeout = TimeSpan.FromSeconds(10),
                    };
                    return new JikanSearchService(httpClient);
                });
                services.AddSingleton<IExternalCatalogProvider>(sp => sp.GetRequiredService<JikanSearchService>());
            });

        await factory.InitializeDatabaseAsync();
        return factory;
    }
}