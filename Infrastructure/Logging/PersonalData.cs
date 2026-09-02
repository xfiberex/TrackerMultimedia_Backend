namespace TrackerMultimedia.Infrastructure.Logging;

/// <summary>
/// Enmascarado de datos personales antes de escribirlos en el log.
///
/// El log es un destino con su propia retención y su propio control de acceso:
/// una dirección de correo completa ahí es tratamiento de datos personales que
/// sobrevive al borrado de la cuenta. La regla del proyecto es registrar el
/// <c>UserId</c> siempre que exista; esto cubre el caso en el que todavía no hay
/// usuario (un alta que falla, un correo que se envía a alguien sin cuenta) y aun
/// así hace falta poder correlacionar el evento con un aviso concreto.
/// </summary>
public static class PersonalData
{
    /// <summary>
    /// Devuelve una forma parcial de la dirección: se conserva la primera letra
    /// del buzón y el dominio íntegro (<c>a***@ejemplo.com</c>). Basta para
    /// reconocer un correo que ya se conoce, no para descubrir uno que no.
    /// </summary>
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return "(vacío)";

        var atIndex = email.IndexOf('@');
        if (atIndex <= 0)
            return "***";

        return $"{email[0]}***{email[atIndex..]}";
    }
}
