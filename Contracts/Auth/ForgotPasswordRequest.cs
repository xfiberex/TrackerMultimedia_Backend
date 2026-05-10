using System.ComponentModel.DataAnnotations;

namespace TrackerMultimedia.Contracts.Auth;

public record ForgotPasswordRequest(
    [Required, EmailAddress, StringLength(256)] string Email);
