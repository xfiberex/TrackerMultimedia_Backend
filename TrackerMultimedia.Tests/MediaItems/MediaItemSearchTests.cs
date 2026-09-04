using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.MediaItems;

/// <summary>
/// T2-04. El parámetro `search` no tenía ni un test, y no por descuido: **no podía
/// tenerlo**. `EF.Functions.ILike` solo existe en Npgsql, así que sobre la SQLite en
/// memoria que usaba la suite este endpoint devolvía 500. Estos tests son lo primero que
/// habilita el cambio a PostgreSQL (T2-02).
///
/// Lo que se cubre es el escapado, que es donde está el riesgo real: `%`, `_` y `\` son
/// comodines de LIKE, y sin escaparlos una búsqueda del usuario se convierte en un patrón
/// que trae de más.
/// </summary>
public class MediaItemSearchTests(AppFactory factory) : IClassFixture<AppFactory>
{
    [Fact]
    public async Task Search_MatchesPartialTitleIgnoringCase()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        await MediaItemTestHelpers.CreateMediaItemAsync(client, request => request.Title = "Frieren: Beyond Journey's End");
        await MediaItemTestHelpers.CreateMediaItemAsync(client, request => request.Title = "Vinland Saga");

        var enMinusculas = await MediaItemTestHelpers.GetMediaItemsAsync(client, "?search=frieren");
        var enMayusculas = await MediaItemTestHelpers.GetMediaItemsAsync(client, "?search=FRIEREN");
        var parcialInterior = await MediaItemTestHelpers.GetMediaItemsAsync(client, "?search=eyond");

        Assert.Single(enMinusculas.Items);
        Assert.Single(enMayusculas.Items);
        Assert.Single(parcialInterior.Items);
        Assert.StartsWith("Frieren", enMinusculas.Items.Single().Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Search_AlsoMatchesTheAlternativeTitle()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        await MediaItemTestHelpers.CreateMediaItemAsync(client, request =>
        {
            request.Title = "Sousou no Frieren";
            request.AlternativeTitle = "La sepulturera";
        });

        var porTituloAlternativo = await MediaItemTestHelpers.GetMediaItemsAsync(client, "?search=sepulturera");

        Assert.Single(porTituloAlternativo.Items);
    }

    /// <summary>
    /// Un solo test que recorre los tres comodines. Sin el escapado, `%` traería todo,
    /// `_` haría de comodín de un carácter y `\` rompería el patrón.
    /// </summary>
    [Fact]
    public async Task Search_EscapesTheLikeWildcards()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);

        await MediaItemTestHelpers.CreateMediaItemAsync(client, request => request.Title = "Descuento 50% garantizado");
        await MediaItemTestHelpers.CreateMediaItemAsync(client, request => request.Title = "Archivo_final");
        await MediaItemTestHelpers.CreateMediaItemAsync(client, request => request.Title = @"Ruta C:\usuarios");
        await MediaItemTestHelpers.CreateMediaItemAsync(client, request => request.Title = "Descuento 50 sin símbolo");
        await MediaItemTestHelpers.CreateMediaItemAsync(client, request => request.Title = "ArchivoXfinal");

        (string Consulta, string Esperado)[] casos =
        [
            // Sin escapar, "50%" sería "empieza por 50" y traería también "50 sin símbolo".
            ("50%", "Descuento 50% garantizado"),
            // Sin escapar, "Archivo_final" haría de comodín y traería "ArchivoXfinal".
            ("Archivo_final", "Archivo_final"),
            // La barra invertida es el carácter de escape: sin duplicarla, el patrón se rompe.
            (@"C:\usuarios", @"Ruta C:\usuarios"),
        ];

        foreach (var (consulta, esperado) in casos)
        {
            var resultado = await MediaItemTestHelpers.GetMediaItemsAsync(
                client, $"?search={Uri.EscapeDataString(consulta)}");

            Assert.Equal(1, resultado.TotalCount);
            Assert.Equal(esperado, resultado.Items.Single().Title);
        }
    }

    [Fact]
    public async Task Search_NeverReachesAnotherUsersLibrary()
    {
        var (clientA, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        var (clientB, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);

        await MediaItemTestHelpers.CreateMediaItemAsync(clientA, request => request.Title = "Solo de A: Berserk");

        var desdeB = await MediaItemTestHelpers.GetMediaItemsAsync(clientB, "?search=Berserk");

        // La invariante más importante del sistema, también por este camino.
        Assert.Empty(desdeB.Items);
    }

    [Fact]
    public async Task Search_WithoutMatchesReturnsAnEmptyPage()
    {
        var (client, _, _) = await MediaItemTestHelpers.CreateAuthenticatedClientAsync(factory);
        await MediaItemTestHelpers.CreateMediaItemAsync(client, request => request.Title = "Monster");

        var resultado = await MediaItemTestHelpers.GetMediaItemsAsync(client, "?search=noexisteestetitulo");

        Assert.Empty(resultado.Items);
        Assert.Equal(0, resultado.TotalCount);
    }
}
