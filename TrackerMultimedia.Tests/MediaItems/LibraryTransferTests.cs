using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TrackerMultimedia.Contracts.Categories;
using TrackerMultimedia.Contracts.MediaItems;
using TrackerMultimedia.Domain.Enums;
using TrackerMultimedia.Services;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.MediaItems;

public class LibraryTransferTests(AppFactory factory) : IClassFixture<AppFactory>
{
    [Fact]
    public async Task ExportJson_ReturnsOnlyCurrentUsersLibraryAndCategories()
    {
        var (clientA, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        var (clientB, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);

        var backlog = await CategoryTestHelpers.CreateCategoryAsync(clientA, request =>
        {
            request.Name = "Backlog";
            request.Color = "#336699";
        });

        await CategoryTestHelpers.CreateCategoryAsync(clientA, request => request.Name = "Sin usar");

        await MediaItemTestHelpers.CreateMediaItemAsync(clientA, request =>
        {
            request.Title = "Owned title";
            request.Type = MediaType.Anime;
            request.CategoryIds = [backlog.Id];
        });

        await MediaItemTestHelpers.CreateMediaItemAsync(clientB, request => request.Title = "Other user title");

        var export = await ExportLibraryAsync(clientA, LibraryTransferFormat.Json);

        Assert.StartsWith("application/json", export.ContentType, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(".json", export.FileName, StringComparison.OrdinalIgnoreCase);

        using var document = JsonDocument.Parse(export.Content);
        var categoryNames = document.RootElement
            .GetProperty("categories")
            .EnumerateArray()
            .Select(category => category.GetProperty("name").GetString())
            .ToList();

        var titles = document.RootElement
            .GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("title").GetString())
            .ToList();

        Assert.Contains("Backlog", categoryNames);
        Assert.Contains("Sin usar", categoryNames);
        Assert.Contains("Owned title", titles);
        Assert.DoesNotContain("Other user title", titles);
    }

    [Fact]
    public async Task ImportJson_RestoresItemsAndUnusedCategoriesFromProjectExport()
    {
        var (sourceClient, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        var (targetClient, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);

        var backlog = await CategoryTestHelpers.CreateCategoryAsync(sourceClient, request =>
        {
            request.Name = "Backlog";
            request.Color = "#224466";
        });

        await CategoryTestHelpers.CreateCategoryAsync(sourceClient, request =>
        {
            request.Name = "Wishlist";
            request.Color = "#AA5500";
        });

        await MediaItemTestHelpers.CreateMediaItemAsync(sourceClient, request =>
        {
            request.Title = "Import me";
            request.Type = MediaType.Manga;
            request.ContentKind = ContentKind.Comic;
            request.Status = MediaTrackingStatus.Completed;
            request.CategoryIds = [backlog.Id];
            request.Notes = "Backup ready";
        });

        var export = await ExportLibraryAsync(sourceClient, LibraryTransferFormat.Json);
        var importResult = await ImportLibraryAsync(targetClient, LibraryTransferFormat.Json, export.Content, export.FileName);

        Assert.Equal(LibraryTransferFormat.Json, importResult.Format);
        Assert.Equal(1, importResult.ItemsProcessed);
        Assert.Equal(1, importResult.ItemsCreated);
        Assert.Equal(0, importResult.ItemsUpdated);
        Assert.Equal(2, importResult.CategoriesCreated);

        var importedItems = await MediaItemTestHelpers.GetMediaItemsAsync(targetClient);
        var importedCategories = await targetClient.GetFromJsonAsync<List<CategoryResponse>>("/api/categories");
        var importedItem = Assert.Single(importedItems.Items);

        Assert.Equal("Import me", importedItem.Title);
        Assert.Single(importedItem.Categories ?? []);
        Assert.Equal("Backlog", importedItem.Categories!.Single().Name);
        Assert.NotNull(importedCategories);
        Assert.Contains(importedCategories!, category => category.Name == "Wishlist");
    }

    [Fact]
    public async Task ImportCsv_RoundTripsQuotedFieldsAndAssignedCategories()
    {
        var (sourceClient, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        var (targetClient, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);

        var backlog = await CategoryTestHelpers.CreateCategoryAsync(sourceClient, request =>
        {
            request.Name = "Backlog";
            request.Color = "#884422";
        });

        await MediaItemTestHelpers.CreateMediaItemAsync(sourceClient, request =>
        {
            request.Title = "Plan; semanal";
            request.Type = null;
            request.ContentKind = ContentKind.Game;
            request.Status = MediaTrackingStatus.InProgress;
            request.SourceType = MediaItemSourceType.Manual;
            request.ProgressUnit = ProgressUnit.Hours;
            request.ProgressCurrent = 12;
            request.ProgressCount = 12;
            request.PersonalScore = 8.5;
            request.Notes = "Linea 1;\nLinea 2";
            request.CategoryIds = [backlog.Id];
        });

        var export = await ExportLibraryAsync(sourceClient, LibraryTransferFormat.Csv);
        var importResult = await ImportLibraryAsync(targetClient, LibraryTransferFormat.Csv, export.Content, export.FileName);

        Assert.StartsWith("text/csv", export.ContentType, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(".csv", export.FileName, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, importResult.ItemsCreated);

        var importedItems = await MediaItemTestHelpers.GetMediaItemsAsync(targetClient);
        var importedItem = Assert.Single(importedItems.Items);

        Assert.Equal("Plan; semanal", importedItem.Title);
        Assert.Equal("Linea 1;\nLinea 2", importedItem.Notes);
        Assert.Single(importedItem.Categories ?? []);
        Assert.Equal("Backlog", importedItem.Categories!.Single().Name);
    }

    [Fact]
    public async Task ExportCsv_NeutralisesCellsThatSpreadsheetsWouldEvaluate()
    {
        // Un solo test en lugar de un [Theory] con cuatro casos: la conexión
        // SQLite que comparte AppFactory no es segura entre hilos, y xUnit
        // ejecutaba los casos en paralelo hasta corromper su estado interno.
        string[] titulosPeligrosos =
        [
            "=1+1",
            "+cmd|'/c calc'!A1",
            "-2+3",
            "@SUM(A1:A9)",
        ];

        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);

        foreach (var titulo in titulosPeligrosos)
        {
            await MediaItemTestHelpers.CreateMediaItemAsync(client, request => request.Title = titulo);
        }

        var export = await ExportLibraryAsync(client, LibraryTransferFormat.Csv);
        var csv = System.Text.Encoding.UTF8.GetString(export.Content);

        foreach (var titulo in titulosPeligrosos)
        {
            // Excel, LibreOffice y Google Sheets evalúan como fórmula toda celda
            // que empiece por =, +, - o @: un título así se ejecutaría al abrir el
            // archivo. El apóstrofo delante la marca como texto y no se muestra.
            var linea = csv.Split('\n')
                .First(l => l.Contains(titulo, StringComparison.Ordinal));

            // Title es la segunda columna, así que va precedida de ";".
            Assert.DoesNotContain(";" + titulo, linea, StringComparison.Ordinal);
            Assert.Contains(";'" + titulo, linea, StringComparison.Ordinal);
        }
    }

    private static async Task<(byte[] Content, string ContentType, string FileName)> ExportLibraryAsync(
        HttpClient client,
        LibraryTransferFormat format)
    {
        var response = await client.GetAsync($"/api/media-items/export?format={format}");
        response.EnsureSuccessStatusCode();

        var contentType = response.Content.Headers.ContentType?.ToString()
            ?? throw new InvalidOperationException("La exportación no devolvió content-type.");

        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName
            ?? throw new InvalidOperationException("La exportación no devolvió nombre de archivo.");

        return (
            await response.Content.ReadAsByteArrayAsync(),
            contentType,
            fileName.Trim('"'));
    }

    [Fact]
    public async Task Import_RejectsFilesWithMoreItemsThanTheLimit()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);

        // El tope de 10 MB del controlador no acota el trabajo: un JSON pequeño
        // puede traer decenas de miles de elementos, que se cargan en memoria
        // junto con la biblioteca del usuario antes de un único SaveChanges.
        var exceso = MediaItemsService.MaxImportItems + 1;
        var elementos = string.Join(",", Enumerable.Range(0, exceso).Select(i =>
            $"{{\"title\":\"Elemento {i}\",\"contentKind\":\"Series\",\"status\":\"Planned\"}}"));
        // El "schemaVersion" no es adorno: sin él, esta prueba dejó de comprobar el tope
        // en cuanto T2-29 empezó a rechazar los archivos que no son exportaciones.
        var json = $"{{\"schemaVersion\":1,\"categories\":[],\"items\":[{elementos}]}}";

        using var multipart = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(json));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        multipart.Add(fileContent, "file", "biblioteca.json");

        var response = await client.PostAsync("/api/media-items/import?format=Json", multipart);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        var cuerpo = await response.Content.ReadAsStringAsync();
        Assert.Contains(exceso.ToString(), cuerpo, StringComparison.Ordinal);
        Assert.Contains(MediaItemsService.MaxImportItems.ToString(), cuerpo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportJson_RejectsAFileThatIsNotAnExport()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);

        // T2-29. Un JSON cualquiera es JSON válido: se deserializa sin protestar y deja el
        // sobre vacío. Hasta el 2026-09-06 eso terminaba en «importación completada sin
        // cambios», con tono de éxito, y quien se equivocara de archivo podía concluir que
        // su copia de seguridad estaba vacía.
        var json = "{\"esto\":\"no es una exportación\"}";

        using var multipart = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(json));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        multipart.Add(fileContent, "file", "cualquier-cosa.json");

