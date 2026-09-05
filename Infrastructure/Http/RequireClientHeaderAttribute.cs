using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TrackerMultimedia.Infrastructure.Http;

/// <summary>
/// Exige una cabecera propia en los endpoints que se autentican con la cookie de refresco.
///
/// Es la defensa contra CSRF de esos dos endpoints, y funciona por cómo trata el navegador
/// las cabeceras no estándar: un <c>&lt;form&gt;</c> de otro sitio no puede añadirlas, y un
/// <c>fetch</c> que lo intente deja de ser una petición simple y dispara un preflight, que
/// el allowlist de CORS rechaza si el origen no está en la lista.
///
/// El valor no se comprueba, solo la presencia: no es un secreto ni pretende serlo. Lo que
/// aporta es que la petición no pueda construirse desde otro sitio, no que quien la manda
/// demuestre nada.
///
/// No va como middleware global a propósito: el resto del API se autentica con el Bearer en
/// memoria, que no es credencial ambiente y por tanto no es atacable por CSRF. Ponerlo en
/// todas partes daría a entender que sí lo es.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequireClientHeaderAttribute : Attribute, IActionFilter
{
    public const string HeaderName = "X-TM-Client";

    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.HttpContext.Request.Headers.ContainsKey(HeaderName))
        {
            context.Result = new UnauthorizedObjectResult("Sesión no válida. Vuelve a iniciar sesión.");
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
