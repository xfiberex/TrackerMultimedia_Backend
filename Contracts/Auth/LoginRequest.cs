using System.ComponentModel.DataAnnotations;

namespace TrackerMultimedia.Contracts.Auth;

// El tope de 100 caracteres es el mismo que impone el registro desde el primer
// commit, así que ninguna cuenta puede tener una contraseña más larga y nadie se
// queda fuera. Sin él, una cadena de megabytes llegaba hasta CheckPasswordAsync y
// gastaba CPU en el hashing PBKDF2 antes de poder fallar: un rechazo barato hecho
// caro. No se declara mínimo a propósito: aquí solo se comprueba, no se define la
// política, y anunciarla en el login no aporta nada a quien ya tiene cuenta.
public record LoginRequest(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(100)] string Password);
