using TrackerMultimedia.Domain.Enums;

namespace TrackerMultimedia.Contracts.MediaItems;

public record MediaItemsStatsResponse(
    int TotalCount,
    int PlannedCount,
    int InProgressCount,
    int CompletedCount,
    int OnHoldCount,
    int DroppedCount,
    int StartedThisMonthCount,
    int CompletedThisMonthCount,
    int BacklogWithoutStartCount,
    double? AveragePersonalScore,
    int ScoredItemsCount,
    IReadOnlyCollection<ContentKindStatResponse> ContentKindBreakdown,
    IReadOnlyCollection<MediaSourceStatResponse> SourceBreakdown,
    IReadOnlyCollection<CategoryStatResponse> CategoryBreakdown,
    IReadOnlyCollection<ContentKindAverageScoreStatResponse> AverageScoreByContentKind);

public record ContentKindStatResponse(ContentKind ContentKind, int Count);

public record MediaSourceStatResponse(MediaItemSourceType SourceType, int Count);

public record CategoryStatResponse(Guid CategoryId, string CategoryName, string? Color, int Count);

public record ContentKindAverageScoreStatResponse(ContentKind ContentKind, double AveragePersonalScore, int ScoredItemsCount);