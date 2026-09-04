# Arquitectura

> Qué es el producto y cómo está montado. El *porqué* de cada decisión está en
> [DECISIONS.md](DECISIONS.md); lo que falta, en [ROADMAP.md](ROADMAP.md).

## Qué es

TrackerMultimedia es una aplicación web personal para llevar la cuenta de lo que uno ve, lee o
juega: anime, manga, manhwa, series, películas, libros, cómics, videojuegos, podcasts y álbumes.
Cada elemento guarda su estado de seguimiento, el progreso (en episodios, capítulos, páginas,
horas o pistas según el tipo), una puntuación personal, notas y las fechas de inicio y fin.

Además del alta manual, permite buscar en catálogos externos —Jikan/MyAnimeList, AniList y
MangaDex— y añadir el resultado con sus metadatos ya rellenos. El usuario organiza su colección
con categorías y formatos que él mismo define, y puede exportar e importar toda la biblioteca en
JSON o CSV, además de descargarse todos sus datos personales.

## Qué queda fuera, y por qué

- **Sincronización con las listas de MyAnimeList o AniList.** Se usan como fuente de metadatos,
  no como destino. Sincronizar en ambos sentidos exige el OAuth de cada catálogo, resolver
  conflictos y seguir sus APIs: es otro producto.
- **Funcionalidad social.** Sin perfiles públicos, listas compartidas, seguidores ni comentarios.
  Cambiaría el modelo de datos, el de permisos y las obligaciones legales.
- **Aplicación móvil nativa.** La interfaz web es adaptable y se usa desde el móvil.
- **Multiusuario con roles.** `IdentityRole` está registrado pero no se usa: no hay administración
  ni permisos diferenciados.
- **Reproducción o alojamiento de contenido.** Solo referencias, metadatos y enlaces a fichas externas.

---

## Los dos repositorios

En local conviven bajo una carpeta contenedora que **no es un repositorio git** y en la que no
debe quedarse nada que tenga que sobrevivir:

```text
TrackerMultimedia/            # Carpeta contenedora. NO es un repositorio
├── Backend/                  # Clon de xfiberex/TrackerMultimedia_Backend
└── Frontend/                 # Clon de xfiberex/TrackerMultimedia_Frontend
```

Cada repositorio es autosuficiente: la suite de pruebas del backend vive dentro del repositorio
de backend, y cada blueprint de despliegue (`render.yaml`, `Frontend/netlify.toml`) vive en la
raíz del repositorio que describe, con rutas relativas a ella.

## Backend

Raíz del repositorio = raíz del proyecto .NET.

```text
Program.cs                    # Composición completa: CORS, rate limiting, Identity, JWT,
                              #   proveedores de búsqueda, cabeceras de seguridad, middleware,
                              #   sondas de salud y migraciones al arrancar
Controllers/                  # Solo traducen HTTP <-> servicios. Sin lógica de negocio
├── AuthController.cs         #   Registro, login, refresh, logout, perfil, contraseña,
│                             #     confirmación, borrado de cuenta y exportación personal
├── OAuthController.cs        #   init / callback / link-confirm de Google y GitHub
├── MediaItemsController.cs   #   CRUD, importación y exportación de biblioteca
├── CategoriesController.cs · FormatsController.cs
└── SearchController.cs       #   Búsqueda federada en catálogos externos
Services/                     # Toda la lógica de negocio
├── MediaItemsService.cs      #   El más grande: consultas, CRUD, import/export JSON y CSV,
│                             #     normalización y validación de dominio
├── AuthSessionService.cs     #   Único punto que emite una sesión (JWT + refresh)
├── TokenService.cs           #   Genera el JWT y el refresh; hashea el refresh con SHA-256
├── ExternalCatalogSearchService.cs  # Abanico sobre los proveedores; el fallo de uno no tumba al resto
├── JikanSearchService.cs · AniListSearchService.cs · MangaDexSearchService.cs
├── GoogleAuthService.cs · GitHubAuthService.cs
├── ExpiredDataCleaner.cs · ExpiredDataCleanupService.cs   # Purga de tokens y states caducados
└── SmtpEmailService.cs       #   MailKit. Con `Smtp:Enabled=false` solo escribe en el log
Domain/Entities/              # ApplicationUser, MediaItem, UserCategory, UserFormat,
                              #   MediaItemCategory, RefreshToken, OAuthState
Domain/Enums/                 # ContentKind, MediaType (legado), ProgressUnit, estados, orden…
Domain/Validation/            # HttpOrHttpsUrlAttribute
Contracts/                    # DTOs de entrada y salida, con anotaciones de validación
Infrastructure/
├── Http/                     #   ClaimsPrincipalExtensions: lectura del claim `sub`
├── Logging/                  #   PersonalData: enmascarado de datos personales en el log
└── Options/                  #   SmtpOptions, OAuthOptions, CleanupOptions
Data/ApplicationDbContext.cs  # Mapeo, índices y relaciones
Migrations/                   # 3 migraciones EF, aplicadas al arrancar desde el 2026-08-27
docs/                         # Esta documentación, común a los dos repositorios
TrackerMultimedia.Tests/      # 165 tests sobre PostgreSQL real
├── Helpers/TestDatabase.cs   #   Una base desechable por clase, copiada de una plantilla
├── Helpers/AppFactory.cs     #   WebApplicationFactory: desactiva el rate limiter y sustituye
│                             #     el correo por un buzón de prueba
├── Auth/                     #   Registro, login, refresh, recuperación, OAuth, cuenta, exportación
├── Authorization/            #   Qué endpoints exigen token
├── Isolation/                #   Que un usuario no ve ni toca datos de otro. La red clave
├── MediaItems/ · Categories/ · Formats/ · Search/
├── Operations/               #   Sondas de salud y publicación de la especificación OpenAPI
└── Services/                 #   Jikan, AniList, MangaDex, Google, GitHub, correo y purga
Dockerfile · render.yaml      # Despliegue, inactivo. Se conservan como receta para volver
```

