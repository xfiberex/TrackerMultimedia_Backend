# Roadmap

> Qué falta por hacer. Plan derivado de la auditoría del **2026-08-27**.
> El *qué cambió* está en [CHANGELOG.md](CHANGELOG.md); el *por qué*, en [DECISIONS.md](DECISIONS.md).

| Tier | Nombre | Tareas | Cerradas | Abiertas |
|------|--------|--------|----------|----------|
| 0 | Crítico / Bloqueante | 6 | 5 | T0-05 en suspenso |
| 1 | Alta prioridad | 22 | 20 | T1-05 (reclasificada Bajo) y T1-13 (reabierta) |
| 2 | Mejoras sustanciales | 26 | 26 | — |
| 3 | Pulido y mantenimiento | 23 | 23 | — |
| 4 | Futuro / Opcional | 11 | 7 | 3 abiertas + T4-06 en suspenso |
| 5 | Sistema de diseño | 6 | 4 | 2, ninguna bloqueante |
| **Total** | | **96** | **85** | **7 + 4 en suspenso o anuladas** |

**Estado (2026-09-05).** Cerrados los Tiers 0, 2 y 3; del Tier 1 quedan T1-05 y T1-13, reabierta. El
**Tier 5 se abre el 2026-09-05** y no viene de la auditoría: sale de revisar el lenguaje visual. Las dos suites
en verde —**171/171** backend sobre PostgreSQL real y **179/179** frontend—, con `npm run lint`,
`tsc -b` y `npm run build` limpios. `npm audit` da **0 vulnerabilidades**, comprobado el
2026-09-02; es una afirmación que caduca, así que lleva fecha.

## El proyecto es de uso local

Decisión del propietario del 2026-08-27: **Render, Neon y Netlify quedan deshabilitados**. Los
repositorios siguen en GitHub. La revocación de las credenciales se dio por hecha y **no lo estaba
en el caso de Neon** (T1-13, reabierta): deshabilitar un servicio y revocar la credencial que lo
abre son dos acciones, y aquí solo se hizo la primera.

Esto no cierra hallazgos por sí solo: **cambia cuáles están activos**. Lo que dependía de haber un
servicio público queda en suspenso, no resuelto, y **vuelve en el momento en que se vuelva a desplegar**.

---

## Abiertas

### T1-05 — Dejar de confiar en cualquier proxy para `X-Forwarded-For`

- **Área:** Seguridad · **Severidad:** Alto → **Bajo** mientras el uso sea local · **Esfuerzo:** medio
- **Ubicación:** `Program.cs:53-54`
- **Reclasificada el 2026-08-27:** el escenario de ataque era Internet a través del proxy de
  Render, que ya no existe. **El defecto sigue en el código sin cambios**, así que vuelve a ser
  Alto en cuanto haya despliegue: es de los primeros que hay que resolver antes de volver a publicar.
- **Qué hacer:** `KnownIPNetworks.Clear()` y `KnownProxies.Clear()` hacen que ASP.NET Core acepte
  `X-Forwarded-For` de cualquier origen. Como el rate limiter particiona por `RemoteIpAddress`
  **después** de `UseForwardedHeaders`, rotar la cabecera salta por completo el límite de 10
  peticiones/minuto que protege login y registro contra fuerza bruta. Restringir a las redes del
  proxy o, si se mantiene el `Clear()`, documentar el riesgo y añadir una partición secundaria por
  email en los endpoints de autenticación.
- **Criterio:** enviar 50 peticiones a `/api/auth/login` con una `X-Forwarded-For` distinta cada
  vez acaba devolviendo 429.

### T1-13 — Revocar de verdad la credencial de Neon · REABIERTA el 2026-09-04

- **Área:** Seguridad · **Severidad:** Alto · **Esfuerzo:** bajo (son cinco minutos en un panel)
- **Por qué vuelve:** la fila de cerradas decía «todas rotadas y Neon deshabilitado». No lo estaba.
  Al recuperar el proyecto en este equipo, `appsettings.Local.json` traía la cadena de conexión de
  Neon **y funcionaba**: el 2026-09-04 se crearon y borraron bases de datos remotas con ella por
  error. Sacar la credencial del disco —ya hecho— no la invalida.
- **Qué hacer:** revocar o rotar el rol `neondb_owner` en el panel de Neon, o eliminar el proyecto
  entero si ya no se usa. Después, actualizar la fila de T1-13 en la tabla de cerradas, que hoy
  afirma algo que no era cierto.
