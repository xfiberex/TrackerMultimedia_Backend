# Decisiones y convenciones

> Por qué el proyecto es como es. Lo que **no** hay que reabrir está marcado como tal.
> Las trampas técnicas que costaron un fallo están aparte, en [PITFALLS.md](PITFALLS.md).

## Sesión y tokens

**Access token en memoria, refresh token en cookie `HttpOnly`.** El access token vive en una
variable de módulo (`Frontend/src/shared/api/tokenStore.ts`), fuera del árbol de React, para que
el interceptor de Axios pueda leerlo sin depender del ciclo de vida de los componentes. Nunca se
persiste. El refresh va en una cookie que el código de la página no puede leer.

*Decisión anterior, superada el 2026-09-04 (T4-01):* el refresh se guardaba en `localStorage`,
legible por cualquier JS del origen. Se aceptó conscientemente a cambio de que la sesión
sobreviviera a las recargas, con la cookie ya registrada como alternativa correcta. Lo que la
cerró es que las mitigaciones que había —hash en servidor, rotación y revocación de familia— son
de **detección**, no de prevención: reducen la ventana de un robo, no lo impiden.

**Solo dos endpoints se autentican con la cookie, y es a propósito.** `/auth/refresh` y
`/auth/logout`. Todo lo demás sigue yendo con el Bearer en memoria, que no es credencial ambiente
y por tanto no es atacable por CSRF. Mantener esa frontera es lo que hace que la superficie de
CSRF sea de dos endpoints y no de toda la API. **No mover un endpoint a la cookie sin volver a
pensar esto.**

**Contra CSRF: `SameSite` más una cabecera obligatoria, no doble envío.** La cookie va
`SameSite=Strict` y los dos endpoints exigen `X-TM-Client`. Un `<form>` de otro sitio no puede
añadir cabeceras, y un `fetch` que lo intente deja de ser una petición simple y dispara un
preflight que el allowlist de CORS rechaza. Se eligió frente al doble envío con token porque no
añade estado que sincronizar en login, refresh, logout y arranque, y porque **sigue funcionando si
la cookie tiene que pasar a `SameSite=None`** en un despliegue con dominios distintos — que es
justo la trampa que dejaba confiar solo en `SameSite`. Los atributos son configurables en
`Auth:RefreshCookie`.

**La renovación de sesión tiene una sola petición en vuelo.** `refreshSession`, en
`shared/api/axios.ts`, es el único camino: lo usan tanto el arranque de la aplicación como el
interceptor de 401. No es una optimización sino correctitud, porque los tokens rotan y dos
renovaciones simultáneas presentan el mismo valor: la segunda parece un robo y revoca todas las
sesiones. **Cualquier llamada nueva a `/auth/refresh` pasa por ahí.**

**El callback de OAuth no lleva ningún token en la URL.** El backend escribe la cookie en la
propia redirección y el fragmento solo lleva `return_path`; el frontend cambia eso por un access
token llamando a `/auth/refresh`. El fragmento no se manda al servidor, así que nunca llegó a los
logs, pero sí queda en el historial del navegador y en cualquier sitio donde se pegue la
dirección, y eso no tiene arreglo una vez ocurre. El precio es una ida y vuelta al entrar.

**El servidor solo guarda el hash del refresh token.** SHA-256 en base64, nunca el valor en claro,
rotado en cada uso: el token consumido se marca revocado y se emite uno nuevo. Presentar uno ya
rotado revoca **todas** las sesiones del usuario.

**Una sola puerta de emisión de sesiones.** `AuthSessionService.CreateSessionAsync` es el único
sitio que emite un par de tokens, vengan de login manual, de Google o de GitHub. Con tres caminos
duplicando la lógica era cuestión de tiempo que uno se olvidara de persistir o de rotar.
**No reabrir**: cualquier camino de acceso nuevo pasa por ahí.

## OAuth

**Vinculación explícita, nunca automática.** Cuando alguien entra con Google o GitHub y ese correo
ya tiene cuenta local con contraseña, el backend genera un token de vinculación de vida corta y
manda al usuario a `/link-account`, donde debe escribir su contraseña. Sin ese paso, cualquiera
que registre una cuenta en un proveedor externo con el correo de otra persona se apodera de su
cuenta. **No reabrir.**

**Se exige correo verificado por el proveedor.** GitHub obliga a que el email sea primario *y*
verificado; Google comprueba la bandera `email_verified`. Un proveedor que no pueda garantizarlo
no debe integrarse por esta vía.

