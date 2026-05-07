namespace TrackerMultimedia.Domain.Entities;

public class MediaItemCategory
{
    public Guid MediaItemId { get; set; }
    public MediaItem MediaItem { get; set; } = null!;

    public Guid UserCategoryId { get; set; }
    public UserCategory UserCategory { get; set; } = null!;
}