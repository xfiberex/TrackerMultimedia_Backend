using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using TrackerMultimedia.Contracts.Auth;
using TrackerMultimedia.Domain.Entities;

namespace TrackerMultimedia.Tests.Helpers;

public static class AuthHelpers
{
    public const string DefaultPassword = "Test1234!";

    public static Task<ApplicationUser> CreateUserAsync(
        IServiceProvider services,
        string? email = null,
        string? password = null,
        bool emailConfirmed = false)
        => CreateUserAsync(services, email, password, emailConfirmed, null);

    public static async Task<ApplicationUser> CreateUserAsync(
        IServiceProvider services,
        string? email,
        string? password,
        bool emailConfirmed,
        string? displayName)
    {
        email ??= $"user_{Guid.NewGuid():N}@test.com";
        password ??= DefaultPassword;

        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            Email = email,
            UserName = email,
            DisplayName = displayName ?? email.Split('@')[0],
            EmailConfirmed = emailConfirmed,
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"CreateUser falló: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        return user;
    }

    /// <summary>
    /// Crea un usuario directamente en BD con EmailConfirmed=true.
    /// Usar en tests que no prueban el registro (login, autorización, aislamiento).
    /// </summary>
    public static async Task<ApplicationUser> CreateConfirmedUserAsync(
        IServiceProvider services,
        string? email = null,
        string? password = null)
    {
        return await CreateUserAsync(services, email, password, emailConfirmed: true);
    }

    /// <summary>
    /// Confirma el email de un usuario ya registrado a través del UserManager (sin SMTP).
    /// Usar en RegisterTests para verificar el flujo completo de confirmación.
    /// </summary>
    public static async Task ConfirmEmailAsync(IServiceProvider services, string email)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"Usuario {email} no encontrado.");

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var result = await userManager.ConfirmEmailAsync(user, token);

        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"ConfirmEmail falló: {string.Join(", ", result.Errors.Select(e => e.Description))}");
    }

    public static async Task<string> GenerateEmailConfirmationTokenAsync(IServiceProvider services, string email)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"Usuario {email} no encontrado.");

        return await userManager.GenerateEmailConfirmationTokenAsync(user);
    }

    public static async Task<string> GeneratePasswordResetTokenAsync(IServiceProvider services, string email)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"Usuario {email} no encontrado.");

        return await userManager.GeneratePasswordResetTokenAsync(user);
    }

    /// <summary>
    /// Llama a POST /api/auth/login y devuelve el AuthResponse.
    /// Requiere que el usuario exista y tenga email confirmado.
    /// </summary>
    public static async Task<AuthResponse> LoginAsync(
        HttpClient client,
        string email,
        string password = DefaultPassword)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AuthResponse>()
               ?? throw new InvalidOperationException("Login devolvió respuesta nula.");
    }

    /// <summary>
    /// Crea un usuario confirmado y hace login. Devuelve (email, AuthResponse).
    /// </summary>
    public static async Task<(string Email, AuthResponse Auth)> CreateAndLoginAsync(
        HttpClient client,
        IServiceProvider services,
        string? email = null,
        string? password = null)
    {
        password ??= DefaultPassword;
        var user = await CreateConfirmedUserAsync(services, email, password);
        var auth = await LoginAsync(client, user.Email!, password);
        return (user.Email!, auth);
    }

    /// <summary>
    /// Como <see cref="CreateAndLoginAsync"/>, pero devuelve también el token de refresco
    /// en claro leído de la cookie. Desde T4-01 ese valor no aparece en ninguna respuesta
    /// del API, así que la cookie es el único sitio del que un test puede sacarlo — y hace
    /// falta para poder afirmar que **no** sale por ningún otro.
    /// </summary>
    public static async Task<(string Email, AuthResponse Auth, string RefreshToken)> CreateAndLoginCapturingCookieAsync(
        HttpClient client,
        IServiceProvider services,
        string? email = null,
        string? password = null)
    {
        password ??= DefaultPassword;
        var user = await CreateConfirmedUserAsync(services, email, password);

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = user.Email!, password });
        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>()
            ?? throw new InvalidOperationException("Login devolvió respuesta nula.");

        return (user.Email!, auth, SessionCookies.Read(response));
    }
}
