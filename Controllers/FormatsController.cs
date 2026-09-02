using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TrackerMultimedia.Contracts.Formats;
using TrackerMultimedia.Infrastructure.Http;
using TrackerMultimedia.Services;

namespace TrackerMultimedia.Controllers;

[ApiController]
[Route("api/formats")]
[Authorize]
[EnableRateLimiting("user")]
public class FormatsController(FormatsService formatsService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<FormatResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var formats = await formatsService.GetAllAsync(User.GetUserId(), cancellationToken);
        return Ok(formats);
    }

    [HttpPost]
    public async Task<ActionResult<FormatResponse>> Create(
        [FromBody] CreateFormatRequest request,
        CancellationToken cancellationToken)
    {
        var result = await formatsService.CreateAsync(request, User.GetUserId(), cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(result.ErrorField!, result.ErrorMessage!);
            return ValidationProblem(ModelState);
        }

        return CreatedAtAction(nameof(GetAll), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<FormatResponse>> Update(
        Guid id,
        [FromBody] UpdateFormatRequest request,
        CancellationToken cancellationToken)
    {
        var result = await formatsService.UpdateAsync(id, request, User.GetUserId(), cancellationToken);
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
        var deleted = await formatsService.DeleteAsync(id, User.GetUserId(), cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
