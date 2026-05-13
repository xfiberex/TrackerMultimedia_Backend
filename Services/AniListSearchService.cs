using System.Net.Http.Json;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrackerMultimedia.Contracts.Search;
using TrackerMultimedia.Domain.Enums;

namespace TrackerMultimedia.Services;

public class AniListSearchService(HttpClient httpClient) : IExternalCatalogProvider
{
    private static readonly MediaSearchType[] SupportedSearchTypes =
    [
        MediaSearchType.All,
        MediaSearchType.Anime,
        MediaSearchType.Donghua,
        MediaSearchType.Manga,
        MediaSearchType.Manhwa,
        MediaSearchType.Manhua,
    ];

    public string Key => "anilist";
    public string DisplayName => "AniList";
    public MediaItemSourceType SourceType => MediaItemSourceType.AniList;
    public IReadOnlyCollection<MediaSearchType> SupportedTypes => SupportedSearchTypes;
    public bool Supports(MediaSearchType type) => SupportedSearchTypes.Contains(type);

    public async Task<IReadOnlyCollection<SearchMediaItemResponse>> SearchAsync(
        SearchMediaItemsRequest request,
        CancellationToken cancellationToken)
    {
        var query = request.Query.Trim();
        if (query.Length == 0) return [];

        var targets = BuildTargets(query, request.Type, request.Limit);
        var results = await Task.WhenAll(targets.Select(t => SearchTargetSafeAsync(t, cancellationToken)));

        var successful = results
            .Where(r => r.Items is not null)
            .SelectMany(r => r.Items!)
            .ToList();

        if (successful.Count == 0)
        {
            var firstError = results.FirstOrDefault(r => r.Error is not null).Error;
            if (firstError is not null)
                ExceptionDispatchInfo.Throw(firstError);
        }

        return successful
            .GroupBy(item => $"{item.SuggestedType}:{item.ExternalMediaKind}:{item.ExternalId}")
            .Select(g => g.First())
            .Take(request.Limit)
            .ToList();
    }