**La máscara de proveedores del frontend es un AND, nunca un OR.** Ocultar «Continuar con Google»
desde la configuración del frontend es solo interfaz: quien decide qué proveedores se aceptan
sigue siendo el servidor. Puede ocultar un proveedor que el servidor acepta, nunca mostrar uno
que el servidor no acepta.

## Datos

**Todo pertenece a un usuario y toda consulta lo filtra.** `MediaItem`, `UserCategory` y
`UserFormat` llevan `UserId`, y cada lectura o escritura incluye `Where(x => x.UserId == userId)`
—incluida la resolución de categorías y formatos al guardar, para que nadie asigne una categoría
ajena mandando su identificador—. `TrackerMultimedia.Tests/Isolation/` lo verifica endpoint por
endpoint. **Es la invariante más importante del sistema: cualquier consulta nueva la respeta o no entra.**

**`MediaType` es legado; `ContentKind` es lo vigente.** `MediaType` (Anime, Manga, Donghua,
Manhwa, Manhua) fue el primer modelo y quedó atado a datos ya creados. `ContentKind` (Series,
Movie, Book, Comic, Game, Podcast, Video, Album, Other) lo sustituye. `ResolveContentKind` acepta
ambos, los mapea y rechaza las combinaciones incoherentes. No se elimina `MediaType` para no
romper los elementos existentes ni las exportaciones ya generadas.

**Los formatos por defecto se crean al dar de alta la cuenta, nunca al leerlos.**
`FormatsService.EnsureDefaultFormatsAsync` es idempotente y lo llaman los dos únicos caminos que
crean usuarios: `AuthController.Register` y el caso B del callback de OAuth. Antes sembraba
`GET /api/formats`, es decir, un GET que escribía: con dos peticiones simultáneas de una cuenta
nueva, la segunda violaba el índice único y devolvía 500. **Consecuencia:** cualquier camino
nuevo que cree usuarios —incluidos los helpers de test, que van por `UserManager`— tiene que
llamar a ese método o la cuenta se queda sin formatos.

**El identificador externo es único por usuario, origen y tipo de medio.** El índice es parcial
(solo cuando hay identificador externo) para que los elementos manuales no colisionen. Se amplió
a `SourceType` el 2026-05-14 al añadir AniList y MangaDex: el mismo número significa cosas
distintas en cada catálogo.

**La exportación personal y la de biblioteca son dos archivos distintos y no se fusionan.**
Responden a dos preguntas —«quiero mudarme de instalación» y «quiero saber qué tenéis sobre mí»—.
El de biblioteca es el que sabe leer `ImportAsync`, así que meterle los datos de cuenta lo
rompería; el personal lo incrusta llamando al mismo código para no duplicar el mapeo. **Quedan
fuera a propósito**, y el test lo comprueba sobre el JSON crudo: contraseñas, tokens de refresco
—ni siquiera su hash, son credenciales en activo— y el `ProviderKey` de los logins externos.

**El borrado de cuenta exige reautenticación aunque el JWT sea válido.** Es la única operación sin
vuelta atrás y un token puede quedar abierto en un equipo prestado. Con contraseña se comprueba
**por el mismo camino que el login** —bloqueo, contar el fallo, reiniciar al acertar—, porque con
`CheckPasswordAsync` a secas el endpoint sería un oráculo de contraseñas sin freno. Las cuentas
solo-OAuth escriben su propia dirección: no prueba identidad, pero sí que la acción es
deliberada. **Lo que la cascada no cubre:** `OAuthStates` no tiene `UserId` y su columna
`PendingEmail` guarda la dirección, así que se borra a mano dentro de la misma transacción.
Si se añaden tablas con datos personales, revisar esto.

**En el log va el `UserId`, no el correo.** Donde todavía no hay cuenta —un alta que falla, un
aviso a alguien sin cuenta— va la dirección parcialmente enmascarada (`Infrastructure/Logging/PersonalData`).
El motivo: el log tiene su propia conservación, y una dirección completa ahí sobrevive al borrado
de la cuenta.

## Operación y diagnóstico

**Un solo identificador de petición, calculado en un solo sitio.** `RequestCorrelation.GetCorrelationId`
es el único punto que decide cuál es, y existe porque antes había dos: la respuesta de error llevaba
`Activity.Current?.Id` y el log escribía `HttpContext.TraceIdentifier`. Valores y formatos distintos,
así que el código que el usuario leía en pantalla no aparecía en ninguna parte del registro. La
correlación que T2-05 quiso montar nunca llegó a funcionar, y **ninguna prueba lo notaba** porque
todas miraban el cuerpo de la respuesta, no el log.

