using System.ComponentModel.DataAnnotations;

namespace TrackerMultimedia.Contracts.Formats;

public class UpdateFormatRequest
{
    [Required]
    [StringLength(60, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
}
