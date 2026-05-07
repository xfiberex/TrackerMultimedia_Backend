namespace TrackerMultimedia.Contracts.MediaItems;

public sealed record LibraryImportResponse(
    LibraryTransferFormat Format,
    int ItemsProcessed,
    int ItemsCreated,
    int ItemsUpdated,
    int CategoriesCreated);