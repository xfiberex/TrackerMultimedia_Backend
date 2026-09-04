using System.Text.Json;
using TrackerMultimedia.Contracts.Search;
using TrackerMultimedia.Domain.Enums;
using TrackerMultimedia.Services;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.Services;

/// <summary>
/// El otro proveedor que estaba al **0 % de cobertura**: 154 líneas sin una sola prueba.
/// Lo destapó la medición de cobertura (T4-05).
/// </summary>
public class MangaDexSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_EmptyQuery_ReturnsEmptyWithoutCallingHttp()
    {
        var service = CreateService((_, _) => throw new InvalidOperationException("HTTP no debería ejecutarse."));

        var resultados = await service.SearchAsync(
            new SearchMediaItemsRequest { Query = "  ", Type = MediaSearchType.Manga, Limit = 5 },
            CancellationToken.None);

        Assert.Empty(resultados);
    }

    [Fact]
    public async Task SearchAsync_MapsTitleCoverAndStatus()
    {
        var service = CreateService((_, _) => Task.FromResult(DelegateHttpMessageHandler.Json(
            """
            {
              "data": [
                {
                  "id": "6b1eb93e-473a-4ab3-9922-1a2725a4d02f",
                  "attributes": {
                    "title": { "en": "Berserk", "ja-ro": "Beruseruku" },
                    "status": "hiatus",
                    "year": 1989,
                    "originalLanguage": "ja"
                  },
                  "relationships": [
                    { "type": "cover_art", "attributes": { "fileName": "portada.jpg" } }
                  ]
                }
              ]
            }
            """)));

        var resultado = Assert.Single(await service.SearchAsync(
            new SearchMediaItemsRequest { Query = "berserk", Type = MediaSearchType.Manga, Limit = 5 },
            CancellationToken.None));

        Assert.Equal("Berserk", resultado.Title);
        Assert.Equal("En Hiato", resultado.ExternalStatusLabel);
        Assert.Equal(1989, resultado.ReleaseYear);
        Assert.Equal(MediaItemSourceType.MangaDex, resultado.SourceType);
        Assert.Contains("portada.jpg", resultado.CoverImageUrl!, StringComparison.Ordinal);
        // MangaDex no da puntuación en este endpoint —haría falta /statistics—, así que
        // se deja nula en vez de inventar un cero que ordenaría mal la lista.
        Assert.Null(resultado.ExternalScore);
    }

    [Fact]
    public async Task SearchAsync_FallsBackThroughTheTitleLanguages()
    {
        var service = CreateService((_, _) => Task.FromResult(DelegateHttpMessageHandler.Json(
            """
            {
              "data": [
                { "id": "d668421b-f1c1-4238-bbf1-4c1c8823277d", "attributes": { "title": { "ja-ro": "Beruseruku" }, "originalLanguage": "ja" } },
                { "id": "3a7f451d-f655-4934-8106-7d230e5747c1", "attributes": { "title": { "de": "Titel auf Deutsch" }, "originalLanguage": "ja" } },
                { "id": "a00ea6ba-a912-4ed0-8a2c-fd224877ae16", "attributes": { "title": {}, "originalLanguage": "ja" } }
              ]
            }
            """)));

        var resultados = await service.SearchAsync(
            new SearchMediaItemsRequest { Query = "x", Type = MediaSearchType.Manga, Limit = 10 },
            CancellationToken.None);

        // Inglés, luego romaji, luego cualquiera. Sin ningún título el elemento se
        // descarta: mostrarlo sin nombre no le sirve a nadie.
        Assert.Equal(2, resultados.Count);
        Assert.Contains(resultados, item => item.Title == "Beruseruku");
        Assert.Contains(resultados, item => item.Title == "Titel auf Deutsch");
    }

    /// <summary>
    /// Un solo test para los tres idiomas de origen, no un [Theory]: comparten la
    /// factoría y separarlos multiplica las conexiones sin añadir información.
    /// </summary>
    [Fact]
    public async Task SearchAsync_InfersTheTypeFromTheOriginalLanguage()
    {
        (string Idioma, MediaType Esperado)[] casos =
        [
            ("ja", MediaType.Manga),
            ("ko", MediaType.Manhwa),
            ("zh", MediaType.Manhua),
        ];

        foreach (var (idioma, esperado) in casos)
        {
            var service = CreateService((_, _) => Task.FromResult(DelegateHttpMessageHandler.Json(
                $$"""
                { "data": [ { "id": "9f00eab4-2fce-4287-97af-3fdac34955a8", "attributes": { "title": { "en": "T" }, "originalLanguage": "{{idioma}}" } } ] }
                """)));

            // En modo "All" el tipo se deduce del idioma; en una búsqueda concreta manda
            // lo que pidió el usuario.
            var resultado = Assert.Single(await service.SearchAsync(
                new SearchMediaItemsRequest { Query = "t", Type = MediaSearchType.All, Limit = 5 },
                CancellationToken.None));

            Assert.Equal(esperado, resultado.SuggestedType);
        }
    }

    [Fact]
    public async Task SearchAsync_ASpecificSearchOverridesTheInferredLanguage()
    {
        var service = CreateService((_, _) => Task.FromResult(DelegateHttpMessageHandler.Json(
            """
            { "data": [ { "id": "85b40672-b504-47d8-937d-a7fd64cb9d27", "attributes": { "title": { "en": "T" }, "originalLanguage": "ja" } } ] }
            """)));

        var resultado = Assert.Single(await service.SearchAsync(
            new SearchMediaItemsRequest { Query = "t", Type = MediaSearchType.Manhwa, Limit = 5 },
            CancellationToken.None));

        Assert.Equal(MediaType.Manhwa, resultado.SuggestedType);
    }

    [Fact]
    public async Task SearchAsync_DiscardsAnItemWithAMalformedId()
    {
        var service = CreateService((_, _) => Task.FromResult(DelegateHttpMessageHandler.Json(
            """
            {
              "data": [
                { "id": "esto-no-es-un-uuid", "attributes": { "title": { "en": "Roto" } } },
                { "id": "0b2a4c1e-8f3d-4a5b-9c6d-7e8f9a0b1c2d", "attributes": { "title": { "en": "Bueno" } } }
              ]
            }
            """)));

        var resultados = await service.SearchAsync(
            new SearchMediaItemsRequest { Query = "x", Type = MediaSearchType.Manga, Limit = 10 },
            CancellationToken.None);

        // El identificador se convierte con Guid.Parse, que lanza FormatException; esa
        // excepción NO está en el filtro del catch de SearchAsync, así que un solo
        // elemento defectuoso devolvía un 500 y se llevaba por delante los buenos.
        Assert.Equal("Bueno", Assert.Single(resultados).Title);
    }

    [Fact]
    public async Task SearchAsync_PropagatesAnUnreadableResponse()
    {
        var service = CreateService((_, _) => throw new JsonException("respuesta ilegible"));

        await Assert.ThrowsAsync<JsonException>(() => service.SearchAsync(
            new SearchMediaItemsRequest { Query = "berserk", Type = MediaSearchType.Manga, Limit = 5 },
            CancellationToken.None));
    }

    [Fact]
    public void Supports_OnlyTheThreePrintedTypes()
    {
        var service = CreateService((_, _) => throw new InvalidOperationException());

        Assert.True(service.Supports(MediaSearchType.Manga));
        Assert.True(service.Supports(MediaSearchType.Manhwa));
        Assert.True(service.Supports(MediaSearchType.Manhua));
        // MangaDex no cataloga animación: decir que sí lo pondría a contestar
        // búsquedas de anime con resultados de manga.
        Assert.False(service.Supports(MediaSearchType.Anime));
        Assert.False(service.Supports(MediaSearchType.Donghua));
    }

    private static MangaDexSearchService CreateService(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        => new(new HttpClient(new DelegateHttpMessageHandler(handler))
        {
            BaseAddress = new Uri("https://api.mangadex.org/"),
            Timeout = TimeSpan.FromSeconds(10),
        });
}
