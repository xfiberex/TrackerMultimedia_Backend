using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TrackerMultimedia.Domain.Entities;

namespace TrackerMultimedia.Services;

/// <summary>
/// Genera access tokens JWT y refresh tokens, y gestiona el hashing de estos últimos.
/// </summary>
public class TokenService(IConfiguration configuration)
{
    // -------------------------------------------------------------------------
    // Access Token
    // -------------------------------------------------------------------------

    public string GenerateAccessToken(ApplicationUser user)
    {
        var secret = configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret no está configurado.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("displayName", user.DisplayName ?? user.Email!.Split('@')[0]),
        };

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(GetAccessTokenLifetimeMinutes()),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // -------------------------------------------------------------------------
    // Refresh Token
    // -------------------------------------------------------------------------

    /// <summary>
    /// Genera un token de refresco aleatorio.
    /// Devuelve el token en claro (se envía al cliente) y su hash SHA-256 (se almacena en BD).
    /// </summary>
    public (string RawToken, string TokenHash) GenerateRefreshToken()
    {
        var rawBytes = RandomNumberGenerator.GetBytes(64);
        var rawToken = Convert.ToBase64String(rawBytes);
        return (rawToken, ComputeHash(rawToken));
    }

    /// <summary>Calcula el hash SHA-256 en Base64 de un token en claro.</summary>
    public string ComputeHash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    // -------------------------------------------------------------------------
    // Configuration helpers
    // -------------------------------------------------------------------------

    public int GetAccessTokenLifetimeMinutes()
        => configuration.GetValue("Jwt:AccessTokenLifetimeMinutes", 15);

    public int GetRefreshTokenLifetimeDays()
        => configuration.GetValue("Jwt:RefreshTokenLifetimeDays", 7);
}