**Se usa `TraceId`, no `Activity.Current.Id`.** `Id` incluye el identificador del *span* actual, que
cambia al entrar en cualquier actividad hija —una llamada HTTP saliente, por ejemplo—, así que el
valor dependía de en qué punto de la petición se leyera. `TraceId` es estable durante toda la
petición. Además son 32 caracteres hexadecimales limpios, que alguien puede transcribir de una
pantalla; `0HNOATN8Q8LHV:00000001` no lo es.

**El `traceId` del 500 se escribe explícitamente, sin confiarlo a `CustomizeProblemDetails`.** Por
ese camino se responde con `Results.Problem`, que compone el cuerpo por su cuenta. Justo la
respuesta para la que se inventó el identificador podía ser la única que llegara sin él.

**JSON en el log fuera de desarrollo, salida legible dentro.** Las llamadas a `ILogger` del proyecto
ya usaban plantillas con parámetros con nombre; lo que se perdía era al escribir, porque el
formateador por defecto aplana todo a texto. En desarrollo se lee a ojo mientras se trabaja y el
texto plano gana; fuera, una línea JSON por evento se filtra con `jq` sin una expresión regular por
cada formato de mensaje. `IncludeScopes` va activo en ambos: es lo que hace que **todas** las líneas
de una petición lleven su `TraceId`, no solo la del error.

**Un fallo parcial que no se registra es un fallo invisible.** La búsqueda federada tolera que un
proveedor se caiga, y eso está bien; lo que estaba mal es que no dejara rastro. Una respuesta 200 con
resultados incompletos es indistinguible de una completa, así que MangaDex podía llevar semanas
caído sin que nadie lo supiera. Cada fallo se registra por separado y con su proveedor: agregarlos en
una sola línea impediría ver que siempre falla el mismo.

**Sin métricas, exportación ni alertas mientras el uso sea local.** Decisión del propietario del
2026-09-04 (T4-06, en suspenso). No se añaden dependencias de OpenTelemetry para alimentar a un
consumidor que no existe. Los medidores que ASP.NET Core, EF Core y el runtime ya publican se leen
con `dotnet-counters` sin tocar el código, que cubre el «mirar algo puntualmente» pero no el
«vigilar sin estar delante».

## Frontend

**Organización por features, no por tipo de archivo.** Cada dominio (`auth`, `media-items`,
`categories`, `catalog`, `search`) contiene su propia `api/`, `schemas/`, `views/` y
`components/`. Lo compartido de verdad vive en `shared/`. La alternativa —carpetas globales—
obliga a saltar entre cuatro sitios para tocar una sola funcionalidad.

**Las respuestas de la API se validan con Zod.** Cada endpoint tiene su esquema. Sirve para
detectar en el cliente los cambios de contrato del backend, en lugar de fallar más tarde con un
`undefined` en mitad del renderizado.

**Toda la aplicación está en español, sin capa de internacionalización.** Decisión consciente para
un producto de un solo idioma. Añadir un segundo exige extraer todos los textos y corregir
`formatDate`, que fija la configuración regional `es-DO` en vez de la del usuario (T4-03).

**No hay framework de CSS, y no hace falta añadirlo.** Un solo `index.css` con 239 clases escritas
a mano y una capa de tokens propia. La paleta no es arbitraria: `#64748b`, `#94a3b8`, `#cbd5e1`,
`#e2e8f0`, `#f1f5f9`, `#0f172a` son la escala `slate` de Tailwind transcrita. El estilo es *soft UI*
con superficies esmeriladas —neutros fríos, `rgba(255,255,255,0.92)`, sombras multicapa suaves,
degradado de fondo, Inter—. Revisado el 2026-09-04: migrar a Tailwind o a una librería de
componentes son semanas de trabajo para llegar al mismo aspecto y tirar por el camino la
accesibilidad ya pagada (T1-16 a T1-22). Lo que sí falta es **completar** la capa de tokens: los que
hay son todos de color y sombra, así que espaciados, radios y tipografía siguen escritos a mano en
cada clase, y eso explica que el archivo tenga casi 3.000 líneas.

