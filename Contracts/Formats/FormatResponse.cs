using TrackerMultimedia.Domain.Enums;

namespace TrackerMultimedia.Contracts.Formats;

public record FormatResponse(Guid Id, string Name, ContentKind? ContentKind, int Order, DateTime CreatedAtUtc);
