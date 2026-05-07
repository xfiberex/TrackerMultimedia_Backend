using TrackerMultimedia.Domain.Enums;

namespace TrackerMultimedia.Contracts.MediaItems;

public record MediaItemResponse(
    Guid Id,
    string Title,
    string? AlternativeTitle,
    MediaType? Type,
    string? Description,
    ContentKind ContentKind,
    MediaTrackingStatus Status,
    MediaItemSourceType SourceType,
    int? ExternalId,
    ExternalMediaKind? ExternalMediaKind,
    string? ExternalStatusLabel,
    double? ExternalScore,
    string? CoverImageUrl,
    string? ReferenceUrl,
    int? ReleaseYear,
    ProgressUnit ProgressUnit,
    int ProgressCount,
    int ProgressCurrent,
    int? ProgressTotal,
    int CurrentSeason,
    double? PersonalScore,
    string? Notes,
    IReadOnlyCollection<MediaItemCategorySummaryResponse> Categories,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public record MediaItemCategorySummaryResponse(
    Guid Id,
    string Name,
    string? Color);