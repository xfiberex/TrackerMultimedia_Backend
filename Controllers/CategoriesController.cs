using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TrackerMultimedia.Contracts.Categories;
using TrackerMultimedia.Services;

namespace TrackerMultimedia.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize]
[EnableRateLimiting("user")]
public class CategoriesController(CategoriesService categoriesService) : ControllerBase
{
    private Guid GetUserId()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("El claim 'sub' no está presente en el token.");
        return Guid.Parse(sub);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<CategoryResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var categories = await categoriesService.GetAllAsync(GetUserId(), cancellationToken);
        return Ok(categories);
    }

    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create(
        [FromBody] CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await categoriesService.CreateAsync(request, GetUserId(), cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(result.ErrorField!, result.ErrorMessage!);
            return ValidationProblem(ModelState);
        }

        return CreatedAtAction(nameof(GetAll), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CategoryResponse>> Update(
        Guid id,
        [FromBody] UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await categoriesService.UpdateAsync(id, request, GetUserId(), cancellationToken);
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
        var deleted = await categoriesService.DeleteAsync(id, GetUserId(), cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}