using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace TrackerMultimedia.Infrastructure.Http;

/// <summary>
/// Lectura del identificador de usuario desde el claim <c>sub</c> del JWT.
///
/// Estaba copiado en tres controladores con el mismo cuerpo palabra por palabra, y en un
/// cuarto con una forma distinta. Duplicar la lectura de un claim es de las cosas que
/// menos molestan hasta que hay que cambiarla: el día que el claim cambie de nombre, hay
/// que acordarse de los cuatro sitios.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Para endpoints con <c>[Authorize]</c>, donde la ausencia del claim no es un caso
    /// de uso sino un token que este backend no pudo emitir: falla ruidosamente.
    /// </summary>
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("El claim 'sub' no está presente en el token.");
        return Guid.Parse(sub);
    }

    /// <summary>
    /// Para los endpoints que prefieren responder 401 antes que romperse.
    /// </summary>
    public static bool TryGetUserId(this ClaimsPrincipal principal, out Guid userId)
    {
        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(sub, out userId);
    }
}
