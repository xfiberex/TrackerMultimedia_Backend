using System.ComponentModel.DataAnnotations;
using TrackerMultimedia.Domain.Enums;
using TrackerMultimedia.Domain.Validation;

namespace TrackerMultimedia.Domain.Entities;

public class MediaItem
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Propietario del elemento. Nunca nulo en BD.</summary>
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(200)]
    public string? AlternativeTitle { get; set; }

    [StringLength(4000)]
    public string? Description { get; set; }

    [EnumDataType(typeof(MediaType))]
    public MediaType? Type { get; set; }

    [EnumDataType(typeof(ContentKind))]
    public ContentKind ContentKind { get; set; }

    [EnumDataType(typeof(MediaTrackingStatus))]
    public MediaTrackingStatus Status { get; set; }

    // Aquí vivían cinco campos de procedencia externa —SourceType, ExternalId,
    // ExternalMediaKind, ExternalStatusLabel y ExternalScore— que solo rellenaba la
    // importación desde catálogos. Se retiraron el 2026-09-06 con «Descubrir»: todo
    // elemento se crea a mano, así que no había nada que pudiera escribirlos.
    //
    // CoverImageUrl y ReferenceUrl se quedan a propósito: no eran de los catálogos, son
    // dos direcciones que el usuario pega él mismo.

    [HttpOrHttpsUrl]
    [StringLength(500)]
    public string? CoverImageUrl { get; set; }

    [HttpOrHttpsUrl]
    [StringLength(500)]
    public string? ReferenceUrl { get; set; }

    [Range(1900, 2100)]
    public int? ReleaseYear { get; set; }

    [Range(0, int.MaxValue)]
    public int ProgressCount { get; set; }

    [Range(0, int.MaxValue)]
    public int ProgressCurrent { get; set; }

    [Range(1, int.MaxValue)]
    public int? ProgressTotal { get; set; }

    [EnumDataType(typeof(ProgressUnit))]
    public ProgressUnit ProgressUnit { get; set; }

    [Range(1, int.MaxValue)]
    public int CurrentSeason { get; set; } = 1;

    [Range(0, 10)]
    public double? PersonalScore { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    public Guid? UserFormatId { get; set; }
    public UserFormat? UserFormat { get; set; }

    public ICollection<MediaItemCategory> MediaItemCategories { get; set; } = new List<MediaItemCategory>();

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