- **Criterio:** intentar conectar con la cadena antigua falla por autenticación.
- **Lección, más allá de esta credencial:** «rotada» y «el servicio está deshabilitado» son
  afirmaciones distintas, y ninguna de las dos se comprueba sola. Lo que cerró la tarea en su día
  fue la intención de revocarla, no la verificación de que lo estuviera.

### Tier 4 — Futuro / Opcional

- **T4-02 · Añadir PKCE a los flujos OAuth** — Seguridad. Con cliente confidencial no es
  obligatorio, pero es defensa en profundidad frente a la interceptación del código. · Esfuerzo medio
- **T4-03 · Internacionalizar la interfaz** — UI/UX. Todos los textos están incrustados en español
  y `formatDate` fija `es-DO` en vez de la configuración del usuario. · Esfuerzo alto
- **T4-04 · Añadir pruebas end-to-end** — QA. Playwright sobre registro, login, OAuth, CRUD e
  importación/exportación. · Esfuerzo alto

### Tier 5 — Sistema de diseño

*Abierto el 2026-09-05.* No sale de la auditoría del 2026-08-27 sino de revisar el lenguaje visual
con la interfaz ya construida, y por eso no comparte numeración con los otros tiers. **Ninguna de
las cuatro es un defecto funcional**: la aplicación se usa igual sin ellas.

El punto de partida es que **el proyecto ya tiene un sistema de diseño**, no CSS suelto: una capa de
33 tokens con nombres semánticos y una paleta que es la escala `slate` de Tailwind transcrita a
mano, en estilo *soft UI* con superficies esmeriladas. Dos análisis independientes —leer el CSS y
consultar la base de datos de la skill `ui-ux-pro-max`— coinciden en la misma dirección
(*Glassmorphism* sobre neutros fríos), así que **lo que toca es terminarlo, no sustituirlo**.
Migrar a Tailwind o a una librería de componentes serían semanas para llegar al mismo aspecto,
tirando por el camino la accesibilidad ya pagada en T1-16 a T1-23.

**T5-01 y T5-02 se cerraron el 2026-09-05**, el mismo día que se abrió el tier. Ver la tabla de cerradas.

**T5-03 · Completar la capa de tokens: espaciado, radios y tipografía** — UI/UX · Esfuerzo alto
: Los 33 tokens son **todos de color y sombra**. Espaciados, radios y tamaños de texto están
  escritos a mano en las 239 clases, y eso explica por sí solo que `index.css` tenga casi 3.000
  líneas. Es la tarea de fondo del tier: sin escala, cada componente nuevo vuelve a inventar sus
  medidas.
: *Nota:* la skill `ui-ux-pro-max` documenta un dial `--density` que debería emitir una tabla
  `--space-*`; al ejecutarlo el 2026-09-05 **no la emitió**, así que la escala hay que definirla a
  mano y no copiarla de ahí.
: *Criterio:* existe la escala y al menos una vista está migrada entera a ella, como plantilla del
  resto.

**T5-04 · Decidir qué hacer con `--surface-strong: #ffffff`** — UI/UX · Esfuerzo bajo
: La ficha de *Glassmorphism* lista «fondos blancos puros» como anti-patrón del estilo: el efecto de
  cristal necesita algo detrás que se transparente, y sobre blanco puro no hay nada que enseñar.
: **Es una decisión, no un defecto**, y puede cerrarse resolviendo que se queda como está. Lo que no
  vale es que nadie lo haya mirado.

---

## En suspenso y anuladas

### T0-05 — Publicar política de privacidad y aviso legal · EN SUSPENSO

- **Área:** Legal · **Severidad:** Crítico → *no aplica hoy* · *Requiere revisión legal*
- **En suspenso desde el 2026-08-27:** sin servicio accesible a terceros no hay tratamiento de
  datos personales de otras personas. **No se ha borrado ni resuelto: se reactiva íntegra en el
  momento en que la aplicación vuelva a estar accesible para alguien que no seas tú**, aunque sea
  un grupo reducido. Servirla con `dev:lan` a otros dispositivos de tu red no cambia nada mientras
  las cuentas sigan siendo tuyas; darle acceso a otras personas, sí.
- **No es asesoramiento jurídico.** Dónde está exactamente la frontera de lo «doméstico» a efectos
  del GDPR **requiere revisión legal** si alguna vez deja de ser evidente.
