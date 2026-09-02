using TrackerMultimedia.Domain.Enums;

namespace TrackerMultimedia.Contracts.Search;

public record SearchMediaItemResponse(
    int ExternalId,
    string Title,
    string? AlternativeTitle,
    MediaType SuggestedType,
    MediaItemSourceType SourceType,
    ExternalMediaKind ExternalMediaKind,
    string? ExternalStatusLabel,
    double? ExternalScore,
    string? CoverImageUrl,
    string? ReferenceUrl,
    int? ReleaseYear);
