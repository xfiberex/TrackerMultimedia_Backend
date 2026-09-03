using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TrackerMultimedia.Contracts.Categories;
using TrackerMultimedia.Contracts.Common;
using TrackerMultimedia.Data;
using TrackerMultimedia.Domain.Entities;

namespace TrackerMultimedia.Services;

public partial class CategoriesService(ApplicationDbContext dbContext)
{
    public async Task<IReadOnlyCollection<CategoryResponse>> GetAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.UserCategories
            .AsNoTracking()
            .Where(category => category.UserId == userId)
            .OrderBy(category => category.Name)
            .Select(category => ToResponse(category))
            .ToListAsync(cancellationToken);
    }

    public async Task<ServiceResult<CategoryResponse>> CreateAsync(
        CreateCategoryRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var nameResult = NormalizeName(request.Name, nameof(request.Name));
        if (!nameResult.IsSuccess)
            return nameResult.ToFailure<CategoryResponse>();

        var colorResult = NormalizeColor(request.Color, nameof(request.Color));
        if (!colorResult.IsSuccess)
            return colorResult.ToFailure<CategoryResponse>();

        var normalizedName = NormalizeNameKey(nameResult.Value!);
        var alreadyExists = await dbContext.UserCategories
            .AnyAsync(category => category.UserId == userId && category.NormalizedName == normalizedName, cancellationToken);
        if (alreadyExists)
        {
            return ServiceResult<CategoryResponse>.Fail(
                nameof(request.Name),
                "Ya existe una categoría con ese nombre en tu cuenta.");
        }

        var category = new UserCategory
        {
            UserId = userId,
            Name = nameResult.Value!,
            NormalizedName = normalizedName,
            Color = colorResult.Value,
            CreatedAtUtc = DateTime.UtcNow,
        };

        dbContext.UserCategories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<CategoryResponse>.Ok(ToResponse(category));
    }

    public async Task<ServiceResult<CategoryResponse>> UpdateAsync(
        Guid id,
        UpdateCategoryRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var nameResult = NormalizeName(request.Name, nameof(request.Name));
        if (!nameResult.IsSuccess)
            return nameResult.ToFailure<CategoryResponse>();

        var colorResult = NormalizeColor(request.Color, nameof(request.Color));
        if (!colorResult.IsSuccess)
            return colorResult.ToFailure<CategoryResponse>();

        var category = await dbContext.UserCategories
            .FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId, cancellationToken);
        if (category is null)
            return ServiceResult<CategoryResponse>.Fail("id", "No encontrado.");

        var normalizedName = NormalizeNameKey(nameResult.Value!);
        var alreadyExists = await dbContext.UserCategories
            .AnyAsync(item => item.UserId == userId && item.Id != id && item.NormalizedName == normalizedName, cancellationToken);
        if (alreadyExists)
        {
            return ServiceResult<CategoryResponse>.Fail(
                nameof(request.Name),
                "Ya existe una categoría con ese nombre en tu cuenta.");
        }

        category.Name = nameResult.Value!;
        category.NormalizedName = normalizedName;
        category.Color = colorResult.Value;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<CategoryResponse>.Ok(ToResponse(category));
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var category = await dbContext.UserCategories
            .FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId, cancellationToken);
        if (category is null)
            return false;

        dbContext.UserCategories.Remove(category);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static CategoryResponse ToResponse(UserCategory category)
        => new(category.Id, category.Name, category.Color, category.CreatedAtUtc);

    private static ServiceResult<string?> NormalizeName(string name, string field)
    {
        var normalized = name.Trim();
        return normalized.Length > 0
            ? ServiceResult<string?>.Ok(normalized)
            : ServiceResult<string?>.Fail(field, "El nombre no puede estar vacío.");
    }

    private static ServiceResult<string?> NormalizeColor(string? color, string field)
    {
        var normalized = color?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return ServiceResult<string?>.Ok(null);

        normalized = normalized.ToUpperInvariant();
        return CategoryColorRegex().IsMatch(normalized)
            ? ServiceResult<string?>.Ok(normalized)
            : ServiceResult<string?>.Fail(field, "Color debe tener formato hexadecimal #RRGGBB.");
    }

    private static string NormalizeNameKey(string name)
        => name.Trim().ToUpperInvariant();

    [GeneratedRegex("^#[0-9A-F]{6}$")]
    private static partial Regex CategoryColorRegex();
}
