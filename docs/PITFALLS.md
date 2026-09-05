# Trampas del stack

> Cada punto de esta lista costó un fallo real. Están aquí para no volver a pagarlo.
> Lo que se decidió a partir de ellos está en [DECISIONS.md](DECISIONS.md).

## EF Core y migraciones

**Una migración sin su `.Designer.cs` no existe para EF Core.** EF identifica las migraciones por
el atributo `[Migration("...")]`, que el generador escribe en el `.Designer.cs`, **no** por el
nombre del `.cs`. `AddUserFormatIdToMediaItems` estaba escrita a mano sin Designer: compilaba, se
veía en `Migrations/`, y `dotnet ef migrations list` no la mencionaba. Como el *snapshot* del
modelo sí incluía `UserFormatId`, EF daba el modelo por representado y tampoco iba a regenerarla:
el arranque decía «no hay migraciones pendientes» sobre una base a la que le faltaba la columna, y
`GET /api/media-items` respondía 500 con `42703: column m.UserFormatId does not exist` (T0-06).
**Crear siempre las migraciones con `dotnet ef migrations add`**, y no fiarse de que el arranque
diga que el esquema está al día: comprobarlo con `dotnet ef migrations list` sobre una base recién creada.

**`dotnet ef migrations add --no-build` genera migraciones vacías.** Usa el ensamblado compilado
anterior, así que no ve los cambios del modelo y produce un `Up()` vacío sin avisar. Y
`dotnet ef migrations remove` se conecta a la base para comprobar si está aplicada, así que falla
sin conexión: en ese caso hay que borrar a mano el `.cs` y el `.Designer.cs`.

**`EnsureCreated()` no ejecuta las migraciones.** Construye el esquema directamente desde el
modelo. Esa es la razón por la que ningún test detectó que `Migrate()` estaba comentado en
producción (T2-03). Desde T2-02 la suite aplica migraciones de verdad.

**Un índice parcial de PostgreSQL no sirve para consultas generales.** El único índice que
empezaba por `MediaItems.UserId` tenía un filtro (`ExternalId IS NOT NULL AND ...`), y EF Core dio
por buena la cobertura de la clave foránea y no creó otro. PostgreSQL no usa un índice parcial
salvo que el predicado de la consulta implique el filtro: en la práctica **no había índice** para
la consulta más frecuente de la aplicación (T1-07).

**Una navegación sobre una entidad que Identity ya mapea crea una segunda clave foránea.**
`IdentityDbContext` configura la relación de `IdentityUserLogin` sin propiedad de navegación. Al
declarar `ICollection<IdentityUserLogin<Guid>> ExternalLogins` sin configurarla, EF la interpretó
como una relación **adicional** y generó la columna sombra `ApplicationUserId` junto a la `UserId`
de Identity. Como `UserManager.AddLoginAsync` solo rellena `UserId`, la sombra quedaba a NULL y la
navegación nunca devolvía nada: por eso el perfil nunca mostraba las cuentas vinculadas. **Hay que
configurarla a mano en `OnModelCreating`** (T1-01).

**`EF.Functions.ILike` solo existe en Npgsql.** Mientras los tests corrieron sobre SQLite, la
búsqueda de la biblioteca devolvía 500 en la suite y no podía tener ni una prueba. La lección es
general: **un proveedor de test distinto del de producción no prueba nada específico del proveedor.**

## Tests

**`WebApplicationFactory` llega tarde a lo que se lee al componer los servicios.** `Program.cs`
guarda el secreto JWT en una variable local durante el arranque, momento en el que el factory
todavía no ha inyectado su configuración. Lo mismo, y peor, con la cadena de conexión: en
`Development` los user-secrets tapaban el hueco, pero al arrancar el host en `Production` para
comprobar la especificación OpenAPI no había **ninguna** cadena y el host ni siquiera construía.
Lo que sí llega a tiempo es `builder.UseSetting(...)`, que entra en la configuración **del host**.
`AppFactory` aplica sus ajustes por las dos vías, más un `PostConfigure<JwtBearerOptions>`.

**El rate limiter agota su cupo en los tests.** `TestServer` no asigna `RemoteIpAddress`, así que
todas las peticiones caen en la misma partición `"unknown"` y el límite de 10/minuto de los
endpoints de autenticación salta a mitad de la suite. `AppFactory` sustituye las tres políticas
por limitadores sin límite.

**En un test de integración, el registro del `DbContext` se reemplaza, no se añade.** Añadir un
segundo registro deja el anterior en pie y el comportamiento depende del orden de resolución.

