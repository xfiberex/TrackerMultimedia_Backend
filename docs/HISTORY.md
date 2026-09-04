# Registro de sesiones

> Qué se hizo en cada sesión de trabajo, en orden. Es el archivo largo del proyecto: si buscas la
> decisión vigente, está en [DECISIONS.md](DECISIONS.md); si buscas la trampa concreta que costó
> un fallo, en [PITFALLS.md](PITFALLS.md). Aquí queda el rastro de cómo se llegó a una y a otra.

## Pendiente de tu decisión

- **197 cuentas de test en la base de desarrollo** (`@test.com` y `@example.com`), junto a 2
  reales. Borrarlas es un borrado sobre datos reales, así que no se hace sin visto bueno.
  *Anotado el 2026-09-02.*

---

## 2026-09-04 — Reorganización de la documentación

Los cuatro documentos comunes (`ROADMAP`, `CHANGELOG`, `CONTEXT` y este registro) vivían sueltos
en la carpeta contenedora, que no es un repositorio: sin copia de seguridad ni historial. Al pasar
el proyecto a otro equipo se movieron dentro del repositorio de backend, en `docs/`, y aquí se
reorganizan.

`CONTEXT.md` se parte en cuatro por responsabilidad, porque era un archivo de 1.200 líneas donde
convivían la arquitectura, las decisiones, las trampas del stack, los comandos y el registro de
sesiones: `ARCHITECTURE.md`, `DECISIONS.md`, `PITFALLS.md`, `WORKFLOW.md` y este `HISTORY.md`. El
`ROADMAP` pasa de listar las 88 tareas con su ficha completa a detallar solo las 6 abiertas y
resumir las 79 cerradas en tablas: la ficha de una tarea cerrada es historia, y la historia ya
tiene su sitio.

Se corrigieron de paso varias afirmaciones que habían envejecido mal: que la suite corre sobre
SQLite en memoria (corre sobre PostgreSQL real desde T2-02), que no existe `appsettings.json`
(existe y está versionado desde T1-19), los recuentos de pruebas (105/124 → 165/165), el número de
migraciones (4 → 3) y la carrera de la conexión SQLite listada como defecto vivo (cerrada en
T2-28). Se eliminó también el `.gitignore` que la carpeta contenedora había arrastrado consigo:
describía rutas de un monorepo que no existe.

**La regla que queda:** nada que deba sobrevivir se queda en la raíz de la carpeta contenedora.

---

## 2026-09-04 — Llevarse los datos, y dos sondas donde había una que no servía

**La exportación personal no es una ampliación de la de biblioteca.** La tentación era añadirle el
correo y las fechas al archivo que ya existe; habría roto la importación, porque ese formato es el
que sabe leer `ImportAsync`. Son dos archivos porque responden a dos preguntas distintas, y el
segundo incrusta al primero llamando al mismo código para no duplicar el mapeo. Efecto lateral
útil: los formatos personalizados no viajaban en ninguna exportación y ahora sí.

**Lo que se deja fuera importa tanto como lo que se incluye.** Ni contraseñas, ni tokens de
refresco —ni siquiera su hash: son credenciales en activo y el archivo acaba en una carpeta de
descargas—, ni el `ProviderKey` de los logins externos. El test mira **el JSON crudo**, no
propiedades deserializadas, y se verificó mutando el servicio para que filtrara el hash y viendo
el test en rojo.

**Dos sondas, y no por ceremonia.** Lo evidente era meter la base de datos dentro de `/health`;
habría sido un error, porque Render reinicia el servicio cuando esa ruta falla y reiniciar el
proceso no levanta una base caída. El test que da sentido a la tarea apunta el `DbContext` a un
host inalcanzable y comprueba las dos cosas a la vez: 200 en una, 503 en la otra.

**Un test que solo puede pasar no comprueba nada.** El primer test del interruptor de OpenAPI
habría pasado igual aunque la especificación se publicara siempre, porque el host de pruebas
arranca en `Development`. Al escribir el contrario apareció que `AppFactory` no sabía cambiar de
entorno, y con ello el fallo de configuración anotado en las trampas. Es la misma lección de la
sonda de salud, el mismo día.

---

## 2026-09-02 — Medir la cobertura, y lo que enseñó

