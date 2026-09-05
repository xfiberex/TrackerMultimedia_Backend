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
