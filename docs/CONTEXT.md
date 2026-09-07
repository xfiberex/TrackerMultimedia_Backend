# Contexto del proyecto

> **Para qué existe este archivo.** Para poder retomar TrackerMultimedia meses después, o desde otro
> equipo y en otra sesión de chat, **sin perder nada de lo hablado**. Todo lo que se decide durante
> una sesión y solo vive en la conversación se pierde al cerrarla; aquí queda escrito.
>
> Es la **puerta de entrada**, no el archivo largo. Cada sección remite al documento que tiene el
> detalle. Si algo se cuenta entero en otro sitio, aquí va el resumen y el enlace, nunca la copia.

| | |
|---|---|
| **Repositorios** | Dos, independientes, bajo una carpeta padre que **no** es un repositorio: `TrackerMultimedia_Backend` y `TrackerMultimedia_Frontend` |
| **Versión publicada** | Ninguna. **No hay etiquetas de git en ninguno de los dos** (ver T6-18). `package.json` dice `1.0.0` desde el primer commit |
| **Stack backend** | .NET 10 · ASP.NET Core · EF Core 10 + Npgsql · PostgreSQL 17 · Identity + JWT · MailKit |
| **Stack frontend** | React 19.2 · TypeScript 6.0 · Vite 8 · React Router 7 · TanStack Query 5 · Zod 4 · i18next (es/en) |
| **Tamaño** | ~5.900 líneas de C# (sin pruebas ni migraciones) · ~14.000 de TS/TSX · 2.986 de CSS en un solo archivo |
| **Pruebas** | 150 backend · 197 frontend · 15 end-to-end. **Las tres verificadas el 2026-09-06** tras retirar «Descubrir»; las e2e con una condición no documentada, ver T6-34 |
| **Estado** | En uso. 30 tareas abiertas, todas del Tier 6 (re-auditoría del 2026-09-06) |
| **Despliegue** | **Ninguno.** Uso local servido por LAN con `npm run dev:lan`. Render, Neon y Netlify retirados el 2026-09-05 |
| **Plan** | [ROADMAP.md](ROADMAP.md) · **Qué cambió** [CHANGELOG.md](CHANGELOG.md) · **Por qué** [DECISIONS.md](DECISIONS.md) |
| **Trabajo diario** | [WORKFLOW.md](WORKFLOW.md) · **Trampas conocidas** [PITFALLS.md](PITFALLS.md) · **Historia** [HISTORY.md](HISTORY.md) |
| **Última actualización** | **2026-09-06** |

---

## 1. Qué es

Un gestor personal de biblioteca multimedia: anime, manga, manhwa, series, libros, videojuegos y
demás. **Los títulos se registran a mano**, se les sigue el progreso, se puntúan, se organizan por
categorías y formatos propios, y se exporta o importa la biblioteca entera en JSON o CSV.

**Lo que deliberadamente no hace**, y conviene saberlo antes de proponerlo:

- **No es multiusuario en el sentido de un producto.** Hay cuentas, aislamiento por usuario y
  autorización real —está probado—, pero el escenario previsto es una persona y sus dispositivos.
  De ahí que T0-05 (política de privacidad) esté en suspenso y no anulada.
- **No es social.** No hay listas públicas, ni seguir a nadie, ni comentarios.
- **No habla con ningún catálogo externo.** Ni Jikan, ni AniList, ni MangaDex, ni como fuente de
  metadatos ni como destino de sincronización. Hubo una pantalla «Descubrir» que sí lo hacía y se
  eliminó el **2026-09-06**: ver *Decisiones* más abajo. Las únicas llamadas salientes que quedan
  son Google y GitHub para el acceso, y el servidor de correo.
- **No tiene panel de estadísticas.** Lo hubo y se eliminó a propósito (T2-08): no estaba conectado
  a ninguna pantalla.

## 2. Arquitectura

Detalle completo en [ARCHITECTURE.md](ARCHITECTURE.md). Lo imprescindible:

