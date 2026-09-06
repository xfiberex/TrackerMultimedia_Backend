using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrackerMultimedia.Data;
using TrackerMultimedia.Domain.Entities;
using TrackerMultimedia.Infrastructure.Http;
using TrackerMultimedia.Services;
using TrackerMultimedia.Tests.Helpers;

namespace TrackerMultimedia.Tests.Auth;

/// <summary>
/// T4-02 — PKCE (RFC 7636) en los flujos OAuth.
///
/// El cliente es confidencial, así que PKCE no es obligatorio: lo que aporta es que un
/// código de autorización interceptado en el navegador **no se pueda canjear**, porque
/// el verificador que lo cierra nunca sale del servidor.
/// </summary>
public class PkceTests
{
    /// <summary>
    /// El vector de prueba del apéndice B del RFC 7636. Comprobar el hash contra un valor
    /// que publica la norma es lo que distingue «implementado» de «implementado bien»:
    /// un Base64 con relleno, o con <c>+</c> y <c>/</c> en vez de <c>-</c> y <c>_</c>,
    /// pasaría cualquier prueba escrita contra la propia implementación.
    /// </summary>
    [Fact]
    public void CreateChallenge_MatchesTheRfc7636TestVector()
    {
        const string verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        const string expectedChallenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

        Assert.Equal(expectedChallenge, Pkce.CreateChallenge(verifier));
    }

    /// <summary>
    /// El verificador debe caer en el juego de caracteres del RFC y medir entre 43 y 128.
    /// </summary>
    [Fact]
    public void GenerateVerifier_IsUrlSafeAndLongEnough()
    {
        var verifier = Pkce.GenerateVerifier();

        Assert.InRange(verifier.Length, 43, 128);
        Assert.Matches("^[A-Za-z0-9\\-._~]+$", verifier);
        Assert.NotEqual(verifier, Pkce.GenerateVerifier());
    }

    /// <summary>
    /// Con PKCE activado, el reto llega al proveedor y el verificador que lo genera queda
    /// guardado con el state. Que uno sea el hash del otro es lo que comprueba el
    /// proveedor al canjear, así que es lo que se comprueba aquí.
    /// </summary>
    [Fact]
    public async Task Init_WithPkceEnabled_SendsTheChallengeAndStoresItsVerifier()
    {
        var stub = new RecordingGoogleAuthService();
        using var factory = await CreateGoogleFactoryAsync(stub, usePkce: true);

        var response = await factory.CreateClient().GetAsync("/api/auth/google/init");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var state = dbContext.OAuthStates.Single();

        Assert.NotNull(state.CodeVerifier);
        Assert.NotNull(stub.LastCodeChallenge);
        Assert.Equal(Pkce.CreateChallenge(state.CodeVerifier!), stub.LastCodeChallenge);

        // El verificador no puede viajar: si lo hiciera, PKCE no protegería de nada.
        Assert.NotEqual(state.CodeVerifier, stub.LastCodeChallenge);
    }

    /// <summary>
    /// Sin PKCE —GitHub viene así, y era el comportamiento anterior de los dos— no se
    /// manda reto ni se guarda verificador. El flujo tiene que seguir funcionando igual.
    /// </summary>
    [Fact]
    public async Task Init_WithPkceDisabled_SendsNoChallenge()
    {
        var stub = new RecordingGoogleAuthService();
        using var factory = await CreateGoogleFactoryAsync(stub, usePkce: false);

        var response = await factory.CreateClient().GetAsync("/api/auth/google/init");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Null(dbContext.OAuthStates.Single().CodeVerifier);
        Assert.Null(stub.LastCodeChallenge);
    }

    /// <summary>
    /// La otra mitad del flujo: en el callback, el verificador guardado se recupera y se
    /// manda al canjear. Sin esto el reto se habría enviado para nada.
    /// </summary>
    [Fact]
    public async Task Callback_SendsTheStoredVerifierWhenExchangingTheCode()
    {
        var stub = new RecordingGoogleAuthService();
        using var factory = await CreateGoogleFactoryAsync(stub, usePkce: true);

        const string stateValue = "state-con-pkce";
        var verifier = Pkce.GenerateVerifier();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.OAuthStates.Add(new OAuthState
            {
                StateValue = stateValue,
                Provider = "google",
                CodeVerifier = verifier,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
            });
            await dbContext.SaveChangesAsync();
        }

        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await client.GetAsync($"/api/auth/google/callback?code=codigo-123&state={stateValue}");

        Assert.Equal(verifier, stub.LastCodeVerifier);
    }

    // -------------------------------------------------------------------------

    private static async Task<AppFactory> CreateGoogleFactoryAsync(
        IGoogleAuthService googleService,
        bool usePkce)
    {
        var factory = new AppFactory(
            configurationOverrides: new Dictionary<string, string?>
            {
                ["OAuth:Google:Enabled"] = "true",
                ["OAuth:Google:UsePkce"] = usePkce ? "true" : "false",
                ["App:FrontendBaseUrl"] = "http://frontend.test"
            },
            configureAdditionalTestServices: services =>
            {
                services.RemoveAll<IGoogleAuthService>();
                services.AddSingleton(googleService);
            });

        await factory.InitializeDatabaseAsync();
        return factory;
    }

    private sealed class RecordingGoogleAuthService : IGoogleAuthService
    {
        public string? LastCodeChallenge { get; private set; }
        public string? LastCodeVerifier { get; private set; }

        public string BuildAuthorizationUrl(string state, string? codeChallenge = null)
        {
            LastCodeChallenge = codeChallenge;
            return $"https://google.test/auth?state={state}";
        }

        public Task<ExternalUserProfile> ExchangeCodeAsync(
            string code,
            string? codeVerifier = null,
            CancellationToken cancellationToken = default)
        {
            LastCodeVerifier = codeVerifier;
            return Task.FromResult(new ExternalUserProfile(
                "google-user", "pkce@test.com", "PKCE User", null));
        }
    }
}
