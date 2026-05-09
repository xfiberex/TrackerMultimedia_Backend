# TrackerMultimedia — Backend

ASP.NET Core 10 Web API con autenticación JWT, correo SMTP y OAuth (Google / GitHub).

## Requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL](https://www.postgresql.org/) 14 o superior
- [`dotnet-ef` tools](https://learn.microsoft.com/en-us/ef/core/cli/dotnet): `dotnet tool install --global dotnet-ef`

---

## Configuración de secretos

Los secretos de desarrollo **nunca se versionan**. Se almacenan fuera del repositorio con `dotnet user-secrets` en `%APPDATA%\Microsoft\UserSecrets\`.

Si prefieres un archivo local para esta máquina, el backend también carga `appsettings.Local.json` y `appsettings.Development.local.json` en entorno `Development`. Ambos están ignorados por Git.

### 1. Inicializar (una sola vez)

```bash
dotnet user-secrets init
```

### 2. Todos los secretos necesarios

```bash
# ── Base de datos ────────────────────────────────────────────────────────────
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Database=tracker_multimedia;Username=<usuario>;Password=<contraseña>"

# ── Seguridad interna ────────────────────────────────────────────────────────
# Header X-Api-Key para rutas administrativas
dotnet user-secrets set "Security:ApiKey" "<cadena-aleatoria-larga>"

# ── JWT ─────────────────────────────────────────────────────────────────────
# Mínimo 32 caracteres
dotnet user-secrets set "Jwt:Secret" "<cadena-aleatoria-de-32-o-mas-caracteres>"

# ── SMTP / Mailtrap ──────────────────────────────────────────────────────────
# Credenciales en: https://mailtrap.io → Email Testing → SMTP Settings
dotnet user-secrets set "Smtp:Host"     "sandbox.smtp.mailtrap.io"
dotnet user-secrets set "Smtp:Port"     "587"
dotnet user-secrets set "Smtp:Username" "<mailtrap-username>"
dotnet user-secrets set "Smtp:Password" "<mailtrap-password>"
dotnet user-secrets set "Smtp:Enabled"  "true"

# ── Google OAuth ─────────────────────────────────────────────────────────────
# Crear en: https://console.cloud.google.com → APIs & Services → Credentials
# Redirect URI autorizada: http://localhost:5218/api/auth/google/callback
dotnet user-secrets set "OAuth:Google:Enabled"      "true"
dotnet user-secrets set "OAuth:Google:ClientId"     "<google-client-id>"
dotnet user-secrets set "OAuth:Google:ClientSecret" "<google-client-secret>"

# ── GitHub OAuth ─────────────────────────────────────────────────────────────
# DESARROLLO: Crear una app de GitHub separada (localhost)
# Crear en: https://github.com/settings/developers → New OAuth App
# Authorization callback URL: http://localhost:5218/api/auth/github/callback
# El backend prioriza DevClientId y DevClientSecret si existen en Development.
# PowerShell:
#   $env:DevClientId = "<github-dev-client-id>"
#   $env:DevClientSecret = "<github-dev-client-secret>"
dotnet user-secrets set "OAuth:GitHub:Enabled"      "true"
dotnet user-secrets set "OAuth:GitHub:DevClientId"  "<github-dev-client-id>"
dotnet user-secrets set "OAuth:GitHub:DevClientSecret" "<github-dev-client-secret>"

# PRODUCCIÓN: Las credenciales se cargan desde Render environment variables:
# OAuth__GitHub__Enabled = true
# OAuth__GitHub__ClientId = <github-producción-client-id>
# OAuth__GitHub__ClientSecret = <github-producción-client-secret>
# OAuth__GitHub__RedirectUri = https://tu-backend-render-url.onrender.com/api/auth/github/callback

> Los valores no-sensibles (`RedirectUri`, `FrontendBaseUrl`, `FromAddress`, tiempos de expiración, etc.) ya están en `appsettings.json` y no necesitan secretos.

### Alternativa: archivo local ignorado por Git

```bash
cp appsettings.Local.example.json appsettings.Local.json
```

Rellena `appsettings.Local.json` con tus valores reales. Ese archivo no se versiona.

### Ver secretos configurados

```bash
dotnet user-secrets list
```

---

## Base de datos

```bash
# Aplicar todas las migraciones pendientes
dotnet ef database update

# Crear una nueva migración
dotnet ef migrations add <NombreMigracion>

# Revertir la última migración
dotnet ef migrations remove
```

---

## Iniciar el servidor

```bash
dotnet run
```

El API queda disponible en `http://localhost:5218`.

Si falla al arrancar por configuración, revisa primero una de estas dos opciones:

- `dotnet user-secrets list`
- `appsettings.Local.json`

---

## Pruebas y artefactos locales

Los archivos fuente de pruebas forman parte del repositorio, pero sus salidas generadas no.

- El `.gitignore` excluye resultados locales como `TestResults/`, `coverage/`, `*.trx`, `*.coverage` y reportes equivalentes.
- Si ejecutas pruebas o cobertura en local, esos artefactos deben quedarse fuera del control de versiones.

---

## Estructura

```
TrackerMultimedia_Backend/
├── Contracts/          # DTOs de entrada/salida
│   ├── Auth/           # Register, Login, OAuth, confirmación de email...
│   ├── Common/         # PagedResponse, ServiceResult
│   ├── MediaItems/     # CRUD de ítems multimedia
│   └── Search/         # Búsqueda externa (Jikan)
├── Controllers/        # Endpoints REST (Auth, OAuth, MediaItems, Search)
├── Data/               # ApplicationDbContext
├── Domain/
│   ├── Entities/       # ApplicationUser, MediaItem, OAuthState, RefreshToken
│   ├── Enums/          # MediaType, MediaStatus
│   └── Validation/     # Constantes de validación de dominio
├── Infrastructure/
│   └── Options/        # SmtpOptions, OAuthOptions (bind de appsettings)
├── Migrations/         # Historial de migraciones EF Core
├── Services/           # Lógica de negocio
│   ├── AuthSessionService.cs   # Emite JWT + RefreshToken
│   ├── EmailTemplates.cs       # HTML de correos
│   ├── GitHubAuthService.cs    # Flujo OAuth GitHub
│   ├── GoogleAuthService.cs    # Flujo OAuth Google
│   ├── IEmailService.cs        # Contrato de envío de email
│   ├── JikanSearchService.cs   # Búsqueda en Jikan API
│   ├── MediaItemsService.cs    # CRUD de ítems
│   ├── SmtpEmailService.cs     # Implementación SMTP (Mailtrap)
│   └── TokenService.cs         # Generación/validación de JWT
├── appsettings.json    # Configuración no-sensible (plantilla)
├── appsettings.Local.example.json # Plantilla local no versionada
└── Program.cs          # Composición de servicios y middleware
```
