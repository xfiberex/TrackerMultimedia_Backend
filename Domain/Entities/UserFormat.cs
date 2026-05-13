using System.ComponentModel.DataAnnotations;
using TrackerMultimedia.Domain.Enums;

namespace TrackerMultimedia.Domain.Entities;

public class UserFormat
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    [Required]
    [StringLength(60)]
    public string Name { get; set; } = string.Empty;

    public ContentKind? ContentKind { get; set; }

    public int Order { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
