using System.ComponentModel.DataAnnotations;
using TrackerMultimedia.Domain.Enums;

namespace TrackerMultimedia.Contracts.Formats;

public class CreateFormatRequest
{
    [Required]
    [StringLength(60, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [EnumDataType(typeof(ContentKind))]
    public ContentKind? ContentKind { get; set; }
}
