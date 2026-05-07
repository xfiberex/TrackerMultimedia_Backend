using System.Net.Http.Json;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrackerMultimedia.Contracts.Search;
using TrackerMultimedia.Domain.Enums;

namespace TrackerMultimedia.Services;

public class JikanSearchService(HttpClient httpClient) : IExternalCatalogProvider
{
    private static readonly MediaSearchType[] SupportedSearchTypes =
    [
        MediaSearchType.All,
        MediaSearchType.Anime,
        MediaSearchType.Donghua,
        MediaSearchType.Manga,
        MediaSearchType.Manhwa,
        MediaSearchType.Manhua
    ];

    public string Key => "jikan";

    public string DisplayName => "Jikan";

    public MediaItemSourceType SourceType => MediaItemSourceType.Jikan;

    public IReadOnlyCollection<MediaSearchType> SupportedTypes => SupportedSearchTypes;

    public bool Supports(MediaSearchType type) => SupportedSearchTypes.Contains(type);

    public async Task<IReadOnlyCollection<SearchMediaItemResponse>> SearchAsync(
        SearchMediaItemsRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedQuery = request.Query.Trim();
        if (normalizedQuery.Length == 0)
        {
            return [];
        }

        var targets = BuildTargets(normalizedQuery, request.Type, request.Limit);
        var results = await Task.WhenAll(targets.Select(target => SearchTargetSafeAsync(target, cancellationToken)));

        var successful = results
            .Where(r => r.Items is not null)
            .SelectMany(r => r.Items!)
            .ToList();

        // Si todos los targets fallaron con error externo, propagamos el primero
        // para que el controller pueda devolver 502/504 en lugar de resultados vacíos
        if (successful.Count == 0)
        {
            var firstError = results.FirstOrDefault(r => r.Error is not null).Error;
            if (firstError is not null)
            {
                ExceptionDispatchInfo.Throw(firstError);
            }
        }

        return successful
            .GroupBy(item => $"{item.SuggestedType}:{item.ExternalMediaKind}:{item.ExternalId}")
            .Select(group => group.First())
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
        var response = await httpClient.GetFromJsonAsync<JikanSearchResponse>(target.RequestPath, cancellationToken);
        if (response?.Data is null || response.Data.Count == 0)
        {
            return [];
        }

        return response.Data
            .Select(item => new SearchMediaItemResponse(
                item.MalId,
                item.Title,
                string.Equals(item.TitleEnglish, item.Title, StringComparison.OrdinalIgnoreCase) ? null : item.TitleEnglish,
                target.SuggestedType,
                MediaItemSourceType.Jikan,
                target.ExternalMediaKind,
                MapExternalStatus(item.Status),
                item.Score,
                item.Images?.Jpg?.ImageUrl,
                item.Url,
                item.Year ?? item.Published?.Prop?.From?.Year))
            .ToList();
    }

    private static IReadOnlyCollection<SearchTarget> BuildTargets(string query, MediaSearchType type, int limit)
    {
        return type switch
        {
            MediaSearchType.Anime => [BuildAnimeTarget(query, limit, MediaType.Anime)],
            MediaSearchType.Donghua => [BuildAnimeTarget(query, limit, MediaType.Donghua)],
            MediaSearchType.Manga => [BuildMangaTarget(query, limit, MediaType.Manga, "manga")],
            MediaSearchType.Manhwa => [BuildMangaTarget(query, limit, MediaType.Manhwa, "manhwa")],
            MediaSearchType.Manhua => [BuildMangaTarget(query, limit, MediaType.Manhua, "manhua")],
            _ =>
            [
                BuildAnimeTarget(query, 6, MediaType.Anime),
                BuildMangaTarget(query, 5, MediaType.Manga, "manga"),
                BuildMangaTarget(query, 3, MediaType.Manhwa, "manhwa"),
                BuildMangaTarget(query, 3, MediaType.Manhua, "manhua")
            ]
        };
    }

    private static SearchTarget BuildAnimeTarget(string query, int limit, MediaType suggestedType)
    {
        var encodedQuery = Uri.EscapeDataString(query);
        return new SearchTarget(
            $"anime?q={encodedQuery}&limit={limit}&sfw=true",
            suggestedType,
            ExternalMediaKind.Anime);
    }

    private static SearchTarget BuildMangaTarget(string query, int limit, MediaType suggestedType, string jikanType)
    {
        var encodedQuery = Uri.EscapeDataString(query);
        return new SearchTarget(
            $"manga?q={encodedQuery}&limit={limit}&type={jikanType}",
            suggestedType,
            ExternalMediaKind.Manga);
    }

    private static string? MapExternalStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        if (status.Contains("Airing", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("Publishing", StringComparison.OrdinalIgnoreCase))
        {
            return "Activo";
        }

        if (status.Contains("Finished", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("Completed", StringComparison.OrdinalIgnoreCase))
        {
            return "Finalizado";
        }

        if (status.Contains("Hiatus", StringComparison.OrdinalIgnoreCase))
        {
            return "En Hiato";
        }

        if (status.Contains("Discontinued", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            return "Cancelado";
        }

        if (status.Contains("yet", StringComparison.OrdinalIgnoreCase))
        {
            return "Próximamente";
        }

        return status;
    }

    private sealed record SearchTarget(string RequestPath, MediaType SuggestedType, ExternalMediaKind ExternalMediaKind);

    private sealed record JikanSearchResponse(
        [property: JsonPropertyName("data")] IReadOnlyCollection<JikanMediaItem>? Data);

    private sealed record JikanMediaItem(
        [property: JsonPropertyName("mal_id")] int MalId,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("title_english")] string? TitleEnglish,
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("score")] double? Score,
        [property: JsonPropertyName("year")] int? Year,
        [property: JsonPropertyName("images")] JikanImages? Images,
        [property: JsonPropertyName("published")] JikanPublished? Published);

    private sealed record JikanImages(
        [property: JsonPropertyName("jpg")] JikanJpg? Jpg);

    private sealed record JikanJpg(
        [property: JsonPropertyName("image_url")] string? ImageUrl);

    private sealed record JikanPublished(
        [property: JsonPropertyName("prop")] JikanPublishedProp? Prop);

    private sealed record JikanPublishedProp(
        [property: JsonPropertyName("from")] JikanPublishedFrom? From);

    private sealed record JikanPublishedFrom(
        [property: JsonPropertyName("year")] int? Year);
}