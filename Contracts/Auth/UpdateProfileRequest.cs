using System.ComponentModel.DataAnnotations;

namespace TrackerMultimedia.Contracts.Auth;

public record UpdateProfileRequest(
    [StringLength(100)] string? DisplayName);