- **Qué haría falta:** vistas `/privacidad` y `/aviso-legal` accesibles sin iniciar sesión y
  enlazadas desde el registro y el layout, con responsable y contacto, categorías de datos, base
  legal, finalidad, retención, destinatarios, transferencias internacionales y derechos.

### T4-06 — Métricas, exportación y alertas · EN SUSPENSO

- **Área:** DevOps · **Esfuerzo:** medio
- **Reducida el 2026-09-04.** De esta tarea salieron ya dos trozos: la sonda de salud (T4-10) y la
  correlación con registro estructurado (T4-11). Lo que queda es la parte que **no tiene destino
  hoy**: sin servicio desplegado no hay dónde exportar métricas ni a quién alertar, y montar un
  Prometheus en local para mirarlo uno mismo es infraestructura sin lector.
- **Decisión del propietario del 2026-09-04:** no se añaden dependencias de OpenTelemetry mientras
  el uso sea local. **Se reactiva en el momento en que se vuelva a desplegar**, igual que T0-05.
- **Mientras tanto no hay ceguera total:** ASP.NET Core, EF Core y el runtime ya publican sus
  medidores, y `dotnet-counters monitor -n TrackerMultimedia` los lee en vivo sin tocar el código.
  Es suficiente para mirar algo puntualmente; no lo es para vigilar sin estar delante.

### T1-12 — Montar integración continua · ANULADA

Anulada el 2026-08-27 por decisión del propietario. **El riesgo no desaparece con la tarea:** lo
que la CI iba a cubrir es exactamente lo que ya había pasado —18 pruebas en rojo sin que nadie se
enterara—. La red que la sustituye es la rutina de [WORKFLOW.md](WORKFLOW.md), ejecutada **antes
de cada commit**. El identificador no se reutiliza.

### T2-16 — Optimizar `GetStatsAsync` · ANULADA

Anulada de rebote el 2026-09-02: el panel de estadísticas se eliminó (T2-08) y el método ya no existe.

### T2-25 — Marcar la página activa en la navegación · ANULADA

Anulada el 2026-08-27 al ir a implementarla: `NavLink` de React Router ya emite `aria-current="page"`.
El hallazgo de la auditoría era erróneo.

---

## Cerradas

Resumen de las 85 tareas cerradas y verificadas. El detalle de cómo se resolvió cada una está en
[HISTORY.md](HISTORY.md), por sesión; el efecto visible, en [CHANGELOG.md](CHANGELOG.md).

**T1-13 sigue apareciendo en la tabla de Tier 1 aunque esté reabierta.** Su fila se conserva, marcada,
porque borrarla escondería justo lo que hay que recordar: que se dio por cerrada sin comprobarlo.

### Tier 0 — Crítico

| ID | Tarea | Cierre |
|---|---|---|
| T0-01 | Restaurar la aplicación de migraciones en el arranque | 2026-08-27 · `MigrateAsync` con log de las pendientes y `try/catch` que no tumba el arranque |
| T0-02 | Poner la suite de tests bajo control de versiones | 2026-08-27 · Movida dentro del repositorio de backend; `DefaultItemExcludes` para que los globs del SDK no se la traguen |
| T0-03 | Alinear los blueprints de despliegue con la estructura real | 2026-08-27 · Dos repositorios independientes; cada blueprint en la raíz del suyo con rutas relativas |
| T0-04 | Actualizar dependencias con vulnerabilidad alta | 2026-08-27 · `Microsoft.OpenApi` 2.12.2 y `SQLitePCLRaw.lib.e_sqlite3` 2.1.13. Sin `NU1903` |
| T0-06 | Reparar la migración que EF Core nunca ejecutaba | 2026-08-27 · Migración huérfana sin `.Designer.cs`: toda la biblioteca fallaba con 500 en cualquier base nueva. Regenerada y verificada desde vacío |

### Tier 1 — Alta prioridad

