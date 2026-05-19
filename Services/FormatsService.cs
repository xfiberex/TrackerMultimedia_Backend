using Microsoft.EntityFrameworkCore;
using TrackerMultimedia.Contracts.Common;
using TrackerMultimedia.Contracts.Formats;
using TrackerMultimedia.Data;
using TrackerMultimedia.Domain.Entities;

namespace TrackerMultimedia.Services;

public class FormatsService(ApplicationDbContext dbContext)
{
    private static readonly string[] DefaultFormats =
    [
        "Anime",
        "Serie",
        "Película",
        "Manga",
        "Cómic",
        "Libro",
        "Videojuego",
        "Podcast",
        "Álbum",
        "Otro",
    ];

    public async Task<IReadOnlyCollection<FormatResponse>> GetAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        var formats = await dbContext.UserFormats
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderBy(f => f.Order)
            .ThenBy(f => f.Name)
            .ToListAsync(cancellationToken);

        if (formats.Count == 0)
        {
            formats = await SeedDefaultFormatsAsync(userId, cancellationToken);
        }

        return formats.Select(ToResponse).ToArray();
    }

    public async Task<ServiceResult<FormatResponse>> CreateAsync(
        CreateFormatRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (name.Length == 0)
            return ServiceResult<FormatResponse>.Fail(nameof(request.Name), "El nombre no puede estar vacío.");

        var normalizedName = NormalizeNameKey(name);
        var alreadyExists = await dbContext.UserFormats
            .AnyAsync(f => f.UserId == userId && f.NormalizedName == normalizedName, cancellationToken);
        if (alreadyExists)
            return ServiceResult<FormatResponse>.Fail(nameof(request.Name), "Ya existe un formato con ese nombre en tu cuenta.");

        var nextOrder = await dbContext.UserFormats
            .Where(f => f.UserId == userId)
            .MaxAsync(f => (int?)f.Order, cancellationToken) ?? 0;

        var format = new UserFormat
        {
            UserId = userId,
            Name = name,
            NormalizedName = normalizedName,
            Order = nextOrder + 1,
            CreatedAtUtc = DateTime.UtcNow,
        };

        dbContext.UserFormats.Add(format);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<FormatResponse>.Ok(ToResponse(format));
    }

    public async Task<ServiceResult<FormatResponse>> UpdateAsync(
        Guid id,
        UpdateFormatRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (name.Length == 0)
            return ServiceResult<FormatResponse>.Fail(nameof(request.Name), "El nombre no puede estar vacío.");

        var format = await dbContext.UserFormats
            .FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId, cancellationToken);
        if (format is null)
            return ServiceResult<FormatResponse>.Fail("id", "Not found");

        var normalizedName = NormalizeNameKey(name);
        var alreadyExists = await dbContext.UserFormats
            .AnyAsync(f => f.UserId == userId && f.Id != id && f.NormalizedName == normalizedName, cancellationToken);
        if (alreadyExists)
            return ServiceResult<FormatResponse>.Fail(nameof(request.Name), "Ya existe un formato con ese nombre en tu cuenta.");

        format.Name = name;
        format.NormalizedName = normalizedName;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<FormatResponse>.Ok(ToResponse(format));
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var format = await dbContext.UserFormats
            .FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId, cancellationToken);
        if (format is null)
            return false;

        dbContext.UserFormats.Remove(format);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<List<UserFormat>> SeedDefaultFormatsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var formats = DefaultFormats
            .Select((name, index) => new UserFormat
            {
                UserId = userId,
                Name = name,
                NormalizedName = NormalizeNameKey(name),
                Order = index + 1,
                CreatedAtUtc = DateTime.UtcNow,
            })
            .ToList();

        dbContext.UserFormats.AddRange(formats);
        await dbContext.SaveChangesAsync(cancellationToken);
        return formats;
    }

    private static string NormalizeNameKey(string name)
        => name.Trim().ToUpperInvariant();

    private static FormatResponse ToResponse(UserFormat format)
        => new(format.Id, format.Name, format.Order, format.CreatedAtUtc);
}
