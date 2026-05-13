using System.Net.Http.Json;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrackerMultimedia.Contracts.Search;
using TrackerMultimedia.Domain.Enums;

namespace TrackerMultimedia.Services;

public class MangaDexSearchService(HttpClient httpClient) : IExternalCatalogProvider
{
    private const string CoverBaseUrl = "https://uploads.mangadex.org/covers";
    private const string ReferenceBaseUrl = "https://mangadex.org/title";

    private static readonly MediaSearchType[] SupportedSearchTypes =
    [
        MediaSearchType.All,
        MediaSearchType.Manga,
        MediaSearchType.Manhwa,
        MediaSearchType.Manhua,
    ];

    public string Key => "mangadex";
    public string DisplayName => "MangaDex";
    public MediaItemSourceType SourceType => MediaItemSourceType.MangaDex;
    public IReadOnlyCollection<MediaSearchType> SupportedTypes => SupportedSearchTypes;
    public bool Supports(MediaSearchType type) => SupportedSearchTypes.Contains(type);

    public async Task<IReadOnlyCollection<SearchMediaItemResponse>> SearchAsync(
        SearchMediaItemsRequest request,
        CancellationToken cancellationToken)
    {
        var query = request.Query.Trim();
        if (query.Length == 0) return [];

        var url = BuildUrl(query, request.Type, request.Limit);

        try
        {
            var response = await httpClient.GetFromJsonAsync<MangaDexResponse>(url, cancellationToken);
            var data = response?.Data;
            if (data is null || data.Count == 0) return [];

            return data
                .Where(item => !string.IsNullOrWhiteSpace(item.Id) && item.Attributes is not null)
                .Select(item => MapToResponse(item, request.Type))
                .OfType<SearchMediaItemResponse>()
                .Take(request.Limit)
                .ToList();
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            ExceptionDispatchInfo.Throw(ex);
            return []; // unreachable
        }
    }

    private static string BuildUrl(string query, MediaSearchType type, int limit)
    {
        var encodedQuery = Uri.EscapeDataString(query);
        var sb = new System.Text.StringBuilder(
            $"manga?title={encodedQuery}&limit={limit}&includes[]=cover_art&order[rating]=desc" +
            "&contentRating[]=safe&contentRating[]=suggestive");

        switch (type)
        {
            case MediaSearchType.Manga:
                sb.Append("&originalLanguage[]=ja");
                break;
            case MediaSearchType.Manhwa:
                sb.Append("&originalLanguage[]=ko");
                break;
            case MediaSearchType.Manhua:
                sb.Append("&originalLanguage[]=zh&originalLanguage[]=zh-hk");
                break;
        }

        return sb.ToString();
    }

    private static SearchMediaItemResponse? MapToResponse(MangaDexManga item, MediaSearchType searchType)
    {
        var attrs = item.Attributes!;

        var title = ResolveTitle(attrs.Title);
        if (string.IsNullOrWhiteSpace(title)) return null;

        var coverFileName = (item.Relationships ?? [])
            .FirstOrDefault(r => r.Type == "cover_art")
            ?.Attributes?.FileName;

        var coverUrl = coverFileName is not null
            ? $"{CoverBaseUrl}/{item.Id}/{coverFileName}.256.jpg"
            : null;

        var (suggestedType, mediaKind) = ResolveType(attrs.OriginalLanguage, searchType);

        return new SearchMediaItemResponse(
            DeriveIntId(item.Id!),
            title,
            null,
            suggestedType,
            MediaItemSourceType.MangaDex,
            mediaKind,
            MapStatus(attrs.Status),
            null, // MangaDex ratings require a separate /statistics endpoint
            coverUrl,
            $"{ReferenceBaseUrl}/{item.Id}",
            attrs.Year);
    }

    private static string? ResolveTitle(Dictionary<string, string>? titles)
    {
        if (titles is null || titles.Count == 0) return null;
        if (titles.TryGetValue("en", out var en) && !string.IsNullOrWhiteSpace(en)) return en;
        if (titles.TryGetValue("ja-ro", out var jaRo) && !string.IsNullOrWhiteSpace(jaRo)) return jaRo;
        return titles.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    }

    private static (MediaType SuggestedType, ExternalMediaKind MediaKind) ResolveType(
        string? originalLanguage,
        MediaSearchType searchType)
    {
        // Specific type searches override language inference.
        if (searchType == MediaSearchType.Manhwa) return (MediaType.Manhwa, ExternalMediaKind.Manga);
        if (searchType == MediaSearchType.Manhua) return (MediaType.Manhua, ExternalMediaKind.Manga);
        if (searchType == MediaSearchType.Manga)  return (MediaType.Manga,  ExternalMediaKind.Manga);

        // "All" mode: infer from originalLanguage.
        return (originalLanguage?.ToLowerInvariant()) switch
        {
            "ko" => (MediaType.Manhwa, ExternalMediaKind.Manga),
            "zh" or "zh-hk" => (MediaType.Manhua, ExternalMediaKind.Manga),
            _ => (MediaType.Manga, ExternalMediaKind.Manga),
        };
    }

    private static string? MapStatus(string? status) => status switch
    {
        "ongoing" => "Activo",
        "completed" => "Finalizado",
        "hiatus" => "En Hiato",
        "cancelled" => "Cancelado",
        _ => null,
    };

    // Derives a stable positive int ID from a MangaDex UUID.
    // Uses the first 4 bytes of the GUID's byte array (deterministic for any given UUID).
    private static int DeriveIntId(string uuid) =>
        BitConverter.ToInt32(Guid.Parse(uuid).ToByteArray(), 0) & 0x7FFF_FFFF;

    // ── Response DTOs ─────────────────────────────────────────────────────────

    private sealed record MangaDexResponse(
        [property: JsonPropertyName("data")] IReadOnlyCollection<MangaDexManga>? Data);

    private sealed record MangaDexManga(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("attributes")] MangaDexAttributes? Attributes,
        [property: JsonPropertyName("relationships")] IReadOnlyCollection<MangaDexRelationship>? Relationships);

    private sealed record MangaDexAttributes(
        [property: JsonPropertyName("title")] Dictionary<string, string>? Title,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("year")] int? Year,
        [property: JsonPropertyName("originalLanguage")] string? OriginalLanguage);

    private sealed record MangaDexRelationship(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("attributes")] MangaDexCoverAttributes? Attributes);

    private sealed record MangaDexCoverAttributes(
        [property: JsonPropertyName("fileName")] string? FileName);
}