```
TrackerMultimedia_Backend/          API REST, .NET 10. Capas, no vertical slices.
├── Controllers/                    Solo HTTP: validan, delegan y traducen a códigos de estado.
├── Services/                       Toda la lógica. MediaItemsService concentra 1.424 líneas (T6-24).
├── Contracts/                      DTOs de entrada y salida. Nunca se exponen entidades.
├── Domain/Entities/                Modelo de EF Core.
├── Data/                           ApplicationDbContext: relaciones, índices y su porqué.
├── Infrastructure/
│   ├── Http/                       Cookie de refresco, PKCE, correlación, cabecera anti-CSRF.
│   ├── Options/                    Configuración tipada. Cada clase documenta el coste de tocarla.
│   └── Logging/                    Enmascarado de datos personales.
├── Migrations/                     Nunca crear una con `--no-build`: salen vacías.
└── docs/                           Estos documentos. Cubren los dos repositorios.

TrackerMultimedia_Frontend/         SPA, React 19 + Vite. Organizado por funcionalidad.
├── src/features/<área>/            Cada una con api/, schemas/, views/, components/.
│   ├── auth/                       Sesión, OAuth, perfil. Es la más grande.
│   ├── media-items/                La biblioteca. La vista más grande.
│   └── categories/ catalog/        Taxonomía propia del usuario.
├── src/shared/                     api/ (axios e interceptores), components/, hooks/, i18n/, utils/.
├── src/index.css                   **Todo el CSS, 2.986 líneas.** Tokens arriba, tema oscuro abajo.
└── e2e/                            Playwright. Requisitos propios: ver WORKFLOW.md y T6-34.
```

**Tres cosas que no se deducen mirando las carpetas:**

1. **La aplicación no habla con ningún tercero desde el navegador**, y desde el 2026-09-06 tampoco
   desde el servidor salvo OAuth y correo. Por eso `connect-src` es `'self'` a secas en la CSP de
   `index.html`. Cuidado con una herencia del pasado: la tabla de la biblioteca usa clases CSS
   `search-*` y el diccionario tiene un bloque `tabla.*` que antes se llamaba `descubrir.*`. Los
   nombres vienen de la pantalla retirada, no de que quede búsqueda externa.
2. **El token de acceso vive en memoria del módulo y el de refresco en una cookie `HttpOnly`.** No
   hay nada de sesión en `localStorage`. El razonamiento entero está en la cabecera de
   `src/shared/api/tokenStore.ts`, y merece leerse antes de tocar nada de autenticación.
3. **`docs/` está en el repositorio del backend pero documenta los dos.** No hay `docs/` en el
   frontend, y no es un olvido.

## 3. Estado actual

**Al 2026-09-06.** La aplicación funciona y se usa. Los Tiers 0 a 5 del roadmap están cerrados —102
tareas— salvo dos suspendidas con condición escrita (T0-05 y T4-06) y tres anuladas.

**Abierto ahora mismo: el Tier 6, con 30 tareas**, salidas de la re-auditoría de la tarde del
2026-09-06, la primera hecha con la aplicación levantada y conducida desde un navegador real. Nació
con 34; cuatro se anularon esa misma noche al eliminarse «Descubrir», porque desapareció el código
que las producía. Cinco son de severidad Alta:

| ID | Qué | Por qué urge |
|---|---|---|
| T6-01 | Cambiar la contraseña no cierra las demás sesiones | Es la acción de quien sospecha un robo, y no sirve de nada |
| T6-02 | El error de validación se pinta fuera de la pantalla | Guardar un registro falla en silencio |
| T6-03 | 39 px de desplazamiento lateral a 360 px | La aplicación se sirve por LAN para usarla desde el móvil |
| T6-04 | El correo bloquea la petición hasta 2 minutos | Registrarse se cuelga si el SMTP tarda |
| T6-34 | Las e2e no pasan con el procedimiento documentado | Una cifra que no se reproduce no es una comprobación |

**Lo que se acaba de cerrar** (mañana del 2026-09-06): T4-03 —español e inglés—, T5-10 a T5-13 y
T2-29. **Y por la noche se eliminó «Descubrir»**, que no era una tarea del roadmap sino un cambio de
alcance del producto: ver la sección siguiente.

**Lo que aguantó la re-auditoría**, y conviene no volver a tocar: el contraste en oscuro cumple AA
con holgura (5,71:1 medido en el navegador), el foco es visible en los trece puntos de tabulación,
el diálogo modal gestiona el foco, Lighthouse da 100 en Accesibilidad / Buenas prácticas / SEO, y la
correlación de registro de T4-11 identificó sola el catálogo caído y su excepción.

## 4. Decisiones y convenciones clave

*La sección más importante.* Cada decisión con el problema real que la provocó. El detalle y las
alternativas descartadas están en [DECISIONS.md](DECISIONS.md); las trampas que costaron un fallo
concreto, en [PITFALLS.md](PITFALLS.md).

