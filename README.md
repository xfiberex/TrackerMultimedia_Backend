# TrackerMultimedia — Backend

ASP.NET Core 10 Web API con autenticación JWT, correo SMTP y OAuth (Google / GitHub).

## Requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/products/docker-desktop/) — la base de datos corre en un
  contenedor; **no hace falta instalar PostgreSQL en la máquina**
- [`dotnet-ef` tools](https://learn.microsoft.com/en-us/ef/core/cli/dotnet): `dotnet tool install --global dotnet-ef`

---

## Configuración de secretos

Los secretos de desarrollo **nunca se versionan**. Se almacenan fuera del repositorio con `dotnet user-secrets` en `%APPDATA%\Microsoft\UserSecrets\`.

El backend también carga `appsettings.Local.json` y `appsettings.Development.local.json` en
entorno `Development`, ambos ignorados por Git. **Úsalos solo para configuración no
sensible** —orígenes CORS, niveles de log, URLs de callback—: los secretos van en
user-secrets y solo ahí.

> **Qué protege realmente user-secrets, y qué no.** No cifra nada: guarda un `secrets.json`
> en claro en `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\`. Lo que aporta es que ese
> archivo vive **fuera de la carpeta del repositorio**, así que no puede acabar en un commit
> por descuido ni colarse en el contexto de build de Docker. Frente a alguien con acceso a
> tu sesión de Windows no te protege; frente a un `git add .` distraído, sí.

### 1. Inicializar (una sola vez)

```bash
dotnet user-secrets init
```

### 2. Los secretos necesarios

**En user-secrets va solo lo que es secreto.** Todo lo demás —host y puerto SMTP, si un
proveedor está activo, las URLs de callback, los tiempos de expiración del token— es
configuración normal y vive en `appsettings.Local.json`.

En PowerShell, **comillas simples para los valores**: entre comillas dobles, PowerShell
expande cualquier cosa que empiece por `$` y te deja la clave vacía sin avisar.

```powershell
# ── Base de datos ────────────────────────────────────────────────────────────
# Puerto 5433 y nombre "trackerMultimedia": es lo que define docker-compose.yml.
# La contraseña es la que pusiste en .env.
dotnet user-secrets set "ConnectionStrings:DefaultConnection" 'Host=localhost;Port=5433;Database=trackerMultimedia;Username=postgres;Password=<la-de-tu-.env>;'

# ── JWT ──────────────────────────────────────────────────────────────────────
# Genéralo sin que aparezca en pantalla ni en el historial de la terminal.
$bytes = New-Object byte[] 48
(New-Object System.Security.Cryptography.RNGCryptoServiceProvider).GetBytes($bytes)
dotnet user-secrets set "Jwt:Secret" ([Convert]::ToBase64String($bytes))
Remove-Variable bytes

# ── SMTP / Mailtrap ──────────────────────────────────────────────────────────
# Credenciales en: https://mailtrap.io → Email Testing → SMTP Settings
# Mailtrap regenera usuario y contraseña a la vez: si rotas, cambia los dos.
dotnet user-secrets set "Smtp:Username" '<mailtrap-username>'
dotnet user-secrets set "Smtp:Password" '<mailtrap-password>'

# ── Google OAuth ─────────────────────────────────────────────────────────────
# https://console.cloud.google.com → APIs & Services → Credentials
# Redirect URI autorizada: http://localhost:5218/api/auth/google/callback
dotnet user-secrets set "OAuth:Google:ClientId"     '<google-client-id>'
dotnet user-secrets set "OAuth:Google:ClientSecret" '<google-client-secret>'

# ── GitHub OAuth ─────────────────────────────────────────────────────────────
# https://github.com/settings/developers → New OAuth App
# Authorization callback URL: http://localhost:5218/api/auth/github/callback
dotnet user-secrets set "OAuth:GitHub:ClientId"     '<github-client-id>'
dotnet user-secrets set "OAuth:GitHub:ClientSecret" '<github-client-secret>'
```

Los *Client ID* de Google y GitHub **no son secretos**: son identificadores públicos que el
navegador ve en la URL de autorización. Están aquí por comodidad, para tener toda la
configuración de un proveedor junta.

`GitHubAuthService` acepta además `OAuth:GitHub:DevClientId` y `DevClientSecret`, que tienen
prioridad en entorno `Development`. Sirven para usar una app de GitHub distinta en local sin
tocar la principal; si no las defines, se usan `ClientId` y `ClientSecret` sin más.

Lo no sensible —`Smtp:Host`, `Smtp:Port`, `Smtp:Enabled`, `OAuth:*:Enabled`,
`OAuth:*:RedirectUri`, `Cors:AllowedOrigins`, `App:FrontendBaseUrl` y los tiempos de vida del
token— ya viene en `appsettings.Local.example.json`. **No existe ningún `appsettings.json`**
en este repositorio.

### Configuración local no sensible

```bash
cp appsettings.Local.example.json appsettings.Local.json
```

Ese archivo no se versiona, pero **no debe contener secretos**: solo orígenes CORS, niveles
de log, tiempos de vida del token, host y puerto SMTP, y las URLs de callback OAuth.

### Ver qué secretos hay configurados

```bash
dotnet user-secrets list        # imprime los valores: no lo pegues en ningún sitio
```

---

## Rotar credenciales

Rotar una clave son siempre dos pasos: **generarla donde vive** y **guardarla en
user-secrets**. Ninguna de las dos mitades sirve sola, y sustituir el valor local sin
invalidar el antiguo en el proveedor no es una rotación.

### Las que puedes generar tú

No dependen de ningún tercero. **Genéralas sin que el valor pase por pantalla**: lo que se
imprime en la terminal queda en el búfer y en el historial, y lo que se pega en un chat o
en un ticket deja de ser un secreto.

```powershell
# Secreto JWT (mínimo 32 bytes; el arranque falla si es más corto)
$bytes = New-Object byte[] 48
(New-Object System.Security.Cryptography.RNGCryptoServiceProvider).GetBytes($bytes)
dotnet user-secrets set "Jwt:Secret" ([Convert]::ToBase64String($bytes))
Remove-Variable bytes
```

```powershell
# Contraseña de PostgreSQL local: cámbiala en .env, recrea el contenedor
# y actualiza la cadena de conexión. Los datos locales se pierden.
docker compose down -v
docker compose up -d
dotnet user-secrets set "ConnectionStrings:DefaultConnection" 'Host=localhost;Port=5433;Database=trackerMultimedia;Username=postgres;Password=<nueva>;'
```

Rotar `Jwt:Secret` invalida todos los access token en circulación: las sesiones abiertas se
caen y hay que volver a entrar. Es el comportamiento correcto.

> ⚠️ **En PowerShell, usa comillas simples para los valores secretos.** Entre comillas
> dobles, PowerShell expande todo lo que empiece por `$`: un secreto como
> `$7kTbEi...` se interpreta como una variable inexistente, se sustituye por cadena vacía
> y `dotnet` responde `Missing parameter value for 'value'`. Con comillas simples el texto
> va literal. La cadena de conexión también lleva simples, porque contiene `;`.

### Las que solo puede rotar su proveedor

Para cada una: **primero** genera la nueva en el panel, **después** guárdala aquí, y **por
último** revoca la antigua.

| Credencial | Dónde se rota |
|---|---|
| Contraseña de Neon | Panel de Neon → *Roles* → `neondb_owner` → *Reset password*. Actualiza también `ConnectionStrings__DefaultConnection` en Render. |
| `OAuth:Google:ClientSecret` | Google Cloud → *APIs & Services* → *Credentials* → tu OAuth Client → *Add secret*, y borra el anterior. |
| `OAuth:GitHub:ClientSecret` | GitHub → *Settings* → *Developer settings* → *OAuth Apps* → *Generate a new client secret*. |
| `Smtp:Password` | Mailtrap → *Email Testing* → *SMTP Settings* → regenerar credenciales. |

```powershell
dotnet user-secrets set "OAuth:Google:ClientSecret" '<nuevo>'
dotnet user-secrets set "OAuth:GitHub:ClientSecret" '<nuevo>'
dotnet user-secrets set "Smtp:Password"             '<nuevo>'
```

Los *Client ID* de Google y GitHub **no son secretos**: son identificadores públicos que el
navegador ve en la URL de autorización. Están en user-secrets por comodidad, no porque haga
falta ocultarlos; no requieren rotación.

---

## Base de datos

### PostgreSQL local con Docker

La base de desarrollo se levanta con `docker-compose.yml`, que está en la raíz de este
repositorio. La contraseña la lee de `.env`, que **no** se versiona: copia `.env.example`
a `.env` y pon una propia antes del primer arranque.

```bash
cp .env.example .env         # y rellena POSTGRES_PASSWORD
docker compose up -d         # levanta PostgreSQL en localhost:5433
docker compose ps            # debe decir "healthy"
```

Después, la cadena de conexión va a los secretos, nunca a un archivo del repositorio:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" 'Host=localhost;Port=5433;Database=trackerMultimedia;Username=postgres;Password=<la-de-tu-.env>;'
```

Detalles que importan:

- **El puerto es 5433, no 5432**, para no chocar con un PostgreSQL instalado nativamente.
- El contenedor **solo escucha en `127.0.0.1`**: no queda expuesto a la red local.
- Los datos viven en un volumen con nombre (`trackermultimedia-postgres-data`), así que
  `docker compose down` los conserva. Para empezar de cero: `docker compose down -v`.
- La imagen está fijada a **`postgres:17-alpine`**. *Pendiente de verificación:* si Neon
  usa otra versión mayor, conviene igualarla aquí para que el entorno local reproduzca
  producción.

### Migraciones

```bash
# Aplicar todas las migraciones pendientes
dotnet ef database update

# Crear una nueva migración
dotnet ef migrations add <NombreMigracion>

# Revertir la última migración (solo si NO está aplicada en ninguna base)
dotnet ef migrations remove

# Ver qué migraciones existen y cuáles están aplicadas
dotnet ef migrations list
```

> ⚠️ **Una migración sin su `.Designer.cs` no existe para EF Core.** EF identifica las
> migraciones por el atributo `[Migration("...")]`, que vive en el archivo `.Designer.cs`,
> **no** por el nombre del archivo. Una migración escrita a mano sin ese archivo se
> compila, se ve en la carpeta y no se ejecuta jamás: `dotnet ef migrations list` no la
> menciona y `database update` informa de que no hay nada pendiente. Esto ya pasó una vez
> y dejó el esquema incompleto en cualquier base creada desde cero. **Crea siempre las
> migraciones con `dotnet ef migrations add`**, nunca copiando un archivo a mano, y
> comprueba con `migrations list` que la nueva aparece.

### Comprobar que el esquema es correcto desde cero

La forma segura de validar una migración es levantar una base vacía y aplicarlas todas,
que es justo lo que Docker hace fácil:

```bash
docker compose down -v && docker compose up -d
dotnet ef database update
dotnet ef migrations list      # las aplicadas salen sin la marca (Pending)
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

La suite vive en `TrackerMultimedia.Tests/`, dentro de este mismo repositorio, y se
abre junto al backend desde `TrackerMultimedia_Backend.slnx`. Un clon limpio basta
para ejecutarla:

```bash
dotnet test TrackerMultimedia_Backend.slnx
```

Son pruebas de integración sobre `WebApplicationFactory` con SQLite en memoria: no
necesitan PostgreSQL ni ningún secreto configurado.

Los archivos fuente de pruebas forman parte del repositorio, pero sus salidas generadas no.

- El `.gitignore` excluye resultados locales como `TestResults/`, `coverage/`, `*.trx`, `*.coverage` y reportes equivalentes.
- Si ejecutas pruebas o cobertura en local, esos artefactos deben quedarse fuera del control de versiones.

> El proyecto de tests está anidado dentro de la carpeta del proyecto de backend, así
> que el `.csproj` del backend lo excluye de sus globs con `DefaultItemExcludes`. Si
> alguna vez se renombra la carpeta, hay que actualizar también esa propiedad y el
> `.dockerignore`, o el backend intentará compilar los tests como código propio.

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
├── TrackerMultimedia.Tests/  # Suite de integración (xUnit + WebApplicationFactory)
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
├── appsettings.Local.example.json # Plantilla local no versionada
├── Dockerfile          # Imagen de runtime usada por Render
├── render.yaml         # Blueprint de despliegue
└── Program.cs          # Composición de servicios y middleware
```

---

## Despliegue en Render — *inactivo*

> ⚠️ **No hay despliegue.** Desde el 2026-08-27 el proyecto se usa **solo en local**: los
> servicios de Render, Neon y Netlify están deshabilitados y sus credenciales, revocadas.
> Esta sección se conserva como receta para volver a desplegar, no como descripción de algo
> que exista hoy. Si la reactivas, **todas las variables hay que crearlas de cero**: las que
> había ya no sirven.

El blueprint `render.yaml` vive en la raíz de este repositorio y describe el servicio
completo: runtime Docker, `Dockerfile` y contexto de build en la raíz, health check en
`/health` y despliegue automático en cada commit. Render lo detecta al conectar el
repositorio; no hay que rellenar rutas a mano.

### Variables que hay que dar en el panel

El blueprint las declara como `sync: false`, es decir, Render las pide sin valor por
defecto porque son secretas o dependen del entorno:

- `ConnectionStrings__DefaultConnection` — cadena de conexión de Neon.
- `App__FrontendBaseUrl` — URL pública del frontend en Netlify. La usan los enlaces de
  los correos de confirmación y de recuperación de contraseña.
- `Cors__AllowedOrigins__0` — la misma URL pública del frontend.

`Jwt__Secret` la genera Render automáticamente (`generateValue: true`); no hay que
inventarla. El backend se niega a arrancar si mide menos de 32 bytes.

### Variables opcionales

Correo (sin ellas el envío queda desactivado y el registro sigue funcionando):
`Smtp__Enabled`, `Smtp__Host`, `Smtp__Port`, `Smtp__Username`, `Smtp__Password`,
`Smtp__FromAddress`, `Smtp__FromName`.

OAuth, un bloque por proveedor: `OAuth__Google__Enabled`, `OAuth__Google__ClientId`,
`OAuth__Google__ClientSecret`, `OAuth__Google__RedirectUri` y sus equivalentes
`OAuth__GitHub__*`. La *redirect URI* de producción apunta a este backend, no al
frontend: `https://<backend>.onrender.com/api/auth/<proveedor>/callback`. Hay que
registrarla también en Google Cloud y en GitHub, o el proveedor rechazará el flujo.

### Notas de producción

- `Database__ApplyMigrationsOnStartup=true` ya viene en el blueprint: el esquema se
  crea y se actualiza al arrancar, y el log de arranque dice qué migraciones aplicó.
  Con varias instancias en paralelo esto es una carrera; hoy el plan es de una sola.
- El backend confía en las cabeceras `X-Forwarded-*` del proxy HTTPS de Render.
- Si añades un dominio propio en Netlify, súmalo como `Cors__AllowedOrigins__1` y
  actualiza `App__FrontendBaseUrl`.
