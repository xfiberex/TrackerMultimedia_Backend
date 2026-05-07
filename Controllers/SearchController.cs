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
                title: "External search is unavailable.",
                detail: "The external catalog request failed. Try again later.",
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (JsonException)
        {
            return Problem(
                title: "External search is unavailable.",
                detail: "An external catalog returned an unexpected response. Try again later.",
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Problem(
                title: "External search timed out.",
                detail: "An external catalog did not respond in time. Try again later.",
                statusCode: StatusCodes.Status504GatewayTimeout);
        }
    }

    [HttpGet("providers")]
    public ActionResult<IReadOnlyCollection<ExternalCatalogProviderResponse>> GetProviders()
        => Ok(externalCatalogSearchService.GetProviders());
}