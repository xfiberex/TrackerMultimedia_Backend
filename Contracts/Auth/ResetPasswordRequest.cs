using System.ComponentModel.DataAnnotations;

namespace TrackerMultimedia.Contracts.Auth;

public record ResetPasswordRequest(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required] string Token,
    [Required, StringLength(100, MinimumLength = 8)] string NewPassword);
