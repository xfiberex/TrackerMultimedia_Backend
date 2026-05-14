using System.ComponentModel.DataAnnotations;
using TrackerMultimedia.Domain.Enums;

namespace TrackerMultimedia.Contracts.Search;

public class SearchMediaItemsRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Query { get; set; } = string.Empty;

    [EnumDataType(typeof(MediaSearchType))]
    public MediaSearchType Type { get; set; } = MediaSearchType.All;

    [Range(1, 12)]
    public int Limit { get; set; } = 12;

    public List<string> Providers { get; set; } = [];
}