| ID | Tarea | Cierre |
|---|---|---|
| T1-01 | Corregir la doble clave foránea en `AspNetUserLogins` | 2026-08-27 · FK sombra `ApplicationUserId` eliminada |
| T1-02 | Devolver los proveedores vinculados en `/api/auth/me` | 2026-08-27 · `AuthSessionService.GetLinkedProvidersAsync` |
| T1-03 | No romper el registro cuando falla el envío de correo | 2026-08-27 · Envío aislado en `SendEmailSafelyAsync` |
| T1-04 | Eliminar la enumeración de cuentas en registro y login | 2026-08-27 · Respuesta única; el aviso va al titular de la dirección |
| T1-06 | Crear el índice de `RefreshTokens.TokenHash` | 2026-08-27 · Índice único |
| T1-07 | Crear un índice utilizable sobre `MediaItems.UserId` | 2026-08-27 · `IX_MediaItems_UserId_CreatedAtUtc`; el que había era parcial y no servía |
| T1-08 | Poner en verde la suite del frontend | 2026-08-27 |
| T1-09 | Simular `matchMedia` en el arranque de los tests | 2026-08-27 |
| T1-10 | Actualizar los tests de `LibraryView` a la interfaz vigente | 2026-08-27 |
| T1-11 | Corregir la aserción errónea del test de Jikan | 2026-08-27 |
| T1-13 | Rotar las credenciales y sacarlas del disco en claro | 2026-08-27 · **Cierre incorrecto: la de Neon seguía activa.** Reabierta el 2026-09-04, ver *Abiertas* |
| T1-14 | Implementar el borrado de cuenta | 2026-09-02 · `DELETE /api/auth/account` con reautenticación, transacción y borrado de los `OAuthStates` que la cascada no cubría |
| T1-15 | Declarar una licencia | 2026-09-02 · MIT en los dos repositorios |
| T1-16 | Gestión de foco en los diálogos modales | 2026-08-27 · Hook compartido; aparecieron tres defectos no previstos por el camino |
| T1-17 | Asociar las etiquetas huérfanas a sus controles | 2026-08-27 · Siete etiquetas a `role="group"` + `aria-labelledby` |
| T1-18 | Añadir un enlace para saltar al contenido | 2026-08-27 |
| T1-19 | Reparar el onboarding documentado en los README | 2026-08-27 · `appsettings.json` versionado; verificado desde una carpeta vacía con solo los dos secretos obligatorios |
| T1-20 | Sacar del repositorio binarios y artefactos | 2026-08-27 · De 378 a 151 archivos versionados, 0 binarios. Siguen en el historial |
| T1-21 | Purgar tokens de refresco y estados OAuth caducados | 2026-08-27 · Sesiones caducadas a los 7 días, estados OAuth al día siguiente |
| T1-22 | Nombre accesible al enlace externo de los resultados | 2026-08-27 · Hallazgo nuevo, no de la auditoría |
| T1-23 | Contraste de texto por debajo de WCAG AA | 2026-09-04 · Hallazgo nuevo. El texto secundario daba 1,96:1 y la pestaña activa del catálogo en oscuro, 1,41:1. Corregido en los tokens y fijado con 7 pruebas que leen el CSS |

### Tier 2 — Mejoras sustanciales

