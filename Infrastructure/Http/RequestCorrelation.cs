using System.Diagnostics;

namespace TrackerMultimedia.Infrastructure.Http;

/// <summary>
/// El identificador que relaciona lo que vio el usuario con lo que quedó en el log.
///
/// Existe porque antes había <b>dos</b>. La respuesta de error llevaba
/// <c>Activity.Current?.Id</c> y el manejador global de excepciones registraba
/// <c>HttpContext.TraceIdentifier</c>: formatos distintos y valores distintos, así que
/// buscar en el log el código que el usuario leía en pantalla no encontraba nada. La
/// correlación que T2-05 quiso montar no llegó a funcionar nunca.
///
/// Se usa <c>TraceId</c> y no <c>Activity.Current.Id</c> por un motivo concreto:
/// <c>Id</c> incluye el identificador del <i>span</i> actual, que cambia al entrar en
/// cualquier actividad hija —una llamada HTTP saliente, por ejemplo—, así que el valor
/// dependía de en qué punto de la petición se leyera. <c>TraceId</c> es estable durante
/// toda la petición, que es justo lo que hace falta para correlacionar.
/// </summary>
public static class RequestCorrelation
{
    /// <summary>
    /// Identificador estable de la petición en curso: los 32 caracteres del
    /// <c>TraceId</c> del W3C Trace Context.
    ///
    /// El respaldo a <c>TraceIdentifier</c> cubre el caso en que no haya ninguna
    /// actividad activa —sin escuchas de diagnóstico, ASP.NET Core no crea una—; es
    /// peor para correlacionar entre procesos, pero sigue siendo único por petición.
    /// Lo importante es que <b>este método es el único sitio donde se decide</b>, de
    /// modo que la respuesta y el log no puedan volver a discrepar.
    /// </summary>
    public static string GetCorrelationId(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
    }
}
