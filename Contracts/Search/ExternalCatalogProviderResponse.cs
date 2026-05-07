using TrackerMultimedia.Domain.Enums;

namespace TrackerMultimedia.Contracts.Search;

public record ExternalCatalogProviderResponse(
    string Key,
    string DisplayName,
    MediaItemSourceType SourceType,
    IReadOnlyCollection<MediaSearchType> SupportedTypes);