using System.ComponentModel.DataAnnotations;
using TrackerMultimedia.Domain.Enums;

namespace TrackerMultimedia.Contracts.MediaItems;

public class GetMediaItemsRequest
{
    [StringLength(200)]
    public string? Search { get; set; }

    [EnumDataType(typeof(MediaType))]
    public MediaType? Type { get; set; }

    [EnumDataType(typeof(MediaTrackingStatus))]
    public MediaTrackingStatus? Status { get; set; }

    public List<Guid>? CategoryIds { get; set; }

    public DateOnly? CreatedFrom { get; set; }

    public DateOnly? CreatedTo { get; set; }

    [Range(0, 10)]
    public double? MinPersonalScore { get; set; }

    [Range(0, 10)]
    public double? MaxPersonalScore { get; set; }

    [EnumDataType(typeof(MediaItemsSortField))]
    public MediaItemsSortField SortBy { get; set; } = MediaItemsSortField.CreatedAt;

    [EnumDataType(typeof(SortDirection))]
    public SortDirection SortDirection { get; set; } = SortDirection.Desc;

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 12;
}
