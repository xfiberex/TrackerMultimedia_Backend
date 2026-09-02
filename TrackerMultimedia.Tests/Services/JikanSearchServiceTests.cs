using System.Text.Json;
using TrackerMultimedia.Contracts.Search;
using TrackerMultimedia.Domain.Enums;
using TrackerMultimedia.Services;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.Services;

public class JikanSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_EmptyQuery_ReturnsEmptyWithoutCallingHttp()
    {
        var service = CreateService((_, _) => throw new InvalidOperationException("HTTP no debería ejecutarse."));

        var results = await service.SearchAsync(new SearchMediaItemsRequest
        {
            Query = "   ",
            Type = MediaSearchType.Anime,
            Limit = 5,
        }, CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_AllType_MergesSuccessfulTargetsAndDeduplicatesItems()
    {
        var service = CreateService((request, _) =>
        {
            var uri = request.RequestUri!.ToString();

            if (uri.Contains("anime?", StringComparison.Ordinal))
            {
                return Task.FromResult(DelegateHttpMessageHandler.Json(
                    """
                    {
                      "data": [
                        {
                          "mal_id": 10,
                          "title": "Anime One",
                          "title_english": "Anime One",
                          "status": "Finished Airing",
                          "score": 8.8,
                          "year": 2021,
                          "url": "https://jikan.test/anime-one"
                        },
                        {
                          "mal_id": 10,
                          "title": "Anime One",
                          "title_english": "Anime One",
                          "status": "Finished Airing",
                          "score": 8.8,
                          "year": 2021,
                          "url": "https://jikan.test/anime-one"
                        }
                      ]
                    }
                    """));
            }

            if (uri.Contains("type=manga", StringComparison.Ordinal))
            {
                throw new HttpRequestException("manga down");
            }

            if (uri.Contains("type=manhwa", StringComparison.Ordinal))
            {
                return Task.FromResult(DelegateHttpMessageHandler.Json(
                    """
                    {
                      "data": [
                        {
                          "mal_id": 20,
                          "title": "Manhwa One",
                          "title_english": null,
                          "status": "On Hiatus",
                          "score": 7.2,
                          "year": 2020,
                          "url": "https://jikan.test/manhwa-one"
                        }
                      ]
                    }
                    """));
            }

            return Task.FromResult(DelegateHttpMessageHandler.Json(
                """
                {
                  "data": [
                    {
                      "mal_id": 30,
                      "title": "Manhua One",
                      "title_english": null,
                      "status": "Not yet published",
                      "score": 6.5,
                      "published": { "prop": { "from": { "year": 2026 } } },
                      "url": "https://jikan.test/manhua-one"
                    }
                  ]
                }
                """));
        });

        var results = await service.SearchAsync(new SearchMediaItemsRequest
        {
            Query = "one piece",
            Type = MediaSearchType.All,
            Limit = 3,
        }, CancellationToken.None);

        Assert.Equal(3, results.Count);
        Assert.Equal("Finalizado", results.First(item => item.ExternalId == 10).ExternalStatusLabel);
        Assert.Equal("En Hiato", results.First(item => item.ExternalId == 20).ExternalStatusLabel);
        Assert.Equal("Próximamente", results.First(item => item.ExternalId == 30).ExternalStatusLabel);
        Assert.Equal(1, results.Count(item => item.ExternalId == 10));
        Assert.Contains(results, item => item.SuggestedType == MediaType.Anime);
        Assert.Contains(results, item => item.SuggestedType == MediaType.Manhwa);
        Assert.Contains(results, item => item.SuggestedType == MediaType.Manhua);
    }

    [Fact]
    public async Task SearchAsync_WhenAllTargetsFail_ThrowsFirstExternalError()
    {
        var service = CreateService((_, _) => throw new JsonException("invalid payload"));

        await Assert.ThrowsAsync<JsonException>(() => service.SearchAsync(new SearchMediaItemsRequest
        {
            Query = "bleach",
            Type = MediaSearchType.All,
            Limit = 5,
        }, CancellationToken.None));
    }

    private static JikanSearchService CreateService(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        var httpClient = new HttpClient(new DelegateHttpMessageHandler(handler))
        {
            BaseAddress = new Uri("https://api.jikan.moe/v4/"),
            Timeout = TimeSpan.FromSeconds(10),
        };

        return new JikanSearchService(httpClient);
    }
}