El número no era lo interesante: 82,8 % de líneas suena bien, 48,4 % de ramas ya no tanto. Lo que
valió la medición fue **qué estaba exactamente a cero**, algo que no se veía leyendo el código:
`AniListSearchService` y `MangaDexSearchService` completos —374 líneas, dos de los tres proveedores
de catálogo— y el actualizar/borrar de formatos. Veinte tests nuevos; cobertura a 87,4 % / 56,1 %.

**La regla que queda:** cuando algo se añade «igual que lo que ya había», hay que comprobar que
también se le añadieron las pruebas. AniList y MangaDex se sumaron a Jikan y nadie las cubrió.

Y salió un defecto real (T4-09): `MangaDexSearchService` derivaba el identificador con
`Guid.Parse`, cuya `FormatException` no estaba en el filtro del `catch`, así que un solo elemento
mal formado devolvía un 500 y se llevaba por delante los resultados buenos de la misma respuesta.

`ReportGenerator` va declarado en `.config/dotnet-tools.json`, no instalado globalmente, para que
la versión sea la misma en cualquier equipo.

---

## 2026-09-02 — La suite pasa a PostgreSQL, y el susto que vino con ello

Iba a escribir los tests de la búsqueda de la biblioteca y lo primero fue comprobar por qué no
tenía ninguno: `EF.Functions.ILike` solo existe en Npgsql y la suite corría sobre SQLite.

**Cómo quedó.** Cada clase recibe una base desechable. Las migraciones se aplican **una vez por
ejecución** sobre una plantilla y cada clase la copia con `CREATE DATABASE ... TEMPLATE`, que tarda
milisegundos. De paso desapareció la `SqliteConnection` compartida que provocaba la carrera de
T2-28.

**El fallo que no hay que repetir.** Al montarlo quité del `AppFactory` un bloque de registro del
`DbContext` en lugar de reemplazarlo, y el comportamiento pasó a depender del orden de resolución.
**La regla:** en un test de integración el registro del `DbContext` **se reemplaza**, no se añade.

