using System.Runtime.ExceptionServices;
using System.Text.Json;
using TrackerMultimedia.Contracts.Search;

namespace TrackerMultimedia.Services;

public class ExternalCatalogSearchService(IEnumerable<IExternalCatalogProvider> providers)
{
    private readonly IReadOnlyCollection<IExternalCatalogProvider> _providers = providers.ToArray();

    public IReadOnlyCollection<ExternalCatalogProviderResponse> GetProviders()
    {
        return _providers
            .OrderBy(provider => provider.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(provider => new ExternalCatalogProviderResponse(
                provider.Key,
                provider.DisplayName,
                provider.SourceType,
                provider.SupportedTypes.ToArray()))
            .ToArray();
    }

    public async Task<IReadOnlyCollection<SearchMediaItemResponse>> SearchAsync(
        SearchMediaItemsRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedQuery = request.Query.Trim();
        if (normalizedQuery.Length == 0)
        {
            return [];
        }

        var normalizedRequest = new SearchMediaItemsRequest
        {
            Query = normalizedQuery,
            Type = request.Type,
            Limit = request.Limit,
            Providers = request.Providers,
        };

        var activeProviders = _providers
            .Where(provider => provider.Supports(normalizedRequest.Type))
            .Where(provider => normalizedRequest.Providers.Count == 0 ||
                               normalizedRequest.Providers.Contains(provider.Key, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (activeProviders.Length == 0)
        {
            return [];
        }

        var results = await Task.WhenAll(activeProviders.Select(provider => SearchProviderSafeAsync(provider, normalizedRequest, cancellationToken)));

        var successfulItems = results
            .Where(result => result.Items is not null)
            .SelectMany(result => result.Items!)
            .ToList();

        if (successfulItems.Count == 0)
        {
            var firstError = results.FirstOrDefault(result => result.Error is not null).Error;
            if (firstError is not null)
            {
                ExceptionDispatchInfo.Throw(firstError);
            }
        }

        return successfulItems
            .GroupBy(item => $"{item.SourceType}:{item.ExternalMediaKind}:{item.ExternalId}")
            .Select(group => group.First())
            .Take(normalizedRequest.Limit)
            .ToArray();
    }

    private static async Task<(IReadOnlyCollection<SearchMediaItemResponse>? Items, Exception? Error)> SearchProviderSafeAsync(
        IExternalCatalogProvider provider,
        SearchMediaItemsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await provider.SearchAsync(request, cancellationToken), null);
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
}
