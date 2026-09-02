using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.Auth;

/// <summary>
/// Sin manejador global, una excepción no controlada salía como un 500 con cuerpo
/// vacío: el usuario no veía nada y no había forma de relacionar su incidencia con
/// ninguna línea del log.
/// </summary>
public class ProblemDetailsTests(AppFactory factory) : IClassFixture<AppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task UnknownRoute_ReturnsProblemDetailsWithATraceId()
    {
        var response = await _client.GetAsync("/api/ruta-que-no-existe");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.TryGetProperty("traceId", out var traceId));
        Assert.False(string.IsNullOrWhiteSpace(traceId.GetString()));
    }

    [Fact]
    public async Task ValidationError_ReturnsProblemDetails()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email = "no-es-un-email" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
