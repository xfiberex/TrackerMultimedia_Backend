using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TrackerMultimedia.Contracts.Common;
using TrackerMultimedia.Contracts.MediaItems;
using TrackerMultimedia.Data;
using TrackerMultimedia.Domain.Entities;
using TrackerMultimedia.Domain.Enums;

namespace TrackerMultimedia.Services;

public class MediaItemsService(ApplicationDbContext dbContext)
{
    private const int LibraryTransferSchemaVersion = 1;
    private static readonly Regex CategoryColorPattern = new("^#[0-9A-F]{6}$", RegexOptions.Compiled);

    private static readonly JsonSerializerOptions TransferJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions TransferPrettyJsonOptions = new(TransferJsonOptions)
    {
        WriteIndented = true
    };

    private static readonly string[] LibraryCsvHeaders =
    [
        "OriginalItemId",
        "Title",
        "AlternativeTitle",
        "Description",
        "Type",
        "ContentKind",
        "Status",
        "SourceType",
        "ExternalId",
        "ExternalMediaKind",
        "ExternalStatusLabel",
        "ExternalScore",
        "CoverImageUrl",
        "ReferenceUrl",
        "ReleaseYear",
        "ProgressUnit",
        "ProgressCount",
        "ProgressCurrent",
        "ProgressTotal",
        "CurrentSeason",
        "PersonalScore",
        "Notes",
        "CategoriesJson",
        "StartedAtUtc",
        "CompletedAtUtc",
        "CreatedAtUtc",
        "UpdatedAtUtc",
    ];

    // -------------------------------------------------------------------------
    // Consultas
    // -------------------------------------------------------------------------

    public async Task<PagedResponse<MediaItemResponse>> GetAllAsync(
        GetMediaItemsRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        IQueryable<MediaItem> query = dbContext.MediaItems
            .AsNoTracking()
            .Where(item => item.UserId == userId);

        var normalizedSearch = NormalizeOptionalText(request.Search);
        if (normalizedSearch is not null)
        {
            var escapedSearch = normalizedSearch
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");
            var searchPattern = $"%{escapedSearch}%";
            query = query.Where(item =>
                EF.Functions.ILike(item.Title, searchPattern, "\\") ||
                (item.AlternativeTitle != null && EF.Functions.ILike(item.AlternativeTitle, searchPattern, "\\")));
        }

        if (request.Type.HasValue)
            query = query.Where(item => item.Type == request.Type.Value);

        if (request.Status.HasValue)
            query = query.Where(item => item.Status == request.Status.Value);

        if (request.SourceType.HasValue)
            query = query.Where(item => item.SourceType == request.SourceType.Value);

        if (request.CategoryIds is { Count: > 0 })
        {
            var categoryIds = request.CategoryIds
                .Where(categoryId => categoryId != Guid.Empty)
                .Distinct()
                .ToArray();

            if (categoryIds.Length > 0)
            {
                query = query.Where(item => item.MediaItemCategories.Any(link => categoryIds.Contains(link.UserCategoryId)));
            }
        }

        if (request.CreatedFrom.HasValue)
        {
            var from = request.CreatedFrom.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(item => item.CreatedAtUtc >= from);
        }

        if (request.CreatedTo.HasValue)
        {
            var toExclusive = request.CreatedTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(item => item.CreatedAtUtc < toExclusive);
        }

        if (request.MinPersonalScore.HasValue)
            query = query.Where(item => item.PersonalScore.HasValue && item.PersonalScore.Value >= request.MinPersonalScore.Value);

        if (request.MaxPersonalScore.HasValue)
            query = query.Where(item => item.PersonalScore.HasValue && item.PersonalScore.Value <= request.MaxPersonalScore.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)request.PageSize);

        query = ApplySorting(query, request.SortBy, request.SortDirection);

