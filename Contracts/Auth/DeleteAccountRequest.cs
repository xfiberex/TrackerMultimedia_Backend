using System.ComponentModel.DataAnnotations;

namespace TrackerMultimedia.Contracts.Auth;

/// <summary>
/// Reautenticación para borrar la cuenta. Un JWT válido no basta: el token pudo quedar
/// abierto en un equipo prestado, y esto es la única operación del sistema que no tiene
/// vuelta atrás.
///
/// Se admiten dos formas porque hay dos clases de cuenta. Las que tienen contraseña la
/// vuelven a escribir. Las creadas por Google o GitHub no tienen ninguna, así que
/// confirman escribiendo su propia dirección de correo: no prueba identidad —el correo
/// se ve en la pantalla de al lado—, pero sí que la acción es deliberada, que es lo que
/// se busca frente a un clic accidental.
/// </summary>
public record DeleteAccountRequest(
    [StringLength(100)] string? Password,
    [StringLength(256)] string? ConfirmationEmail);
