using System.Runtime.ExceptionServices;
using System.Text.Json;
using TrackerMultimedia.Contracts.Search;

namespace TrackerMultimedia.Services;

/// <summary>
/// Abanico sobre los proveedores de catálogo: el fallo de uno no tumba al resto.
///
/// Esa tolerancia tenía un precio que no se estaba pagando. Mientras **algún**
/// proveedor respondiera, los errores de los demás se descartaban sin registrarlos en
/// ningún sitio: MangaDex podía llevar semanas caído, la búsqueda devolver menos
/// resultados de los debidos, y no quedaba ni una línea en el log. Un fallo parcial y
/// silencioso es indistinguible de «no hay resultados para esa consulta» (T4-11).
/// </summary>
public class ExternalCatalogSearchService(
    IEnumerable<IExternalCatalogProvider> providers,
    ILogger<ExternalCatalogSearchService> logger)
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

        // Cada fallo se registra por separado y con su proveedor: agregarlos en una
        // sola línea impediría ver que siempre falla el mismo.
        foreach (var (provider, result) in activeProviders.Zip(results))
        {
            if (result.Error is not null)
            {
                logger.LogWarning(
                    result.Error,
                    "El proveedor {Proveedor} falló al buscar. Los resultados que devuelva esta búsqueda están incompletos.",
                    provider.Key);
            }
        }

        var successfulItems = results
            .Where(result => result.Items is not null)
            .SelectMany(result => result.Items!)
            .ToList();

        if (successfulItems.Count == 0)
        {
            var firstError = results.FirstOrDefault(result => result.Error is not null).Error;
            if (firstError is not null)
            {
                // Fallaron todos: el error sube y acaba en el manejador global, que le
                // pondrá el identificador de correlación. Aquí no se registra otra vez.
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
