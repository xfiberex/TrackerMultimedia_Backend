# CONTEXT — TrackerMultimedia

> Qué se decidió, por qué, y qué se aprendió. Este archivo existe para que cualquiera —tú dentro de seis meses, u otra sesión de trabajo desde cero— pueda retomar el proyecto sin haber estado en las conversaciones anteriores.
>
> **Qué falta por hacer:** [ROADMAP.md](ROADMAP.md) · **Qué cambió en cada versión:** [CHANGELOG.md](CHANGELOG.md)

## Reglas de mantenimiento de este archivo

- Se actualiza **en el mismo commit** que el cambio que documenta, para que el contexto viaje con el código.
- **Las fechas son siempre absolutas.** Nunca "ayer", "la semana pasada" ni "hace poco".
- Si una decisión se revierte, **no se borra**: se marca como superada, con la fecha y el hecho nuevo que la cambió.
- Ante la duda entre este archivo y `CHANGELOG.md`: el **qué** va al changelog, el **por qué** va aquí.
- Cada sesión de trabajo relevante añade una entrada al registro del final.

---

## 1. Estado

| | |
|---|---|
| **Alcance** | Uso **personal y local**. No hay servicio público ni usuarios ajenos al propietario. |
| **Repositorios** | Dos independientes: [xfiberex/TrackerMultimedia_Backend](https://github.com/xfiberex/TrackerMultimedia_Backend) y [xfiberex/TrackerMultimedia_Frontend](https://github.com/xfiberex/TrackerMultimedia_Frontend). **La carpeta raíz del monorepo no es un repositorio git.** |
| **Versión publicada** | Ninguna etiquetada. `package.json` declara `1.0.0`; no hay tags ni releases en ninguno de los dos repositorios. |
| **Stack backend** | .NET 10 (`net10.0`), ASP.NET Core, ASP.NET Identity, JWT Bearer, EF Core 10 + Npgsql 10, MailKit 4.16 |
| **Stack frontend** | React 19.2, TypeScript ~6.0, Vite 8, React Router 7, TanStack Query 5, Axios, Zod 4, CSS propio (sin framework de UI) |
| **Base de datos** | PostgreSQL 17 **instalado en la máquina**, puerto **5433**. En Windows, servicio `postgresql-x64-17`. Neon quedó deshabilitado. |
| **Despliegue** | **Ninguno.** Render, Neon y Netlify deshabilitados el 2026-08-27. Uso local a través del servidor de Vite. Los blueprints se conservan como receta para volver. |
| **Pruebas** | Backend: 165 (xUnit + `WebApplicationFactory` sobre **PostgreSQL real**, una base desechable por clase) — **165 pasan**. Frontend: 165 (Vitest + Testing Library) — **165 pasan**. Ambas en verde. |
| **CI** | **No la habrá.** Decisión del 2026-08-27: proyecto de un solo desarrollador, verificación en local antes de cada commit (T1-12 anulada). |
| **Estado** | Funcional, en uso local. **79 de las 88 tareas del roadmap cerradas**; cerrados los Tiers 0, 2 y 3, y del 1 solo queda T1-05. Lo legal (T0-05) queda en suspenso mientras no vuelva a publicarse. |
| **Última actualización** | 2026-09-04 |

---

## 2. Qué es

TrackerMultimedia es una aplicación web personal para llevar la cuenta de lo que uno ve, lee o juega: anime, manga, manhwa, series, películas, libros, cómics, videojuegos, podcasts y álbumes. Cada elemento guarda su estado de seguimiento, el progreso (en episodios, capítulos, páginas, horas o pistas según el tipo), una puntuación personal, notas y las fechas de inicio y fin. Además de la alta manual, permite buscar en catálogos externos —Jikan/MyAnimeList, AniList y MangaDex— y añadir el resultado a la biblioteca con sus metadatos ya rellenos. El usuario organiza su colección con categorías y formatos que él mismo define, y puede exportar e importar toda la biblioteca en JSON o CSV.

**Lo que deliberadamente no hace:** no es una red social (no hay perfiles públicos, ni seguidores, ni comentarios, ni listas compartidas), no reproduce ni aloja contenido, no recomienda automáticamente, y no sincroniza con las listas de MyAnimeList o AniList: las usa solo como fuente de metadatos para el alta.

---

## 3. Arquitectura

```text
TrackerMultimedia/                        # Carpeta contenedora. NO es un repositorio git.
├── README.md                             # ⚠ No versionado. Solo explica que esta carpeta es un contenedor
├── ROADMAP.md · CHANGELOG.md · CONTEXT.md # ⚠ No versionados. Cubren los dos repositorios a la vez
│
├── TrackerMultimedia_Backend/            # Repositorio git propio. Su raíz ES la raíz del proyecto .NET
│   ├── render.yaml                       # Blueprint de Render, con rutas relativas a esta raíz
│   ├── TrackerMultimedia_Backend.slnx    # Abre el backend y los tests juntos
│   ├── TrackerMultimedia.Tests/          # 105 tests, dentro del repositorio desde el 2026-08-27 (T0-02)
│   │   ├── Helpers/AppFactory.cs         #   WebApplicationFactory: sustituye Npgsql por SQLite en memoria,
│   │   │                                 #     desactiva el rate limiter y sustituye el email por un buzón de prueba
│   │   ├── Auth/                         #   Registro, login, refresh, recuperación, OAuth, gestión de cuenta
│   │   ├── Authorization/                #   Qué endpoints exigen token
│   │   ├── Isolation/                    #   Que un usuario no ve ni toca datos de otro. La red de seguridad clave
│   │   ├── MediaItems/ · Categories/ · Search/
│   │   └── Services/                     #   Jikan, Google, GitHub y correo, con handler HTTP simulado
│   ├── Program.cs                        # Composición completa: CORS, rate limiting, Identity, JWT,
│   │                                     #   proveedores de búsqueda, cabeceras de seguridad y middleware
│   ├── Controllers/                      # Solo traducen HTTP ↔ servicios. Sin lógica de negocio
│   │   ├── AuthController.cs             #   Registro, login, refresh, logout, perfil, contraseña, confirmación
│   │   ├── OAuthController.cs            #   init / callback / link-confirm de Google y GitHub
│   │   ├── MediaItemsController.cs       #   CRUD, estadísticas, importación y exportación
│   │   ├── CategoriesController.cs · FormatsController.cs
│   │   └── SearchController.cs           #   Búsqueda federada en catálogos externos
│   ├── Services/                         # Toda la lógica de negocio
│   │   ├── MediaItemsService.cs          #   1.600 líneas: consultas, CRUD, import/export JSON y CSV,
│   │   │                                 #   normalización y validación de dominio (candidato a dividir, T3-11)
│   │   ├── AuthSessionService.cs         #   Único punto que emite una sesión (JWT + refresh), venga de donde venga
│   │   ├── TokenService.cs               #   Genera el JWT y el refresh token; hashea el refresh con SHA-256
│   │   ├── ExternalCatalogSearchService.cs #  Abanico sobre los proveedores; el fallo de uno no tumba al resto
│   │   ├── JikanSearchService.cs · AniListSearchService.cs · MangaDexSearchService.cs
│   │   ├── GoogleAuthService.cs · GitHubAuthService.cs
│   │   └── SmtpEmailService.cs           #   MailKit. Con `Smtp:Enabled=false` solo escribe en el log
│   ├── Domain/Entities/                  # ApplicationUser, MediaItem, UserCategory, UserFormat,
│   │                                     #   MediaItemCategory, RefreshToken, OAuthState
│   ├── Domain/Enums/                     # ContentKind, MediaType (legado), ProgressUnit, estados, orden…
│   ├── Contracts/                        # DTOs de entrada y salida, con anotaciones de validación
│   ├── Data/ApplicationDbContext.cs      # Mapeo, índices y relaciones
│   ├── Migrations/                       # 4 migraciones EF, aplicadas al arrancar desde el 2026-08-27
│   ├── Dockerfile                        # Build multi-etapa; runtime con usuario sin privilegios
│   └── artifacts/ · .vs/                 # ⚠ 229 binarios y cachés versionados por error (T1-20)
│
└── TrackerMultimedia_Frontend/           # Repositorio git propio. Su raíz ES la raíz del proyecto de Vite
    ├── netlify.toml                      # Blueprint de Netlify, sin `base` porque no hay carpeta intermedia
    ├── index.html                        # Contiene la CSP como <meta> (T2-14)
    └── src/
        ├── router.tsx                    # Rutas públicas y protegidas por AuthGuard
        ├── config/env.ts                 # Valida las variables de entorno con Zod al importarse
        ├── features/                     # Organización por dominio, cada uno con api/ schemas/ views/ components/
        │   ├── auth/                     #   Contexto de sesión, 9 vistas, esquemas Zod
        │   ├── media-items/              #   Biblioteca: la vista más grande de la aplicación
        │   ├── categories/ · catalog/    #   Gestión de categorías y de formatos
        │   └── search/                   #   Descubrimiento en catálogos externos
        ├── layouts/AppLayout.tsx         # Cabecera, navegación y conmutador de tema
        └── shared/
            ├── api/axios.ts              # Interceptores: adjunta el token y renueva la sesión al recibir 401
            ├── api/tokenStore.ts         # Access token en memoria de módulo, fuera del árbol de React
            ├── components/               # SidePanelDialog, ConfirmDialog, ToastProvider, EmptyState, Loader
            └── hooks/ · utils/
```

---

## 4. Estado actual

La aplicación **no está desplegada** desde el 2026-08-27 y es funcional en local. La primera auditoría (2026-08-27) encontró 80 elementos accionables, y lo que cambia el orden de trabajo es el Tier 0:

**Corregido el 2026-08-27** (detalle en el registro de sesión): migraciones restauradas,
FK sombra eliminada, enumeración de cuentas cerrada, índices creados, dependencias
vulnerables actualizadas, las dos suites en verde y las barreras de accesibilidad más
graves resueltas.

**Corregido el 2026-08-27, segunda tanda:** decidida la estructura de repositorios —**dos
independientes**— y aplicadas sus consecuencias. La suite de pruebas vive dentro del
repositorio de backend y los blueprints de despliegue, en la raíz del repositorio que
describen (T0-02, T0-03).

**Lo que sigue roto:**

1. **Sin textos legales.** El borrado de cuenta (T1-14) y la exportación de datos personales (T4-08) **sí están hechos**, aunque habían dejado de ser obligatorios al no haber servicio público. La política de privacidad y el aviso legal siguen sin existir (T0-05, en suspenso): el día que la aplicación vuelva a estar accesible para alguien más, vuelve con su severidad original, y si el contenido de la exportación cumple con la normativa aplicable **requiere revisión legal**.
2. **Nada ejecuta las suites salvo la disciplina de ejecutarlas.** No es un defecto pendiente sino una consecuencia asumida de descartar la CI (T1-12), pero conviene tenerlo delante: las suites llevaban tiempo en rojo sin que nadie lo notara, y lo que lo evita a partir de ahora es la rutina de verificación previa al commit, no una comprobación automática.
3. **Sin verificar y ya no verificable: el esquema que tenía Neon.** T0-06 arregló la migración que faltaba, pero la base de producción se deshabilitó sin mirarla. Si conservaba la columna `UserFormatId` sin que constara en `__EFMigrationsHistory`, un despliegue nuevo fallará al intentar crearla y habrá que insertar la fila del historial a mano en vez de ejecutar el `Up()`. Apuntado por si algún día se recupera esa base.
4. **Los 227 archivos de build siguen en el historial de git.** T1-20 los sacó del índice, así que dejan de crecer, pero los commits anteriores los conservan: el repositorio sigue pesando lo mismo al clonarlo. Limpiarlo de verdad exige reescribir el historial (`git filter-repo`), que es una operación con consecuencias y no se ha hecho.

**Antes de tocar `Program.cs` en producción:** comprobar qué migraciones están realmente
aplicadas en Neon. Ahora que `MigrateAsync()` vuelve a ejecutarse, un esquema creado a
mano o con `EnsureCreated` (sin tabla `__EFMigrationsHistory`) hará que la migración
falle al intentar crear tablas que ya existen. El fallo queda registrado como error y no
tumba el arranque, pero el esquema se quedaría sin actualizar.

**Lo que está sólido:** el aislamiento entre usuarios, la emisión de sesiones centralizada, la validación de entrada, el escapado de las consultas y la organización por features. Detalle en el informe de auditoría.

---

## 5. Decisiones y convenciones clave

### Sesión y tokens

**Access token en memoria, refresh token en `localStorage`.** El access token vive en una variable de módulo (`shared/api/tokenStore.ts`) fuera del árbol de React, para que el interceptor de Axios pueda leerlo sin depender del ciclo de vida de los componentes. Nunca se persiste: un XSS no puede extraerlo del almacenamiento. El refresh token sí se persiste, porque sin él la sesión se perdería en cada recarga. Se aceptó el riesgo de forma consciente y la alternativa correcta —cookie `httpOnly`— está registrada como T4-01, no descartada.

**No hay ninguna mitigación de cookie sobre el refresh token.** El comentario de `tokenStore.ts` llegó a afirmar que estaba protegido "con `SameSite=Strict`"; era falso, porque `SameSite` es un atributo de cookie y no existe en `localStorage`. Corregido en T2-13. Las defensas reales son las tres del servidor: hash, rotación en cada uso y revocación de toda la familia ante reutilización.

**El servidor solo guarda el hash del refresh token.** Se almacena el SHA-256 en base64, nunca el valor en claro, y se rota en cada uso: el token consumido se marca como revocado y se emite uno nuevo. Presentar uno ya rotado revoca **todas** las sesiones del usuario (T2-12, cerrada).

**Una sola puerta de emisión de sesiones.** `AuthSessionService.CreateSessionAsync` es el único sitio que emite un par de tokens, vengan de login manual, de Google o de GitHub. Se hizo así porque con tres caminos duplicando la lógica era cuestión de tiempo que uno se olvidara de persistir o de rotar. **No reabrir**: cualquier camino de acceso nuevo debe pasar por ahí.

**Los tokens de OAuth vuelven al frontend en el fragmento de la URL (`#`), no en la query.** El fragmento no se envía al servidor, así que no acaba en los logs de acceso de Render ni en el `Referer`. Se descartó pasarlos por query string precisamente por eso. `OAuthCallbackView` los lee de `window.location.hash` al montarse y luego navega con `replace`.

### OAuth

**Vinculación explícita, nunca automática.** Cuando alguien entra con Google o GitHub y ese correo ya tiene una cuenta local con contraseña, el backend **no** vincula sin más: genera un token de vinculación de vida corta y manda al usuario a `/link-account`, donde debe escribir su contraseña. El problema real que lo motivó: sin ese paso, cualquiera que registre una cuenta en un proveedor externo con el correo de otra persona se apodera de su cuenta. **No reabrir.**

**Se exige correo verificado por el proveedor.** GitHub obliga a que el email sea primario *y* verificado (`GitHubAuthService.cs:70-83`) y Google comprueba la bandera `email_verified`. Un proveedor que no pueda garantizarlo no debe integrarse por esta vía.

### Datos

**Todo pertenece a un usuario y toda consulta lo filtra.** `MediaItem`, `UserCategory` y `UserFormat` llevan `UserId`, y cada consulta de lectura o escritura incluye `Where(x => x.UserId == userId)` — incluida la resolución de categorías y formatos al guardar, para que nadie pueda asignar una categoría ajena mandando su identificador. Hay una carpeta de pruebas dedicada (`Isolation/`) que lo verifica endpoint por endpoint. **Es la invariante más importante del sistema: cualquier consulta nueva la respeta o no entra.**

**`MediaType` es legado; `ContentKind` es lo vigente.** `MediaType` (Anime, Manga, Donghua, Manhwa, Manhua) fue el primer modelo y quedó atado a datos ya creados. `ContentKind` (Series, Movie, Book, Comic, Game, Podcast, Video, Album, Other) lo sustituye con un vocabulario que cubre todo el catálogo. `ResolveContentKind` acepta ambos, los mapea y rechaza las combinaciones incoherentes. No se eliminó `MediaType` para no romper los elementos existentes ni las exportaciones ya generadas.

**Los formatos por defecto se crean al dar de alta la cuenta, nunca al leerlos.** `FormatsService.EnsureDefaultFormatsAsync` es idempotente y lo llaman los dos únicos caminos que crean usuarios: `AuthController.Register` y el caso B del callback de OAuth. Antes sembraba `GET /api/formats`, lo que hacía escribir a un GET y, con dos peticiones simultáneas de una cuenta nueva, la segunda violaba el índice único y devolvía 500 (T2-20). **Consecuencia a tener presente**: cualquier camino nuevo que cree usuarios —incluidos los helpers de test, que van por `UserManager` directamente— tiene que llamar a ese método o la cuenta se queda sin formatos.

**El identificador externo es único por usuario, origen y tipo de medio.** El índice único es parcial (solo cuando hay identificador externo) para que los elementos manuales no colisionen entre sí. Se amplió a `SourceType` el 2026-05-14 al añadir AniList y MangaDex: el mismo número de identificador significa cosas distintas en cada catálogo.

### Frontend

**Organización por features, no por tipo de archivo.** Cada dominio (`auth`, `media-items`, `categories`, `catalog`, `search`) contiene su propia `api/`, `schemas/`, `views/` y `components/`. Lo compartido de verdad vive en `shared/`. La alternativa —carpetas `components/`, `hooks/`, `api/` globales— se descartó porque obliga a saltar entre cuatro carpetas para tocar una sola funcionalidad.

**Las respuestas de la API se validan con Zod.** No se confía en la forma de la respuesta: cada endpoint tiene su esquema. Sirve para detectar en el cliente los cambios de contrato del backend en lugar de fallar más tarde con un `undefined` en mitad del renderizado.

**Toda la aplicación está en español, sin capa de internacionalización.** Es una decisión consciente para un producto de un solo idioma. Si alguna vez se añade un segundo idioma, hay que extraer todos los textos y también corregir `formatDate`, que fija la configuración regional `es-DO` en vez de la del usuario (T4-03).

### Trampas del stack aprendidas a base de fallo

- **La navegación `ExternalLogins` en `ApplicationUser` creó una segunda clave foránea.** `IdentityDbContext` ya configura la relación de `IdentityUserLogin` sin propiedad de navegación. Al declarar `ICollection<IdentityUserLogin<Guid>> ExternalLogins` sin configurarla explícitamente, EF Core la interpretó como una relación **adicional** y generó la columna sombra `ApplicationUserId` junto a la `UserId` de Identity. Como `UserManager.AddLoginAsync` solo rellena `UserId`, la columna nueva queda siempre a NULL y la navegación nunca devuelve nada: por eso el perfil nunca muestra las cuentas vinculadas. Está en la migración inicial, línea 127. Lección: al añadir una navegación sobre una entidad que Identity ya mapea, hay que configurarla a mano en `OnModelCreating` (T1-01).
- **`WebApplicationFactory` lee el secreto JWT antes de aplicar los overrides de configuración.** `Program.cs` guarda `jwtSecret` en una variable local durante el arranque, momento en el que el factory de tests todavía no ha inyectado su configuración, así que la clave de validación se quedaba con el secreto de user-secrets. La solución vigente está en `AppFactory.cs:135` con un `PostConfigure<JwtBearerOptions>`. Si algún día el arranque se refactoriza para leer el secreto de `IOptions`, ese parche puede desaparecer.
- **El rate limiter agota su cupo en los tests.** `TestServer` no asigna `RemoteIpAddress`, así que todas las peticiones caen en la misma partición `"unknown"` y el límite de 10/minuto de los endpoints de autenticación salta a mitad de la suite. `AppFactory` sustituye las tres políticas por limitadores sin límite.
- **Un índice parcial de PostgreSQL no sirve para consultas generales.** El único índice que empieza por `MediaItems.UserId` tiene un filtro (`ExternalId IS NOT NULL AND ...`), y EF Core dio por buena la cobertura de la clave foránea y no creó otro. PostgreSQL no usa un índice parcial salvo que el predicado de la consulta implique el filtro, así que en la práctica **no hay índice** para la consulta más frecuente de la aplicación (T1-07).
- **`EF.Functions.ILike` solo existe en Npgsql.** La búsqueda de la biblioteca lo usa, y mientras los tests corrieron sobre SQLite esa ruta devolvía 500 en la suite y no podía tener ni una prueba. **Resuelto:** desde T2-02 la suite corre sobre PostgreSQL real. Se deja escrito porque la lección es general: un proveedor de test distinto del de producción no prueba nada específico del proveedor (T2-02, T2-04).
- **`EnsureCreated()` no ejecuta las migraciones.** Construye el esquema directamente desde el modelo. Esa es la razón por la que ningún test detectó que `Migrate()` estaba comentado en producción (T2-03).
- **`Microsoft.OpenApi` no puede pasar de la rama 2.x.** La 3.10.2 corrige el aviso de seguridad GHSA-v5pm-xwqc-g5wc, pero rompe la compilación: el generador de código de `Microsoft.AspNetCore.OpenApi` 10.0.7 asigna a `IOpenApiMediaType.Example`, que en la 3.x pasó a ser de solo lectura (`error CS0200`). La versión que corrige el aviso y compila es **2.12.2**, fijada explícitamente en el `.csproj` aunque sea una dependencia transitiva.
- **`--legacy-peer-deps` no es «instalar ignorando un aviso»: rehace el árbol entero.** Al añadir `eslint-plugin-jsx-a11y`, cuyo rango de *peer* llega hasta ESLint 9 cuando el proyecto usa el 10, la instalación con ese flag **dejó fuera `@testing-library/dom`** —una dependencia de pares de `@testing-library/react`— y rompió los tipos de todos los archivos de test, con un error que no mencionaba npm por ninguna parte. La forma correcta de forzar un *peer* concreto es un `overrides` en `package.json`, que toca solo lo que se nombra.
- **La suite del backend comparte una sola `SqliteConnection` y eso es una carrera.** EF registra una colación en la conexión cada vez que construye un `DbContext`, sobre una colección que no es segura entre hilos. El síntoma es `Operations that change non-concurrent collections must have exclusive access` desde `SqliteConnection.CreateCollation`, sin relación con el test que se esté ejecutando, y aparece sobre todo con `--filter`. Desactivar el paralelismo de xUnit **no** lo arregla: la concurrencia está dentro del host (T2-28).
- **`autoFocus` se aplica antes que los efectos, y rompe la devolución del foco.** React lo procesa durante el commit, así que cuando un efecto de un diálogo lee `document.activeElement` para recordar «quién tenía el foco antes», ya lo tiene un campo de dentro del propio diálogo. Al cerrarse, ese campo no existe y el foco se pierde al `<body>`. Por eso `useModalDialog` no lee `activeElement` sin más: mantiene un seguimiento del último foco **fuera** de cualquier diálogo, con un listener de `focusin` instalado al importar el módulo.
- **`offsetParent !== null` no sirve para detectar visibilidad dentro de un modal.** Es la comprobación habitual para filtrar elementos ocultos, pero `offsetParent` también es `null` para todo lo que está dentro de un contenedor `position: fixed` —que es exactamente lo que es un diálogo—. Con ese filtro la lista de elementos enfocables salía vacía siempre y la trampa de foco no llegaba a existir. Se usa `checkVisibility()` cuando está disponible.
- **Cambiar estado en un efecto para animar una salida desmonta el componente un render.** Los dos diálogos activaban `isClosing` en un `useEffect`, así que entre «`open` pasa a false» y «el efecto marca que se está cerrando» había un render con ambos a false donde el diálogo devolvía `null`. Se desmontaba y se volvía a montar al instante: el foco caía al `<body>` y el formulario se reiniciaba durante la animación. La solución es ajustar el estado **durante el render**, comparando con el valor anterior guardado también en estado — no en una ref, que `react-hooks/refs` prohíbe leer ahí.
- **PowerShell expande `$` dentro de comillas dobles, y se come los secretos.** `dotnet user-secrets set "Jwt:Secret" "$7kTbEi..."` falla con `Missing parameter value for 'value'`: PowerShell interpreta `$7kTbEi...` como una variable que no existe y la sustituye por cadena vacía, así que `dotnet` recibe la clave sin valor. Con **comillas simples** el texto va literal. Afecta a cualquier secreto que empiece por `$` o lo contenga, y también a la cadena de conexión, que además lleva `;`. Mejor todavía: generar el valor en una variable y pasarla, para que nunca aparezca en pantalla ni en el historial de la terminal.
- **Una migración sin su `.Designer.cs` no existe para EF Core.** EF identifica las migraciones por el atributo `[Migration("...")]`, que el generador escribe en el archivo `.Designer.cs`, **no** por el nombre del `.cs`. `AddUserFormatIdToMediaItems` estaba escrita a mano sin Designer: compilaba, se veía en `Migrations/`, y `dotnet ef migrations list` no la mencionaba siquiera. Como el *snapshot* del modelo sí incluía `UserFormatId`, EF daba el modelo por representado y tampoco iba a regenerarla: el arranque decía «no hay migraciones pendientes» sobre una base a la que le faltaba la columna, y `GET /api/media-items` respondía 500 con `42703: column m.UserFormatId does not exist` (T0-06). Lección doble: **crear siempre las migraciones con `dotnet ef migrations add`**, y no fiarse de que el arranque diga que el esquema está al día — comprobarlo con `dotnet ef migrations list` sobre una base recién creada.
- **`dotnet ef migrations add --no-build` genera migraciones vacías.** Usa el ensamblado compilado anterior, así que no ve los cambios que acabas de hacer en el modelo y produce un `Up()` vacío sin avisar de nada. Y `dotnet ef migrations remove` intenta conectarse a la base de datos para comprobar si está aplicada, así que falla sin conexión: en ese caso hay que borrar a mano el `.cs` y el `.Designer.cs`.
- **`NavLink` de React Router ya emite `aria-current="page"`.** No hace falta añadirlo a mano; la auditoría lo señaló como una carencia y era falso.
- **Anidar un proyecto dentro de la carpeta de otro proyecto .NET rompe la compilación del que envuelve.** El `.csproj` del backend está en la raíz de su repositorio, así que sus globs por defecto (`**/*.cs`) se tragaron los 23 archivos de `TrackerMultimedia.Tests/` al moverlos dentro. El síntoma es una avalancha de `error CS0246: no se encontró 'Fact'` **atribuidos al proyecto de backend**, no al de tests, lo cual despista bastante. La solución vigente es `DefaultItemExcludes` en `TrackerMultimedia_Backend.csproj:12`, que excluye la carpeta de todos los globs de una vez. Si se renombra la carpeta de tests hay que actualizar esa propiedad **y** el `.dockerignore`.
- **`AddHealthChecks()` sin comprobaciones registradas devuelve siempre `Healthy`.** No es un valor por defecto conservador: es que no hay nada que evaluar. `/health` respondía «todo bien» con PostgreSQL caído, y como respondía, nadie lo miró en un año. Una sonda que no puede fallar no es una sonda (T4-10).
- **`ConfigureAppConfiguration` de `WebApplicationFactory` llega tarde para lo que se lee al componer los servicios.** Es la misma trampa del secreto JWT vista desde otro ángulo, y explota fuera de `Development`: en desarrollo los user-secrets tapaban el hueco, así que la cadena de conexión de test parecía aplicarse; al arrancar el host de pruebas en `Production` para comprobar la especificación OpenAPI, `Program.cs` no encontró **ninguna** cadena y el host ni siquiera construyó. Lo que sí llega a tiempo es `builder.UseSetting(...)`, que entra en la configuración **del host**. `AppFactory` ahora aplica sus ajustes por las dos vías.
- **Un `label` sin `htmlFor` no falla en ningún sitio salvo en los tests de accesibilidad.** ESLint no lo detecta porque no está `eslint-plugin-jsx-a11y`; TypeScript tampoco. Los descubrió Testing Library con "no form control was found associated to that label" (T1-17, T2-22).

---

## 6. Tareas comunes

| Comando | Para qué | Requisitos |
|---|---|---|
| `Start-Service postgresql-x64-17` | Arranca la base de datos si su servicio está en *Manual* | PowerShell como administrador |
| `dotnet ef database drop --force` | **Borra la base local entera.** Solo para comprobar el esquema desde cero | El servicio arrancado |
| `dotnet run` (en `TrackerMultimedia_Backend/`) | Levanta la API en `http://localhost:5218` | El servicio de PostgreSQL arrancado y los secretos en `dotnet user-secrets` |
| `dotnet user-secrets list` | Ver la configuración sensible de esta máquina | Ejecutar dentro de `TrackerMultimedia_Backend/` |
| `dotnet ef database update` | Aplicar las migraciones pendientes a mano | `dotnet-ef` instalado globalmente. Desde el 2026-08-27 el arranque también las aplica (T0-01) |
| `dotnet ef migrations add <Nombre>` | Crear una migración tras cambiar el modelo | Igual. **Nunca con `--no-build`**: genera migraciones vacías |
| `dotnet test TrackerMultimedia_Backend.slnx` | Suite del backend (~4 s). No necesita base de datos: usa SQLite en memoria | Desde la raíz del repositorio de backend |
| `npm run dev` (en `TrackerMultimedia_Frontend/`) | Interfaz en `http://localhost:5173` con recarga en caliente | `.env` copiado de `.env.example` |
| `npm run test` | Suite del frontend (~35 s) | — |
| `npm run lint` | ESLint. **Ojo:** `npm run build` no lo ejecuta (T2-23) | — |
| `npm run build` | Build de producción a `dist/` (~2 s) | — |

**Puertos:** backend `5218`, frontend `5173`. El proxy de Vite reenvía `/api` al backend, pero `src/config/env.ts` usa por defecto la URL absoluta `http://localhost:5218/api`, así que el proxy solo entra en juego si se configura `VITE_API_URL=/api`. Los valores documentados no coinciden entre sí (T3-01).

**Base de datos:** PostgreSQL 17 **instalado en la máquina**, no en contenedor. Escucha en el **puerto 5433**, no en el 5432 por defecto: es el detalle que más veces se olvida al escribir una cadena de conexión. En Windows es el servicio `postgresql-x64-17` y su arranque está en *Manual*, así que hay que iniciarlo antes de levantar el backend. El nombre de la base, `trackerMultimedia`, lleva mayúscula intercalada: en SQL va **siempre entre comillas dobles**, porque PostgreSQL pasa a minúsculas todo identificador sin comillar.

*El 2026-08-27 la base pasó brevemente a Docker y se revirtió el mismo día, a petición del propietario: el proyecto arrancó con la instalación nativa. No volver a proponer el cambio.*

**El servicio nativo escucha en `0.0.0.0:5433`**, es decir, en todas las interfaces, no solo en loopback. En una red doméstica de confianza no es grave, pero conviene saberlo si alguna vez se sirve la aplicación con `vite --host`: la base también es alcanzable desde la red, y su única defensa es la contraseña del rol `postgres`.

**Secretos:** van **solo** en `dotnet user-secrets` desde el 2026-08-27. `appsettings.Local.json` sigue existiendo pero ya no contiene ninguno: es configuración no sensible. **No existe** `appsettings.json`, y el `.gitignore` del backend lo excluye, así que tampoco puede crearse sin quitar antes esa línea (T1-19).

**user-secrets no cifra nada.** Guarda un `secrets.json` en claro en `%APPDATA%\Microsoft\UserSecrets\`. Lo que aporta es que ese archivo vive fuera de la carpeta del repositorio, así que no puede colarse en un commit ni en el contexto de build de Docker. Frente a quien tenga acceso a la sesión de Windows, no protege.

### Verificación local antes de cada commit

No hay CI y no la va a haber (T1-12 anulada, 2026-08-27), así que **esta rutina es la única
red de seguridad del proyecto**. Se ejecuta entera antes de cada commit, no cuando uno se
acuerda: los cuatro comandos juntos tardan menos de un minuto.

En `TrackerMultimedia_Backend/`:

```bash
dotnet test TrackerMultimedia_Backend.slnx     # 105 pruebas, ~4 s. Debe decir "Con error: 0"
dotnet restore                                  # No debe emitir ningún NU1903
```

En `TrackerMultimedia_Frontend/`:

```bash
npm run lint                                    # Debe salir sin ningún error
npm run test -- --run                           # 124 pruebas, ~9 s
npm run build                                   # Incluye tsc -b; falla si hay error de tipos
```

Dos cosas que ya han mordido y por las que ninguno de los cinco comandos sobra:

- **`npm run build` no ejecuta el linter.** Solo hace `tsc -b && vite build`, así que un
  error de ESLint llega a producción sin que el build se queje (T2-23).
- **`tsc -b` no ejecuta los tests, y los tests no comprueban los tipos.** Un test puede
  pasar con un error de tipos delante, y al revés.

Y una comprobación que **no** está en esa lista porque no hace falta a diario, pero sí cada
vez que se toca el modelo o una migración: recrear la base desde cero y aplicar las
migraciones. `dotnet ef database drop --force && dotnet ef database update` — y ojo, eso
**borra los datos locales**; si hay algo que conservar, la comprobación se hace sobre una
base aparte, como explica el README del backend.
Es lo único que detecta un esquema incompleto, porque la suite usa `EnsureCreated()` sobre
SQLite y se salta las migraciones por completo (T2-03). Así apareció T0-06.

Un test en rojo que parece obsoleto **se lee antes de reescribirlo.** El 2026-08-27 uno de
ellos estaba señalando un defecto real de accesibilidad que llevaba tiempo en producción.

**Los dos repositorios se clonan por separado.** No hay ningún comando que opere sobre los dos a la vez, y ninguna ruta puede cruzar de uno a otro: lo que esté fuera de `TrackerMultimedia_Backend/` o de `TrackerMultimedia_Frontend/` no existe para nadie que clone.

---

## 7. Qué queda fuera, y por qué

- **Sincronización con las listas de MyAnimeList o AniList.** Se usan como fuente de metadatos, no como destino. Sincronizar en ambos sentidos exige gestionar el OAuth de cada catálogo, resolver conflictos y mantenerse al día de sus APIs; es otro producto.
- **Funcionalidad social.** Sin perfiles públicos, listas compartidas, seguidores ni comentarios. Cambiaría por completo el modelo de datos, el de permisos y las obligaciones legales.
- **Aplicación móvil nativa.** La interfaz web es adaptable y se usa desde el móvil; no hay planes de tiendas de aplicaciones.
- **Multiusuario con roles.** `IdentityRole` está registrado pero no se usa: no hay administración ni permisos diferenciados. Cada usuario ve solo lo suyo y no hay panel de administración.
- **Reproducción o alojamiento de contenido.** Solo se guardan referencias, metadatos y enlaces a fichas externas.

---

## 8. Registro de cambios de sesión

### 2026-08-27 — Primera auditoría técnica completa

**Qué se hizo.** Auditoría de las 13 áreas del proceso de revisión, a profundidad exhaustiva, contra GDPR y WCAG 2.2 AA. Alcance acordado: todo el código propio de los tres proyectos; excluidos solo generados y dependencias (`node_modules`, `dist`, `bin`, `obj`, `.vs`, `artifacts`, `package-lock.json`, `TestResults`). El anexo de aplicaciones de escritorio Windows no aplica: es una aplicación web. Se crearon `ROADMAP.md`, `CHANGELOG.md` y este archivo, que no existían.

**Qué se descubrió.** Se ejecutaron de verdad las dos suites, el linter y el build, en lugar de estimar:

- `dotnet test`: **101 de 102 pasan**. El fallo es una aserción mal escrita en `JikanSearchServiceTests.cs:113` (espera "Activo" donde el mock envía "Finished Airing"), no un fallo del código.
- `npm run test`: **107 de 124 pasan**, 17 fallos en 5 archivos.
- `npm run lint`: **1 error** (`react-hooks/set-state-in-effect` en `ProfileView.tsx:18`). `tsc -b` y `npm run build` salen limpios; el bundle principal pesa 375 kB (118 kB comprimido).
- `dotnet restore`: **dos avisos `NU1903`** de vulnerabilidad alta conocida.

Los cinco hallazgos que cambian el orden de las prioridades: migraciones desactivadas en `Program.cs:235`; suite de tests fuera de todo repositorio git; blueprints de despliegue que no corresponden a los repositorios reales; doble clave foránea en `AspNetUserLogins` que deja los proveedores vinculados invisibles para siempre; y ausencia total de textos legales y de borrado de cuenta en una aplicación pública con datos personales.

**Lo que se comprobó y resultó estar bien** (para no volver a investigarlo): DOMPurify y las herramientas de desarrollo de React Query no llegan al bundle de producción; los iconos de Heroicons ya emiten `aria-hidden`; la credencial de Neon de `appsettings.Local.json` nunca entró en el historial de git; no hay cookies ni rastreadores, así que no hace falta banner de consentimiento; y `returnPath` del callback de OAuth no es explotable como redirección abierta porque `history.pushState` rechaza otros orígenes. Está todo anotado en las "Decisiones cerradas" del roadmap.

**Qué quedó a medias.** Nada: la auditoría está completa. No se modificó **ningún** archivo de código; los únicos cambios son los tres documentos nuevos. Lo que no se pudo cubrir y necesitaría acceso adicional: medición real de rendimiento en producción, revisión del contraste de color sobre la interfaz renderizada, verificación con lector de pantalla, revisión de la configuración real de los paneles de Render, Netlify y Neon, y comprobación de qué migraciones están aplicadas en la base de datos de producción.

**Siguiente paso recomendado.** Tier 0 en orden, empezando por comprobar el estado del esquema en la base de datos de producción antes de tocar `Program.cs:235`.

### 2026-08-27 — Primera tanda de correcciones del roadmap

**Qué se hizo.** 18 tareas cerradas y verificadas ejecutando las suites, el linter y el
build, no por inspección.

*Backend* — Se restauró la aplicación de migraciones (`MigrateAsync` con registro de las
pendientes y `try/catch` que no tumba el arranque). Se corrigió la doble clave foránea de
`AspNetUserLogins` configurando la navegación `ExternalLogins` sobre `UserId`, con la
migración `FixExternalLoginsFkAndAddQueryIndexes`, que además crea `IX_RefreshTokens_TokenHash`
(único) e `IX_MediaItems_UserId_CreatedAtUtc`. Se añadió `AuthSessionService.GetLinkedProvidersAsync`
para que los proveedores vinculados lleguen de verdad al cliente. El envío de correo se
aisló en `SendEmailSafelyAsync`, de modo que un SMTP caído ya no rompe el registro. Se
cerró la enumeración de cuentas: registro y login responden ahora siempre lo mismo, y al
titular de una dirección ya registrada se le avisa por correo con la plantilla nueva
`AccountAlreadyExists`. Se fijaron las dos dependencias vulnerables.

*Frontend* — Se simuló `matchMedia` en el arranque de los tests y se blindó `useDarkMode`.
Las siete etiquetas huérfanas pasaron a `role="group"` + `aria-labelledby`, y el selector de
color se nombra desde su disparador con `aria-labelledby`. Se añadió el enlace «Saltar al
contenido». `prefers-reduced-motion` ahora anula también las transiciones. Se resolvió el
error de ESLint de `ProfileView` moviendo la sincronización del nombre del efecto al render.

**Qué se descubrió.** Dos cosas que cambian lo que decía la auditoría:

1. **Un hallazgo era falso.** T2-25 daba por ausente `aria-current` en la navegación;
   `NavLink` ya lo emite. Tarea anulada, ID no reutilizable.
2. **Un hallazgo nuevo, oculto tras un test "desactualizado".** El enlace externo de los
   resultados de búsqueda no tenía nombre accesible: solo contenía un icono con
   `aria-hidden`. El test llevaba tiempo señalándolo en rojo y se había clasificado como
   deriva de la interfaz. Registrado como T1-22 y corregido. La lección es que un test rojo
   que parece obsoleto conviene leerlo antes de reescribirlo.

**Estado al cerrar.** Backend **105/105**, frontend **124/124**, `npm run lint` y `tsc -b`
limpios, `npm run build` correcto y `dotnet restore` sin avisos de vulnerabilidad. Es la
primera vez que las dos suites están en verde a la vez.

**Qué quedó a medias.** Nada empezado sin terminar. Lo que **no** se tocó y por qué:
`T0-02` y `T0-03` (estructura de repositorios) necesitan una decisión del propietario entre
monorepo y dos repos; `T0-05` y `T1-14` necesitan datos legales reales; `T1-13` requiere
rotar la contraseña en el panel de Neon; `T1-20` toca el índice de git; y `T1-10` dejó fuera
las aserciones sobre `getStats` porque su destino depende de `T2-08`, que también es una
decisión de producto.

### 2026-08-27 — Estructura de repositorios: dos, no monorepo

**La decisión.** El propietario cerró la pregunta que bloqueaba todo el Tier 0 restante:
se mantienen **dos repositorios independientes**, `TrackerMultimedia_Backend` y
`TrackerMultimedia_Frontend`. La carpeta que los contiene en local no es ni va a ser un
repositorio. La consecuencia práctica, que conviene tener presente antes de crear
cualquier archivo: **lo que se deje en la raíz de esa carpeta no existe para nadie que
clone el proyecto.** Eso fue exactamente lo que había pasado con la suite de pruebas y
con los dos blueprints de despliegue.

**Qué se movió.** La suite de pruebas pasó a `TrackerMultimedia_Backend/TrackerMultimedia.Tests/`
y el `.slnx` dejó de referenciarla con `../`, fuera de su propio repositorio. `render.yaml`
pasó a la raíz del backend con `dockerfilePath: ./Dockerfile` y `dockerContext: .`;
`netlify.toml` pasó a la raíz del frontend y perdió el `base` que apuntaba a una carpeta
inexistente. La documentación de despliegue, que solo vivía en el README sin versionar de
la carpeta contenedora, se repartió entre el README de cada repositorio; el de la raíz se
reescribió para decir únicamente lo que es: un contenedor local.

**Lo que costó.** Mover el proyecto de tests dentro de la carpeta del backend rompió la
compilación de golpe. El `.csproj` del backend está en la raíz de su repositorio, así que
sus globs por defecto absorbieron los 23 archivos de test, y el error llegó como una
avalancha de `CS0246: no se encontró 'Fact'` **atribuidos al proyecto de backend**, no al
de tests. Se resolvió con `DefaultItemExcludes` en el `.csproj`, replicado en el
`.dockerignore` para que la imagen tampoco los arrastre.

**Verificado, no supuesto.** `dotnet test TrackerMultimedia_Backend.slnx` da 105/105 desde
dentro del repositorio; `dotnet publish -c Release` no incluye ningún ensamblado de test; y
ninguno de los 24 archivos fuente del proyecto de tests cae bajo una regla de `.gitignore`.
Lo que **no** se ha podido verificar es cómo están configurados hoy los servicios en los
paneles de Render y Netlify: si se crearon a mano en lugar de por blueprint, mover los
archivos no cambia nada por sí solo. Queda anotado en T0-03.

**Un hallazgo que resultó ser un duplicado.** Al revisar la configuración apareció que
`appsettings.json` no existe y además está en el `.gitignore`. Se abrió como tarea nueva
y, al ir a redactarla, resultó que **T1-19 ya lo cubría** y con más precisión: se afirmó
que ninguna lectura de configuración era obligatoria, y al comprobarlo en `Program.cs:36-37`
resultó falso —`Cors:AllowedOrigins` lanza `InvalidOperationException` si falta. La tarea
duplicada se retiró y la información correcta se incorporó a T1-19. Lección: antes de abrir
un hallazgo «nuevo», buscarlo en el roadmap; y comprobar en el código toda afirmación sobre
si algo es obligatorio, en vez de deducirla de que existe un valor por defecto al lado.

### 2026-08-27 — Sin CI: la verificación es local

**La decisión.** El propietario descartó la integración continua: nada de GitHub Actions,
workflows ni comprobaciones obligatorias de fusión. El proyecto lo lleva una sola persona,
no hay pull requests que revisar, y todo el testing se ejecuta en local. T1-12 queda
anulada y su identificador no se reutiliza.

**Por qué se anota aquí y no solo en el roadmap.** La CI no estaba propuesta por completismo:
estaba propuesta porque el problema que detecta **ya había ocurrido**. Cuando empezó la
auditoría había 18 pruebas en rojo —una del backend y diecisiete del frontend— que llevaban
tiempo así sin que nadie se enterara, precisamente porque nada las ejecutaba salvo la
voluntad de hacerlo. Descartar la CI es una decisión legítima para un proyecto de un solo
desarrollador; lo que no se puede es descartarla y dar por resuelto el riesgo. Queda
trasladado íntegro a la disciplina de ejecutar la verificación local antes de cada commit.

**Lo que sustituye a la CI.** Cinco comandos documentados en la sección *Tareas comunes*,
menos de un minuto en total: `dotnet test` y `dotnet restore` en el backend; `npm run lint`,
`npm run test -- --run` y `npm run build` en el frontend. Ninguno sobra: `npm run build` no
ejecuta el linter, y `tsc -b` no ejecuta los tests. Si en algún momento entra otra persona
al proyecto, esta decisión se revisa; hasta entonces, no volver a proponer CI.

### 2026-08-27 — Secretos a user-secrets, base en Docker, y un fallo crítico que apareció por el camino

> **Parcialmente superada el mismo día.** La parte de Docker se revirtió a petición del
> propietario: la base vuelve a ser la instalación nativa de PostgreSQL. Lo que se cuenta
> aquí sobre los secretos y sobre el fallo de la migración sigue vigente; lo de
> `docker-compose.yml` ya no existe. Ver la entrada final de este registro.

**Lo que se pidió.** Rotar las claves y llevarlas a donde deben estar —user-secrets— y
levantar la base de datos de desarrollo en Docker.

**Lo que se hizo con los secretos.** Las ocho claves están ahora en `dotnet user-secrets`
y `appsettings.Local.json` quedó reducido a configuración no sensible. Por el camino se
eliminaron dos entradas que **ningún código leía**: `ConnectionStrings:DefaultConnectionPro`,
que era la cadena de producción de Neon y cuyo único efecto era tener la credencial de
producción en el portátil, y `Security:ApiKey`. Se regeneraron de cero `Jwt:Secret` y la
contraseña de PostgreSQL. Las que dependen de un tercero —Neon, Google, GitHub, Mailtrap—
se movieron tal cual: **siguen siendo válidas hasta que se revoquen en cada panel**, y
mientras tanto la rotación no ha ocurrido de verdad. Los comandos están en el README.

**La base en Docker.** `docker-compose.yml` levanta PostgreSQL 17 en `127.0.0.1:5433`, con
la contraseña en `.env` fuera del control de versiones. El puerto no es el 5432 para no
chocar con una instalación nativa, y el contenedor no se expone a la red local.

**Y entonces apareció T0-06.** Al aplicar las migraciones sobre esa base recién creada —la
primera vez que alguien construía el esquema desde cero— faltaba la columna
`MediaItems.UserFormatId`. La causa: la migración que la añadía estaba escrita a mano y
**sin su archivo `.Designer.cs`**, que es donde vive el atributo `[Migration("...")]`. Sin
ese atributo EF no la registra: la clase compilaba, el archivo se veía en la carpeta, y
`dotnet ef migrations list` mostraba tres migraciones de las cuatro presentes. Como el
*snapshot* del modelo sí incluía la propiedad, EF daba el modelo por representado y tampoco
iba a generarla nunca. El arranque informaba «Esquema de base de datos al día» sobre una
base incompleta.

**El impacto, verificado y no supuesto.** Se levantó la API contra esa base y
`GET /api/media-items` devolvió 500 con `Npgsql.PostgresException 42703: column
m.UserFormatId does not exist`. Toda la biblioteca —la funcionalidad central del
producto— rota en cualquier despliegue nuevo. Tras regenerar la migración se repitió el
ciclo completo: alta 201, lectura 200, y el formato viaja de ida y vuelta correctamente.

**Por qué llevaba meses invisible.** Por dos cosas a la vez. La suite usa `EnsureCreated()`
sobre SQLite, que construye el esquema desde el modelo y **se salta las migraciones**
(T2-03), así que 105 pruebas en verde no dicen absolutamente nada sobre si las migraciones
funcionan. Y la base real de desarrollo se había creado hacía tiempo, con la columna ya
puesta a mano o por un `EnsureCreated` anterior. Nadie había construido un esquema desde
cero, y por eso nadie lo había visto.

**La lección operativa.** Poder tirar y recrear la base no es una comodidad: es un método
de verificación que no existía. Cada vez que se toque el modelo o una migración, hay que
construir el esquema desde cero. Es lo único que comprueba lo que los tests no pueden
comprobar. *(El comando de entonces era `docker compose down -v && docker compose up -d`;
con la base nativa es `dotnet ef database drop --force && dotnet ef database update`. **La
práctica es lo que importa, no la herramienta.**)*

**Lo que queda sin verificar.** El esquema real en Neon. Si allí la columna existe sin que
conste la migración que la crea, el próximo despliegue fallará al intentar añadirla y habrá
que insertar la fila de `__EFMigrationsHistory` a mano en vez de ejecutar el `Up()`. No se
ha inspeccionado la base de producción.

### 2026-08-27 — El proyecto deja de estar desplegado

**La decisión.** Render, Neon y Netlify quedan deshabilitados y sus credenciales revocadas.
La aplicación se usa **solo en local**: interfaz por el servidor de desarrollo de Vite,
backend en `localhost:5218`, base de datos en el PostgreSQL instalado en la máquina. Los
repositorios siguen en GitHub.

**Lo que se conserva y por qué.** `render.yaml`, `netlify.toml` y el `Dockerfile` no se
borran: son la receta para volver a desplegar y borrarlos no gana nada. Quedan marcados
como inactivos en la sección de despliegue de cada README, con el aviso de que todas las
variables habría que crearlas de cero.

**El error que hay que evitar aquí.** Es tentador dar por cerrados los hallazgos de
seguridad y los legales, porque «ya no hay producción». No lo están: **cambia cuáles están
activos, no si existen**. Tres quedan en suspenso o rebajados y vuelven íntegros el día que
haya despliegue:

- **T0-05** (política de privacidad, aviso legal): en suspenso. Sin servicio accesible a
  terceros no hay tratamiento de datos de otras personas. Vuelve en cuanto alguien que no
  seas tú pueda entrar. *Dónde está exactamente esa frontera requiere revisión legal*; lo
  que aquí se afirma es solo que hoy el supuesto que motivaba la tarea no se da.
- **T1-14** (borrado de cuenta): de deber legal pasa a carencia funcional. Sigue sin haber
  forma de borrar una cuenta.
- **T1-05** (`X-Forwarded-For` de cualquier proxy): el escenario de ataque era Internet a
  través del proxy de Render. **El código no ha cambiado ni una línea.** Es de lo primero
  que hay que resolver antes de volver a publicar.

Lo que **no** cambia: T1-15 (licencia) y T1-20 (229 binarios en el índice) siguen vigentes,
porque los repositorios continúan publicados en GitHub.

**`VITE_API_URL` pasa a ser una ruta relativa.** Cerrando T3-01 se unificaron los cinco
valores documentados en uno solo: `/api`. La elección no es cosmética. Una ruta relativa la
resuelve el navegador contra el host desde el que cargó la página, así que funciona igual en
`localhost` y al servir con `vite --host` desde otro dispositivo de la red; una URL absoluta
a `localhost` rompe ese segundo caso en silencio, porque el otro dispositivo hablaría con su
propio localhost. Consecuencias: el esquema de Zod tuvo que ampliarse —`z.string().url()`
rechaza las rutas—, se añadió el mismo proxy en `preview` (sin él `/api` daba 404 al
previsualizar el build) y se eliminó `apiBaseWithoutPath`, que no usaba nadie y con una ruta
relativa habría quedado como cadena vacía.

**Si algún día se sirve con `--host`.** La aplicación queda accesible a toda la red local,
sin nada en medio. En una red doméstica es razonable; en una compartida, no. El backend
sigue escuchando solo en `localhost` y únicamente lo alcanza el proxy de Vite, que es el
comportamiento correcto. **Tiene que seguir siendo una opción que se pide, no el
comportamiento por defecto**: ver la nota del 2026-09-02.

### 2026-08-27 — Vuelta a PostgreSQL instalado en la máquina

**La decisión.** El propietario revierte el paso a Docker: la base de datos vuelve a ser la
instalación nativa de **PostgreSQL 17 en el puerto 5433**, que es como arrancó el proyecto.
En Windows es el servicio `postgresql-x64-17`, con arranque en *Manual*.

**Qué se eliminó.** El contenedor `trackermultimedia-db`, el volumen
`trackermultimedia-postgres-data` y la red del proyecto. Del repositorio salieron
`docker-compose.yml`, `.env` y `.env.example`, y el `.gitignore` recuperó su forma anterior.
`Dockerfile` y `.dockerignore` **se quedan**: son del despliegue en Render, no de la base de
datos, y siguen conservados como receta aunque el despliegue esté inactivo.

**Lo que no se revierte, y es lo importante.** Docker fue el medio, no el fin. Lo que
realmente aportó fue poder construir el esquema desde cero, y **eso es lo que destapó
T0-06**: una migración que EF nunca ejecutaba y que dejaba la biblioteca rota en cualquier
instalación nueva. Esa comprobación sigue siendo obligatoria tras tocar el modelo o una
migración, ahora en su forma nativa:

```powershell
dotnet ef database drop --force && dotnet ef database update
```

Con una diferencia que hay que tener presente: `database drop` **borra los datos locales**,
mientras que recrear un contenedor solo borraba un volumen desechable. Si hay algo que
conservar, la comprobación se hace sobre una base aparte; el README del backend explica cómo.

**Dos detalles de la instalación nativa que no estaban antes.** El servicio escucha en
`0.0.0.0:5433`, o sea en todas las interfaces, no solo en loopback como hacía el contenedor:
la base es alcanzable desde la red local y su única defensa es la contraseña del rol
`postgres`. Y el nombre de la base, `trackerMultimedia`, lleva mayúscula intercalada, así que
en SQL va siempre entre comillas dobles: sin ellas PostgreSQL crearía `trackermultimedia`.

**Lo que quedó pendiente al cerrar la sesión.** La cadena de conexión en user-secrets seguía
llevando la contraseña del contenedor, así que `dotnet ef` fallaba con `28P01: la
autentificación password falló para el usuario postgres`. Se dejó al propietario ponerla,
para que su contraseña no pasara por la conversación.

### 2026-08-27 — Diálogos accesibles con teclado, y tres defectos que no estaban en el informe

**Lo que pedía la tarea.** T1-16: los dos diálogos declaraban `role="dialog"` con
`aria-modal="true"` pero no movían el foco al abrirse, no lo devolvían al cerrarse y no
acotaban el tabulador. No había ni una llamada a `.focus()` en toda la aplicación.

**La solución.** Un hook compartido, `src/shared/hooks/useModalDialog.ts`, con el patrón de
diálogo modal de ARIA APG: foco inicial, tabulador acotado al contenido, devolución del foco
a quien abrió, Escape y bloqueo del scroll del fondo. `SidePanelDialog` y `ConfirmDialog` lo
usan; la lógica duplicada que ambos tenían desaparece.

**Lo que apareció al implementarlo.** Tres defectos reales que el informe no había visto,
cada uno detectado porque el anterior dejó de tapar al siguiente:

1. **El fondo oscuro era el único control de cierre de dos paneles.** Un `<button>` con
   `aria-label`, colocado *fuera* del diálogo: el teclado llegaba a él mientras los lectores
   de pantalla lo ocultaban por `aria-modal`. Al hacerlo no enfocable había que comprobar
   antes que cada panel tuviera su propio cierre — los cuatro tienen «Cancelar» dentro.
2. **Los diálogos se desmontaban y volvían a montarse al cerrarse.** `isClosing` se activaba
   en un efecto, así que existía un render intermedio en el que `isRendered` daba `false` y
   el componente devolvía `null`. Además de tirar el foco al `<body>`, reiniciaba el
   formulario durante la animación de salida. Solo se vio porque la devolución del foco
   fallaba sin motivo aparente.
3. **`autoFocus` le gana la carrera a los efectos.** React lo aplica durante el commit, y
   cuatro formularios autoenfocan su primer campo. El hook guardaba como «foco anterior» un
   campo de dentro del propio diálogo, que al cerrarse ya no existía. Se resolvió con un
   seguimiento del último foco fuera de cualquier diálogo.

**Un cambio de comportamiento deliberado.** En los diálogos destructivos el foco inicial pasa
a «Cancelar». El `autoFocus` estaba en el botón de confirmar, así que abrir un aviso de
borrado y pulsar Intro por inercia bastaba para perder el elemento.

**Cómo se encontró el segundo defecto, que es lo transferible.** Los tests del hook pasaban
en aislamiento y fallaban dentro de `LibraryView`. En lugar de ajustar el test, se instrumentó
el hook con `console.log` para ver qué elemento guardaba y cuál tenía el foco al cerrar. La
respuesta —«guardo un elemento vacío», «el cierre ni siquiera se registra»— llevó a las dos
causas en un par de minutos. Adivinar habría costado mucho más.

**Lo que no se ha verificado.** Nada de esto se ha probado con un lector de pantalla real.
Lo comprobado son las relaciones de foco, los roles y los nombres accesibles, con 10 tests.
NVDA o VoiceOver siguen pendientes.

### 2026-08-27 — Onboarding, limpieza del repositorio y retención de datos

Tres tareas del Tier 1 que no dependían de nadie: T1-19, T1-20 y T1-21.

**El clon limpio ya arranca (T1-19).** Faltaba `appsettings.json` y además el `.gitignore`
lo excluía, mientras los tres README afirmaban que contenía la configuración no sensible ya
preparada. Ahora existe, versionado, con todo lo que no es secreto; los dos únicos valores
obligatorios que quedan fuera son la cadena de conexión y `Jwt:Secret`. La comprobación no
fue leer el README y darlo por bueno: se exportaron los archivos versionados a una carpeta
vacía y se arrancó allí en `Production` con solo esos dos por variable de entorno.

**El repositorio vuelve a ser código (T1-20).** De 378 archivos versionados a 151, sin
ningún binario. `artifacts/` eran 68 MB de una publicación volcada dentro del repositorio
—con una copia anidada de sí misma— y `.vs/` la caché de Visual Studio, que incluye
`DocumentLayout.json` con rutas absolutas de la máquina. `git rm -r --cached` los saca del
índice sin tocar el disco.

> **Lo que esto no arregla.** Los archivos siguen en el historial: quien clone se los baja
> igual. Vaciarlos de verdad exige reescribir el historial con `git filter-repo`, que cambia
> todos los hashes y obliga a forzar el push. No se ha hecho, y queda anotado arriba como
> pendiente real.

**Y una corrección al informe.** El hallazgo afirmaba que `.codegraph/codegraph.db` (2,5 MB)
estaba versionado. No lo estaba: `.codegraph/` trae su propio `.gitignore` que ya lo excluye.
Van dos hallazgos de la auditoría que resultan inexactos al ir a corregirlos —el otro fue
`aria-current`—, y en ambos casos el error fue dar por hecho el estado de algo sin
comprobarlo. Comprobarlo cuesta un comando.

**Retención de datos (T1-21).** Ni `RefreshTokens` ni `OAuthStates` se limpiaban nunca.
Ahora un `BackgroundService` purga al arrancar y cada 6 h. Los plazos son distintos a
propósito: 7 días para los tokens de refresco —caducados ya se rechazan al usarse, así que
el margen solo sirve por si se añade detección de reutilización— y 1 día para los estados
OAuth, que guardan el email y el nombre del perfil externo y cuya vida útil real son diez
minutos.

La lógica de borrado vive en `ExpiredDataCleaner`, separada del servicio que la programa.
No es purismo: un `BackgroundService` con temporizador no se puede comprobar sin esperar, y
separarlo permite ejecutar la purga desde un test y verificar exactamente qué se va y qué se
queda. `Cleanup:Enabled=false` en `AppFactory` evita además que la suite arrastre un
temporizador de fondo en cada arranque del host.

Esta tarea dependía de T0-05 para documentar el plazo en la política de privacidad. Con
T0-05 en suspenso, el plazo se documentó en el README del backend: una dependencia
bloqueada no siempre obliga a esperar, a veces solo obliga a cambiar dónde acaba la
información.

### 2026-08-27 — Tanda de seguridad del Tier 2

Cinco hallazgos de seguridad cerrados de una vez, porque comparten una forma: una
regla que se aplica en un sitio y se olvida en otro.

**El mismo control en los tres caminos (T2-10).** El login con contraseña rechazaba a
usuarios bloqueados y sin correo confirmar. El callback de OAuth emitía sesión sin
comprobar ninguna de las dos cosas, y `LinkConfirm` sin comprobar la segunda. Una cuenta
bloqueada por intentos fallidos quedaba cerrada por la puerta principal y abierta por la
lateral. La lección no es «faltaba un `if`»: es que una regla de negocio repartida por tres
controladores se rompe por el que menos se toca.

**Reutilización de tokens de refresco (T2-12).** Los tokens rotan, así que presentar uno ya
revocado no es un error del usuario: significa que existen dos copias y una no es del
titular. Ahora se revocan todas sus sesiones. Y el refresco comprueba el estado de la
cuenta, que antes se ignoraba por completo: bloquear a alguien no tenía efecto hasta que
caducase su token, o sea hasta siete días después.

**El `state` de OAuth ya no se resucita (T2-07).** El flujo de vinculación ponía
`IsUsed = false` sobre el state recién consumido y le daba quince minutos más. La entidad
documenta «solo se puede consumir una vez» y el código hacía lo contrario. Ahora la
vinculación pendiente vive en su propia fila.

**Y el hallazgo que apareció por el camino (T2-27).** Al quitar del backend el mensaje de
excepción que acababa en la URL (T2-11), fui a ver quién lo leía. El frontend no solo lo
leía: **lo mostraba con prioridad sobre el código de error**. Cualquiera podía enviar un
enlace con `?oauth_error_message=Tu+cuenta+fue+suspendida,+llama+al+900...` y ese texto
aparecía en la página de login auténtica, con su dominio y su candado. No hace falta
comprometer nada. La auditoría solo había visto la mitad de servidor.

> **Lo transferible:** al arreglar un extremo de un dato, mirar el otro. Un valor que el
> servidor deja de enviar no deja de ser aceptado por el cliente, y quien lo envíe ya no
> tiene por qué ser el servidor.

**Inyección de fórmulas en el CSV (T2-09).** `EscapeCsvCell` entrecomillaba separadores
pero no neutralizaba las celdas que empiezan por `=`, `+`, `-` o `@`. Exportar la
biblioteca era ofrecer un archivo que ejecuta lo que el propio usuario escribió en un
título. Se prefijan con apóstrofo.

**Un test que afirmaba lo incorrecto.** `Callback_WithExistingManualUser_RedirectsToLinkAccount`
comprobaba que `IsUsed` volvía a `false`, es decir, verificaba el defecto. Va el tercer
caso en esta sesión de test que hay que leer con cuidado antes de tocarlo: uno señalaba un
defecto real que se tomó por obsoleto, otro se rompió por un cambio correcto, y este
codificaba el comportamiento equivocado como si fuera el esperado.

**Una trampa de tests, sin relación con la seguridad.** Escribir el caso del CSV como
`[Theory]` con cuatro `InlineData` hizo que xUnit ejecutara los casos en paralelo sobre la
conexión SQLite compartida de `AppFactory`, que no es segura entre hilos: el fallo no fue
una aserción sino `Operations that change non-concurrent collections must have exclusive
access`. Con esa `AppFactory`, los casos que tocan base de datos van en un único `[Fact]`
que itera.

### 2026-08-27 — Accesibilidad automática, límite de error y respuestas de error con identificador

Cuatro tareas del Tier 2: T2-22, T2-21, T2-05 y T2-17.

**El linter ya comprueba accesibilidad (T2-22).** No comprobaba ninguna regla, y esa es
exactamente la razón de que las siete etiquetas sin control asociado de T1-17 llegaran hasta
la auditoría. La comprobación no fue «instalar y ver que pasa», sino crear un archivo con
defectos a propósito y confirmar que los señala: la etiqueta huérfana, un `alt` ausente, un
`onClick` sin equivalente de teclado y un `href` inválido.

De las once apariciones de `no-autofocus` sobre el código existente, cuatro estaban dentro de
diálogos y se convirtieron en `data-dialog-autofocus`, que gestiona `useModalDialog`. Eso no
es callar al linter: es **mejor que antes**, porque React aplica `autoFocus` durante el commit
y le ganaba la carrera al hook, que era el tercer defecto de T1-16. Las seis de páginas de
autenticación conservan `autoFocus` con una excepción documentada; una página cuyo único
contenido es el formulario es el caso para el que existe la excepción de la regla.

**Límite de error (T2-21).** Va montado **por fuera** de los proveedores. Colocado por dentro,
un fallo del contexto de sesión se lo llevaría por delante junto con todo lo demás.

**Respuestas de error con identificador (T2-05).** El valor no está en el formato sino en el
`traceId`: sin él, «me salió un error» y las líneas del log son dos conjuntos que no se
pueden cruzar.

**Tope de elementos en la importación (T2-17).** Había un límite de 10 MB, que acota el
archivo pero no el trabajo: en 10 MB de JSON caben decenas de miles de elementos que se
cargan en memoria junto a la biblioteca entera del usuario. El límite útil se mide en
elementos, no en bytes.

**Lo que no se arregló, y por qué se deja escrito.** Al probar el test de importación
apareció por segunda vez en la sesión un fallo intermitente de la suite:
`Operations that change non-concurrent collections must have exclusive access`. La causa está
identificada —`AppFactory` comparte una sola `SqliteConnection` y EF le registra una colación
por cada `DbContext`— y probé dos mitigaciones que **no** funcionan: `xunit.runner.json` y
`[assembly: CollectionBehavior(DisableTestParallelization = true)]`. Ambas se revirtieron en
lugar de dejarlas puestas con un comentario que afirmara algo falso. Queda como T2-28 con el
diagnóstico y una propuesta concreta. La suite completa es estable: 117/117 en tres pasadas.

**Un aviso sobre npm.** Instalar con `--legacy-peer-deps` para saltarse un rango de *peer*
desactualizado rehízo el árbol de dependencias y dejó fuera `@testing-library/dom`, rompiendo
los tipos de todos los tests con un error que no mencionaba npm. La herramienta correcta para
forzar un *peer* concreto es `overrides` en `package.json`.


### 2026-09-02 — Formatos, validación silenciosa y datos personales en el log

**Un formato inválido dejaba de serlo por el camino (T2-19).** `ResolveUserFormatIdAsync`
devolvía `null` tanto para «no me mandaron formato» como para «el formato es de otra
cuenta», y quien llamaba no podía distinguirlos: el elemento se guardaba sin formato y la
pantalla mostraba un guardado correcto. `ResolveCategoriesAsync`, a dos pantallas de
distancia en el mismo archivo, sí devolvía error en el caso equivalente. Ahora devuelve
`ServiceResult<Guid?>`. `Guid.Empty` se sigue tratando como «sin formato», porque hay
clientes que lo mandan en lugar de omitir el campo.

**El GET que escribía (T2-20).** Ver la convención en *Datos*. Lo que conviene recordar del
proceso: antes de mover el sembrado comprobé si quedaban cuentas que dependieran de él,
consultando la base (`0` usuarios), en lugar de suponer que no las había. En una base con
usuarios este cambio necesita un `INSERT` puntual antes de aplicarse.

**El hallazgo de los logs estaba a medias (T2-15).** El informe señalaba once puntos en
`AuthController`; al ir a corregirlos, ya registraban `UserId` —se habían arreglado sin
querer al pasar por T2-12— y quedaban cuatro sitios reales, en `OAuthController` y
`SmtpEmailService`. Un hallazgo de auditoría envejece: hay que releer el código antes de
tocarlo, no confiar en las líneas que cita el informe.

**La regla, para que no haya que decidirla otra vez:** en el log va el `UserId`. Cuando
todavía no existe usuario, va `PersonalData.MaskEmail` —primera letra y dominio— que sirve
para reconocer una dirección que ya se conoce, no para descubrir una nueva. Nunca la
dirección completa.


### 2026-09-02 — El `--host` que no estaba donde parecía

`vite.config.ts` llevaba un `server.host: true` sin commitear. Al quitarlo, la aplicación
seguía escuchando en todas las interfaces, porque **el `--host` real vivía en el script
`dev` de `package.json`** (`"dev": "vite --host"`, commiteado en `6504b51` el 2026-08-28) y
la opción de la línea de órdenes manda sobre la del archivo de configuración.

Lo que lo delata es el propio README: documenta `npm run dev -- --host` en una sección
aparte, con dos advertencias sobre exponer la aplicación a la red. Es decir, la
documentación describía una opción que había que pedir mientras el script la aplicaba
siempre. Se quitó del script para que ambos digan lo mismo.

**Comprobado, no supuesto:** arrancado el servidor, Vite imprime `Network: use --host to
expose` y `netstat` muestra la escucha en `[::1]:5173`, solo loopback.

**La regla que queda:** el modo por defecto es el más cerrado. Exponer a la red es una
decisión que se toma en el momento, y se ve en la orden que se escribe. Por eso hay dos
scripts y no una opción escondida: `dev` escucha solo en loopback y `dev:lan` (`vite --host`)
expone a la red. **No volver a meter `--host` en `dev`**; quien lo necesite a diario escribe
`dev:lan`.

Comprobados los dos: `dev` deja `[::1]:5173` y `dev:lan` deja `0.0.0.0:5173` con las tres
direcciones de red impresas.

**Un segundo efecto del mismo cambio.** El archivo llegó reformateado entero: comillas
dobles, punto y coma y sangría de cuatro espacios, contra las comillas simples, sin punto y
coma y dos espacios del resto del proyecto. El proyecto **no tiene Prettier ni
`.editorconfig`**, así que no hay nada que arbitre el estilo y lo puso el formateador por
defecto del editor. Se revirtió el reformateo, pero la causa sigue ahí y volverá a pasar:
queda como candidato a añadir un `.editorconfig` y Prettier con el estilo vigente.


### 2026-09-02 — Validaciones que solo cubrían un proveedor, y errores que salían por la puerta equivocada

**La condición estaba escrita al revés (T2-18).** `ValidateExternalSource` empezaba con
`if (sourceType != MediaItemSourceType.Jikan) return null;`. Cuando se añadieron AniList y
MangaDex al enum, la validación no se enteró: sus elementos podían guardarse sin
identificador externo y, de paso, se libraban del índice único de duplicados, que es parcial
y solo actúa cuando hay identificador. Ahora la condición va contra `Manual`. **La forma
correcta de escribir estas comprobaciones es la excepción, no la enumeración**: `== Manual`
cubre por adelantado cualquier proveedor futuro; `!= Jikan` obliga a acordarse de volver
aquí, y nadie se acuerda.

**Un `catch` demasiado estrecho en una redirección de navegador (T2-06).** El callback de
OAuth solo capturaba `InvalidOperationException`, y el fallo más corriente de todos —un
código de autorización caducado— no es de ese tipo: **GitHub responde 200 con
`{"error":"bad_verification_code"}`**, así que el estado HTTP no delata nada y
`GetProperty("access_token")` lanzaba `KeyNotFoundException`. Resultado: página de error 500
en vez de vuelta al login. Se corrigió en los dos niveles: el servicio de GitHub detecta ese
cuerpo y lo traduce, y el `catch` del controlador cubre además red, JSON y tiempo de espera.

**El criterio, por si aparece otro endpoint así:** en un endpoint al que llega el navegador
por redirección, todo fallo tiene que terminar en una página de la aplicación con un código
de error. Un 500 ahí no es un error técnico más: es un callejón sin salida para alguien que
solo quería entrar.

**Los mensajes de error, todos en español (T2-26).** Dos cosas que el informe daba por
sentadas y no eran ciertas: **«Not found» nunca llegaba al usuario** —los tres controladores
comprueban `ErrorField == "id"` y devuelven `NotFound()` sin cuerpo, así que ese texto se
descarta—, y los mensajes sobre `ExternalId` desaparecieron solos al reescribir T2-18. Se
tradujo igualmente por coherencia. La convención queda fijada: **todo mensaje que pueda
llegar al cliente va en español y no nombra campos internos de la API** — por eso «Type o
ContentKind es obligatorio» pasó a «Debes indicar el tipo de contenido».


### 2026-09-02 — Fuera el panel de estadísticas

Decisión del propietario: eliminarlo, no reconectarlo. Estaba escrito entero —componente,
endpoint, contratos, estilos y tests— y **no se mostraba en ninguna pantalla**, así que nunca
llegó a verlo nadie.

**Comprobado antes de borrar, no dado por cierto:** `MediaStatsPanel` no se importaba desde
ninguna vista, `MediaItemsApi.getStats` no se llamaba desde ningún sitio y
`queryKeys.mediaItems.stats` tampoco. Los únicos usos vivos eran sus propios tests y un mock
vestigial en `LibraryView.test.tsx`, que simulaba una llamada que la vista no hace.

Lo retirado: en el backend, `GET /api/media-items/stats`, `GetStatsAsync` (~110 líneas y ocho
consultas), el ayudante `GetStatusCount` y los cinco contratos; en el frontend, el componente,
su test, la función de API, cinco interfaces del esquema, la clave de consulta y **106 líneas
de CSS**. Las clases se comprobaron una a una antes de tocarlas: `stats-grid`, `stat-card*`,
`breakdown-*` y `panel--dense` no las usaba nadie más, mientras que `hero-chip` y
`category-pill__swatch` —que el panel también usaba— siguen vivas en cinco y siete archivos.
Ese es el paso que se salta cuando se borra un componente y quedan hojas de estilo muertas.

**T2-16 queda anulada de rebote**: pedía optimizar `GetStatsAsync`, que ya no existe. Se
registra ahí el motivo para que no parezca un descuido, y con la nota de que el hallazgo
vuelve si algún día vuelve el panel.


### 2026-09-02 — Limpieza del Tier 3, y un aviso de seguridad que apareció de rebote

Siete tareas de limpieza. Lo que merece quedar escrito no es lo borrado, sino tres cosas que
salieron al comprobarlo:

**Una tarea ya estaba hecha y no por mí (T3-13).** El comentario con la errata «Uitlizar»
había desaparecido en el commit `2b2dc76`, al reescribirse el bloque de migraciones. Se
marca como cerrada dejando constancia de dónde ocurrió, en lugar de apuntársela esta sesión.
**Un hallazgo de auditoría envejece**: es la segunda vez en dos sesiones —la primera fue
T2-15— que el código ya no está como lo describe el informe.

**El `npm uninstall` se hizo mirando el lockfile.** Quitar `dompurify` y `@types/dompurify`
dejó un diff de 28 líneas y `@testing-library/dom` en su sitio. Se comprobó a propósito,
porque en esta misma sesión un `--legacy-peer-deps` rehízo el árbol entero y rompió los tipos
de todos los tests con un error que no mencionaba npm.

**Y de ahí salió lo importante: `npm audit` da 13 avisos —1 crítico, 10 altos, 2 bajos—**,
ninguno causado por la desinstalación. Son posteriores a la auditoría del 2026-08-27. La
frase «sin vulnerabilidades conocidas en las dependencias» que llevaban el ROADMAP y el
informe **se retira**: era cierta cuando se escribió y ha dejado de serlo. Queda como T3-21,
con el matiz de contexto —el crítico es de `vitest` y solo aplica si se lanza su servidor de
UI, cosa que este proyecto no hace— y con el aviso de hacer copia del lockfile antes de
tocarlo.

**El tope de la contraseña de login (T3-20)** se puso en 100 tras mirar el historial de git:
el registro impone ese mismo límite desde el primer commit, así que ninguna cuenta existente
puede tener una más larga. Un límite en un campo de entrada que ya existía en otro sitio es
seguro; puesto a ojo, deja gente fuera.


### 2026-09-02 — Estilo fijado, y un fallo que encontró el test antes que yo

**El `.editorconfig` va en los dos repositorios (T3-15).** En el backend, `dotnet format
whitespace` tocó **67 archivos**; `ApplicationDbContext` llegaba a mezclar tabuladores y
espacios dentro del mismo bloque. Sobre la codificación: se puso `charset = utf-8` **sin**
BOM porque es lo que ya tenían **86 de los 112** archivos —contados con un bucle, no
supuestos—, así que la opción contraria habría reescrito el triple. Ese es el criterio
cuando se normaliza algo a posteriori: **gana la mayoría existente, no la preferencia**.

**Lo que EditorConfig no cubre.** Sangría, fin de línea y espacios finales, sí. Comillas y
punto y coma, no. Como el reformateo que apareció al abrir `vite.config.ts` incluía las tres
cosas, el `.editorconfig` del frontend resuelve la mitad del problema; la otra mitad necesita
Prettier y queda como **T3-22**, con la nota de hacerlo en un commit propio para no mezclar
formateo con cambios de comportamiento.

**Dos comportamientos, no uno duplicado (T3-12).** `GetUserId` estaba copiado palabra por
palabra en tres controladores y `AuthController` hacía otra cosa: devolver 401 en vez de
lanzar. Al extraerlo salieron **dos** métodos —`GetUserId()` que lanza y `TryGetUserId(out)`
que no—, y no uno solo al que forzar los cuatro sitios. Unificar de más habría cambiado
comportamiento en cuatro endpoints bajo la etiqueta de «refactor».

**Y el hallazgo que no era mío ni de la auditoría.** Al escribir el test del caso «variable
sin definir» para T3-17, falló: el esquema de Zod **rechazaba la cadena vacía**, mientras que
el código que resuelve `apiUrl` diez líneas más abajo sí la contemplaba (`length > 0`). O
sea: una línea `VITE_API_URL=` sin valor en el `.env` —lo más parecido a «no la he
configurado»— tumbaba la aplicación entera antes de que React montara. Corregido, y
registrado como **T3-23** el trabajo general de comprobar que ninguna otra validación
contradiga al código que consume el valor.

**El criterio de T3-17, por si aparece otra validación al importar:** el mensaje se pinta con
`append`, nunca con `innerHTML` —el valor viene de la configuración, pero pintarlo como
markup convertiría un error de configuración en un punto de inyección—, y **no** se cae a un
valor por defecto: taparlo haría que la aplicación hablara con un backend que no es.


### 2026-09-02 — Los proveedores OAuth se pueden ocultar desde el .env

Trabajo iniciado en otra sesión e integrado aquí. `VITE_ENABLE_GOOGLE_AUTH` y
`VITE_ENABLE_GITHUB_AUTH` enmascaran lo que responde `/auth/methods`. El caso de uso es
concreto: servir por la LAN para probar desde el móvil, donde el callback de Google no
puede volver a este origen —solo admite `localhost` o un dominio público https—, así que
ese camino termina en «localhost rechazó la conexión» y hay que entrar con credenciales
locales.

**La máscara es un AND, nunca un OR, y eso no se toca.** Puede ocultar un proveedor que el
backend ofrece; no puede encender uno que el backend tiene apagado. Si alguna vez se
convierte en OR, el frontend ofrecerá un botón que el servidor rechaza. Hay un test que fija
justamente esa dirección. **La autorización vive entera en el servidor**: esta variable es
presentación.

Se aplica en `AuthContext`, sobre la respuesta de `/auth/methods`, y no en cada vista,
porque ese es el único sitio del que salen los botones: una vista nueva hereda la decisión
sin acordarse de ella.

**El fallo que traía, y que conviene no repetir.** La suite pasó a depender del `.env` de
cada equipo. Vite carga el `.env` también al ejecutar los tests, así que con
`VITE_ENABLE_GOOGLE_AUTH=false` en la máquina de uno, `AuthContext.test.tsx` quedaba en rojo
mientras que en un clon limpio pasaba. **Un test cuyo resultado depende de un archivo sin
versionar no sirve para lo que sirve un test.** Corregido declarando los interruptores con
un mock de `@/config/env`, y comprobado en las dos situaciones: 143/143 con `.env` y 143/143
tras apartarlo. Regla que queda: **todo test que lea configuración la declara; nunca la
hereda del entorno.**

**`host: true` volvió y se quitó otra vez, por decisión del propietario.** Había reaparecido
en `server` y en `preview`, con lo que `npm run dev` escuchaba de nuevo en `0.0.0.0` y
`dev:lan` dejaba de distinguirse de `dev`. Para no perder lo que `preview.host` daba se
añadió **`preview:lan`**, y el README lleva ahora la advertencia explícita de no volver a
poner `server.host` en el archivo de configuración.

**El criterio, que ya ha hecho falta tres veces:** exponer a la red es una opción que se
pide en la orden que se escribe (`dev:lan`, `preview:lan`), nunca un valor del archivo de
configuración. La razón no es de estilo: la opción del archivo se aplica *siempre*, así que
convierte el modo por defecto en el abierto y no queda rastro en la orden de que eso está
pasando.


### 2026-09-02 — Tema, indexación, tipos de resultado y los README

**El tema tenía dos defectos y el segundo anulaba el primero (T3-18).** Que el hook no se
suscribiera a `prefers-color-scheme` era lo visible. Lo que lo hacía irrelevante es que el
efecto escribía en `localStorage` **al montar**: desde la primera visita había preferencia
guardada, así que «seguir al sistema» dejaba de existir sin que nadie lo hubiera desactivado
y suscribirse no habría servido de nada. El estado pasa a tener tres valores —`dark`,
`light` y `null` = seguir al sistema— y **solo se persiste al pulsar el interruptor**.
Generalizable: *un valor por defecto que se persiste deja de ser un valor por defecto.*

**El destello y la CSP.** El tema se aplica ahora en un script del `<head>`, antes del primer
pintado. La CSP del proyecto prohíbe scripts en línea, así que va declarado por **hash
SHA-256** en `script-src` — no con `'unsafe-inline'`, que habría abierto la política entera
para resolver un parpadeo. Consecuencia a recordar: **si se toca ese script hay que
recalcular el hash**, y si no se recalcula el navegador lo bloquea en silencio y vuelve el
destello sin que nada falle a la vista. El script duplica a propósito la clave y los valores
del hook; no hay forma de importarlo sin reintroducir el destello.

Comprobado en Chrome, no solo con tests: sin violación de CSP en la consola, `data-theme`
ya puesto al cargar, y **nada escrito en `localStorage` tras la carga**.

**`robots.txt` (T3-19).** El motivo no es el SEO sino la reescritura SPA: `/* /index.html
200` hace que **cualquier** ruta devuelva 200 con el HTML de la aplicación, así que para un
rastreador `/library` o `/profile` existen. Se excluyen la zona privada y las pantallas que
solo tienen sentido con un token en la URL. Queda escrito en el propio archivo que
**`robots.txt` pide, no impide**: el control de acceso es el backend.

**Un solo tipo de resultado (T3-11).** Los tres `record struct` privados tenían la misma
forma que `ServiceResult<T>`; existían por separado porque propagar un error entre tipos
distintos era incómodo. Eso se resuelve con `ToFailure<TOther>()`, no duplicando el tipo.
Detalle de C# que salió al hacerlo y conviene no olvidar: en `ServiceResult<ContentKind>` la
propiedad `Value` **no** es `ContentKind?` —`T?` con un `T` sin restringir no envuelve en
`Nullable` para tipos valor—, así que seis `contentKindResult.Value!.Value` se quedaron en
`.Value`.

**Y el `.editorconfig` ya trabaja.** Al reescribir archivos con Python los volví a guardar
con BOM y `dotnet format --verify-no-changes` lo cazó en tres. Es exactamente para lo que se
puso.


### 2026-09-02 — Tier 3 cerrado: dependencias, Prettier, y la trampa que avisé y me pilló igual

**`npm audit fix` salió mejor de lo esperado (T3-21).** Sin `--force`, los 13 avisos caben
dentro de los rangos `^` que ya había: **`package.json` no cambia** y el diff es solo del
lockfile. Se hizo copia previa de los dos archivos y se comprobó el árbol después
—`@testing-library/dom` presente, tipos, lint, suite y build—, porque un `--legacy-peer-deps`
ya lo rompió una vez este mismo día. **La afirmación «sin vulnerabilidades» caduca**: en el
ROADMAP va ahora con la fecha en que se comprobó.

**Prettier fija lo que había, no lo que Prettier prefiere (T3-22).** Sin punto y coma,
comillas simples, ancho 100: el estilo que ya seguía el código. Configuración y pasada de
formateo en **commits separados**, para que el diff de 79 archivos se pueda saltar de un
vistazo. `.md` queda fuera —reflowar los párrafos convertiría cualquier cambio futuro de una
frase en un diff de página entera— y también los archivos de otras herramientas
(`.agents/`, `.codegraph/`, `.mcp.json`, `skills-lock.json`), que las gestionan ellas.

**Y la pasada destapó a la primera exactamente lo que yo mismo había dejado advertido dos
horas antes:** Prettier reformateó el script en línea de `index.html` y el hash SHA-256 de la
CSP dejó de cuadrar. Es el peor tipo de fallo —el navegador bloquea el script en silencio y
vuelve el destello sin que nada falle a la vista— y **la advertencia escrita no sirvió de
nada**, que es justamente la lección: un aviso en un documento no protege un acoplamiento;
lo protege un mecanismo.

Los dos que se pusieron:

- **`<!-- prettier-ignore -->` suelto.** Prettier solo reconoce la directiva si el comentario
  no lleva nada más. Lo probé primero con la explicación dentro del mismo comentario y **no
  surtió efecto**; la explicación va ahora en un comentario aparte, justo encima.
- **Un test** (`src/config/index-csp.test.ts`) que recalcula el hash desde `index.html` y, si
  no cuadra, dice cuál es el valor correcto. Comprobado que falla de verdad quitándole un
  punto y coma al script. Hay un segundo test que impide «arreglar» un problema de CSP
  metiendo `'unsafe-inline'` en `script-src` — acotado a `script-src` a propósito, porque
  `style-src` sí lo lleva y lo necesita; mi primera versión no distinguía y falló.

**El repaso de T3-23 encontró lo que buscaba.** `apiUrl` hace `.trim()`, pero el esquema
validaba el valor **sin recortar**: un `VITE_API_URL= /api` con un espacio de más tumbaba la
aplicación entera. Misma familia que la cadena vacía. **La regla que queda: la validación se
hace sobre el mismo valor que va a usarse, no sobre el que llega.**


### 2026-09-02 — Licencia MIT y borrado de cuenta

**Licencia (T1-15).** MIT, elegida por el propietario. `LICENSE` en los dos repositorios y
declarada en ambos README. El aviso de copyright nombra la identidad de git (`xfiberex`);
queda anotado en el README que conviene sustituirla por el nombre legal si la autoría tiene
que poder acreditarse. **Esto no es asesoramiento jurídico:** con valor comercial o
colaboradores, requiere revisión legal.

**Borrado de cuenta (T1-14).** Tres decisiones que conviene no reabrir:

**La reautenticación no es opcional aunque el JWT sea válido.** Es la única operación del
sistema sin vuelta atrás, y un token puede quedar abierto en un equipo prestado. Con
contraseña se comprueba **por el mismo camino que el login** —comprobar bloqueo, contar el
fallo, reiniciar el contador al acertar—: con `CheckPasswordAsync` a secas, este endpoint
sería un oráculo de contraseñas sin freno para quien tuviera un token robado. Las cuentas
de Google o GitHub no tienen contraseña, así que escriben su propia dirección; no prueba
identidad —el correo se ve en la pantalla de al lado— pero sí que la acción es deliberada,
que es lo que protege del clic accidental.

**Lo que la cascada no cubría, y hay que recordar si se añaden tablas.** `MediaItems`,
`UserCategories`, `UserFormats`, `RefreshTokens` y los logins externos caen solos al borrar
el usuario. **`OAuthStates` no**: no tiene `UserId` y su columna `PendingEmail` guarda la
dirección, así que sin borrarlos a mano el correo sobrevivía a la cuenta hasta que la purga
lo retirase al día siguiente. Regla: *toda tabla nueva que guarde datos personales sin
`UserId` hay que sumarla a mano a este borrado.* Va todo en una transacción, porque a medio
camino quedaría una cuenta sin datos o unos datos sin cuenta y ninguna de las dos cosas se
arregla desde la aplicación.

**En el cliente, `deleteAccount` limpia la sesión local solo si el servidor confirma** — al
revés que `logout`, que la limpia pase lo que pase. Si se limpiara igualmente, quien
escribiera mal la contraseña acabaría en el login creyendo que borró su cuenta, sin forma de
saber desde el cliente que sigue existiendo.

**Comprobado contra PostgreSQL real, no solo con la suite:** cuenta con 1 elemento, 10
formatos y 1 refresh token; con la contraseña incorrecta devuelve 400 y la cuenta sigue ahí;
con la correcta, 204 y **cero filas huérfanas** en las cuatro tablas.

**Un descuido propio, para que no se repita:** encadené `dotnet format --verify-no-changes`
con `;` en vez de `&&`, así que su fallo no detuvo el commit y di por bueno un formato que
no lo estaba. Lo cazó el propio verify al repetirlo, no yo. **En una cadena de verificación,
`&&` y nunca `;`.**


### 2026-09-02 — La suite pasa a PostgreSQL, y el susto que vino con ello

**El sondeo primero.** Iba a escribir los tests de la búsqueda (T2-04) y lo primero fue
comprobar si eran posibles: no lo eran. `GET /api/media-items?search=...` devolvía **500**
sobre la SQLite en memoria de la suite, porque `EF.Functions.ILike` solo existe en Npgsql.
Una funcionalidad visible del producto no podía tener ni un test. Eso convirtió T2-04 en
T2-02.

**Cómo quedó.** Cada clase de test recibe una base desechable. Las migraciones se aplican
**una vez por ejecución** sobre una plantilla y cada clase la copia con
`CREATE DATABASE ... TEMPLATE`, que es una operación de archivos: se ejercitan de verdad
(T2-03) sin pagarlas veinte veces. La cadena sale de `TRACKERMULTIMEDIA_TEST_POSTGRES` o de
los mismos user-secrets del backend, de los que solo se reutilizan servidor y credenciales.
De rebote desaparece la `SqliteConnection` compartida y con ella la carrera de T2-28.

**El fallo que hay que no repetir.** Al montarlo quité del `AppFactory` un bloque de
`RemoveAll` que parecía redundante. No lo era: **sobrescribir la cadena de conexión en la
configuración no basta.** `Program.cs` la lee de `builder.Configuration` al componer los
servicios, *antes* de que se apliquen las fuentes que añade la factoría de tests, así que el
`AddDbContext` de la aplicación se queda con la de los user-secrets — **la base real**. Una
tanda de tests escribió **199 usuarios** en la base de desarrollo. Lo destapó un test que
falló diciendo que un correo ya existía; fui a ver dónde existía y estaba en la base
equivocada.

**La regla:** en un test de integración, el registro del `DbContext` **se reemplaza**, no se
reconfigura. Y la comprobación que lo cierra: contar filas en la base real antes y después
de una ejecución completa (199 → 199).

**El escapado de la búsqueda estaba bien.** Los tests de `%`, `_` y `\` pasaron a la
primera, cada uno con un señuelo que caería si el escapado fallara. No todo test nuevo
encuentra un fallo; este documenta que algo delicado ya funcionaba.

**Y dos veces me pilló el `.editorconfig` con finales de línea LF.** La segunda descubrí por
qué se me escapaba: `comando | tail -3 && echo ok` usa el estado de salida de `tail`, no del
comando. Con `set -o pipefail` sí falla. Es la misma trampa del `;` de antes, disfrazada de
tubería.

**CSP (T2-14).** `connect-src` queda en `'self'`: los catálogos externos y los proveedores
OAuth los consulta el backend, no el navegador. **Una CSP que permite orígenes que no se
usan no protege de menos, describe mal el sistema.** Las cabeceras que un `<meta>` no puede
declarar viven en `netlify.toml`; la política de contenido sigue en un único sitio para que
el hash del script en línea no pueda desincronizarse.

**Queda pendiente de tu decisión:** la base de desarrollo tiene 197 cuentas de test
(`@test.com` y `@example.com`) y 2 reales. Borrarlas es un borrado sobre datos reales, así
que no se hace sin visto bueno.


### 2026-09-02 — Medir la cobertura, y lo que enseñó

**El número no era lo interesante.** 82,8 % de líneas suena bien; 48,4 % de ramas ya no
tanto. Pero lo que valió la medición fue **qué estaba exactamente a cero**, algo que no se
ve leyendo el código:

- `AniListSearchService` y `MangaDexSearchService`, enteros. **374 líneas y dos de los tres
  proveedores de catálogo sin una sola prueba.** Solo Jikan tenía tests, porque fue el
  primero; los otros dos se añadieron después y nadie volvió a mirar.
- `FormatsService.UpdateAsync` y `DeleteAsync`, y `FormatsController.Update`. Las categorías
  tenían su `CategoryCrudTests` desde el principio y los formatos no: el mismo tipo de
  asimetría, invisible salvo que se mida.

**La regla que queda:** cuando algo se añade «igual que lo que ya había», hay que comprobar
que también se le añadieron los tests. La cobertura de ramas es la que delata esto; la de
líneas se queda alta aunque falten casos enteros.

**Y salió un defecto de verdad (T4-09).** `MangaDexSearchService` deriva el identificador
entero con `Guid.Parse`, que lanza `FormatException` si el UUID viene mal formado. Esa
excepción **no** está en el filtro del `catch` de `SearchAsync`, así que un único elemento
defectuoso devolvía un 500 y se llevaba por delante los resultados buenos de la misma
respuesta. Es exactamente el patrón de T2-06: una suposición sobre la forma de la respuesta
de un proveedor que se escapa del bloque que debía contenerla. Ahora se descarta ese
elemento, igual que ya se descartaba uno sin título.

**Herramienta, no instalación global.** `ReportGenerator` va en
`.config/dotnet-tools.json`, así que `dotnet tool restore` deja la misma versión en
cualquier equipo. Una herramienta global sería una dependencia invisible del proyecto.

---

### 2026-09-04 — Llevarse los datos, y dos sondas donde había una que no servía

**La exportación personal no es una ampliación de la de biblioteca, y esa fue la decisión
de fondo.** La tentación era añadirle el correo y las fechas al archivo que ya existe.
Habría roto la importación: ese formato es el que sabe leer `ImportAsync`, y por eso deja
fuera justamente lo que no se puede reimportar. Son dos archivos porque responden a dos
preguntas distintas —«quiero mudarme de instalación» y «quiero saber qué tenéis sobre
mí»—, y el segundo incrusta al primero llamando al mismo código, para que el mapeo de cada
campo no se duplique.

**Lo que se deja fuera a propósito importa tanto como lo que se incluye.** Ni contraseñas,
ni tokens de refresco —ni siquiera su hash: son credenciales en activo, y el archivo acaba
en una carpeta de descargas o adjunto en un correo—, ni el `ProviderKey` de los logins
externos, que identifica a la persona dentro de Google o GitHub y no le sirve de nada a
quien se lleva sus datos. El test que lo comprueba mira **el JSON crudo**, no propiedades
deserializadas: lo que no debe salir no aparece en ningún campo que se pueda leer. Se
verificó mutando el servicio para que filtrara el hash y viendo el test en rojo.

**Efecto lateral útil:** los formatos personalizados no viajan en el archivo de biblioteca,
así que hasta ahora no estaban en **ninguna** exportación. Ahora sí.

**Dos sondas, no una, y no por ceremonia.** `/health` era la única y no comprobaba nada.
Lo evidente era meterle la base de datos dentro; habría sido un error. `render.yaml` apunta
ahí con `healthCheckPath`, y Render **reinicia el servicio** cuando esa ruta falla:
reiniciar el proceso no levanta una base caída, así que lo único que se conseguiría es un
bucle de reinicios durante toda la avería. `/health` se queda como sonda de vida y la
comprobación de base vive en `/health/ready`. El test que da sentido a la tarea es el que
apunta el `DbContext` a un host inalcanzable y comprueba las dos cosas a la vez: 200 en una,
503 en la otra.

**La especificación OpenAPI se publica de dos formas porque el problema tenía dos mitades.**
El archivo versionado en `docs/openapi.json` resuelve «quiero consultar el contrato» sin
arrancar ni desplegar nada. El interruptor `OpenApi__Exposed` resuelve «quiero verla en el
servicio». No se sirve por defecto fuera de desarrollo: no contiene secretos ni habilita
nada, pero entrega el mapa completo de rutas y parámetros, y eso se decide por despliegue.

**Y el test negativo hizo falta de verdad.** El primero que escribí comprobaba que con el
interruptor activado la especificación se sirve — y habría pasado igual aunque se publicara
siempre, porque el host de pruebas arranca en `Development`. Al escribir el contrario
—«fuera de desarrollo no se sirve»— apareció que `AppFactory` no sabía cambiar de entorno, y
al enseñárselo apareció el fallo de configuración que está anotado arriba en las trampas.
Un test que solo puede pasar no comprueba nada; es la misma lección de la sonda de salud, en
el mismo día.
