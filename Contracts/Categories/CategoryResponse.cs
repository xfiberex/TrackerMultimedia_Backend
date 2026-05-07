namespace TrackerMultimedia.Contracts.Categories;

public record CategoryResponse(
    Guid Id,
    string Name,
    string? Color,
    DateTime CreatedAtUtc);