**Un test que solo puede pasar no comprueba nada.** El primer test del interruptor de OpenAPI
comprobaba que con el interruptor activado la especificación se sirve — y habría pasado igual
aunque se publicara siempre, porque el host de pruebas arranca en `Development`. La misma lección
que la sonda de salud: `AddHealthChecks()` **sin comprobaciones registradas devuelve siempre
`Healthy`**, así que `/health` decía «todo bien» con PostgreSQL caído y nadie lo miró en un año.
**Una sonda que no puede fallar no es una sonda** (T4-10).

**Un test en rojo que parece obsoleto se lee antes de reescribirlo.** El 2026-08-27 uno de ellos
estaba señalando un defecto real de accesibilidad que llevaba tiempo en producción.

*(Histórico, ya resuelto: la suite compartía una sola `SqliteConnection` y EF registraba una
colación en ella por cada `DbContext`, sobre una colección no segura entre hilos. El síntoma era
`Operations that change non-concurrent collections must have exclusive access`, sin relación con
el test en ejecución. Desactivar el paralelismo de xUnit no lo arreglaba: la concurrencia estaba
dentro del host. Desapareció al pasar la suite a PostgreSQL con una base por clase, T2-28.)*

## React y accesibilidad

**`autoFocus` se aplica antes que los efectos, y rompe la devolución del foco.** React lo procesa
durante el commit, así que cuando el efecto de un diálogo lee `document.activeElement` para
recordar «quién tenía el foco antes», ya lo tiene un campo de dentro del propio diálogo. Al
cerrarse, ese campo no existe y el foco se pierde al `<body>`. Por eso `useModalDialog` mantiene
un seguimiento del último foco **fuera** de cualquier diálogo, con un listener de `focusin`
instalado al importar el módulo.

**`offsetParent !== null` no sirve para detectar visibilidad dentro de un modal.** `offsetParent`
también es `null` para todo lo que está dentro de un contenedor `position: fixed` —que es
exactamente lo que es un diálogo—, así que la lista de elementos enfocables salía vacía y la
trampa de foco no llegaba a existir. Se usa `checkVisibility()` cuando está disponible.

**Cambiar estado en un efecto para animar una salida desmonta el componente un render.** Entre
«`open` pasa a false» y «el efecto marca que se está cerrando» hay un render con ambos a false
donde el diálogo devuelve `null`: se desmonta y se vuelve a montar al instante, el foco cae al
`<body>` y el formulario se reinicia durante la animación. La solución es ajustar el estado
**durante el render**, comparando con el valor anterior guardado también en estado —no en una ref,
que `react-hooks/refs` prohíbe leer ahí—.

**`StrictMode` monta los efectos dos veces, y con tokens que rotan eso es un robo aparente.**
El efecto que recupera la sesión al arrancar salía dos veces en desarrollo. Las dos peticiones
partían antes de que llegara la respuesta de la primera, así que **ambas presentaban el mismo
token de refresco**; la segunda llegaba con uno ya rotado, que es exactamente la señal de robo de
T2-12, y el backend revocaba **todas** las sesiones del usuario. El síntoma era peor que un error
visible: la aplicación seguía funcionando con el access token que ya había obtenido, y la sesión
aparecía muerta solo en la recarga siguiente, sin nada que la relacionara con la anterior.
Apareció el 2026-09-04 al comprobar T4-01 en un navegador de verdad; la suite no podía verlo
porque jsdom no monta dos veces y cada test tenía su propio módulo. Es **anterior a T4-01** —el
código que leía el token de `localStorage` tenía la misma carrera—, solo que sin cookie nadie
había mirado. La solución es que la renovación tenga **una sola petición en vuelo**
(`refreshSession` en `shared/api/axios.ts`), compartida por el arranque y por el interceptor de
401. **Regla general: con tokens de un solo uso, cualquier llamada que pueda solaparse consigo
misma necesita deduplicación, no reintentos.**

**El límite de error va por fuera de los proveedores.** Colocado por dentro, un fallo del propio
proveedor no lo alcanza.

**Un `label` sin `htmlFor` no falla en ningún sitio salvo en los tests de accesibilidad.** ESLint
no lo detecta sin `eslint-plugin-jsx-a11y`, y TypeScript tampoco. Los descubrió Testing Library
con «no form control was found associated to that label» (T1-17, T2-22).

**El tema se aplica en un script del `<head>`, y la CSP lo bloquea si cambia un byte.** Como la
CSP prohíbe scripts en línea, el script se declara por hash SHA-256. Prettier lo reformateó y el
hash dejó de cuadrar: **fallo mudo** —el navegador bloquea el script y vuelve el destello sin que
nada falle a la vista—. Van dos medidas: un `<!-- prettier-ignore -->` suelto (Prettier solo
reconoce la directiva si el comentario no lleva nada más) y un test que recalcula el hash.

## Herramientas y entorno