### No reabrir

- **La biblioteca se escribe a mano; ningún catálogo externo.** Decisión del propietario del
  **2026-09-06**, ejecutada el mismo día: fuera la pantalla «Descubrir», los tres proveedores, la
  búsqueda en abanico y los cinco campos de procedencia del modelo, con su migración. *Lo que
  empujó la decisión:* ese día **dos de los tres catálogos estaban caídos** —AniList devolvía 403
  con «temporarily disabled due to severe stability issues», Jikan 504— y nadie se había enterado.
  El detalle completo está en [DECISIONS.md](DECISIONS.md), *Alcance del producto*. **Volver a
  añadir un catálogo no es cambiar una pantalla:** reabre el modelo, su migración, el índice de
  duplicados, las llamadas salientes con sus plazos, y la atribución de cada proveedor.
- **Uso local por LAN, sin desplegar.** Decisión del propietario del 2026-08-27, completada el
  2026-09-05: Neon eliminada, y lo desplegado revocado y borrado. *No proponer volver a desplegar
  sin que lo pida.* Lo que dependía de haber un servicio público está **en suspenso, no resuelto**,
  y vuelve el día que se despliegue: T0-05, T4-06, T6-08, T6-21 y T6-22.
- **PostgreSQL nativo, no en contenedor.** Se probó Docker el 2026-08-27 y se revirtió el mismo día.
- **Sin integración continua** (T1-12, anulada). La sustituye la rutina previa a cada commit de
  [WORKFLOW.md](WORKFLOW.md). *El riesgo no desapareció con la tarea:* lo que la CI iba a cubrir es
  exactamente lo que ya pasó una vez —18 pruebas en rojo sin que nadie se enterara— y lo que volvió
  a pasar el 2026-09-06 con las e2e (T6-34).
- **Se termina el sistema de diseño propio, no se migra a Tailwind.** Serían semanas para llegar al
  mismo aspecto, tirando la accesibilidad ya pagada en T1-16 a T1-23.
- **Los secretos van solo en `dotnet user-secrets`.** Nunca en `appsettings.Local.json`, aunque sea
  temporalmente: así ocurrió la fuga que costó T1-13.

### Convenciones

- **Los mensajes de la interfaz y de la documentación van en español llano**, sin jerga de
  implementación. Es una convención real y bien sostenida; las dos excepciones que quedan están
  recogidas en T6-26, y los mensajes de validación del backend, en T6-10.
- **Toda cifra medida lleva fecha.** Es la lección de T1-13 y de la nota de las «197 cuentas» que
  siguió cuatro días pidiendo permiso sobre un mundo que ya no existía. Una afirmación medida
  caduca; si no lleva fecha, no se puede saber si sigue siendo cierta.
- **Un hallazgo cerrado se verifica contra su criterio, no contra la impresión de que ya está.**
  Marcar `[x]` lo que no se arregló es lo que la re-auditoría tiene que salir a cazar.
- **Los identificadores de tarea son permanentes.** No se reutilizan ni los de tareas eliminadas:
  viven en commits y en este archivo.

### Trampas que ya costaron un fallo

Las recoge enteras [PITFALLS.md](PITFALLS.md). Las tres que más veces han vuelto:

1. **«Deshabilitado» y «revocado» son dos acciones distintas.** Nueve días de documentación
   afirmando que unas credenciales estaban revocadas cuando solo se había deshabilitado el servicio
   (T1-13). El mismo patrón reapareció en T6-08: `netlify.toml` declara seis cabeceras de seguridad
   que **el despliegue vigente no envía**, y `index.html` afirma que están ahí.
2. **`dotnet ef migrations add` sin `--no-build`.** Con él salen migraciones vacías.
3. **jsdom no maqueta.** Ninguna prueba de componente puede ver posición, desbordamiento ni
   visibilidad. Es la causa de que los Tiers 5 y 6 existan, y de que sus defectos entren siempre por
   el mismo sitio: alguien mirando la pantalla.

## 5. Tareas comunes

Tabla completa en [WORKFLOW.md](WORKFLOW.md). Lo que hay que saber sí o sí:

