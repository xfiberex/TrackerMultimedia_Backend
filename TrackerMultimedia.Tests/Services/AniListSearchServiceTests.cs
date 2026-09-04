using System.Text.Json;
using TrackerMultimedia.Contracts.Search;
using TrackerMultimedia.Domain.Enums;
using TrackerMultimedia.Services;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.Services;

/// <summary>
/// Este servicio estaba al **0 % de cobertura**: 220 líneas sin una sola prueba. Solo el
/// proveedor de Jikan tenía tests, y los otros dos se añadieron después sin ninguno. Lo
/// destapó la medición de cobertura (T4-05), no una lectura del código.
/// </summary>
public class AniListSearchServiceTests
{
    private const string PaginaConDosMedios =
        """
        {
          "data": { "Page": { "media": [
            { "id": 1, "title": { "romaji": "Sousou no Frieren", "english": "Frieren" },
              "status": "FINISHED", "averageScore": 90, "countryOfOrigin": "JP",
              "coverImage": { "large": "https://anilist.test/1.jpg" },
              "siteUrl": "https://anilist.test/anime/1", "startDate": { "year": 2023 } },
            { "id": 2, "title": { "romaji": "Sin puntuacion" },
              "status": "RELEASING", "countryOfOrigin": "CN",
              "siteUrl": "https://anilist.test/anime/2" }
          ] } }
        }
        """;

    [Fact]
    public async Task SearchAsync_EmptyQuery_ReturnsEmptyWithoutCallingHttp()
    {
        var service = CreateService((_, _) => throw new InvalidOperationException("HTTP no debería ejecutarse."));

        var resultados = await service.SearchAsync(
            new SearchMediaItemsRequest { Query = "   ", Type = MediaSearchType.Anime, Limit = 5 },
            CancellationToken.None);

        Assert.Empty(resultados);
    }

    [Fact]
    public async Task SearchAsync_MapsTheFieldsTheLibraryNeeds()
    {
        var service = CreateService((_, _) => Task.FromResult(DelegateHttpMessageHandler.Json(PaginaConDosMedios)));

        var resultados = await service.SearchAsync(
            new SearchMediaItemsRequest { Query = "frieren", Type = MediaSearchType.Anime, Limit = 10 },
            CancellationToken.None);

        var primero = resultados.First(item => item.ExternalId == 1);
        Assert.Equal("Sousou no Frieren", primero.Title);
        Assert.Equal("Frieren", primero.AlternativeTitle);
        Assert.Equal(MediaItemSourceType.AniList, primero.SourceType);
        // AniList puntúa sobre 100 y la biblioteca sobre 10.
        Assert.Equal(9.0, primero.ExternalScore);
        Assert.Equal(2023, primero.ReleaseYear);

        // Sin puntuación no se inventa un cero, que ordenaría mal.
        var segundo = resultados.First(item => item.ExternalId == 2);
        Assert.Null(segundo.ExternalScore);
    }

    [Fact]
    public async Task SearchAsync_DiscardsMediaWithoutIdOrTitle()
    {
        var service = CreateService((_, _) => Task.FromResult(DelegateHttpMessageHandler.Json(
            """
            {
              "data": { "Page": { "media": [
                { "id": 0, "title": { "romaji": "Sin identificador" } },
                { "id": 7, "title": { "romaji": "   " } },
                { "id": 8, "title": { "romaji": "Valido" } }
              ] } }
            }
            """)));

        var resultados = await service.SearchAsync(
            new SearchMediaItemsRequest { Query = "x", Type = MediaSearchType.Anime, Limit = 10 },
            CancellationToken.None);

        // Sin identificador o sin título el elemento no se puede guardar después:
        // caería en la validación de origen externo (T2-18) o quedaría sin nombre.
        Assert.Equal(8, Assert.Single(resultados).ExternalId);
    }

    [Fact]
    public async Task SearchAsync_RespectsTheRequestedLimit()
    {
        var service = CreateService((_, _) => Task.FromResult(DelegateHttpMessageHandler.Json(PaginaConDosMedios)));

        var resultados = await service.SearchAsync(
            new SearchMediaItemsRequest { Query = "frieren", Type = MediaSearchType.Anime, Limit = 1 },
            CancellationToken.None);

        Assert.Single(resultados);
    }

    [Fact]
    public async Task SearchAsync_WhenEveryTargetFails_PropagatesTheError()
    {
        var service = CreateService((_, _) => throw new JsonException("respuesta ilegible"));

        // Que falle es correcto: `SearchController` lo traduce a un 502 con mensaje.
        // Tragárselo devolvería una lista vacía indistinguible de "no hay resultados".
        await Assert.ThrowsAsync<JsonException>(() => service.SearchAsync(
            new SearchMediaItemsRequest { Query = "frieren", Type = MediaSearchType.All, Limit = 5 },
            CancellationToken.None));
    }

    [Fact]
    public void Supports_CoversTheTypesAniListCanAnswer()
    {
        var service = CreateService((_, _) => throw new InvalidOperationException());

        // AniList cataloga los seis tipos que conoce el buscador, a diferencia de
        // MangaDex, que solo responde de los tres impresos.
        Assert.All(
            Enum.GetValues<MediaSearchType>(),
            tipo => Assert.True(service.Supports(tipo), $"AniList debería soportar {tipo}"));
        Assert.Equal(MediaItemSourceType.AniList, service.SourceType);
    }

    private static AniListSearchService CreateService(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        => new(new HttpClient(new DelegateHttpMessageHandler(handler))
        {
            BaseAddress = new Uri("https://graphql.anilist.co"),
            Timeout = TimeSpan.FromSeconds(10),
        });
}