        var items = await query
            .Include(item => item.MediaItemCategories)
            .ThenInclude(link => link.UserCategory)
            .Include(item => item.UserFormat)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return new PagedResponse<MediaItemResponse>(
            items.Select(ToResponse).ToList(),
            request.Page,
            request.PageSize,
            totalCount,
            totalPages);
    }

    public async Task<MediaItemsStatsResponse> GetStatsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var baseQuery = dbContext.MediaItems
            .AsNoTracking()
            .Where(item => item.UserId == userId);

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonthStart = monthStart.AddMonths(1);

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var statusCounts = await baseQuery
            .GroupBy(item => item.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.Status, group => group.Count, cancellationToken);

        var startedThisMonthCount = await baseQuery
            .CountAsync(
                item => item.StartedAtUtc.HasValue &&
                    item.StartedAtUtc.Value >= monthStart &&
                    item.StartedAtUtc.Value < nextMonthStart,
                cancellationToken);

        var completedThisMonthCount = await baseQuery
            .CountAsync(
                item => item.CompletedAtUtc.HasValue &&
                    item.CompletedAtUtc.Value >= monthStart &&
                    item.CompletedAtUtc.Value < nextMonthStart,
                cancellationToken);

        var backlogWithoutStartCount = await baseQuery
            .CountAsync(
                item => item.Status == MediaTrackingStatus.Planned && !item.StartedAtUtc.HasValue,
                cancellationToken);

        var scoredItems = await baseQuery
            .Where(item => item.PersonalScore.HasValue)
            .Select(item => item.PersonalScore!.Value)
            .ToListAsync(cancellationToken);

        var contentKindBreakdown = await baseQuery
            .GroupBy(item => item.ContentKind)
            .Select(group => new { ContentKind = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var sourceBreakdown = await baseQuery
            .GroupBy(item => item.SourceType)
            .Select(group => new { SourceType = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var categoryBreakdown = await dbContext.MediaItemCategories
            .AsNoTracking()
            .Where(link => link.MediaItem.UserId == userId)
            .GroupBy(link => new { link.UserCategoryId, link.UserCategory.Name, link.UserCategory.Color })
            .Select(group => new
            {
                group.Key.UserCategoryId,
                group.Key.Name,
                group.Key.Color,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        var averageScoreByContentKind = await baseQuery
            .Where(item => item.PersonalScore.HasValue)
            .GroupBy(item => item.ContentKind)
            .Select(group => new
            {
                ContentKind = group.Key,
                AveragePersonalScore = group.Average(item => item.PersonalScore!.Value),
                ScoredItemsCount = group.Count()
            })
            .ToListAsync(cancellationToken);

        return new MediaItemsStatsResponse(
            totalCount,
            GetStatusCount(statusCounts, MediaTrackingStatus.Planned),
            GetStatusCount(statusCounts, MediaTrackingStatus.InProgress),
            GetStatusCount(statusCounts, MediaTrackingStatus.Completed),
            GetStatusCount(statusCounts, MediaTrackingStatus.OnHold),
            GetStatusCount(statusCounts, MediaTrackingStatus.Dropped),
            startedThisMonthCount,
            completedThisMonthCount,
            backlogWithoutStartCount,
            scoredItems.Count == 0 ? null : Math.Round(scoredItems.Average(), 1),
            scoredItems.Count,
            contentKindBreakdown
                .Select(item => new ContentKindStatResponse(item.ContentKind, item.Count))
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.ContentKind)
                .ToList(),
            sourceBreakdown
                .Select(item => new MediaSourceStatResponse(item.SourceType, item.Count))
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.SourceType)
                .ToList(),
            categoryBreakdown
                .Select(item => new CategoryStatResponse(item.UserCategoryId, item.Name, item.Color, item.Count))
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.CategoryName)
                .ToList(),
            averageScoreByContentKind
                .Select(item => new ContentKindAverageScoreStatResponse(
                    item.ContentKind,
                    Math.Round(item.AveragePersonalScore, 1),
                    item.ScoredItemsCount))
                .OrderByDescending(item => item.AveragePersonalScore)
                .ThenBy(item => item.ContentKind)
                .ToList());
    }

    public async Task<MediaItemResponse?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var item = await dbContext.MediaItems
            .AsNoTracking()
            .Include(mediaItem => mediaItem.MediaItemCategories)
            .ThenInclude(link => link.UserCategory)
            .Include(mediaItem => mediaItem.UserFormat)
            .FirstOrDefaultAsync(mediaItem => mediaItem.Id == id && mediaItem.UserId == userId, cancellationToken);

        return item is null ? null : ToResponse(item);
    }

    public async Task<(byte[] Content, string ContentType, string FileName)> ExportAsync(
        LibraryTransferFormat format,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var categories = await dbContext.UserCategories
            .AsNoTracking()
            .Where(category => category.UserId == userId)
            .OrderBy(category => category.Name)
            .ToListAsync(cancellationToken);

        var items = await dbContext.MediaItems
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .Include(item => item.MediaItemCategories)
            .ThenInclude(link => link.UserCategory)
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.Title)
            .ToListAsync(cancellationToken);

        var envelope = new LibraryTransferEnvelope
        {
            SchemaVersion = LibraryTransferSchemaVersion,
            ExportedAtUtc = DateTime.UtcNow,
            Categories = categories.Select(ToTransferCategory).ToList(),
            Items = items.Select(ToTransferItem).ToList(),
        };

        return format switch
        {
            LibraryTransferFormat.Json => BuildJsonExport(envelope),
            LibraryTransferFormat.Csv => BuildCsvExport(envelope),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
        };
    }

    /// <summary>
    /// Máximo de elementos por importación. El límite de 10 MB del controlador
    /// acota el tamaño del archivo, no el trabajo que genera: importar una
    /// biblioteca real no llega a este número ni de lejos, así que superarlo
    /// apunta a un archivo generado, no a un uso normal.
    /// </summary>
    public const int MaxImportItems = 5_000;

    public async Task<ServiceResult<LibraryImportResponse>> ImportAsync(
        Stream stream,
        LibraryTransferFormat format,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var envelopeResult = await ReadTransferEnvelopeAsync(stream, format, cancellationToken);
        if (!envelopeResult.IsSuccess)
            return ServiceResult<LibraryImportResponse>.Fail(envelopeResult.ErrorField!, envelopeResult.ErrorMessage!);

        // El tope de 10 MB del controlador no acota el trabajo: en 10 MB de JSON
        // caben decenas de miles de elementos, y todos se cargan en memoria junto
        // con la biblioteca entera del usuario antes de un único SaveChanges.
        // El coste real va por número de elementos, no por bytes.
        var itemCount = envelopeResult.Value!.Items.Count;
        if (itemCount > MaxImportItems)
        {
            return ServiceResult<LibraryImportResponse>.Fail(
                "file",
                $"El archivo contiene {itemCount} elementos y el máximo por importación es {MaxImportItems}. " +
                "Divide la biblioteca en varios archivos y vuelve a intentarlo.");
        }

        var categoryBlueprintsResult = NormalizeImportedCategories(envelopeResult.Value!);
        if (!categoryBlueprintsResult.IsSuccess)
            return ServiceResult<LibraryImportResponse>.Fail(categoryBlueprintsResult.ErrorField!, categoryBlueprintsResult.ErrorMessage!);

        var existingCategories = await dbContext.UserCategories
            .Where(category => category.UserId == userId)
            .ToListAsync(cancellationToken);

        var categoryLookup = existingCategories.ToDictionary(category => category.NormalizedName, StringComparer.Ordinal);
        var categoriesCreated = 0;

        foreach (var blueprint in categoryBlueprintsResult.Value!)
        {
            if (categoryLookup.ContainsKey(blueprint.NormalizedName))
                continue;

            var category = new UserCategory
            {
                UserId = userId,
                Name = blueprint.Name,
                NormalizedName = blueprint.NormalizedName,
                Color = blueprint.Color,
                CreatedAtUtc = blueprint.CreatedAtUtc,
            };

            dbContext.UserCategories.Add(category);
            categoryLookup[blueprint.NormalizedName] = category;
            categoriesCreated += 1;
        }

        var existingItems = await dbContext.MediaItems
            .Include(item => item.MediaItemCategories)
            .ThenInclude(link => link.UserCategory)
            .Where(item => item.UserId == userId)
            .ToListAsync(cancellationToken);

        var itemsById = existingItems.ToDictionary(item => item.Id);
        var itemsByExternalKey = existingItems
            .Where(item => item.SourceType != MediaItemSourceType.Manual && item.ExternalId.HasValue && item.ExternalMediaKind.HasValue)
            .ToDictionary(
                item => new ExternalItemKey(item.SourceType, item.ExternalId!.Value, item.ExternalMediaKind!.Value),
                item => item);

        var itemsCreated = 0;
        var itemsUpdated = 0;

        foreach (var rawItem in envelopeResult.Value!.Items)
        {
            var normalizedItemResult = NormalizeImportedItem(rawItem);
            if (!normalizedItemResult.IsSuccess)
                return ServiceResult<LibraryImportResponse>.Fail(normalizedItemResult.ErrorField!, normalizedItemResult.ErrorMessage!);

            var importedItem = normalizedItemResult.Value!;
            var existingItem = FindExistingImportedItem(importedItem, itemsById, itemsByExternalKey);

            if (existingItem is null)
            {
                existingItem = new MediaItem
                {
                    UserId = userId,
                };

                dbContext.MediaItems.Add(existingItem);
                itemsCreated += 1;
            }
            else
            {
                itemsUpdated += 1;
            }

            ApplyImportedItem(existingItem, importedItem, categoryLookup);
            itemsById[existingItem.Id] = existingItem;

            if (importedItem.OriginalItemId.HasValue && importedItem.OriginalItemId.Value != Guid.Empty)
                itemsById[importedItem.OriginalItemId.Value] = existingItem;

            if (existingItem.SourceType != MediaItemSourceType.Manual && existingItem.ExternalId.HasValue && existingItem.ExternalMediaKind.HasValue)
            {
                itemsByExternalKey[new ExternalItemKey(existingItem.SourceType, existingItem.ExternalId.Value, existingItem.ExternalMediaKind.Value)] = existingItem;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<LibraryImportResponse>.Ok(
            new LibraryImportResponse(
                format,
                envelopeResult.Value.Items.Count,
                itemsCreated,
                itemsUpdated,
                categoriesCreated));
    }

    // -------------------------------------------------------------------------
    // Escritura
    // -------------------------------------------------------------------------

    public async Task<ServiceResult<MediaItemResponse>> CreateAsync(
        CreateMediaItemRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var titleResult = NormalizeTitle(request.Title, nameof(request.Title));
        if (titleResult.IsFailure)
            return ServiceResult<MediaItemResponse>.Fail(titleResult.ErrorField!, titleResult.ErrorMessage!);

        var contentKindResult = ResolveContentKind(request.Type, request.ContentKind, nameof(request.ContentKind));
        if (contentKindResult.IsFailure)
            return ServiceResult<MediaItemResponse>.Fail(contentKindResult.ErrorField!, contentKindResult.ErrorMessage!);

        var lifecycleValidation = ValidateLifecycleDates(request.StartedAtUtc, request.CompletedAtUtc);
        if (lifecycleValidation is not null)
            return ServiceResult<MediaItemResponse>.Fail(lifecycleValidation.Value.Field, lifecycleValidation.Value.Message);

        var externalValidation = ValidateExternalSource(request.SourceType, request.ExternalId, request.ExternalMediaKind);
        if (externalValidation is not null)
            return ServiceResult<MediaItemResponse>.Fail(externalValidation.Value.Field, externalValidation.Value.Message);

        var categoriesResult = await ResolveCategoriesAsync(request.CategoryIds, userId, cancellationToken);
        if (!categoriesResult.IsSuccess)
            return ServiceResult<MediaItemResponse>.Fail(categoriesResult.ErrorField!, categoriesResult.ErrorMessage!);

        var progressCurrent = ResolveProgressCurrent(request.ProgressCurrent, request.ProgressCount);
        var progressUnit = ResolveProgressUnit(request.ProgressUnit, request.Type, contentKindResult.Value!.Value);
        var createdAtUtc = DateTime.UtcNow;

        if (request.SourceType != MediaItemSourceType.Manual && request.ExternalId.HasValue && request.ExternalMediaKind.HasValue)
        {
            var isDuplicate = await dbContext.MediaItems
                .AnyAsync(item =>
                    item.UserId == userId &&
                    item.SourceType == request.SourceType &&
                    item.ExternalId == request.ExternalId &&
                    item.ExternalMediaKind == request.ExternalMediaKind, cancellationToken);
            if (isDuplicate)
                return ServiceResult<MediaItemResponse>.Fail(
                    nameof(request.ExternalId),
                    "Ya existe un elemento con este identificador externo en tu biblioteca.");
        }

        var formatResult = await ResolveUserFormatIdAsync(request.UserFormatId, userId, cancellationToken);
        if (!formatResult.IsSuccess)
            return ServiceResult<MediaItemResponse>.Fail(formatResult.ErrorField!, formatResult.ErrorMessage!);
        var userFormatId = formatResult.Value;

        var item = new MediaItem
        {
            UserId = userId,
            Title = titleResult.Value!,
            AlternativeTitle = NormalizeOptionalText(request.AlternativeTitle),
            Description = NormalizeOptionalText(request.Description),
            Type = request.Type,
            ContentKind = contentKindResult.Value.Value,
            Status = request.Status,
            SourceType = request.SourceType,
            ExternalId = request.SourceType != MediaItemSourceType.Manual ? request.ExternalId : null,
            ExternalMediaKind = request.SourceType != MediaItemSourceType.Manual ? request.ExternalMediaKind : null,
            ExternalStatusLabel = request.SourceType != MediaItemSourceType.Manual ? NormalizeOptionalText(request.ExternalStatusLabel) : null,
            ExternalScore = request.SourceType != MediaItemSourceType.Manual ? request.ExternalScore : null,
            CoverImageUrl = NormalizeOptionalText(request.CoverImageUrl),
            ReferenceUrl = NormalizeOptionalText(request.ReferenceUrl),
            ReleaseYear = request.ReleaseYear,
            ProgressCount = progressCurrent,
            ProgressCurrent = progressCurrent,
            ProgressTotal = request.ProgressTotal,
            ProgressUnit = progressUnit,
            CurrentSeason = request.CurrentSeason,
            PersonalScore = request.PersonalScore,
            Notes = NormalizeOptionalText(request.Notes),
            UserFormatId = userFormatId,
            StartedAtUtc = NormalizeOptionalUtc(request.StartedAtUtc),
            CompletedAtUtc = NormalizeOptionalUtc(request.CompletedAtUtc),
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc,
        };

        ReplaceCategoryLinks(item, categoriesResult.Value!);

        dbContext.MediaItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<MediaItemResponse>.Ok(
            await LoadOwnedResponseAsync(item.Id, userId, cancellationToken));
    }

    public async Task<ServiceResult<MediaItemResponse>> UpdateAsync(
        Guid id,
        UpdateMediaItemRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var titleResult = NormalizeTitle(request.Title, nameof(request.Title));
        if (titleResult.IsFailure)
            return ServiceResult<MediaItemResponse>.Fail(titleResult.ErrorField!, titleResult.ErrorMessage!);

        var contentKindResult = ResolveContentKind(request.Type, request.ContentKind, nameof(request.ContentKind));
        if (contentKindResult.IsFailure)
            return ServiceResult<MediaItemResponse>.Fail(contentKindResult.ErrorField!, contentKindResult.ErrorMessage!);

        var lifecycleValidation = ValidateLifecycleDates(request.StartedAtUtc, request.CompletedAtUtc);
        if (lifecycleValidation is not null)
            return ServiceResult<MediaItemResponse>.Fail(lifecycleValidation.Value.Field, lifecycleValidation.Value.Message);

        var externalValidation = ValidateExternalSource(request.SourceType, request.ExternalId, request.ExternalMediaKind);
        if (externalValidation is not null)
            return ServiceResult<MediaItemResponse>.Fail(externalValidation.Value.Field, externalValidation.Value.Message);

        var categoriesResult = await ResolveCategoriesAsync(request.CategoryIds, userId, cancellationToken);
        if (!categoriesResult.IsSuccess)
            return ServiceResult<MediaItemResponse>.Fail(categoriesResult.ErrorField!, categoriesResult.ErrorMessage!);

        var progressCurrent = ResolveProgressCurrent(request.ProgressCurrent, request.ProgressCount);
        var progressUnit = ResolveProgressUnit(request.ProgressUnit, request.Type, contentKindResult.Value!.Value);

        var item = await dbContext.MediaItems
            .Include(mediaItem => mediaItem.MediaItemCategories)
            .FirstOrDefaultAsync(mediaItem => mediaItem.Id == id && mediaItem.UserId == userId, cancellationToken);

        if (item is null)
            return ServiceResult<MediaItemResponse>.Fail("id", "No encontrado.");

        if (request.SourceType != MediaItemSourceType.Manual && request.ExternalId.HasValue && request.ExternalMediaKind.HasValue)
        {
            var isDuplicate = await dbContext.MediaItems
                .AnyAsync(mediaItem =>
                    mediaItem.Id != id &&
                    mediaItem.UserId == userId &&
                    mediaItem.SourceType == request.SourceType &&
                    mediaItem.ExternalId == request.ExternalId &&
                    mediaItem.ExternalMediaKind == request.ExternalMediaKind,
                    cancellationToken);

            if (isDuplicate)
                return ServiceResult<MediaItemResponse>.Fail(
                    nameof(request.ExternalId),
                    "Ya existe un elemento con este identificador externo en tu biblioteca.");
        }

        var formatResult = await ResolveUserFormatIdAsync(request.UserFormatId, userId, cancellationToken);
        if (!formatResult.IsSuccess)
            return ServiceResult<MediaItemResponse>.Fail(formatResult.ErrorField!, formatResult.ErrorMessage!);
        var userFormatId = formatResult.Value;

        item.Title = titleResult.Value!;
        item.AlternativeTitle = NormalizeOptionalText(request.AlternativeTitle);
        item.Description = NormalizeOptionalText(request.Description);
        item.Type = request.Type;
        item.ContentKind = contentKindResult.Value.Value;
        item.Status = request.Status;
        item.SourceType = request.SourceType;
        item.ExternalId = request.SourceType != MediaItemSourceType.Manual ? request.ExternalId : null;
        item.ExternalMediaKind = request.SourceType != MediaItemSourceType.Manual ? request.ExternalMediaKind : null;
        item.ExternalStatusLabel = request.SourceType != MediaItemSourceType.Manual ? NormalizeOptionalText(request.ExternalStatusLabel) : null;
        item.ExternalScore = request.SourceType != MediaItemSourceType.Manual ? request.ExternalScore : null;
        item.CoverImageUrl = NormalizeOptionalText(request.CoverImageUrl);
        item.ReferenceUrl = NormalizeOptionalText(request.ReferenceUrl);
        item.ReleaseYear = request.ReleaseYear;
        item.ProgressCount = progressCurrent;
        item.ProgressCurrent = progressCurrent;
        item.ProgressTotal = request.ProgressTotal;
        item.ProgressUnit = progressUnit;
        item.CurrentSeason = request.CurrentSeason;
        item.PersonalScore = request.PersonalScore;
        item.Notes = NormalizeOptionalText(request.Notes);
        item.UserFormatId = userFormatId;
        ReplaceCategoryLinks(item, categoriesResult.Value!);
        item.StartedAtUtc = NormalizeOptionalUtc(request.StartedAtUtc);
        item.CompletedAtUtc = NormalizeOptionalUtc(request.CompletedAtUtc);
        item.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<MediaItemResponse>.Ok(
            await LoadOwnedResponseAsync(item.Id, userId, cancellationToken));
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var item = await dbContext.MediaItems
            .FirstOrDefaultAsync(mediaItem => mediaItem.Id == id && mediaItem.UserId == userId, cancellationToken);

        if (item is null)
            return false;

        dbContext.MediaItems.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    // -------------------------------------------------------------------------
    // Helpers privados
    // -------------------------------------------------------------------------

    private static NormalizeTitleResult NormalizeTitle(string title, string field)
    {
        var normalized = title.Trim();
        return normalized.Length > 0
            ? NormalizeTitleResult.Ok(normalized)
            : NormalizeTitleResult.Fail(field, "El título no puede estar vacío.");
    }

    private static LibraryTransferCategoryRecord ToTransferCategory(UserCategory category)
        => new()
        {
            Name = category.Name,
            Color = category.Color,
            CreatedAtUtc = category.CreatedAtUtc,
        };

    private static LibraryTransferItemRecord ToTransferItem(MediaItem item)
        => new()
        {
            OriginalItemId = item.Id,
            Title = item.Title,
            AlternativeTitle = item.AlternativeTitle,
            Description = item.Description,
            Type = item.Type,
            ContentKind = item.ContentKind,
            Status = item.Status,
            SourceType = item.SourceType,
            ExternalId = item.ExternalId,
            ExternalMediaKind = item.ExternalMediaKind,
            ExternalStatusLabel = item.ExternalStatusLabel,
            ExternalScore = item.ExternalScore,
            CoverImageUrl = item.CoverImageUrl,
            ReferenceUrl = item.ReferenceUrl,
            ReleaseYear = item.ReleaseYear,
            ProgressUnit = item.ProgressUnit,
            ProgressCount = item.ProgressCount,
            ProgressCurrent = item.ProgressCurrent,
            ProgressTotal = item.ProgressTotal,
            CurrentSeason = item.CurrentSeason,
            PersonalScore = item.PersonalScore,
            Notes = item.Notes,
            Categories = item.MediaItemCategories
                .OrderBy(link => link.UserCategory.Name)
                .Select(link => ToTransferCategory(link.UserCategory))
                .ToList(),
            StartedAtUtc = item.StartedAtUtc,
            CompletedAtUtc = item.CompletedAtUtc,
            CreatedAtUtc = item.CreatedAtUtc,
            UpdatedAtUtc = item.UpdatedAtUtc,
        };

    private static (byte[] Content, string ContentType, string FileName) BuildJsonExport(LibraryTransferEnvelope envelope)
    {
        var payload = JsonSerializer.Serialize(envelope, TransferPrettyJsonOptions);
        return (
            Encoding.UTF8.GetBytes(payload),
            "application/json; charset=utf-8",
            BuildTransferFileName("json"));
    }

    private static (byte[] Content, string ContentType, string FileName) BuildCsvExport(LibraryTransferEnvelope envelope)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(';', LibraryCsvHeaders));

        foreach (var item in envelope.Items)
        {
            var values = new[]
            {
                FormatGuid(item.OriginalItemId),
                item.Title ?? string.Empty,
                item.AlternativeTitle ?? string.Empty,
                item.Description ?? string.Empty,
                item.Type?.ToString() ?? string.Empty,
                item.ContentKind.ToString(),
                item.Status.ToString(),
                item.SourceType.ToString(),
                FormatNullableInt(item.ExternalId),
                item.ExternalMediaKind?.ToString() ?? string.Empty,
                item.ExternalStatusLabel ?? string.Empty,
                FormatNullableDouble(item.ExternalScore),
                item.CoverImageUrl ?? string.Empty,
                item.ReferenceUrl ?? string.Empty,
                FormatNullableInt(item.ReleaseYear),
                item.ProgressUnit.ToString(),
                item.ProgressCount.ToString(CultureInfo.InvariantCulture),
                item.ProgressCurrent.ToString(CultureInfo.InvariantCulture),
                FormatNullableInt(item.ProgressTotal),
                item.CurrentSeason.ToString(CultureInfo.InvariantCulture),
                FormatNullableDouble(item.PersonalScore),
                item.Notes ?? string.Empty,
                JsonSerializer.Serialize(item.Categories, TransferJsonOptions),
                FormatDate(item.StartedAtUtc),
                FormatDate(item.CompletedAtUtc),
                FormatDate(item.CreatedAtUtc),
                FormatDate(item.UpdatedAtUtc),
            };

            builder.AppendLine(string.Join(';', values.Select(EscapeCsvCell)));
        }

        var csvContent = Encoding.UTF8.GetBytes(builder.ToString());
        var preamble = Encoding.UTF8.GetPreamble();
        var fileBytes = new byte[preamble.Length + csvContent.Length];
        Buffer.BlockCopy(preamble, 0, fileBytes, 0, preamble.Length);
        Buffer.BlockCopy(csvContent, 0, fileBytes, preamble.Length, csvContent.Length);

        return (
            fileBytes,
            "text/csv; charset=utf-8",
            BuildTransferFileName("csv"));
    }

    private static string BuildTransferFileName(string extension)
        => $"tracker-library-{DateTime.UtcNow:yyyyMMdd-HHmmss}.{extension}";

    /// <summary>
    /// Caracteres con los que Excel, LibreOffice y Google Sheets interpretan una
    /// celda como fórmula. Un título como <c>=cmd|'/c calc'!A1</c> se ejecuta al
    /// abrir el archivo, así que exportar la biblioteca se convierte en un vector
    /// de ataque contra quien abra el CSV.
    /// </summary>
    private static readonly char[] CsvFormulaTriggers = ['=', '+', '-', '@', '\t', '\r'];

    private static string EscapeCsvCell(string value)
    {
        // Prefijo de apóstrofo: la hoja de cálculo lo trata como marca de texto
        // literal, no lo muestra en la celda y no evalúa el contenido.
        if (value.Length > 0 && Array.IndexOf(CsvFormulaTriggers, value[0]) >= 0)
        {
            value = "'" + value;
        }

        if (value.IndexOfAny([';', '"', '\r', '\n']) < 0)
            return value;

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string FormatGuid(Guid? value)
        => value?.ToString() ?? string.Empty;

    private static string FormatNullableInt(int? value)
        => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static string FormatNullableDouble(double? value)
        => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static string FormatDate(DateTime? value)
        => NormalizeOptionalUtc(value)?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty;

    private static async Task<ServiceResult<LibraryTransferEnvelope>> ReadTransferEnvelopeAsync(
        Stream stream,
        LibraryTransferFormat format,
        CancellationToken cancellationToken)
    {
        try
        {
            return format switch
            {
                LibraryTransferFormat.Json => await ReadJsonTransferEnvelopeAsync(stream, cancellationToken),
                LibraryTransferFormat.Csv => await ReadCsvTransferEnvelopeAsync(stream, cancellationToken),
                _ => ServiceResult<LibraryTransferEnvelope>.Fail("file", "El formato de importación no es compatible."),
            };
        }
        catch (JsonException)
        {
            return ServiceResult<LibraryTransferEnvelope>.Fail("file", "El archivo no tiene un JSON válido para importarse.");
        }
        catch (FormatException exception)
        {
            return ServiceResult<LibraryTransferEnvelope>.Fail("file", exception.Message);
        }
    }

    private static async Task<ServiceResult<LibraryTransferEnvelope>> ReadJsonTransferEnvelopeAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var envelope = await JsonSerializer.DeserializeAsync<LibraryTransferEnvelope>(stream, TransferJsonOptions, cancellationToken);
        if (envelope is null)
            return ServiceResult<LibraryTransferEnvelope>.Fail("file", "El archivo JSON no contiene datos para importar.");

        if (envelope.SchemaVersion != LibraryTransferSchemaVersion)
        {
            return ServiceResult<LibraryTransferEnvelope>.Fail(
                "file",
                $"La versión del archivo ({envelope.SchemaVersion}) no es compatible con esta importación.");
        }

        envelope.Categories ??= [];
        envelope.Items ??= [];

        return ServiceResult<LibraryTransferEnvelope>.Ok(envelope);
    }

    private static async Task<ServiceResult<LibraryTransferEnvelope>> ReadCsvTransferEnvelopeAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, true, leaveOpen: true);
        var content = await reader.ReadToEndAsync(cancellationToken);
        var rows = ParseCsvRows(content)
            .Where(row => row.Any(cell => !string.IsNullOrWhiteSpace(cell)))
            .ToList();

        if (rows.Count == 0)
            return ServiceResult<LibraryTransferEnvelope>.Fail("file", "El CSV está vacío.");

        var headerMap = rows[0]
            .Select((header, index) => new { Header = header.Trim(), Index = index })
            .ToDictionary(item => item.Header, item => item.Index, StringComparer.OrdinalIgnoreCase);

        foreach (var header in LibraryCsvHeaders)
        {
            if (!headerMap.ContainsKey(header))
            {
                return ServiceResult<LibraryTransferEnvelope>.Fail(
                    "file",
                    $"Falta la columna obligatoria '{header}' en el CSV exportado.");
            }
        }

        var items = new List<LibraryTransferItemRecord>();
        for (var rowIndex = 1; rowIndex < rows.Count; rowIndex += 1)
        {
            items.Add(ParseCsvItemRecord(rows[rowIndex], headerMap, rowIndex + 1));
        }

        return ServiceResult<LibraryTransferEnvelope>.Ok(
            new LibraryTransferEnvelope
            {
                SchemaVersion = LibraryTransferSchemaVersion,
                ExportedAtUtc = DateTime.UtcNow,
                Categories = [],
                Items = items,
            });
    }

    private static List<string[]> ParseCsvRows(string content)
    {
        var rows = new List<string[]>();
        var currentRow = new List<string>();
        var currentCell = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < content.Length; index += 1)
        {
            var character = content[index];

            if (inQuotes)
            {
                if (character == '"')
                {
                    if (index + 1 < content.Length && content[index + 1] == '"')
                    {
                        currentCell.Append('"');
                        index += 1;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    currentCell.Append(character);
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    inQuotes = true;
                    break;

                case ';':
                    currentRow.Add(currentCell.ToString());
                    currentCell.Clear();
                    break;

                case '\r':
                    if (index + 1 < content.Length && content[index + 1] == '\n')
                        index += 1;

                    currentRow.Add(currentCell.ToString());
                    currentCell.Clear();
                    rows.Add(currentRow.ToArray());
                    currentRow = [];
                    break;

                case '\n':
                    currentRow.Add(currentCell.ToString());
                    currentCell.Clear();
                    rows.Add(currentRow.ToArray());
                    currentRow = [];
                    break;

                default:
                    currentCell.Append(character);
                    break;
            }
        }

        if (inQuotes)
            throw new FormatException("El CSV no es válido: falta cerrar unas comillas.");

        if (currentCell.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(currentCell.ToString());
            rows.Add(currentRow.ToArray());
        }

        if (rows.Count > 0 && rows[0].Length > 0)
            rows[0][0] = rows[0][0].TrimStart('\uFEFF');

        return rows;
    }

    private static LibraryTransferItemRecord ParseCsvItemRecord(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> headerMap,
        int rowNumber)
    {
        string GetValue(string header)
            => headerMap.TryGetValue(header, out var index) && index < row.Count ? row[index] : string.Empty;

        List<LibraryTransferCategoryRecord> ParseCategories()
        {
            var rawValue = GetValue("CategoriesJson");
            if (string.IsNullOrWhiteSpace(rawValue))
                return [];

            try
            {
                return JsonSerializer.Deserialize<List<LibraryTransferCategoryRecord>>(rawValue, TransferJsonOptions) ?? [];
            }
            catch (JsonException)
            {
                throw new FormatException($"La columna CategoriesJson en la fila {rowNumber} no contiene un JSON válido.");
            }
        }

        return new LibraryTransferItemRecord
        {
            OriginalItemId = ParseGuid(GetValue("OriginalItemId"), "OriginalItemId", rowNumber),
            Title = GetValue("Title"),
            AlternativeTitle = NullIfWhiteSpace(GetValue("AlternativeTitle")),
            Description = NullIfWhiteSpace(GetValue("Description")),
            Type = ParseEnum<MediaType>(GetValue("Type"), "Type", rowNumber),
            ContentKind = ParseRequiredEnum<ContentKind>(GetValue("ContentKind"), "ContentKind", rowNumber),
            Status = ParseRequiredEnum<MediaTrackingStatus>(GetValue("Status"), "Status", rowNumber),
            SourceType = ParseRequiredEnum<MediaItemSourceType>(GetValue("SourceType"), "SourceType", rowNumber),
            ExternalId = ParseInt(GetValue("ExternalId"), "ExternalId", rowNumber),
            ExternalMediaKind = ParseEnum<ExternalMediaKind>(GetValue("ExternalMediaKind"), "ExternalMediaKind", rowNumber),
            ExternalStatusLabel = NullIfWhiteSpace(GetValue("ExternalStatusLabel")),
            ExternalScore = ParseDouble(GetValue("ExternalScore"), "ExternalScore", rowNumber),
            CoverImageUrl = NullIfWhiteSpace(GetValue("CoverImageUrl")),
            ReferenceUrl = NullIfWhiteSpace(GetValue("ReferenceUrl")),
            ReleaseYear = ParseInt(GetValue("ReleaseYear"), "ReleaseYear", rowNumber),
            ProgressUnit = ParseRequiredEnum<ProgressUnit>(GetValue("ProgressUnit"), "ProgressUnit", rowNumber),
            ProgressCount = ParseRequiredInt(GetValue("ProgressCount"), "ProgressCount", rowNumber),
            ProgressCurrent = ParseRequiredInt(GetValue("ProgressCurrent"), "ProgressCurrent", rowNumber),
            ProgressTotal = ParseInt(GetValue("ProgressTotal"), "ProgressTotal", rowNumber),
            CurrentSeason = ParseRequiredInt(GetValue("CurrentSeason"), "CurrentSeason", rowNumber),
            PersonalScore = ParseDouble(GetValue("PersonalScore"), "PersonalScore", rowNumber),
            Notes = NullIfWhiteSpace(GetValue("Notes")),
            Categories = ParseCategories(),
            StartedAtUtc = ParseDate(GetValue("StartedAtUtc"), "StartedAtUtc", rowNumber),
            CompletedAtUtc = ParseDate(GetValue("CompletedAtUtc"), "CompletedAtUtc", rowNumber),
            CreatedAtUtc = ParseDate(GetValue("CreatedAtUtc"), "CreatedAtUtc", rowNumber),
            UpdatedAtUtc = ParseDate(GetValue("UpdatedAtUtc"), "UpdatedAtUtc", rowNumber),
        };
    }

    private static Guid? ParseGuid(string value, string columnName, int rowNumber)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return Guid.TryParse(value, out var parsed)
            ? parsed
            : throw new FormatException($"La columna {columnName} en la fila {rowNumber} no contiene un GUID válido.");
    }

    private static T? ParseEnum<T>(string value, string columnName, int rowNumber)
        where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return Enum.TryParse<T>(value, ignoreCase: true, out var parsed)
            ? parsed
            : throw new FormatException($"La columna {columnName} en la fila {rowNumber} no contiene un valor válido.");
    }

    private static T ParseRequiredEnum<T>(string value, string columnName, int rowNumber)
        where T : struct, Enum
        => ParseEnum<T>(value, columnName, rowNumber)
            ?? throw new FormatException($"La columna {columnName} en la fila {rowNumber} es obligatoria.");

    private static int? ParseInt(string value, string columnName, int rowNumber)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"La columna {columnName} en la fila {rowNumber} no contiene un número válido.");
    }

    private static int ParseRequiredInt(string value, string columnName, int rowNumber)
        => ParseInt(value, columnName, rowNumber)
            ?? throw new FormatException($"La columna {columnName} en la fila {rowNumber} es obligatoria.");

    private static double? ParseDouble(string value, string columnName, int rowNumber)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"La columna {columnName} en la fila {rowNumber} no contiene un decimal válido.");
    }

    private static DateTime? ParseDate(string value, string columnName, int rowNumber)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind | DateTimeStyles.AllowWhiteSpaces,
            out var parsed)
            ? parsed
            : throw new FormatException($"La columna {columnName} en la fila {rowNumber} no contiene una fecha válida.");
    }

    private static string? NullIfWhiteSpace(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static ServiceResult<List<ImportedLibraryCategory>> NormalizeImportedCategories(LibraryTransferEnvelope envelope)
    {
        var categoriesByKey = new Dictionary<string, ImportedLibraryCategory>(StringComparer.Ordinal);

        foreach (var category in envelope.Categories.Concat(envelope.Items.SelectMany(item => item.Categories)))
        {
            var categoryResult = NormalizeImportedCategory(category);
            if (!categoryResult.IsSuccess)
                return ServiceResult<List<ImportedLibraryCategory>>.Fail(categoryResult.ErrorField!, categoryResult.ErrorMessage!);

            var normalizedCategory = categoryResult.Value!;
            if (categoriesByKey.TryGetValue(normalizedCategory.NormalizedName, out var existingCategory))
            {
                categoriesByKey[normalizedCategory.NormalizedName] = existingCategory with
                {
                    Color = existingCategory.Color ?? normalizedCategory.Color,
                    CreatedAtUtc = existingCategory.CreatedAtUtc <= normalizedCategory.CreatedAtUtc
                        ? existingCategory.CreatedAtUtc
                        : normalizedCategory.CreatedAtUtc,
                };

                continue;
            }

            categoriesByKey[normalizedCategory.NormalizedName] = normalizedCategory;
        }

        return ServiceResult<List<ImportedLibraryCategory>>.Ok(
            categoriesByKey.Values
                .OrderBy(category => category.Name)
                .ToList());
    }

    private static ServiceResult<ImportedLibraryCategory> NormalizeImportedCategory(LibraryTransferCategoryRecord record)
    {
        var rawName = record.Name ?? string.Empty;
        var trimmedName = rawName.Trim();
        if (trimmedName.Length == 0)
            return ServiceResult<ImportedLibraryCategory>.Fail("file", "El archivo contiene una categoría sin nombre.");

        var normalizedColor = NormalizeImportedCategoryColor(record.Color);
        if (normalizedColor is null && !string.IsNullOrWhiteSpace(record.Color))
        {
            return ServiceResult<ImportedLibraryCategory>.Fail(
                "file",
                $"La categoría '{trimmedName}' tiene un color inválido. Debe usar formato #RRGGBB.");
        }

        return ServiceResult<ImportedLibraryCategory>.Ok(
            new ImportedLibraryCategory(
                trimmedName,
                NormalizeCategoryNameKey(trimmedName),
                normalizedColor,
                NormalizeOptionalUtc(record.CreatedAtUtc) ?? DateTime.UtcNow));
    }

    private static string? NormalizeImportedCategoryColor(string? color)
    {
        var normalized = color?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        normalized = normalized.ToUpperInvariant();
        return CategoryColorPattern.IsMatch(normalized) ? normalized : null;
    }

    private static string NormalizeCategoryNameKey(string name)
        => name.Trim().ToUpperInvariant();

    private static ServiceResult<ImportedLibraryItem> NormalizeImportedItem(LibraryTransferItemRecord record)
    {
        var titleResult = NormalizeTitle(record.Title ?? string.Empty, "file");
        if (titleResult.IsFailure)
            return ServiceResult<ImportedLibraryItem>.Fail("file", "El archivo contiene un elemento sin título válido.");

        var contentKindResult = ResolveContentKind(record.Type, record.ContentKind, "file");
        if (contentKindResult.IsFailure)
            return ServiceResult<ImportedLibraryItem>.Fail("file", contentKindResult.ErrorMessage!);

        var lifecycleValidation = ValidateLifecycleDates(record.StartedAtUtc, record.CompletedAtUtc);
        if (lifecycleValidation is not null)
            return ServiceResult<ImportedLibraryItem>.Fail("file", lifecycleValidation.Value.Message);

        var externalValidation = ValidateExternalSource(record.SourceType, record.ExternalId, record.ExternalMediaKind);
        if (externalValidation is not null)
            return ServiceResult<ImportedLibraryItem>.Fail("file", externalValidation.Value.Message);

        if (record.ProgressCount < 0 || record.ProgressCurrent < 0)
            return ServiceResult<ImportedLibraryItem>.Fail("file", "El progreso importado no puede ser negativo.");

        if (record.ProgressTotal.HasValue && record.ProgressTotal.Value <= 0)
            return ServiceResult<ImportedLibraryItem>.Fail("file", "El progreso total importado debe ser mayor que cero.");

        var progressUnit = ResolveProgressUnit(record.ProgressUnit, record.Type, contentKindResult.Value!.Value);

        return ServiceResult<ImportedLibraryItem>.Ok(
            new ImportedLibraryItem(
                record.OriginalItemId,
                titleResult.Value!,
                NormalizeOptionalText(record.AlternativeTitle),
                NormalizeOptionalText(record.Description),
                record.Type,
                contentKindResult.Value.Value,
                record.Status,
                record.SourceType,
                record.SourceType != MediaItemSourceType.Manual ? record.ExternalId : null,
                record.SourceType != MediaItemSourceType.Manual ? record.ExternalMediaKind : null,
                record.SourceType != MediaItemSourceType.Manual ? NormalizeOptionalText(record.ExternalStatusLabel) : null,
                record.SourceType != MediaItemSourceType.Manual ? record.ExternalScore : null,
                NormalizeOptionalText(record.CoverImageUrl),
                NormalizeOptionalText(record.ReferenceUrl),
                record.ReleaseYear,
                progressUnit,
                record.ProgressCount,
                record.ProgressCurrent,
                record.ProgressTotal,
                record.CurrentSeason <= 0 ? 1 : record.CurrentSeason,
                record.PersonalScore,
                NormalizeOptionalText(record.Notes),
                record.Categories
                    .Select(category => NormalizeCategoryNameKey(category.Name ?? string.Empty))
                    .Where(categoryKey => categoryKey.Length > 0)
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
                NormalizeOptionalUtc(record.StartedAtUtc),
                NormalizeOptionalUtc(record.CompletedAtUtc),
                NormalizeOptionalUtc(record.CreatedAtUtc) ?? DateTime.UtcNow,
                NormalizeOptionalUtc(record.UpdatedAtUtc)
                    ?? NormalizeOptionalUtc(record.CreatedAtUtc)
                    ?? DateTime.UtcNow));
    }

    private static MediaItem? FindExistingImportedItem(
        ImportedLibraryItem importedItem,
        IReadOnlyDictionary<Guid, MediaItem> itemsById,
        IReadOnlyDictionary<ExternalItemKey, MediaItem> itemsByExternalKey)
    {
        if (importedItem.OriginalItemId.HasValue && importedItem.OriginalItemId.Value != Guid.Empty)
        {
            if (itemsById.TryGetValue(importedItem.OriginalItemId.Value, out var itemById))
                return itemById;
        }

        if (importedItem.SourceType != MediaItemSourceType.Manual && importedItem.ExternalId.HasValue && importedItem.ExternalMediaKind.HasValue)
        {
            var externalKey = new ExternalItemKey(importedItem.SourceType, importedItem.ExternalId.Value, importedItem.ExternalMediaKind.Value);
            if (itemsByExternalKey.TryGetValue(externalKey, out var itemByExternalKey))
                return itemByExternalKey;
        }

        return null;
    }

    private void ApplyImportedItem(
        MediaItem target,
        ImportedLibraryItem importedItem,
        IReadOnlyDictionary<string, UserCategory> categoryLookup)
    {
        var categories = importedItem.CategoryKeys
            .Where(categoryLookup.ContainsKey)
            .Select(categoryKey => categoryLookup[categoryKey])
            .DistinctBy(category => category.Id)
            .ToList();

        target.Title = importedItem.Title;
        target.AlternativeTitle = importedItem.AlternativeTitle;
        target.Description = importedItem.Description;
        target.Type = importedItem.Type;
        target.ContentKind = importedItem.ContentKind;
        target.Status = importedItem.Status;
        target.SourceType = importedItem.SourceType;
        target.ExternalId = importedItem.ExternalId;
        target.ExternalMediaKind = importedItem.ExternalMediaKind;
        target.ExternalStatusLabel = importedItem.ExternalStatusLabel;
        target.ExternalScore = importedItem.ExternalScore;
        target.CoverImageUrl = importedItem.CoverImageUrl;
        target.ReferenceUrl = importedItem.ReferenceUrl;
        target.ReleaseYear = importedItem.ReleaseYear;
        target.ProgressUnit = importedItem.ProgressUnit;
        target.ProgressCount = importedItem.ProgressCount;
        target.ProgressCurrent = importedItem.ProgressCurrent;
        target.ProgressTotal = importedItem.ProgressTotal;
        target.CurrentSeason = importedItem.CurrentSeason;
        target.PersonalScore = importedItem.PersonalScore;
        target.Notes = importedItem.Notes;
        target.StartedAtUtc = importedItem.StartedAtUtc;
        target.CompletedAtUtc = importedItem.CompletedAtUtc;
        target.CreatedAtUtc = importedItem.CreatedAtUtc;
        target.UpdatedAtUtc = importedItem.UpdatedAtUtc;

        ReplaceCategoryLinks(target, categories);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private async Task<ServiceResult<List<UserCategory>>> ResolveCategoriesAsync(
        List<Guid>? categoryIds,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (categoryIds is null || categoryIds.Count == 0)
            return ServiceResult<List<UserCategory>>.Ok([]);

        var distinctIds = categoryIds
            .Where(categoryId => categoryId != Guid.Empty)
            .Distinct()
            .ToArray();

        if (distinctIds.Length == 0)
            return ServiceResult<List<UserCategory>>.Ok([]);

        var categories = await dbContext.UserCategories
            .Where(category => category.UserId == userId && distinctIds.Contains(category.Id))
            .ToListAsync(cancellationToken);

        return categories.Count == distinctIds.Length
            ? ServiceResult<List<UserCategory>>.Ok(categories)
            : ServiceResult<List<UserCategory>>.Fail(
                nameof(CreateMediaItemRequest.CategoryIds),
                "Todas las categorías deben existir y pertenecer al usuario actual.");
    }

    private void ReplaceCategoryLinks(MediaItem item, IReadOnlyCollection<UserCategory> categories)
    {
        if (item.MediaItemCategories.Count > 0)
        {
            dbContext.MediaItemCategories.RemoveRange(item.MediaItemCategories);
            item.MediaItemCategories.Clear();
        }

        foreach (var category in categories)
        {
            item.MediaItemCategories.Add(new MediaItemCategory
            {
                MediaItemId = item.Id,
                UserCategoryId = category.Id,
                UserCategory = category,
            });
        }
    }

    private async Task<MediaItemResponse> LoadOwnedResponseAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var item = await dbContext.MediaItems
            .AsNoTracking()
            .Include(mediaItem => mediaItem.MediaItemCategories)
            .ThenInclude(link => link.UserCategory)
            .Include(mediaItem => mediaItem.UserFormat)
            .FirstAsync(mediaItem => mediaItem.Id == id && mediaItem.UserId == userId, cancellationToken);

        return ToResponse(item);
    }

    private async Task<ServiceResult<Guid?>> ResolveUserFormatIdAsync(Guid? requestedId, Guid userId, CancellationToken cancellationToken)
    {
        if (!requestedId.HasValue || requestedId.Value == Guid.Empty)
            return ServiceResult<Guid?>.Ok(null);

        var exists = await dbContext.UserFormats
            .AnyAsync(f => f.Id == requestedId.Value && f.UserId == userId, cancellationToken);

        // Un formato ajeno o inexistente es un error de validación, no un "sin formato":
        // devolverlo en silencio guardaba el elemento incompleto mostrando éxito.
        return exists
            ? ServiceResult<Guid?>.Ok(requestedId.Value)
            : ServiceResult<Guid?>.Fail(
                nameof(CreateMediaItemRequest.UserFormatId),
                "El formato debe existir y pertenecer al usuario actual.");
    }

    private static DateTime? NormalizeOptionalUtc(DateTime? value)
    {
        if (!value.HasValue)
            return null;

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
            _ => value.Value.ToUniversalTime(),
        };
    }

    private static ResolveDomainResult ResolveContentKind(MediaType? legacyType, ContentKind? contentKind, string field)
    {
        if (contentKind.HasValue)
        {
            if (legacyType.HasValue && MapLegacyTypeToContentKind(legacyType.Value) != contentKind.Value)
            {
                return ResolveDomainResult.Fail(
                    field,
                    "ContentKind no coincide con el MediaType legado enviado.");
            }

            return ResolveDomainResult.Ok(contentKind.Value);
        }

        if (legacyType.HasValue)
            return ResolveDomainResult.Ok(MapLegacyTypeToContentKind(legacyType.Value));

        return ResolveDomainResult.Fail(field, "Debes indicar el tipo de contenido.");
    }

    private static int ResolveProgressCurrent(int? progressCurrent, int progressCount)
        => progressCurrent ?? progressCount;

    private static ProgressUnit ResolveProgressUnit(
        ProgressUnit? progressUnit,
        MediaType? legacyType,
        ContentKind contentKind)
    {
        if (progressUnit.HasValue)
            return progressUnit.Value;

        if (legacyType.HasValue)
        {
            return legacyType.Value switch
            {
                MediaType.Anime or MediaType.Donghua => ProgressUnit.Episodes,
                MediaType.Manga or MediaType.Manhua or MediaType.Manhwa => ProgressUnit.Chapters,
                _ => InferProgressUnit(contentKind),
            };
        }

        return InferProgressUnit(contentKind);
    }

    private static ProgressUnit InferProgressUnit(ContentKind contentKind)
        => contentKind switch
        {
            ContentKind.Series => ProgressUnit.Episodes,
            ContentKind.Movie => ProgressUnit.None,
            ContentKind.Book => ProgressUnit.Pages,
            ContentKind.Comic => ProgressUnit.Chapters,
            ContentKind.Game => ProgressUnit.Hours,
            ContentKind.Podcast => ProgressUnit.Episodes,
            ContentKind.Video => ProgressUnit.Items,
            ContentKind.Album => ProgressUnit.Tracks,
            _ => ProgressUnit.Items,
        };

    private static ContentKind MapLegacyTypeToContentKind(MediaType legacyType)
        => legacyType switch
        {
            MediaType.Anime or MediaType.Donghua => ContentKind.Series,
            MediaType.Manga or MediaType.Manhua or MediaType.Manhwa => ContentKind.Comic,
            _ => ContentKind.Other,
        };

    private static (string Field, string Message)? ValidateLifecycleDates(DateTime? startedAtUtc, DateTime? completedAtUtc)
    {
        if (!startedAtUtc.HasValue || !completedAtUtc.HasValue)
            return null;

        var normalizedStartedAtUtc = NormalizeOptionalUtc(startedAtUtc)!.Value;
        var normalizedCompletedAtUtc = NormalizeOptionalUtc(completedAtUtc)!.Value;

        return normalizedCompletedAtUtc < normalizedStartedAtUtc
            ? (nameof(CreateMediaItemRequest.CompletedAtUtc), "CompletedAtUtc no puede ser anterior a StartedAtUtc.")
            : null;
    }

    private static (string Field, string Message)? ValidateExternalSource(
        MediaItemSourceType sourceType,
        int? externalId,
        ExternalMediaKind? externalMediaKind)
    {
        // La comprobación va contra Manual, no contra Jikan. Cuando estaba escrita
        // como `!= Jikan` cubría un único proveedor y AniList y MangaDex pasaban sin
        // identificador: además de guardar el elemento incompleto, se libraban del
        // índice único que evita duplicados, porque ese índice es parcial y solo
        // aplica cuando hay identificador. Cualquier proveedor que se añada al enum
        // queda cubierto desde el primer día sin tocar esta función.
        if (sourceType == MediaItemSourceType.Manual)
            return null;

        if (!externalId.HasValue)
            return (nameof(CreateMediaItemRequest.ExternalId),
                $"Falta el identificador externo, obligatorio para los elementos de {sourceType}.");

        if (!externalMediaKind.HasValue)
            return (nameof(CreateMediaItemRequest.ExternalMediaKind),
                $"Falta el tipo de medio externo, obligatorio para los elementos de {sourceType}.");

        return null;
    }

    private static IQueryable<MediaItem> ApplySorting(
        IQueryable<MediaItem> query,
        MediaItemsSortField sortBy,
        SortDirection sortDirection)
    {
        return (sortBy, sortDirection) switch
        {
            (MediaItemsSortField.PersonalScore, SortDirection.Asc) => query
                .OrderBy(item => item.PersonalScore == null)
                .ThenBy(item => item.PersonalScore)
                .ThenBy(item => item.Title),

            (MediaItemsSortField.PersonalScore, SortDirection.Desc) => query
                .OrderBy(item => item.PersonalScore == null)
                .ThenByDescending(item => item.PersonalScore)
                .ThenBy(item => item.Title),

            (MediaItemsSortField.ReleaseYear, SortDirection.Asc) => query
                .OrderBy(item => item.ReleaseYear == null)
                .ThenBy(item => item.ReleaseYear)
                .ThenBy(item => item.Title),

            (MediaItemsSortField.ReleaseYear, SortDirection.Desc) => query
                .OrderBy(item => item.ReleaseYear == null)
                .ThenByDescending(item => item.ReleaseYear)
                .ThenBy(item => item.Title),

            (MediaItemsSortField.Title, SortDirection.Asc) => query
                .OrderBy(item => item.Title)
                .ThenByDescending(item => item.CreatedAtUtc),

            (MediaItemsSortField.Title, SortDirection.Desc) => query
                .OrderByDescending(item => item.Title)
                .ThenByDescending(item => item.CreatedAtUtc),

            (MediaItemsSortField.CreatedAt, SortDirection.Asc) => query
                .OrderBy(item => item.CreatedAtUtc)
                .ThenBy(item => item.Title),

            _ => query
                .OrderByDescending(item => item.CreatedAtUtc)
                .ThenBy(item => item.Title),
        };
    }

    private static int GetStatusCount(
        IReadOnlyDictionary<MediaTrackingStatus, int> statusCounts,
        MediaTrackingStatus targetStatus)
        => statusCounts.TryGetValue(targetStatus, out var count) ? count : 0;

    private static MediaItemResponse ToResponse(MediaItem item) => new(
        item.Id,
        item.Title,
        item.AlternativeTitle,
        item.Type,
        item.Description,
        item.ContentKind,
        item.Status,
        item.SourceType,
        item.ExternalId,
        item.ExternalMediaKind,
        item.ExternalStatusLabel,
        item.ExternalScore,
        item.CoverImageUrl,
        item.ReferenceUrl,
        item.ReleaseYear,
        item.ProgressUnit,
        item.ProgressCount,
        item.ProgressCurrent,
        item.ProgressTotal,
        item.CurrentSeason,
        item.PersonalScore,
        item.Notes,
        item.MediaItemCategories
            .OrderBy(link => link.UserCategory.Name)
            .Select(link => new MediaItemCategorySummaryResponse(
                link.UserCategoryId,
                link.UserCategory.Name,
                link.UserCategory.Color))
            .ToList(),
        item.StartedAtUtc,
        item.CompletedAtUtc,
        item.CreatedAtUtc,
        item.UpdatedAtUtc,
        item.UserFormatId,
        item.UserFormat?.Name);

    // Tipo interno para el resultado de normalización de título
    private readonly record struct NormalizeTitleResult(string? Value, string? ErrorField, string? ErrorMessage)
    {
        public bool IsFailure => ErrorMessage is not null;
        public static NormalizeTitleResult Ok(string value) => new(value, null, null);
        public static NormalizeTitleResult Fail(string field, string message) => new(null, field, message);
    }

    private readonly record struct ResolveDomainResult(ContentKind? Value, string? ErrorField, string? ErrorMessage)
    {
        public bool IsFailure => ErrorMessage is not null;
        public static ResolveDomainResult Ok(ContentKind value) => new(value, null, null);
        public static ResolveDomainResult Fail(string field, string message) => new(null, field, message);
    }

    private readonly record struct ExternalItemKey(MediaItemSourceType SourceType, int ExternalId, ExternalMediaKind ExternalMediaKind);

    private readonly record struct ImportedLibraryCategory(
        string Name,
        string NormalizedName,
        string? Color,
        DateTime CreatedAtUtc);

    private readonly record struct ImportedLibraryItem(
        Guid? OriginalItemId,
        string Title,
        string? AlternativeTitle,
        string? Description,
        MediaType? Type,
        ContentKind ContentKind,
        MediaTrackingStatus Status,
        MediaItemSourceType SourceType,
        int? ExternalId,
        ExternalMediaKind? ExternalMediaKind,
        string? ExternalStatusLabel,
        double? ExternalScore,
        string? CoverImageUrl,
        string? ReferenceUrl,
        int? ReleaseYear,
        ProgressUnit ProgressUnit,
        int ProgressCount,
        int ProgressCurrent,
        int? ProgressTotal,
        int CurrentSeason,
        double? PersonalScore,
        string? Notes,
        IReadOnlyCollection<string> CategoryKeys,
        DateTime? StartedAtUtc,
        DateTime? CompletedAtUtc,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc);

    private sealed class LibraryTransferEnvelope
    {
        public int SchemaVersion { get; set; } = LibraryTransferSchemaVersion;
        public DateTime ExportedAtUtc { get; set; }
        public List<LibraryTransferCategoryRecord> Categories { get; set; } = [];
        public List<LibraryTransferItemRecord> Items { get; set; } = [];
    }

    private sealed class LibraryTransferCategoryRecord
    {
        public string? Name { get; set; }
        public string? Color { get; set; }
        public DateTime? CreatedAtUtc { get; set; }
    }

    private sealed class LibraryTransferItemRecord
    {
        public Guid? OriginalItemId { get; set; }
        public string? Title { get; set; }
        public string? AlternativeTitle { get; set; }
        public string? Description { get; set; }
        public MediaType? Type { get; set; }
        public ContentKind ContentKind { get; set; }
        public MediaTrackingStatus Status { get; set; }
        public MediaItemSourceType SourceType { get; set; }
        public int? ExternalId { get; set; }
        public ExternalMediaKind? ExternalMediaKind { get; set; }
        public string? ExternalStatusLabel { get; set; }
        public double? ExternalScore { get; set; }
        public string? CoverImageUrl { get; set; }
        public string? ReferenceUrl { get; set; }
        public int? ReleaseYear { get; set; }
        public ProgressUnit ProgressUnit { get; set; }
        public int ProgressCount { get; set; }
        public int ProgressCurrent { get; set; }
        public int? ProgressTotal { get; set; }
        public int CurrentSeason { get; set; } = 1;
        public double? PersonalScore { get; set; }
        public string? Notes { get; set; }
        public List<LibraryTransferCategoryRecord> Categories { get; set; } = [];
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public DateTime? CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
    }

}
