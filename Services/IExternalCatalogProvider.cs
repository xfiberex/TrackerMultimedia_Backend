using TrackerMultimedia.Contracts.Search;
using TrackerMultimedia.Domain.Enums;

namespace TrackerMultimedia.Services;

public interface IExternalCatalogProvider
{
    string Key { get; }

    string DisplayName { get; }

    MediaItemSourceType SourceType { get; }

    IReadOnlyCollection<MediaSearchType> SupportedTypes { get; }

    bool Supports(MediaSearchType type);

    Task<IReadOnlyCollection<SearchMediaItemResponse>> SearchAsync(
        SearchMediaItemsRequest request,
        CancellationToken cancellationToken);
}