using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TrackerMultimedia.Contracts.Common;
using TrackerMultimedia.Contracts.MediaItems;
using TrackerMultimedia.Services;

namespace TrackerMultimedia.Controllers;

[ApiController]
[Route("api/media-items")]
[Authorize]
[EnableRateLimiting("user")]
public class MediaItemsController(MediaItemsService mediaItemsService) : ControllerBase
{
    /// <summary>
    /// Extrae el UserId del JWT. Lanza si el claim no existe (no debería ocurrir con [Authorize]).
    /// </summary>
    private Guid GetUserId()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("El claim 'sub' no está presente en el token.");
        return Guid.Parse(sub);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<MediaItemResponse>>> GetAll(
        [FromQuery] GetMediaItemsRequest request,
        CancellationToken cancellationToken)
    {
        var response = await mediaItemsService.GetAllAsync(request, GetUserId(), cancellationToken);
        return Ok(response);
    }

    [HttpGet("stats")]
    public async Task<ActionResult<MediaItemsStatsResponse>> GetStats(CancellationToken cancellationToken)
    {
        var stats = await mediaItemsService.GetStatsAsync(GetUserId(), cancellationToken);
        return Ok(stats);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MediaItemResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await mediaItemsService.GetByIdAsync(id, GetUserId(), cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportLibrary(
        [FromQuery] LibraryTransferFormat format,
        CancellationToken cancellationToken)
    {
        var export = await mediaItemsService.ExportAsync(format, GetUserId(), cancellationToken);
        return File(export.Content, export.ContentType, export.FileName);
    }

    // Límite máximo de 10 MB para archivos de importación.
    private const long MaxImportFileSizeBytes = 10 * 1024 * 1024;

    [HttpPost("import")]
    public async Task<ActionResult<LibraryImportResponse>> ImportLibrary(
        [FromQuery] LibraryTransferFormat format,
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError("file", "Debes adjuntar un archivo exportado por TrackerMultimedia.");
            return ValidationProblem(ModelState);
        }

        if (file.Length > MaxImportFileSizeBytes)
        {
            ModelState.AddModelError("file", $"El archivo supera el límite de {MaxImportFileSizeBytes / (1024 * 1024)} MB.");
            return ValidationProblem(ModelState);
        }

        await using var stream = file.OpenReadStream();
        var result = await mediaItemsService.ImportAsync(stream, format, GetUserId(), cancellationToken);

        if (!result.IsSuccess)
        {
            ModelState.AddModelError(result.ErrorField!, result.ErrorMessage!);
            return ValidationProblem(ModelState);
        }

        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<ActionResult<MediaItemResponse>> Create(
        [FromBody] CreateMediaItemRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediaItemsService.CreateAsync(request, GetUserId(), cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(result.ErrorField!, result.ErrorMessage!);
            return ValidationProblem(ModelState);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MediaItemResponse>> Update(
        Guid id,
        [FromBody] UpdateMediaItemRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediaItemsService.UpdateAsync(id, request, GetUserId(), cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.ErrorField == "id")
                return NotFound();

            ModelState.AddModelError(result.ErrorField!, result.ErrorMessage!);
            return ValidationProblem(ModelState);
        }

        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await mediaItemsService.DeleteAsync(id, GetUserId(), cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}

