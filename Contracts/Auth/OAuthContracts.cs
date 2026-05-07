using System.ComponentModel.DataAnnotations;

namespace TrackerMultimedia.Contracts.Auth;

/// <summary>Respuesta al iniciar un flujo OAuth: devuelve la URL a la que redirigir al usuario.</summary>
public record OAuthInitResponse(string AuthorizationUrl);

/// <summary>
/// Cuando el email del proveedor ya existe en una cuenta manual,
/// el backend devuelve este objeto en lugar de emitir una sesión.
/// El frontend debe mostrar el login manual para confirmar la vinculación.
/// </summary>
public record OAuthLinkRequiredResponse(
    /// <summary>Token temporal de vinculación (vida corta, un solo uso).</summary>
    string LinkToken,
    /// <summary>Proveedor que originó la solicitud, p. ej. "google" o "github".</summary>
    string Provider,
    /// <summary>Email de la cuenta existente (ya normalizado en minúsculas).</summary>
    string Email);

/// <summary>Solicitud para finalizar la vinculación de un proveedor a una cuenta existente.</summary>
public record OAuthLinkConfirmRequest(
    [Required] string LinkToken,
    [Required] string Provider,
    [Required, EmailAddress] string Email,
    [Required] string Password);
