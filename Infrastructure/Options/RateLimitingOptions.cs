namespace TrackerMultimedia.Infrastructure.Options;

/// <summary>
/// Cupos del limitador de peticiones. **Los valores por defecto son los que el proyecto
/// ha tenido siempre**; lo que cambia es que dejan de estar escritos en `Program.cs`.
///
/// Se hicieron configurables al montar las pruebas end-to-end (T4-04), que agotaban el
/// cupo de autenticación a mitad de suite: cada prueba registra una cuenta y entra, y
/// todas llegan desde la misma dirección, así que caen en la misma partición.
///
/// Sirve para lo mismo en casa. Servido por LAN, el proxy de Vite hace que todas las
/// peticiones lleguen al backend desde <c>localhost</c>, de modo que **los diez intentos
/// de acceso por minuto se reparten entre todos los dispositivos**, no uno por cada uno.
/// Con varias personas usándolo a la vez, subir <c>Auth.PermitLimit</c> es la salida.
///
/// **Subirlo tiene coste:** ese cupo es el freno a la fuerza bruta contra login y
/// registro. En un despliegue público hay que dejarlo bajo y declarar el proxy, para que
/// la partición vuelva a ser por dirección real (T1-05).
/// </summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Login, registro y renovación de sesión. El freno de fuerza bruta.</summary>
    public RateLimitPolicyOptions Auth { get; init; } = new() { PermitLimit = 10 };

    // Hubo una tercera política, `Search`, con 30 peticiones por minuto. Se retiró el
    // 2026-09-06 al eliminarse la búsqueda en catálogos externos: protegía el único
    // endpoint que llamaba a terceros y ya no existe ninguno.

    /// <summary>
    /// Resto de endpoints autenticados. Particiona por usuario, no por dirección, así que
    /// no sufre el reparto entre dispositivos que sí afecta a los otros dos.
    /// </summary>
    public RateLimitPolicyOptions User { get; init; } = new() { PermitLimit = 200 };
}

/// <summary>Cupo de una política: cuántas peticiones caben en cuánto tiempo.</summary>
public sealed class RateLimitPolicyOptions
{
    /// <summary>Peticiones permitidas por ventana.</summary>
    public int PermitLimit { get; init; }

    /// <summary>Duración de la ventana, en segundos.</summary>
    public int WindowSeconds { get; init; } = 60;
}