**El contraste del texto se comprueba con una prueba, no con la vista.** Los colores de texto se
eligen contra el **peor** fondo de su tema —el arranque del degradado de página, no el blanco de las
tarjetas—, y hay siete pruebas que leen los tokens del CSS y fallan con el número exacto si alguno
baja de 4,5:1 (T1-23). Hizo falta porque nada más lo detecta: `eslint-plugin-jsx-a11y` mira el
marcado y no los colores, `tsc` no ve CSS, y un test de componente pasa igual con texto ilegible
porque `getByText` encuentra el nodo aunque nadie pueda leerlo. Estuvo mal meses.

**Los tokens de acción no son tokens de estado.** `--status-*` describe un elemento de la
biblioteca —planeado, en curso, abandonado—; `--action-*` y `--destructive-*` describen lo que hace
un control. Compartirlos parecía economía y era acoplamiento: el botón de borrar tomaba su tinta de
`--status-dropped`, de modo que retocar cómo se ve «abandonado» repintaba el control más destructivo
de la aplicación. Y como ese color se afinó para el tema claro y el oscuro solo sobreescribía su
fondo, el botón de borrar quedó en **2,47:1** en oscuro (T5-01).

**Cada tema define su tinta de peligro, no la hereda.** En claro es `#b14a52`; en oscuro, `#f87171`.
Un mismo rojo no puede contrastar sobre un fondo claro y sobre uno oscuro, y dar por hecho que sí es
exactamente cómo se llegó al defecto anterior.

**Una tinta fija que no sigue al tema desaparece; un fondo fijo solo se ve raro.** Por eso la regla
que se comprueba en la pantalla de acceso es sobre `color`: ninguna de sus reglas fija la tinta a
mano. Esa pantalla se escribió entera con valores del tema claro y solo dos reglas
`[data-theme='dark']`; lo que se veía bien se salvaba porque una regla oscura genérica aparecía más
abajo en el archivo y ganaba el desempate **por orden de aparición**. En cuanto una de esas
desapareció al tokenizar los botones, el acceso se volvió ilegible de golpe (T5-05).

**`--accent-*` son tonos de superficie; la tinta es `--accent-ink`.** Usar un color de acento como
color de texto es el error que dejó la pestaña activa del catálogo en 1,41:1 sobre el panel oscuro,
indistinguible de las inactivas.

**El modo por defecto es siempre el más cerrado.** Exponer la aplicación a la red es una opción
que se escribe al lanzarla (`npm run dev:lan`), no un comportamiento que traiga el script.
Este criterio ha hecho falta tres veces: `vite --host` reapareció dos veces en `dev` y se quitó
las dos.

**En un endpoint al que llega el navegador, el `catch` cubre el fallo, no solo un tipo de fallo.**
Un callback de OAuth que responde 500 deja al usuario delante de una pantalla de error del
servidor. Se redirige a la pantalla de inicio de sesión con un aviso.

---

## Decisiones cerradas

Revisado y decidido **no** hacerlo. No volver a proponerlo sin un hecho nuevo.

