using System.ComponentModel.DataAnnotations;

namespace TrackerMultimedia.Contracts.Auth;

public record ConfirmEmailRequest(
    [Required, EmailAddress] string Email,
    [Required] string Token);

public record ResendConfirmationRequest(
    [Required, EmailAddress] string Email);

/// <summary>
/// Devuelto por GET /api/auth/methods para que el frontend muestre
/// solo los caminos de acceso habilitados.
/// </summary>
public record AuthMethodsResponse(
    bool ManualEnabled,
    bool GoogleEnabled,
    bool GitHubEnabled);
