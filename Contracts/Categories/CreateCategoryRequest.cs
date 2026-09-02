using System.ComponentModel.DataAnnotations;

namespace TrackerMultimedia.Contracts.Categories;

public class CreateCategoryRequest
{
    [Required]
    [StringLength(60)]
    public string Name { get; set; } = string.Empty;

    [StringLength(7)]
    public string? Color { get; set; }
}