**`--legacy-peer-deps` no es «instalar ignorando un aviso»: rehace el árbol entero.** Al añadir
`eslint-plugin-jsx-a11y`, la instalación con ese flag **dejó fuera `@testing-library/dom`** —una
dependencia de pares de `@testing-library/react`— y rompió los tipos de todos los archivos de
test, con un error que no mencionaba npm por ninguna parte. La forma correcta de forzar un *peer*
concreto es un `overrides` en `package.json`, que toca solo lo que se nombra. Tras cualquier
operación que rehaga el lockfile (`npm audit fix` incluido): copia previa, y después comprobar
suite, `tsc -b`, lint y build.

**PowerShell expande `$` dentro de comillas dobles, y se come los secretos.**
`dotnet user-secrets set "Jwt:Secret" "$7kTbEi..."` falla con `Missing parameter value for 'value'`:
PowerShell sustituye `$7kTbEi...` por cadena vacía. Con **comillas simples** el texto va literal.
Afecta a cualquier secreto que empiece por `$` o lo contenga, y a la cadena de conexión, que
además lleva `;`. Mejor todavía: generar el valor en una variable y pasarla, para que no aparezca
en pantalla ni en el historial de la terminal.

**Anidar un proyecto dentro de la carpeta de otro proyecto .NET rompe la compilación del que
envuelve.** El `.csproj` del backend está en la raíz del repositorio, así que sus globs
(`**/*.cs`) se tragaron los archivos de `TrackerMultimedia.Tests/` al moverlos dentro. El síntoma
es una avalancha de `error CS0246: no se encontró 'Fact'` **atribuidos al proyecto de backend**,
no al de tests. La solución es `DefaultItemExcludes` en `TrackerMultimedia_Backend.csproj:12`; si
se renombra la carpeta de tests hay que actualizar esa propiedad **y** el `.dockerignore`.

**`core.autocrlf` de Git rompe el hash de la CSP al clonar en Windows.** Apareció el 2026-09-04 al
mover el proyecto a otro equipo. Git en Windows trae `core.autocrlf=true`, y el repositorio del
frontend no tenía `.gitattributes`: el checkout convirtió los 319 archivos de texto a CRLF. El
`index.html` servido deja de coincidir con el hash SHA-256 con el que la CSP autoriza su script en
línea, así que **el navegador lo bloquea y vuelve el destello de tema claro que T3-18 corrigió**,
sin un solo error a la vista. El test de la CSP lo detectó, que es exactamente para lo que se
escribió. El `.editorconfig` no protege de esto: manda sobre el editor, no sobre el checkout. La
solución es un `.gitattributes` con `* text=auto eol=lf`, que además no genera ningún cambio de
contenido porque los blobs del repositorio ya eran LF. **Ojo con el diagnóstico:** tras añadirlo,
`git status` marca cientos de archivos como modificados mientras `git diff` no ve nada — es caché
de `stat`, y se asienta con `git add --renormalize .` seguido de `git reset`.

**Y el mismo problema al revés en el backend:** su `.editorconfig` declara `end_of_line = crlf`, así
que ahí CRLF es lo correcto. Cualquier script que reescriba archivos del backend con LF —los de
Python con `newline='
'`, por ejemplo— deja `dotnet format whitespace --verify-no-changes` en
rojo. Es la tercera vez que este proyecto tropieza con los finales de línea.

**`npm run build` no ejecuta el linter.** Solo hace `tsc -b && vite build`, así que un error de
ESLint llega a producción sin que el build se queje (T2-23). Y al revés: `tsc -b` no ejecuta los
tests, y los tests no comprueban los tipos.

**`localhost` es un vecindario compartido, y las cookies no distinguen el puerto.** Para
`localStorage` el origen es esquema + host + **puerto**, así que `localhost:5173` y `localhost:5174`
están aislados. Para las **cookies no**: el puerto no forma parte de su identidad. Una cookie puesta
en `localhost:5173` se manda a cualquier cosa que escuche en `localhost`, sea del proyecto que sea.

Aquí eso importa dos veces. La primera, comprobada el 2026-09-04: en el `localStorage` de
`localhost:5173` conviven las claves de otros proyectos locales que usan el puerto por defecto de
Vite —y una de ellas guardaba un refresh token ajeno en claro—, porque **todos comparten el mismo
origen**. La segunda es sobre `tm_refresh` (T4-01): `Path=/api/auth` la limita, pero cualquier
proyecto que sirva esa ruta en `localhost` la recibiría.

No es un fallo de la aplicación, es cómo está definido el origen, y solo pasa en desarrollo: en
producción cada proyecto tiene su dominio. La defensa buena es **darle a cada proyecto un host
propio** —los navegadores resuelven cualquier `*.localhost` a `127.0.0.1` sin tocar el `hosts`—
porque cambiar el host sí separa el `localStorage` **y** el tarro de cookies. Fijar puertos
distintos con `strictPort` separa el `localStorage`, pero **no las cookies**.