| ID | Tarea | Cierre |
|---|---|---|
| T2-01 | Alinear los tests de `CategoriesView`, `DiscoverView` y `SearchResultCard` | 2026-08-27 |
| T2-02 | Ejecutar los tests del backend contra PostgreSQL | 2026-09-02 · Base desechable por clase desde una plantilla |
| T2-03 | Ejercitar las migraciones en los tests | 2026-09-02 · Una migración rota ya se detecta |
| T2-04 | Cubrir con tests la búsqueda de la biblioteca | 2026-09-02 · No podía tener ninguno mientras la suite usaba SQLite |
| T2-05 | Manejador global de excepciones con `ProblemDetails` | 2026-08-27 · Con `traceId` para relacionar lo que vio el usuario con lo registrado |
| T2-06 | Capturar fallos de red y de formato en los servicios OAuth | 2026-09-02 · Un enlace caducado devuelve al login con aviso, no un 500 |
| T2-07 | No reutilizar el `state` OAuth en el flujo de vinculación | 2026-08-27 |
| T2-08 | Decidir el futuro del panel de estadísticas | 2026-09-02 · Eliminado por decisión del propietario; no estaba conectado a ninguna pantalla |
| T2-09 | Sanear el CSV exportado contra inyección de fórmulas | 2026-08-27 · Un título como `=1+1` se ejecutaba al abrirlo en Excel |
| T2-10 | Exigir email confirmado y cuenta no bloqueada en todos los caminos | 2026-08-27 · Los tres caminos de acceso comprueban lo mismo |
| T2-11 | Dejar de propagar mensajes de excepción al frontend | 2026-08-27 · Ya no acaban en la barra de direcciones ni en el historial |
| T2-12 | Detectar la reutilización de tokens de refresco | 2026-08-27 · Reutilizar uno rotado revoca todas las sesiones |
| T2-13 | Corregir el comentario de política de `tokenStore` | 2026-08-27 · Afirmaba una protección `SameSite` inexistente |
| T2-14 | Revisar la CSP y añadir cabeceras de seguridad | 2026-09-02 · `connect-src` en `'self'`; fuera tres orígenes que el frontend nunca usa |
| T2-15 | Minimizar los datos personales en los logs | 2026-09-02 · `UserId` donde hay cuenta, dirección enmascarada donde no |
| T2-17 | Limitar el tamaño de las importaciones | 2026-08-27 · Tope de 5.000 elementos |
| T2-18 | Validar `ExternalId` para todos los proveedores | 2026-09-02 · La condición estaba escrita al revés: solo cubría MyAnimeList |
| T2-19 | No descartar en silencio un formato inválido | 2026-09-02 |
| T2-20 | Sacar el sembrado de formatos del endpoint GET | 2026-09-02 · Un GET que escribía; con dos pestañas, la segunda daba 500 |
| T2-21 | Añadir `ErrorBoundary` al frontend | 2026-08-27 · Por fuera de los proveedores |
| T2-22 | Añadir `eslint-plugin-jsx-a11y` | 2026-08-27 · Su ausencia explicaba las siete etiquetas de T1-17 |
| T2-23 | Corregir el error de ESLint y hacer que el build lo ejecute | 2026-08-27 |
| T2-24 | Extender `prefers-reduced-motion` a las transiciones | 2026-08-27 |
| T2-26 | Unificar el idioma de los mensajes de error del backend | 2026-09-02 |
| T2-27 | Impedir que el login muestre texto arbitrario de la URL | 2026-08-27 · Hallazgo nuevo: un enlace preparado mostraba cualquier mensaje sobre el sitio auténtico |
| T2-28 | Aislar la conexión SQLite compartida en la suite | 2026-09-02 · Desapareció al pasar la suite a PostgreSQL |

### Tier 3 — Pulido y mantenimiento

| ID | Tarea | Cierre |
|---|---|---|
| T3-01 | Unificar los cinco valores documentados de `VITE_API_URL` | 2026-08-27 · Un único valor, `/api`, ruta relativa |
| T3-02 | Eliminar o implementar `Security:ApiKey` | 2026-08-27 · Eliminada; no existía en el código. Quedaba una copia en el `appsettings.Local.json` de este equipo, borrada el 2026-09-04 |
| T3-03 | Eliminar `DefaultConnectionPro` | 2026-08-27 · Cadena de producción que solo servía para tener la credencial en el portátil |
| T3-04 | Sustituir `TODO.md` por documentación vigente | 2026-08-27 · Eliminado |
| T3-05 | Documentar cómo ejecutar las pruebas | 2026-08-27 |
| T3-06 | Actualizar el árbol de estructura de los README | 2026-09-02 · Y dos recuentos de pruebas que se habían quedado atrás |
| T3-07 | Eliminar `Compliance-Platform-DESIGN.md` | 2026-09-02 · 190 líneas del sistema de diseño de otro producto |
| T3-08 | Eliminar `sanitize.ts` y las dependencias de DOMPurify | 2026-09-02 · Ninguna de sus funciones se usaba fuera de su propio test |
| T3-09 | Eliminar los campos sin uso de `env` | 2026-09-02 · `env` expone solo `apiUrl` |
| T3-10 | Eliminar `OAuthLinkRequiredResponse` | 2026-09-02 · El controlador redirige en lugar de devolverlo |
| T3-11 | Unificar los tipos de resultado equivalentes | 2026-09-02 · Un solo `ServiceResult<T>`, con `ToFailure<TOther>()` para propagar |
| T3-12 | Extraer `GetUserId()` a una extensión | 2026-09-02 · Dos comportamientos, no uno copiado cuatro veces |
| T3-13 | Corregir la errata y los comentarios de `Program.cs` | 2026-09-02 · Ya estaba resuelto en un commit anterior; se deja constancia |
| T3-14 | Corregir el comentario de `GoogleAuthService` | 2026-09-02 · Decía que validaba un `id_token`; pide el perfil a `userinfo` |
| T3-15 | Normalizar la indentación | 2026-09-02 · `.editorconfig` en los dos repositorios; 67 archivos formateados |
| T3-16 | Evitar el rechazo no gestionado al cerrar sesión | 2026-09-02 · Se avisa de que el servidor no llegó a enterarse |
| T3-17 | Evitar la pantalla en blanco por variable de entorno inválida | 2026-09-02 · Aviso legible pintado sin React, antes de que React monte |
| T3-18 | Reaccionar a los cambios de tema del sistema y evitar el destello | 2026-09-02 · Dos defectos, y el segundo anulaba el primero |
| T3-19 | Añadir `robots.txt` y `sitemap.xml` | 2026-09-02 · La reescritura SPA hacía que las rutas privadas «existieran» para un rastreador |
| T3-20 | Poner longitud máxima a la contraseña de login | 2026-09-02 · 100 caracteres, el mismo tope que el registro |
| T3-21 | Resolver los 13 avisos de `npm audit` | 2026-09-02 · De 13 a 0 sin `--force`, solo el lockfile |
| T3-22 | Añadir Prettier al frontend | 2026-09-02 · Fija el estilo que ya seguía el código, no los valores por defecto |
| T3-23 | Revisar el resto de validaciones de entorno | 2026-09-02 · El esquema validaba sin recortar y `apiUrl` hacía `.trim()`: un espacio de más tumbaba la aplicación |

