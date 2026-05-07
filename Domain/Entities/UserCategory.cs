using System.ComponentModel.DataAnnotations;

namespace TrackerMultimedia.Domain.Entities;

public class UserCategory
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    [Required]
    [StringLength(60)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(60)]
    public string NormalizedName { get; set; } = string.Empty;

    [StringLength(7)]
    public string? Color { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<MediaItemCategory> MediaItemCategories { get; set; } = new List<MediaItemCategory>();
}