El `.csproj` está en la raíz del repositorio, así que sus globs por defecto se tragarían los
archivos de test: `DefaultItemExcludes` en `TrackerMultimedia_Backend.csproj:12` los excluye, y
el `.dockerignore` repite la exclusión. **Si se renombra la carpeta de tests hay que cambiar las dos.**

## Frontend

Raíz del repositorio = raíz del proyecto de Vite.

```text
netlify.toml                  # Blueprint de Netlify, inactivo
index.html                    # Contiene la CSP como <meta> y el script de tema del <head>
public/                       # robots.txt y sitemap.xml
src/
├── router.tsx                # Rutas públicas y protegidas por AuthGuard
├── config/env.ts             # Valida las variables de entorno con Zod al importarse
├── features/                 # Organización por dominio: api/ schemas/ views/ components/
│   ├── auth/                 #   Contexto de sesión, vistas de acceso y perfil
│   ├── media-items/          #   Biblioteca: la vista más grande de la aplicación
│   ├── categories/ · catalog/#   Gestión de categorías y de formatos
│   └── search/               #   Descubrimiento en catálogos externos
├── layouts/AppLayout.tsx     # Cabecera, navegación y conmutador de tema
└── shared/
    ├── api/axios.ts          # Interceptores: adjunta el token y renueva la sesión ante un 401
    ├── api/tokenStore.ts     # Access token en memoria de módulo, fuera del árbol de React
    ├── components/           # SidePanelDialog, ConfirmDialog, ToastProvider, EmptyState, Loader
    └── hooks/ · utils/       # useModalDialog, useDarkMode…
```

---

## Lo que está sólido

El aislamiento entre usuarios, la emisión de sesiones centralizada, la validación de entrada, el
escapado de las consultas y la organización por features. Todo ello con pruebas dedicadas.

## Lo que sigue roto o sin verificar

1. **Sin textos legales.** El borrado de cuenta y la exportación personal **sí están hechos**. La
   política de privacidad y el aviso legal no existen (T0-05, en suspenso): vuelven con su
   severidad original el día que la aplicación sea accesible para alguien más.
2. **Nada ejecuta las suites salvo la disciplina de ejecutarlas.** Consecuencia asumida de
   descartar la CI. Las suites llegaron a estar en rojo mucho tiempo sin que nadie lo notara.
3. **`X-Forwarded-For` se acepta de cualquier origen** (T1-05). Inofensivo en local; grave detrás
   de un proxy. Es de lo primero que hay que resolver antes de volver a publicar.
4. **El esquema que tenía Neon no se verificó y ya no es verificable.** Si conservaba la columna
   `UserFormatId` sin la fila correspondiente en `__EFMigrationsHistory`, un despliegue nuevo
   fallará al intentar crearla y habrá que insertar esa fila a mano.
5. **Los binarios siguen en el historial de git.** Salieron del índice (T1-20), así que dejan de
   crecer, pero los commits anteriores los conservan: el clon sigue pesando lo mismo. Limpiarlo
   exige reescribir el historial con `git filter-repo`, que no se ha hecho.
