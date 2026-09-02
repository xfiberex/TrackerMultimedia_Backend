using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TrackerMultimedia.Contracts.Search;
using TrackerMultimedia.Services;

namespace TrackerMultimedia.Controllers;

[ApiController]
[Route("api/discover")]
[Authorize]
public class SearchController(ExternalCatalogSearchService externalCatalogSearchService) : ControllerBase
{
    [HttpGet("search")]
    [HttpGet("~/api/search")]
    [EnableRateLimiting("search")]
    public async Task<ActionResult<IReadOnlyCollection<SearchMediaItemResponse>>> Search(
        [FromQuery] SearchMediaItemsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var results = await externalCatalogSearchService.SearchAsync(request, cancellationToken);
            return Ok(results);
        }
        catch (HttpRequestException)
        {
            return Problem(
                title: "La búsqueda externa no está disponible.",
                detail: "No se pudo contactar con el catálogo externo. Inténtalo de nuevo más tarde.",
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (JsonException)
        {
            return Problem(
                title: "La búsqueda externa no está disponible.",
                detail: "Un catálogo externo devolvió una respuesta inesperada. Inténtalo de nuevo más tarde.",
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Problem(
                title: "La búsqueda externa ha tardado demasiado.",
                detail: "Un catálogo externo no respondió a tiempo. Inténtalo de nuevo más tarde.",
                statusCode: StatusCodes.Status504GatewayTimeout);
        }
    }

    [HttpGet("providers")]
    public ActionResult<IReadOnlyCollection<ExternalCatalogProviderResponse>> GetProviders()
        => Ok(externalCatalogSearchService.GetProviders());
}