        var response = await client.PostAsync("/api/media-items/import?format=Json", multipart);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        var cuerpo = await response.Content.ReadAsStringAsync();
        Assert.Contains("no es una exportación de TrackerMultimedia", cuerpo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportJson_AcceptsAnExportWithNothingInside()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);

        // La otra mitad de T2-29, y la razón de que el arreglo no sea «rechazar lo que no
        // crea nada»: una biblioteca vacía se exporta igual, y volver a importarla tiene
        // que seguir siendo un éxito sin cambios. Si esta prueba se pone en rojo, el aviso
        // nuevo se ha llevado por delante un caso legítimo.
        var json = "{\"schemaVersion\":1,\"exportedAtUtc\":\"2026-09-06T00:00:00Z\"," +
            "\"categories\":[],\"items\":[]}";

        var resultado = await ImportLibraryAsync(
            client,
            LibraryTransferFormat.Json,
            System.Text.Encoding.UTF8.GetBytes(json),
            "biblioteca-vacia.json");

        Assert.Equal(0, resultado.ItemsProcessed);
        Assert.Equal(0, resultado.ItemsCreated);
        Assert.Equal(0, resultado.ItemsUpdated);
        Assert.Equal(0, resultado.CategoriesCreated);
    }

    private static async Task<LibraryImportResponse> ImportLibraryAsync(
        HttpClient client,
        LibraryTransferFormat format,
        byte[] content,
        string fileName)
    {
        using var multipart = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            format == LibraryTransferFormat.Json ? "application/json" : "text/csv");
        multipart.Add(fileContent, "file", fileName);

        var response = await client.PostAsync($"/api/media-items/import?format={format}", multipart);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<LibraryImportResponse>(MediaItemTestHelpers.JsonOpts)
            ?? throw new InvalidOperationException("No se pudo deserializar LibraryImportResponse.");
    }
}