    private async Task<(IReadOnlyCollection<SearchMediaItemResponse>? Items, Exception? Error)> SearchTargetSafeAsync(
        SearchTarget target,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await SearchTargetAsync(target, cancellationToken), null);
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return (null, ex);
        }
    }

    private async Task<IReadOnlyCollection<SearchMediaItemResponse>> SearchTargetAsync(
        SearchTarget target,
        CancellationToken cancellationToken)
    {
        var variables = new Dictionary<string, object>
        {
            ["search"] = target.Query,
            ["type"] = target.AniListType,
            ["perPage"] = target.Limit,
        };
        if (target.CountryOfOrigin is not null)
            variables["countryOfOrigin"] = target.CountryOfOrigin;

        var requestBody = new
        {
            query = BuildGraphQlQuery(target.CountryOfOrigin is not null),
            variables,
        };

        using var httpResponse = await httpClient.PostAsJsonAsync("/", requestBody, cancellationToken);
        httpResponse.EnsureSuccessStatusCode();

        var result = await httpResponse.Content.ReadFromJsonAsync<AniListResponse>(cancellationToken: cancellationToken);
        var media = result?.Data?.Page?.Media;
        if (media is null || media.Count == 0) return [];

        return media
            .Where(item => item.Id > 0 && !string.IsNullOrWhiteSpace(item.Title?.Romaji))
            .Select(item => MapToResponse(item, target))
            .ToList();
    }

    private static SearchMediaItemResponse MapToResponse(AniListMedia item, SearchTarget target)
    {
        var suggestedType = ResolveSuggestedType(item.CountryOfOrigin, target);
        var alternativeTitle = ResolveAlternativeTitle(item.Title?.Romaji, item.Title?.English);
        var score = item.AverageScore.HasValue
            ? Math.Round(item.AverageScore.Value / 10.0, 1)
            : (double?)null;

        return new SearchMediaItemResponse(
            item.Id,
            item.Title!.Romaji!,
            alternativeTitle,
            suggestedType,
            MediaItemSourceType.AniList,
            target.ExternalMediaKind,
            MapStatus(item.Status),
            score,
            item.CoverImage?.Large,
            item.SiteUrl,
            item.StartDate?.Year);
    }

    private static MediaType ResolveSuggestedType(string? countryOfOrigin, SearchTarget target)
    {
        // For specific type searches, always use the target's suggested type.
        if (target.ForcedSuggestedType.HasValue)
            return target.ForcedSuggestedType.Value;

        // "All" mode: infer from the country of origin of each result.
        if (target.AniListType == "ANIME")
        {
            return countryOfOrigin == "CN" ? MediaType.Donghua : MediaType.Anime;
        }

        // MANGA type
        return countryOfOrigin switch
        {
            "KR" => MediaType.Manhwa,
            "CN" or "TW" => MediaType.Manhua,
            _ => MediaType.Manga,
        };
    }

    private static string? ResolveAlternativeTitle(string? romaji, string? english)
    {
        if (string.IsNullOrWhiteSpace(english)) return null;
        return string.Equals(romaji, english, StringComparison.OrdinalIgnoreCase) ? null : english;
    }

    private static string? MapStatus(string? status) => status switch
    {
        "RELEASING" => "Activo",
        "FINISHED" => "Finalizado",
        "NOT_YET_RELEASED" => "Próximamente",
        "CANCELLED" => "Cancelado",
        "HIATUS" => "En Hiato",
        _ => null,
    };

    private static IReadOnlyCollection<SearchTarget> BuildTargets(string query, MediaSearchType type, int limit)
    {
        return type switch
        {
            MediaSearchType.Anime => [new(query, "ANIME", null, MediaType.Anime, ExternalMediaKind.Anime, limit)],
            MediaSearchType.Donghua => [new(query, "ANIME", "CN", MediaType.Donghua, ExternalMediaKind.Anime, limit)],
            MediaSearchType.Manga => [new(query, "MANGA", "JP", MediaType.Manga, ExternalMediaKind.Manga, limit)],
            MediaSearchType.Manhwa => [new(query, "MANGA", "KR", MediaType.Manhwa, ExternalMediaKind.Manga, limit)],
            MediaSearchType.Manhua => [new(query, "MANGA", "CN", MediaType.Manhua, ExternalMediaKind.Manga, limit)],
            _ =>
            [
                // "All" mode: search both anime and manga without country filter.
                // SuggestedType will be resolved per-item using countryOfOrigin from the response.
                new(query, "ANIME", null, null, ExternalMediaKind.Anime, limit / 2 + limit % 2),
                new(query, "MANGA", null, null, ExternalMediaKind.Manga, limit / 2),
            ],
        };
    }

    private static string BuildGraphQlQuery(bool withCountryFilter) => withCountryFilter
        ? """
          query ($search: String, $type: MediaType, $perPage: Int, $countryOfOrigin: CountryCode) {
            Page(perPage: $perPage) {
              media(search: $search, type: $type, countryOfOrigin: $countryOfOrigin, isAdult: false, sort: [SEARCH_MATCH]) {
                id
                title { romaji english }
                status
                averageScore
                startDate { year }
                coverImage { large }
                siteUrl
                countryOfOrigin
              }
            }
          }
          """
        : """
          query ($search: String, $type: MediaType, $perPage: Int) {
            Page(perPage: $perPage) {
              media(search: $search, type: $type, isAdult: false, sort: [SEARCH_MATCH]) {
                id
                title { romaji english }
                status
                averageScore
                startDate { year }
                coverImage { large }
                siteUrl
                countryOfOrigin
              }
            }
          }
          """;

    // ── Targets ──────────────────────────────────────────────────────────────

    // ForcedSuggestedType == null means "All" mode: infer per-item from countryOfOrigin.
    private sealed record SearchTarget(
        string Query,
        string AniListType,
        string? CountryOfOrigin,
        MediaType? ForcedSuggestedType,
        ExternalMediaKind ExternalMediaKind,
        int Limit);

    // ── Response DTOs ─────────────────────────────────────────────────────────

    private sealed record AniListResponse(
        [property: JsonPropertyName("data")] AniListData? Data);

    private sealed record AniListData(
        [property: JsonPropertyName("Page")] AniListPage? Page);

    private sealed record AniListPage(
        [property: JsonPropertyName("media")] IReadOnlyCollection<AniListMedia>? Media);

    private sealed record AniListMedia(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("title")] AniListTitle? Title,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("averageScore")] int? AverageScore,
        [property: JsonPropertyName("startDate")] AniListDate? StartDate,
        [property: JsonPropertyName("coverImage")] AniListCoverImage? CoverImage,
        [property: JsonPropertyName("siteUrl")] string? SiteUrl,
        [property: JsonPropertyName("countryOfOrigin")] string? CountryOfOrigin);

    private sealed record AniListTitle(
        [property: JsonPropertyName("romaji")] string? Romaji,
        [property: JsonPropertyName("english")] string? English);

    private sealed record AniListDate(
        [property: JsonPropertyName("year")] int? Year);

    private sealed record AniListCoverImage(
        [property: JsonPropertyName("large")] string? Large);
}
