using System.ComponentModel.DataAnnotations;

namespace TrackerMultimedia.Contracts.Auth;

/// <summary>Respuesta al iniciar un flujo OAuth: devuelve la URL a la que redirigir al usuario.</summary>
public record OAuthInitResponse(string AuthorizationUrl);

/// <summary>Solicitud para finalizar la vinculación de un proveedor a una cuenta existente.</summary>
public record OAuthLinkConfirmRequest(
    [Required] string LinkToken,
    [Required] string Provider,
    [Required, EmailAddress] string Email,
    [Required] string Password);
