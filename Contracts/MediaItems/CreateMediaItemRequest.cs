using System.ComponentModel.DataAnnotations;
using TrackerMultimedia.Domain.Enums;
using TrackerMultimedia.Domain.Validation;

namespace TrackerMultimedia.Contracts.MediaItems;

public class CreateMediaItemRequest
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(200)]
    public string? AlternativeTitle { get; set; }

    [EnumDataType(typeof(MediaType))]
    public MediaType? Type { get; set; }

    [StringLength(4000)]
    public string? Description { get; set; }

    [EnumDataType(typeof(ContentKind))]
    public ContentKind? ContentKind { get; set; }

    [Required]
    [EnumDataType(typeof(MediaTrackingStatus))]
    public MediaTrackingStatus Status { get; set; }

    [HttpOrHttpsUrl]
    [StringLength(500)]
    public string? CoverImageUrl { get; set; }

    [HttpOrHttpsUrl]
    [StringLength(500)]
    public string? ReferenceUrl { get; set; }

    [Range(1900, 2100)]
    public int? ReleaseYear { get; set; }

    [EnumDataType(typeof(ProgressUnit))]
    public ProgressUnit? ProgressUnit { get; set; }

    [Range(0, int.MaxValue)]
    public int ProgressCount { get; set; }

    [Range(0, int.MaxValue)]
    public int? ProgressCurrent { get; set; }

    [Range(1, int.MaxValue)]
    public int? ProgressTotal { get; set; }

    [Range(1, int.MaxValue)]
    public int CurrentSeason { get; set; } = 1;

    [Range(0, 10)]
    public double? PersonalScore { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    public List<Guid>? CategoryIds { get; set; }

    public Guid? UserFormatId { get; set; }

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }
}