| Decisión | Motivo |
|---|---|
| **El proyecto no se despliega: uso local a través de Vite** | Decidido por el propietario el 2026-08-27. Render, Neon y Netlify deshabilitados y credenciales revocadas. Los blueprints y el `Dockerfile` **se conservan** como receta para volver: borrarlos no ganaría nada. Lo que **no** hay que hacer es dar por resueltos los hallazgos de producción —T1-05 sigue en el código tal cual— ni por cerrado lo legal: T0-05 está en suspenso, no hecha. |
| **No unificar los dos repositorios en un monorepo** | Decidido el 2026-08-27 al resolver T0-03. La carpeta que los contiene en local **no** es un repositorio y no debe volver a comportarse como si lo fuera: nada que deba sobrevivir puede quedarse en su raíz. Es la razón por la que esta documentación se movió dentro del repositorio de backend el 2026-09-04. |
| **No montar integración continua (T1-12)** | Decidido el 2026-08-27: un solo desarrollador, sin pull requests ni revisores. **La contrapartida hay que asumirla:** la CI existía para detectar que las suites se ponen en rojo, y eso ya había pasado —18 pruebas fallando sin que nadie lo notara—. La única red que queda es la rutina de [WORKFLOW.md](WORKFLOW.md). No volver a proponer CI salvo que entre otra persona al proyecto. |
| ~~**La base de desarrollo va en Docker**~~ · **REVERTIDA el 2026-08-27** | Vuelta a la instalación nativa de PostgreSQL 17 en el 5433, que es como arrancó el proyecto. Contenedor, volumen y `docker-compose.yml` eliminados. **Lo que sí se conserva es la práctica que Docker introdujo:** recrear la base desde cero antes de dar por buena una migración. Eso destapó T0-06, un fallo crítico invisible durante meses, y no depende de Docker sino de que alguien construya el esquema desde cero alguna vez. |
| **`VITE_API_URL` es una ruta relativa, no una URL absoluta** | Decidido el 2026-08-27 al cerrar T3-01. `/api` se resuelve contra el host desde el que el navegador cargó la página, así que funciona igual en `localhost` y al servir con `--host` desde otro dispositivo. Una URL absoluta a `localhost` rompe el segundo caso sin avisar: el otro dispositivo hablaría con su propio localhost. |
| **No añadir banner de consentimiento de cookies** | Revisado el 2026-08-27: no hay cookies, ni analítica, ni rastreadores. Solo `localStorage` para el token de refresco, el tema y los proveedores seleccionados, todos estrictamente necesarios. No requiere consentimiento previo, aunque sí describirse en la política de privacidad (T0-05). |
| **No eliminar `ReactQueryDevtools` de `main.tsx`** | Revisado el 2026-08-27 sobre `dist/assets/index-*.js`: Vite lo elimina por completo en el build. |
| ~~**No sustituir la lectura de tokens desde el fragmento de URL en el callback OAuth**~~ · **SUPERADA el 2026-09-04** | Se cumplió lo que la propia decisión anunciaba: la sustitución real era T4-01, y llegó. El fragmento ya no lleva tokens, solo `return_path`. ~~Decisión original:~~ revisado el 2026-08-27, el patrón mantenía los tokens fuera de los logs del servidor, y por eso no se cambió por un parche intermedio. |
| **No tratar `navigate(returnPath)` del callback como redirección abierta** | Revisado el 2026-08-27: `returnPath` llega sin revalidar desde el fragmento, pero `history.pushState` rechaza destinos de otro origen y no se pudo construir un caso explotable. Defensa en profundidad, no vulnerabilidad. |
| **No añadir `aria-hidden` a los iconos de Heroicons** | Revisado el 2026-08-27 en `node_modules`: la librería ya lo emite por defecto. |
| **No añadir `aria-current` a mano en la navegación (T2-25)** | Revisado el 2026-08-27 al ir a implementarlo: `NavLink` de React Router **ya lo emite**. El hallazgo de la auditoría era erróneo. Segunda vez que un hallazgo de auditoría envejece mal; verificar antes de implementar. |
| **No subir `Microsoft.OpenApi` a la rama 3.x** | Probado el 2026-08-27: la 3.10.2 corrige el aviso GHSA-v5pm-xwqc-g5wc pero rompe la compilación (`error CS0200`). La versión que corrige y compila es **2.12.2**, fijada explícitamente en el `.csproj` aunque sea transitiva. |
| **Eliminado el panel de estadísticas (T2-08)** | Decisión del propietario el 2026-09-02. Estaba escrito de punta a punta pero no se mostraba en ninguna pantalla: nadie llegó a verlo. Retirado de los dos proyectos con su endpoint y sus estilos. De rebote anula T2-16, que pedía optimizar un `GetStatsAsync` que ya no existe. |
| **La especificación OpenAPI no se sirve por defecto fuera de desarrollo** | Decidido el 2026-09-04. No contiene secretos ni habilita nada, pero entrega el mapa completo de rutas y parámetros, y eso es una decisión por despliegue: `OpenApi__Exposed=true`. El contrato se consulta sin arrancar nada en `docs/openapi.json`. |
| **`/health` no comprueba la base de datos; `/health/ready` sí** | Decidido el 2026-09-04. Meter la base en `/health` sería un error: `render.yaml` apunta ahí con `healthCheckPath` y Render **reinicia el servicio** cuando falla. Reiniciar el proceso no levanta una base caída, así que lo único que se conseguiría es un bucle de reinicios durante toda la avería. Ninguna de las dos devuelve el detalle del fallo: el mensaje de Npgsql lleva host, puerto y usuario, y eso se queda en el log. |
| **Licencia MIT** | Elegida por el propietario el 2026-09-02. `LICENSE` en los dos repositorios. El aviso nombra la identidad de git (`xfiberex`); conviene sustituirla por el nombre legal si la autoría tiene que poder acreditarse. **No es asesoramiento jurídico:** si el proyecto llega a tener valor comercial o colaboradores, requiere revisión legal. |
