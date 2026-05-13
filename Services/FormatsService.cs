using Microsoft.EntityFrameworkCore;
using TrackerMultimedia.Contracts.Common;
using TrackerMultimedia.Contracts.Formats;
using TrackerMultimedia.Data;
using TrackerMultimedia.Domain.Entities;
using TrackerMultimedia.Domain.Enums;

namespace TrackerMultimedia.Services;

public class FormatsService(ApplicationDbContext dbContext)
{
    private static readonly (string Name, ContentKind ContentKind)[] DefaultFormats =
    [
        ("Serie",    ContentKind.Series),
        ("Película", ContentKind.Movie),
        ("Libro",    ContentKind.Book),
        ("Cómic",   ContentKind.Comic),
        ("Juego",    ContentKind.Game),
        ("Podcast",  ContentKind.Podcast),
        ("Video",    ContentKind.Video),
        ("Álbum",   ContentKind.Album),
        ("Otro",     ContentKind.Other),
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

        var nextOrder = await dbContext.UserFormats
            .Where(f => f.UserId == userId)
            .MaxAsync(f => (int?)f.Order, cancellationToken) ?? 0;

        var format = new UserFormat
        {
            UserId = userId,
            Name = name,
            ContentKind = request.ContentKind,
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

        format.Name = name;
        format.ContentKind = request.ContentKind;

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
            .Select((entry, index) => new UserFormat
            {
                UserId = userId,
                Name = entry.Name,
                ContentKind = entry.ContentKind,
                Order = index + 1,
                CreatedAtUtc = DateTime.UtcNow,
            })
            .ToList();

        dbContext.UserFormats.AddRange(formats);
        await dbContext.SaveChangesAsync(cancellationToken);
        return formats;
    }

    private static FormatResponse ToResponse(UserFormat format)
        => new(format.Id, format.Name, format.ContentKind, format.Order, format.CreatedAtUtc);
}