| Comando | Para qué | Requisitos |
|---|---|---|
| `Start-Service postgresql-x64-17` | Arrancar la base | PowerShell como administrador |
| `dotnet run` | API en `http://localhost:5218` | PostgreSQL en marcha y los 8 secretos en user-secrets |
| `npm run dev:lan` | Interfaz accesible desde el móvil | `.env` copiado de `.env.example` |
| `dotnet test TrackerMultimedia_Backend.slnx` | 150 pruebas | PostgreSQL en marcha |
| `npm run test` | 197 pruebas | — |
| `npm run test:e2e` | 15 pruebas | PostgreSQL, backend en marcha **y el cupo de `auth` subido** — ver T6-34 |

**El puerto de PostgreSQL depende del equipo: compruébalo, no lo supongas.** `netstat -an | grep 543`.
En este equipo es el **5433** (medido el 2026-09-06; `WORKFLOW.md` decía 5432, ver T6-17). El nombre
de la base, `trackerMultimedia`, lleva mayúscula intercalada: en SQL va **siempre entre comillas
dobles**.

**Al cambiar de equipo los secretos se vuelven a poner a mano.** No hay nada que los transporte, y
ese es el precio de que no estén en el repositorio. `appsettings.Local.example.json` dice qué claves
hacen falta.

## 6. Qué queda fuera, y por qué

- **Integración continua**, por decisión (T1-12).
- **Métricas, exportación y alertas** (T4-06): sin servicio desplegado no hay dónde exportar ni a
  quién alertar. `dotnet-counters monitor -n TrackerMultimedia` sirve para mirar algo puntualmente;
  no para vigilar sin estar delante. **Ese hueco tiene coste real y ya se ha visto**: AniList lleva
  caída un tiempo indeterminado y nadie se enteró hasta que se miró a mano (T6-05).
- **Textos legales** (T0-05): mientras las cuentas sean tuyas no hay datos de terceros que tratar.
- **Aplicación móvil nativa.** La SPA servida por LAN cubre el caso.
- **Los catálogos externos** (2026-09-06). No es que falten: se retiraron. Ver *Decisiones*.

## 7. Registro de sesiones

El registro largo, sesión por sesión, está en [HISTORY.md](HISTORY.md). Aquí solo las entradas
posteriores a la creación de este archivo.

### 2026-09-06 (noche) — Fuera «Descubrir»

**Qué se hizo.** Se eliminó por completo la búsqueda en catálogos externos, por decisión del
propietario tomada al ver el informe de la re-auditoría. El alcance se acordó antes de tocar nada:
borrado completo, incluidos los cinco campos de procedencia del modelo, porque la tabla `MediaItems`
tenía **0 filas** y no había dato alguno en riesgo —se comprobó antes de decidir, no después—.

Se retiró: en el backend, un controlador, cinco servicios, tres contratos, tres enums, tres clientes
HTTP salientes, una política de límite y cuatro archivos de prueba; en el modelo, cinco columnas y un
índice único parcial, con la migración `RemoveExternalCatalogFields`; en el frontend, ocho archivos
de la funcionalidad, una ruta, una entrada de navegación, un filtro, una columna de tabla y **97
líneas de CSS**. `openapi.json` baja de 30 rutas a 27.

**Qué se conservó, y por qué.** `CoverImageUrl` y `ReferenceUrl` no venían de los catálogos: los
escribe el usuario. La exportación e importación siguen intactas, solo sin las cinco columnas.

**Dos trampas que había que ver antes de borrar, y que un borrado a ciegas se habría llevado por
delante.** La primera: la tabla de la **biblioteca** reutiliza las clases CSS `search-*` y cinco
claves de traducción que vivían bajo `descubrir.*` —también las usan Catálogo y Categorías—. Borrar
por prefijo habría roto tres pantallas. Las clases se conservan; las claves se movieron a un bloque
`tabla.*`, que es lo que de verdad son. La segunda: de las 52 reglas CSS con prefijo `search-`, 15
seguían vivas. Se resolvió cruzando las clases definidas en el CSS con las que el marcado usa hoy,
en vez de a ojo.

**Cómo quedó.** Las tres suites en verde tras el cambio: **150 backend** (eran 182), **197 frontend**
(eran 205) y **15 end-to-end**. Con `npm run lint`, `tsc -b`, `npm run build` y Prettier limpios. La
aplicación se revisó además en el navegador con una cuenta de prueba, borrada al terminar: la
navegación tiene dos entradas, la tabla mantiene sus ocho columnas alineadas y el estado vacío ya no
manda a una pantalla que no existe.

