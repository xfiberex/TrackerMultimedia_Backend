using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TrackerMultimedia.Data;
using TrackerMultimedia.Domain.Entities;

namespace TrackerMultimedia.Services;

/// <summary>
/// Reúne en un solo archivo todo lo que la aplicación guarda sobre una persona.
///
/// La exportación de biblioteca (<c>GET /api/media-items/export</c>) existe para llevarse
/// los datos a otra instalación, así que su formato es el que sabe leer la importación y
/// deja fuera lo que no se puede reimportar: el correo, el nombre visible, las fechas de
/// la cuenta, los proveedores externos vinculados y las sesiones abiertas. Eso es
/// justamente lo que pide la portabilidad, de ahí que este archivo sea distinto del otro
/// y no una ampliación suya: cambiar el esquema de transferencia rompería la importación.
/// </summary>
public class PersonalDataExportService(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    MediaItemsService mediaItemsService)
{
    public const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions ExportJsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
            WriteIndented = true
        };

    /// <summary>
    /// Devuelve el archivo, o <c>null</c> si el usuario ya no existe: el JWT puede seguir
    /// siendo válido unos minutos después de borrar la cuenta.
    /// </summary>
    public async Task<(byte[] Content, string ContentType, string FileName)?> ExportAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return null;

        var logins = await userManager.GetLoginsAsync(user);

        var sessions = await dbContext.RefreshTokens
            .AsNoTracking()
            .Where(token => token.UserId == userId)
            .OrderByDescending(token => token.CreatedAtUtc)
            .Select(token => new PersonalDataSession(
                token.CreatedAtUtc,
                token.ExpiresAtUtc,
                token.IsRevoked))
            .ToListAsync(cancellationToken);

        var formats = await dbContext.UserFormats
            .AsNoTracking()
            .Where(format => format.UserId == userId)
            .OrderBy(format => format.Order)
            .ThenBy(format => format.Name)
            .Select(format => new PersonalDataFormat(
                format.Name,
                format.Order,
                format.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var payload = new PersonalDataExport(
            SchemaVersion,
            DateTime.UtcNow,
            new PersonalDataAccount(
                user.Id,
                user.Email,
                user.EmailConfirmed,
                user.DisplayName,
                user.CreatedAtUtc,
                await userManager.HasPasswordAsync(user),
                user.LockoutEnd?.UtcDateTime),
            [.. logins.Select(login => new PersonalDataExternalLogin(
                login.LoginProvider,
                login.ProviderDisplayName))],
            sessions,
            formats,
            await mediaItemsService.BuildLibraryPayloadAsync(userId, cancellationToken));

        var content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, ExportJsonOptions));

        return (
            content,
            "application/json; charset=utf-8",
            $"tracker-datos-personales-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
    }

    public sealed record PersonalDataExport(
        int SchemaVersion,
        DateTime ExportedAtUtc,
        PersonalDataAccount Account,
        IReadOnlyList<PersonalDataExternalLogin> ExternalLogins,
        IReadOnlyList<PersonalDataSession> Sessions,
        IReadOnlyList<PersonalDataFormat> Formats,
        object Library);

    public sealed record PersonalDataAccount(
        Guid Id,
        string? Email,
        bool EmailConfirmed,
        string? DisplayName,
        DateTime CreatedAtUtc,
        bool HasPassword,
        DateTime? LockoutEndUtc);

    /// <summary>
    /// Se dice qué proveedores hay vinculados, no con qué identificador. El
    /// <c>ProviderKey</c> es el identificador de esa persona dentro de Google o GitHub:
    /// no aporta nada a quien se lleva sus datos y el archivo acaba en una carpeta de
    /// descargas o en un correo.
    /// </summary>
    public sealed record PersonalDataExternalLogin(string Provider, string? ProviderDisplayName);

    /// <summary>
    /// Sesiones abiertas: cuándo se abrieron, cuándo caducan y si siguen vivas. El token
    /// no aparece —ni siquiera su hash—, porque un token de refresco es una credencial en
    /// activo: incluirlo convertiría el archivo exportado en una llave de la cuenta.
    /// </summary>
    public sealed record PersonalDataSession(DateTime CreatedAtUtc, DateTime ExpiresAtUtc, bool IsRevoked);

    public sealed record PersonalDataFormat(string Name, int Order, DateTime CreatedAtUtc);
}