El escapado de la búsqueda resultó estar bien: los tests de `%`, `_` y `\` pasaron a la primera.
Y dos veces me pilló el `.editorconfig` con los finales de línea al reescribir archivos con Python.

En la misma sesión, la CSP (T2-14): `connect-src` queda en `'self'` —los catálogos externos y los
proveedores los consulta el servidor, no el navegador— y las cabeceras que impiden incrustar la
aplicación se declaran en `netlify.toml`, mientras la política de contenido sigue en un único
sitio para que el hash del script en línea no se desincronice.

---

## 2026-09-02 — Licencia MIT y borrado de cuenta

**Licencia.** MIT, elegida por el propietario, en los dos repositorios y declarada en ambos README.

**Borrado de cuenta.** Tres decisiones que conviene no reabrir, todas recogidas en
[DECISIONS.md](DECISIONS.md): la reautenticación no es opcional aunque el JWT sea válido; con
contraseña se comprueba por el mismo camino que el login, para que el endpoint no sea un oráculo
de contraseñas; y `deleteAccount` limpia la sesión local **solo si el servidor confirma** —al
revés que `logout`—, porque limpiarla igualmente dejaría a quien escribe mal la contraseña
creyendo que borró su cuenta.

**Lo que la cascada no cubría:** `OAuthStates` no tiene `UserId` y su columna `PendingEmail` guarda
la dirección, así que sin borrarlos a mano el correo sobrevivía a la cuenta hasta la purga del día
siguiente. Todo dentro de una transacción. Ocho tests que cuentan filas en vez de fiarse del 204, y
comprobado contra PostgreSQL real: contraseña incorrecta → 400 y la cuenta sigue; correcta → 204 y
cero filas huérfanas en las cuatro tablas.

**Un descuido propio, para que no se repita:** encadené `dotnet format --verify-no-changes` con
otro comando y di por bueno un resultado que no había leído.

---

## 2026-09-02 — Tier 3 cerrado: dependencias, Prettier, y la trampa que avisé y me pilló igual

`npm audit fix` sin `--force` cubrió los 13 avisos: todo cabía dentro de los rangos `^` que ya
había, así que `package.json` no cambia y el diff es solo del lockfile. Con copia previa de
`package.json` y `package-lock.json`, y comprobación posterior del árbol entero, porque un
`--legacy-peer-deps` ya lo había roto una vez en esta misma sesión.

**Prettier fija lo que había, no lo que Prettier prefiere:** sin punto y coma, comillas simples,
comas finales, ancho 100. Configuración y pasada de formateo en commits separados. `.md` queda
fuera —reflowar los párrafos convertiría cualquier cambio de una frase en un diff de página
entera— y también `.agents/`, `.codegraph/`, `.mcp.json` y `skills-lock.json`.

**Y la pasada destapó a la primera exactamente lo que yo mismo había dejado advertido dos sesiones
antes:** reformateó el script en línea de `index.html` y el hash SHA-256 de la CSP dejó de cuadrar.
Fallo mudo. Dos medidas: un `<!-- prettier-ignore -->` suelto y un test que recalcula el hash,
comprobado quitándole un punto y coma al script.

El repaso de T3-23 encontró lo que buscaba: `apiUrl` hace `.trim()` pero el esquema validaba el
valor sin recortar, así que un `VITE_API_URL= /api` con un espacio de más tumbaba la aplicación
entera antes de que React montara.

---

## 2026-09-02 — Tema, indexación, tipos de resultado y los README

**El tema tenía dos defectos y el segundo anulaba el primero.** El hook no se suscribía a
`prefers-color-scheme`, sí; pero además escribía en `localStorage` al montar, así que desde la
primera visita siempre había preferencia guardada y el modo «seguir al sistema» dejaba de existir
sin que nadie lo desactivara. Ahora el estado tiene tres valores y solo se persiste al pulsar el
interruptor. El destello se corrige con un script en el `<head>`, declarado por hash porque la CSP
prohíbe scripts en línea.

**`robots.txt`** no es SEO: es que la reescritura SPA hace que cualquier ruta devuelva 200 con el
HTML de la aplicación, también `/library` o `/profile`, así que para un rastreador «existen».
Quedan fuera la zona privada y las pantallas que solo tienen sentido con un token en la URL.
**`robots.txt` pide, no impide:** el control de acceso lo hace el backend.

**Un solo tipo de resultado (T3-11).** Los tres `record struct` privados tenían la misma forma.
Se añadió `ToFailure<TOther>()` para propagar el error entre tipos, que era el motivo real de que
existieran por separado. Un detalle: en `ServiceResult<ContentKind>` la propiedad `Value` no es
`ContentKind?` —`T?` con un `T` sin restringir no envuelve en `Nullable` para tipos valor—, así que
seis `.Value!.Value` se quedaron en `.Value`.

---

## 2026-09-02 — Los proveedores OAuth se pueden ocultar desde el `.env`

Sirve sobre todo al abrir la aplicación desde el móvil: Google solo sabe volver a `localhost` o a
un dominio público, así que desde otro dispositivo ese camino termina en error.

**La máscara es un AND, nunca un OR, y eso no se toca.** Puede ocultar un proveedor que el
servidor acepta; nunca mostrar uno que el servidor no acepta.

**El fallo que traía:** la suite pasó a depender del `.env` de la máquina donde se ejecutara.

**`host: true` volvió y se quitó otra vez**, por decisión del propietario. **El criterio, que ya ha
hecho falta tres veces:** exponer a la red es una opción que se escribe al lanzar, no un
comportamiento que traiga el script por defecto.

---

## 2026-09-02 — Estilo fijado, y un fallo que encontró el test antes que yo

`.editorconfig` en los dos repositorios y `dotnet format whitespace` sobre 67 archivos. Se eligió
`charset = utf-8` sin BOM porque era lo que ya tenían 86 de los 112 archivos —contados, no
supuestos—. EditorConfig cubre sangría, fin de línea y espacios finales; comillas y punto y coma
necesitan Prettier, registrado aparte como T3-22.

**Dos comportamientos, no uno duplicado (T3-12).** `GetUserId` estaba copiado palabra por palabra
en cuatro controladores, pero no hacía lo mismo en todos: en los tres CRUD la ausencia del claim
significa un token que este backend no pudo emitir y debe lanzar; `AuthController` necesitaba
responder 401. De ahí `GetUserId()` y `TryGetUserId(out)`.

**Y el hallazgo que no era mío ni de la auditoría.** Al escribir el test del caso «variable de
entorno vacía» apareció que el esquema la rechazaba mientras el código que la consume sí
contemplaba ese caso.

---

## 2026-09-02 — Limpieza del Tier 3, y un aviso de seguridad que apareció de rebote

Se eliminaron un documento de diseño de otro producto (190 líneas), el módulo de saneado de HTML
con sus dos dependencias, tres campos de configuración y un contrato de OAuth que ninguna respuesta
devolvía. Todo comprobado antes con `grep`, no supuesto.

**Una tarea ya estaba hecha y no por mí (T3-13):** el comentario con la errata había desaparecido
en un commit anterior. Se deja constancia en vez de marcarla como trabajo hecho aquí. **Un hallazgo
de auditoría envejece**: es la segunda vez en dos sesiones.

**El `npm uninstall` se hizo mirando el lockfile**, y de ahí salió lo importante: `npm audit` daba
13 avisos —1 crítico, 10 altos, 2 bajos— publicados después de la auditoría. Registrado como T3-21.

El tope de la contraseña de login se puso en 100 tras mirar el historial de git: es el mismo que el
registro impone desde el primer commit, así que ninguna cuenta existente queda fuera.

---

## 2026-09-02 — Fuera el panel de estadísticas

Estaba escrito de punta a punta pero no se mostraba en ninguna pantalla. **Comprobado antes de
borrar, no dado por cierto:** `MediaStatsPanel` no se importaba desde ningún sitio. Se retira de
los dos proyectos junto con el endpoint y los estilos que solo él usaba. T2-16 queda anulada de
rebote: pedía optimizar un `GetStatsAsync` que ya no existe.

---

## 2026-09-02 — Validaciones que solo cubrían un proveedor, y errores por la puerta equivocada

**La condición estaba escrita al revés (T2-18).** `ValidateExternalSource` solo comprobaba el
identificador de MyAnimeList, así que los de AniList y MangaDex se guardaban a medias y además
esquivaban la comprobación de duplicados.

**Un `catch` demasiado estrecho en una redirección de navegador (T2-06).** El callback de OAuth
respondía 500 cuando el enlace había caducado o se cortaba la red. **El criterio:** en un endpoint
al que llega el navegador, el `catch` cubre el fallo, no solo un tipo de fallo, y la salida es una
redirección con aviso, no una página de error del servidor.

**Los mensajes de error, todos en español (T2-26).** Dos cosas que el informe daba por hechas
seguían en inglés, entre ellas la que se ve al fallar la búsqueda en catálogos externos.

---

## 2026-09-02 — El `--host` que no estaba donde parecía

`npm run dev` llevaba `vite --host`, que dejaba la aplicación accesible a cualquiera conectado al
mismo router en cada arranque, sin pedirlo y sin avisar. **Comprobado, no supuesto:** arrancado el
servidor, Vite imprime `Network: use --host to expose` cuando no está.

**La regla que queda:** el modo por defecto es el más cerrado. Exponer a la red se escribe en la
orden — `npm run dev:lan`, `npm run preview:lan`.

Un segundo efecto del mismo cambio: el archivo llegó reformateado entero por el editor, lo que
acabó motivando T3-22.

---

## 2026-09-02 — Formatos, validación silenciosa y datos personales en el log

**Un formato inválido dejaba de serlo por el camino (T2-19).** `ResolveUserFormatIdAsync` devolvía
null tanto si no había formato como si el formato era de otra cuenta, y el elemento se guardaba sin
él diciendo que todo había ido bien.

**El GET que escribía (T2-20).** Los formatos por defecto se sembraban al leer la pantalla de
formatos: con dos pestañas abiertas, la segunda violaba el índice único y devolvía 500. Pasan a
crearse al dar de alta la cuenta.

**El hallazgo de los logs estaba a medias (T2-15).** El informe señalaba once puntos; había más.
**La regla, para que no haya que decidirla otra vez:** en el log va el `UserId`; cuando todavía no
hay cuenta, la dirección enmascarada.

---

## 2026-08-27 — Accesibilidad automática, límite de error y errores con identificador

El linter no comprobaba **ninguna** regla de accesibilidad, y esa es la razón de que las siete
etiquetas sin control asociado sobrevivieran hasta la auditoría.

**El límite de error va por fuera de los proveedores.** Colocado por dentro, un fallo del propio
proveedor no lo alcanza.

**Las respuestas de error con identificador (T2-05).** El valor no está en el formato sino en el
`traceId`: sirve para relacionar lo que vio el usuario con lo que quedó registrado.

**Tope de elementos en la importación (T2-17).** Había un límite de 10 MB, que acota el archivo
pero no el trabajo: 5.000 elementos caben de sobra en 10 MB y agotaban la memoria.

**Un aviso sobre npm.** Instalar con `--legacy-peer-deps` para saltarse un rango de *peer* rehace
el árbol entero: dejó fuera `@testing-library/dom` y rompió los tipos de todos los tests.

---

## 2026-08-27 — Tanda de seguridad del Tier 2

**El mismo control en los tres caminos (T2-10).** El login con contraseña rechazaba a quien tuviera
la cuenta bloqueada o sin confirmar; los otros dos caminos, no.

**Reutilización de tokens de refresco (T2-12).** Presentar uno ya rotado revoca **todas** las
sesiones del titular.

**El `state` de OAuth ya no se resucita (T2-07).**

**El hallazgo que apareció por el camino (T2-27).** Al quitar del backend el mensaje de excepción
de la URL se vio que la página de login mostraba tal cual el texto que le llegara por la dirección:
bastaba un enlace preparado para poner un aviso falso sobre el sitio auténtico.

**Inyección de fórmulas en el CSV (T2-09).** `EscapeCsvCell` entrecomillaba separadores pero no
neutralizaba los prefijos que Excel interpreta como fórmula.

**Un test que afirmaba lo incorrecto** y **una trampa de tests sin relación con la seguridad**:
escribir el caso del CSV comprobando la cadena completa lo hacía frágil frente a cualquier cambio
de orden de columnas.

---

## 2026-08-27 — Onboarding, limpieza del repositorio y retención de datos

**El clon limpio ya arranca (T1-19).** Faltaba `appsettings.json` y además el `.gitignore` lo
excluía, pese a que la documentación decía lo contrario. Verificado exportando lo versionado a una
carpeta vacía y arrancándolo con solo los dos secretos obligatorios.

**El repositorio vuelve a ser código (T1-20).** De 378 archivos versionados a 151, sin binarios.
Los archivos siguen en el disco; simplemente dejan de subirse. **Y una corrección al informe:** el
hallazgo afirmaba que `.codegraph/codegraph.db` estaba versionado y ya estaba excluido.

**Retención de datos (T1-21).** Ni `RefreshTokens` ni `OAuthStates` se limpiaban nunca. Sesiones
caducadas a los 7 días; estados OAuth —que guardan el correo de la cuenta externa— al día siguiente.

---

## 2026-08-27 — Diálogos accesibles con teclado, y tres defectos que no estaban en el informe

Los dos diálogos declaraban `role="dialog"` con `aria-modal="true"` pero no movían el foco, no lo
devolvían y no atrapaban el tabulador: no había ni una llamada a `.focus()` en toda la aplicación.
La solución es un hook compartido, `useModalDialog`, con el patrón de ARIA APG.

**Lo que apareció al implementarlo:** tres defectos reales que el informe no había visto —el fondo
como único cierre, el desmontaje al cerrar y `autoFocus` ganándole la carrera al hook—, todos
anotados en [PITFALLS.md](PITFALLS.md).

**Un cambio de comportamiento deliberado:** en los diálogos destructivos el foco inicial pasa a
«Cancelar». Antes lo tenía el botón de borrar, así que pulsar Intro sin leer bastaba para perder el
elemento.

**Lo que no se ha verificado:** nada de esto se ha probado con un lector de pantalla real.

---

## 2026-08-27 — Vuelta a PostgreSQL instalado en la máquina

El propietario revierte el paso a Docker: contenedor, volumen, red y `docker-compose.yml`
eliminados. **Lo que no se revierte, y es lo importante:** Docker fue el medio, no el fin. La
práctica que introdujo —recrear la base desde cero antes de dar por buena una migración— se
conserva con `dotnet ef database drop --force && dotnet ef database update`.

**Dos detalles de la instalación nativa:** el servicio escucha en `0.0.0.0:5433`, no solo en
loopback, y su arranque está en *Manual*.

---

## 2026-08-27 — El proyecto deja de estar desplegado

Render, Neon y Netlify quedan deshabilitados y sus credenciales revocadas. `render.yaml`,
`netlify.toml` y el `Dockerfile` **se conservan** como receta para volver.

**El error que hay que evitar aquí** es dar por cerrados los hallazgos de producción: T1-05 sigue
en el código tal cual y T0-05 está en suspenso, no hecha.

**`VITE_API_URL` pasa a ser una ruta relativa.** Es la única que funciona igual en `localhost` y al
servir desde otro dispositivo de la red. **Si algún día se sirve con `--host`**, la aplicación
queda accesible a toda la red local, y la base también.

---

## 2026-08-27 — Secretos a user-secrets, base en Docker, y un fallo crítico por el camino

Las ocho claves pasan a `dotnet user-secrets`. Se eliminaron además dos que sobraban: la cadena de
conexión de producción de Neon, que ningún código leía y solo servía para tener esa credencial en
el portátil, y una clave de API igualmente muerta. `Jwt:Secret` se regeneró de cero.

**Y entonces apareció T0-06.** Al aplicar las migraciones sobre una base recién creada —la primera
vez que alguien construía el esquema desde cero— salió que una migración escrita a mano **sin su
`.Designer.cs`** no existía para EF Core. El impacto, verificado y no supuesto: `GET /api/media-items`
devolvía 500 con `42703: column m.UserFormatId does not exist`. Es decir, **toda la biblioteca rota
en cualquier despliegue nuevo**.

**Por qué llevaba meses invisible.** Por dos cosas a la vez: la suite usaba `EnsureCreated()`, que
se salta las migraciones, y nadie había creado nunca una base vacía.

**La lección operativa.** Poder tirar y recrear la base no es una comodidad: es un método.

**Lo que quedó sin verificar:** el esquema real en Neon.

---

## 2026-08-27 — Sin CI: la verificación es local

El propietario descartó la integración continua. **Se anota aquí y no solo en el roadmap** porque
la CI no estaba propuesta por completismo: iba a cubrir exactamente lo que ya había pasado, 18
pruebas en rojo sin que nadie se enterara. Lo que la sustituye son cinco comandos documentados en
[WORKFLOW.md](WORKFLOW.md), ejecutados antes de cada commit.

---

## 2026-08-27 — Estructura de repositorios: dos, no monorepo

El propietario cerró la pregunta que bloqueaba el resto del Tier 0: **dos repositorios
independientes**. La suite de pruebas pasó dentro del repositorio de backend y cada blueprint a la
raíz del repositorio que describe.

**Lo que costó.** Mover el proyecto de tests dentro de la carpeta del backend rompió la
compilación: los globs por defecto del `.csproj` se tragaron los archivos de test y produjeron una
avalancha de `error CS0246` **atribuidos al proyecto de backend**.

**Verificado, no supuesto:** `dotnet test` desde un `.slnx` con rutas internas, `dotnet publish`
sin arrastrar nada de los tests, y ningún archivo de test cayendo bajo una regla de `.gitignore`.

---

## 2026-08-27 — Primera tanda de correcciones del roadmap

18 tareas cerradas y verificadas ejecutando las suites, el linter y el build en cada paso. Al
cerrar: backend 105/105, frontend 124/124, `npm run lint` y `tsc -b` limpios.

**Dos cosas que cambian lo que decía la auditoría:** el hallazgo de `aria-current` era falso, y uno
de los tests que parecían obsoletos estaba señalando un defecto real de accesibilidad. De ahí la
regla: **un test en rojo que parece obsoleto se lee antes de reescribirlo.**

---

## 2026-08-27 — Primera auditoría técnica completa

Auditoría de 13 áreas a profundidad exhaustiva, contra GDPR y WCAG 2.2 AA, sobre todo el código
propio de los dos proyectos. Se ejecutaron de verdad las dos suites, el linter y el build en lugar
de estimar. Se crearon `ROADMAP.md`, `CHANGELOG.md` y `CONTEXT.md`, que no existían. **No se
modificó ningún archivo de código.**

**Lo que se comprobó y resultó estar bien** (para no volver a investigarlo, y recogido en
*Decisiones cerradas*): DOMPurify y las herramientas de desarrollo de React Query no llegan al
bundle; los iconos de Heroicons ya emiten `aria-hidden`; la credencial de Neon nunca entró en el
historial de git; no hay cookies ni rastreadores; y `returnPath` del callback de OAuth no es
explotable como redirección abierta.

**Lo que no se pudo cubrir** y necesitaría acceso adicional: medición real de rendimiento en
producción, contraste de color sobre la interfaz renderizada, verificación con lector de pantalla,
configuración real de los paneles de Render, Netlify y Neon, y qué migraciones estaban aplicadas en
la base de producción.