### Tier 4 — Futuro / Opcional

| ID | Tarea | Cierre |
|---|---|---|
| T4-01 | Migrar el refresh token a una cookie `httpOnly` | 2026-09-04 · Cookie `HttpOnly` + `SameSite=Strict`, cabecera `X-TM-Client` contra CSRF, y el callback OAuth deja de llevar tokens en la URL |
| T4-05 | Medir y publicar la cobertura de pruebas | 2026-09-02 · Destapó que AniList y MangaDex no tenían ni una prueba. Veinte tests nuevos; 87,4 % líneas / 56,1 % ramas |
| T4-07 | Publicar la especificación OpenAPI | 2026-09-04 · `docs/openapi.json` versionado (30 rutas) y, en ejecución, tras `OpenApi__Exposed=true` fuera de desarrollo |
| T4-08 | Exportación completa de datos personales | 2026-09-04 · `GET /api/auth/account/export`. De paso, los formatos personalizados quedan por fin exportables |
| T4-09 | Un identificador mal formado de MangaDex tumbaba la búsqueda entera | 2026-09-02 · Hallazgo nuevo aparecido al escribir los tests de T4-05. `Guid.Parse` lanzaba fuera del filtro del `catch` |
| T4-10 | La sonda de salud no comprobaba nada | 2026-09-04 · Separada de T4-06. Dos sondas: `/health` de vida, `/health/ready` con base de datos |
| T4-11 | Correlación de peticiones y registro estructurado | 2026-09-04 · Separada de T4-06. Un solo identificador entre respuesta y log —antes eran dos distintos—, salida JSON fuera de desarrollo y los fallos parciales de búsqueda dejan de ser silenciosos |

### Tier 5 — Sistema de diseño

| ID | Tarea | Cierre |
|---|---|---|
| T5-01 | Separar la semántica de acción de la de estado | 2026-09-05 · El botón de borrar tomaba su tinta de `--status-dropped` y en oscuro nadie sobreescribía el color: **2,47:1**, ilegible. Tokens `--action-*` y `--destructive-*` propios de cada tema, y 3 pruebas más |
| T5-02 | Números tabulares en los progresos | 2026-09-05 · `font-variant-numeric` no aparecía ni una vez. Progreso, año y puntuación se comparan en columna y con Inter proporcional bailaban de fila en fila |
| T5-06 | El enlace «Saltar al contenido» era ilegible en oscuro | 2026-09-05 · Pedía `--surface`, `--border` y `--accent`, tres variables que el proyecto nunca definió: caían en su respaldo y el fondo se quedaba blanco fijo mientras el texto sí seguía al tema. **1,48:1** en la propia ayuda de accesibilidad de T1-18 |
| T5-05 | La pantalla de acceso no seguía al tema oscuro | 2026-09-05 · Estaba escrita entera en claro con solo dos reglas oscuras; lo legible se salvaba por orden de aparición. Marca, título, enlaces del pie y mensaje de error en tinta fija: entre **1,1:1** y **2,77:1**. Tokenizada y verificada en el navegador |
