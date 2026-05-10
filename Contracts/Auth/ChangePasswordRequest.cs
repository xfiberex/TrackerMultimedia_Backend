using System.ComponentModel.DataAnnotations;

namespace TrackerMultimedia.Contracts.Auth;

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, StringLength(100, MinimumLength = 8)] string NewPassword);
