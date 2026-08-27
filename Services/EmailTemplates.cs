using System.Net;

namespace TrackerMultimedia.Services;

/// <summary>
/// Genera el HTML de los correos transaccionales de autenticación.
/// Mantener las plantillas aquí simplifica los tests y los cambios de estilo.
/// </summary>
public static class EmailTemplates
{
    public static string ConfirmEmail(string displayName, string confirmUrl) =>
        $"""
        <div style="font-family:sans-serif;max-width:520px;margin:auto;padding:32px">
          <h2>Confirma tu dirección de correo</h2>
          <p>Hola <strong>{WebUtility.HtmlEncode(displayName)}</strong>,</p>
          <p>Gracias por registrarte en <strong>TrackerMultimedia</strong>.
             Pulsa el botón de abajo para activar tu cuenta.</p>
          <p style="text-align:center;margin:32px 0">
            <a href="{confirmUrl}"
               style="background:#6366f1;color:#fff;padding:12px 28px;
                      border-radius:6px;text-decoration:none;font-weight:600">
              Confirmar cuenta
            </a>
          </p>
          <p style="color:#888;font-size:0.85em">
            Si no te registraste, ignora este correo.
            El enlace expira en 24&nbsp;horas.
          </p>
        </div>
        """;

    /// <summary>
    /// Se envía cuando alguien intenta registrarse con un correo que ya tiene cuenta.
    /// El aviso va al titular de la dirección, nunca a quien envió la petición: así la
    /// respuesta del endpoint es idéntica exista o no la cuenta.
    /// </summary>
    public static string AccountAlreadyExists(string displayName, string loginUrl, string resetPasswordUrl) =>
        $"""
        <div style="font-family:sans-serif;max-width:520px;margin:auto;padding:32px">
          <h2>Ya tienes una cuenta en TrackerMultimedia</h2>
          <p>Hola <strong>{WebUtility.HtmlEncode(displayName)}</strong>,</p>
          <p>Alguien ha intentado crear una cuenta con esta dirección de correo,
             pero ya existe una registrada.</p>
          <p style="text-align:center;margin:32px 0">
            <a href="{loginUrl}"
               style="background:#6366f1;color:#fff;padding:12px 28px;
                      border-radius:6px;text-decoration:none;font-weight:600">
              Iniciar sesión
            </a>
          </p>
          <p>¿No recuerdas la contraseña? Puedes
             <a href="{resetPasswordUrl}">restablecerla aquí</a>.</p>
          <p style="color:#888;font-size:0.85em">
            Si no has sido tú, no hace falta que hagas nada: tu cuenta sigue segura.
          </p>
        </div>
        """;

    public static string ResetPassword(string displayName, string resetUrl) =>
        $"""
        <div style="font-family:sans-serif;max-width:520px;margin:auto;padding:32px">
          <h2>Restablecer contraseña</h2>
          <p>Hola <strong>{WebUtility.HtmlEncode(displayName)}</strong>,</p>
          <p>Recibimos una solicitud para restablecer la contraseña de tu cuenta en
             <strong>TrackerMultimedia</strong>.</p>
          <p style="text-align:center;margin:32px 0">
            <a href="{resetUrl}"
               style="background:#6366f1;color:#fff;padding:12px 28px;
                      border-radius:6px;text-decoration:none;font-weight:600">
              Restablecer contraseña
            </a>
          </p>
          <p style="color:#888;font-size:0.85em">
            Si no solicitaste este cambio, ignora este correo.
            El enlace expira en 1&nbsp;hora.
          </p>
        </div>
        """;
}
