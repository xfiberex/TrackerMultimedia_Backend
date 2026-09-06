using System.Security.Cryptography;
using System.Text;

namespace TrackerMultimedia.Infrastructure.Http;

/// <summary>
/// PKCE — *Proof Key for Code Exchange*, RFC 7636 (T4-02).
///
/// El flujo: se genera un verificador aleatorio, se manda su hash SHA-256 al proveedor
/// en la autorización, y al canjear el código se manda el verificador en claro. El
/// proveedor comprueba que uno es el hash del otro, de modo que **un código robado no
/// sirve sin el verificador**, que nunca ha salido de este servidor.
///
/// Solo se emite el método <c>S256</c>. El otro que permite el RFC, <c>plain</c>, manda
/// el verificador tal cual en la autorización y no protege de nada.
/// </summary>
public static class Pkce
{
    /// <summary>
    /// Verificador nuevo: 32 bytes aleatorios en Base64 URL-safe, que son 43 caracteres.
    /// El RFC exige entre 43 y 128 del juego <c>[A-Za-z0-9-._~]</c>.
    /// </summary>
    public static string GenerateVerifier() =>
        Base64Url(RandomNumberGenerator.GetBytes(32));

    /// <summary>Reto correspondiente a un verificador: <c>BASE64URL(SHA256(verifier))</c>.</summary>
    public static string CreateChallenge(string verifier) =>
        Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    /// <summary>
    /// Base64 URL-safe sin relleno, tal y como lo define el RFC 7636. El relleno con
    /// <c>=</c> y los caracteres <c>+</c> y <c>/</c> del Base64 corriente no son válidos
    /// en la URL de autorización.
    /// </summary>
    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
}