**Efecto en el roadmap:** cuatro tareas del Tier 6 —T6-05, T6-16, T6-20 y T6-22— quedan **anuladas,
no resueltas**: desapareció el código que las producía. T6-26 se reduce a la mitad. De 34 a 30.

**Lo que conviene no olvidar de todo esto.** El abanico de proveedores **degradaba bien** —con los
tres marcados devolvía resultados aunque dos fallaran— y lo que lo anulaba era un valor por defecto
de la interfaz. Un diseño tolerante a fallos se pierde si un valor por defecto lo estrecha, y eso no
tiene nada que ver con catálogos.

---

### 2026-09-06 (tarde) — Re-auditoría con la aplicación en marcha

**Qué se hizo.** Auditoría completa de doce áreas —todas menos SEO, declarada no aplicable por ser
una SPA privada tras login— con el backend, el frontend y PostgreSQL levantados, y la aplicación
conducida desde un Chrome real con el MCP de DevTools. Se creó una cuenta de prueba por el flujo de
registro auténtico (`qa+auditoria@example.com`), se recorrieron las cinco pantallas en tres anchos y
en los dos temas, y se sondeó la API directamente. **La cuenta y sus datos se borraron al terminar.**

**Qué se encontró.** 34 hallazgos, seis de severidad Alta, todos en el Tier 6 del roadmap. **Catorce
eran imposibles de ver leyendo el código.** Los que más enseñan:

- **Cambiar la contraseña no cierra ninguna sesión** (T6-01). Verificado en vivo: cookie de antes del
  cambio → `/auth/refresh` → 200. `ResetPassword` sí revoca; `ChangePassword` no. El mismo riesgo con
  dos comportamientos, y el que falta es el del camino que más se usa.
- **Un error de validación pintado 193 píxeles por encima del área visible** (T6-02): guardar un
  registro falla sin que ocurra nada en pantalla. Lleva `role="alert"`, así que un lector de pantalla
  sí lo anuncia — **la aplicación es más accesible para quien no mira que para quien mira**.
- **39 píxeles de desplazamiento lateral a 360 px** (T6-03), causados por un pseudo-elemento
  decorativo con `right: -48px` sobre un contenedor sin `overflow: hidden`. Localizarlo costó
  descartar tres sospechosos: un pseudo-elemento no aparece en ningún recorrido del DOM.
- **AniList devuelve 403 con un mensaje explícito** —«temporarily disabled due to severe stability
  issues»— y Jikan 504 (T6-05). La arquitectura de abanico degrada correctamente y devuelve
  resultados de MangaDex... salvo que la interfaz trae **solo AniList** marcado por defecto. El
  diseño resolvió el problema y un valor por defecto lo desactivó.
- **La suite e2e no pasa con el procedimiento documentado** (T6-34): 12 fallos con `dotnet run` a
  secas, 15/15 con el cupo de `auth` subido. La suite está bien; el requisito no está escrito en
  ninguna parte.

**Qué se descubrió por el camino, y no estaba en ninguna ficha.** Dos falsos positivos que estuvieron
a punto de entrar en el informe y no lo hicieron por comprobarlos:

- El árbol de accesibilidad decía que «Temporada» tenía `max="0"` con valor 1 —un campo
  permanentemente inválido—. Los atributos reales no tenían `max`: era un artefacto del árbol. Lo que
  sí había allí era otra cosa (T6-13).
- El primer intento de medir el desplazamiento horizontal en móvil dio 399 px contra 360. Era un
  artefacto de emular `deviceScaleFactor: 3`, donde `window.innerWidth` no coincide con el viewport.
  Repetido sin emulación de dispositivo, **el defecto seguía ahí** y era otro, con otra causa.

La lección se parece a la de las «197 cuentas»: **una medición que no se cuestiona produce un
hallazgo falso con la misma facilidad con la que produce uno verdadero.**

**Qué quedó a medias.** El oráculo temporal del login (T6-32) está anotado como *pendiente de
verificación*: medirlo exige muchas peticiones cronometradas y el cupo lo impide. No se afirma que
exista, solo que el código tiene la forma que lo produciría.

**Qué no se tocó.** Nada de código: la auditoría se detiene en el informe, y las correcciones
esperan aprobación. `CHANGELOG.md` **no se ha modificado a propósito**: no ha cambiado nada que un
usuario pueda notar, y anotar allí una auditoría sería mezclar el *qué cambió* con el *qué falta*.
