# Roadmap

> Qué falta por hacer. Plan derivado de la auditoría del **2026-08-27**.
> El *qué cambió* está en [CHANGELOG.md](CHANGELOG.md); el *por qué*, en [DECISIONS.md](DECISIONS.md).

| Tier | Nombre | Tareas | Cerradas | Abiertas |
|------|--------|--------|----------|----------|
| 0 | Crítico / Bloqueante | 6 | 5 | T0-05 en suspenso |
| 1 | Alta prioridad | 22 | 22 | — |
| 2 | Mejoras sustanciales | 27 | 27 | — |
| 3 | Pulido y mantenimiento | 23 | 23 | — |
| 4 | Futuro / Opcional | 11 | 10 | T4-06 en suspenso |
| 5 | Sistema de diseño | 12 | 12 | — |
| **Total** | | **101** | **99** | **0 + 2 en suspenso** |

Las tres anuladas —T1-12, T2-16 y T2-25— quedan fuera del recuento: ver *En suspenso y anuladas*.
**Los totales de esta tabla estuvieron mal hasta el 2026-09-05**, y de dos maneras: el total decía 98
cuando la suma de la columna da 96, y la sección de cerradas hablaba de 85 cuando eran 88. Corregido
al cerrar T1-13; conviene volver a sumar la columna cada vez que se toque una fila.

**Estado (2026-09-06).** **No queda ninguna tarea abierta.** Cerrados los seis tiers, salvo las
dos suspendidas por decisión del propietario —T0-05 y T4-06—, que no están resueltas sino
esperando a que el proyecto cambie: ver su ficha para saber qué las reactiva. El 2026-09-06 se
cerraron T4-03 —la interfaz en español e inglés—, T5-10, T2-29, T5-11 y T5-12.

**Hay tres suites**: **182/182** backend sobre PostgreSQL real, **204/204** frontend y **14/14**
end-to-end con Playwright (T4-04), estas últimas contra la aplicación entera y con requisitos
propios —ver [WORKFLOW.md](WORKFLOW.md)—. Con `npm run lint`, `tsc -b` y `npm run build` limpios.
`npm audit` da **0 vulnerabilidades** y `dotnet restore` no emite ningún `NU1903`, comprobado el
2026-09-06; es una afirmación que caduca, así que lleva fecha.

Doce de las pruebas del frontend no comprueban comportamiento sino presentación —T4-03, T5-10,
T5-11— y una de las end-to-end tampoco —T5-12—. Todas nacen de defectos que **ninguna prueba de
comportamiento podía ver**, porque la aplicación funcionaba perfectamente con ellos dentro. Cinco de
los seis los vio el propietario mirando la pantalla, que sigue siendo el único instrumento que
detecta esta clase de fallo.

## El proyecto es de uso local, servido por LAN

Decisión del propietario del 2026-08-27, **completada el 2026-09-05**: Render, Neon y Netlify
quedan deshabilitados, y desde el 2026-09-05 **la base de Neon está eliminada y lo desplegado,
revocado y borrado**. Los repositorios siguen en GitHub. Durante nueve días la documentación
afirmó que las credenciales estaban revocadas sin que lo estuvieran (T1-13): deshabilitar un
servicio y revocar la credencial que lo abre son dos acciones, y hasta esa fecha solo se había
hecho la primera.

**La forma de uso es `npm run dev:lan`**: el servidor de Vite escucha en la red local y otros
dispositivos de la casa entran por la IP del equipo. Sigue siendo uso personal —las cuentas son
tuyas—, así que T0-05 continúa en suspenso; ver su ficha para dónde está exactamente esa frontera.

Esto no cierra hallazgos por sí solo: **cambia cuáles están activos**. Lo que dependía de haber un
servicio público queda en suspenso, no resuelto, y **vuelve en el momento en que se vuelva a desplegar**.

### Lo que cambia al servir por LAN, y lo que no

Comprobado el 2026-09-05 sobre la configuración real, porque «local» y «local por LAN» no son lo mismo:

- **La cookie del token de refresco sigue funcionando.** `Secure` se calcula como
  `_options.Secure ?? !environment.IsDevelopment()` en `Infrastructure/Http/RefreshTokenCookie.cs:49`,
  así que en desarrollo va sin `Secure` y sobrevive a `http://` sobre la red local.
- **CORS no interviene.** El navegador del otro dispositivo habla solo con el origen de Vite, y es
  Vite quien reenvía `/api` a `localhost:5218` con `changeOrigin`. La petición nunca es de origen
  cruzado, de modo que `Cors:AllowedOrigins` —que solo lista `localhost`— no se ejerce.
- **Google y GitHub no funcionan desde otro dispositivo**, y no es un fallo nuevo: sus `RedirectUri`
  apuntan a `localhost`. Por eso existe la opción de ocultar los botones desde el `.env` del frontend.
  Entrar con correo y contraseña es el camino previsto en LAN.
- **El límite de peticiones pasa a ser compartido.** Vite **no** añade `X-Forwarded-For` —su proxy
  solo lo hace con `xfwd`, que no está activado—, así que todas las peticiones llegan al backend
  desde `localhost`. Las políticas `search` (30/min) y `auth` (10/min) particionan por
  `RemoteIpAddress`, luego **los diez intentos de login por minuto se reparten entre todos los
  dispositivos de la red**, no uno por dispositivo. Con dos o tres personas en casa usándolo a la
  vez es un 429 esperando a pasar. La política `user` (200/min) no sufre esto: particiona por
  identificador de usuario.
- **T1-05 subía de exposición al pasar a LAN** —el escenario dejaba de ser «solo tú en esta máquina»
  para ser «cualquiera que esté en tu red»— y **por eso se implementó ese mismo día**, en vez de
  dejarlo para el despliegue. Ver la fila de cerradas del Tier 1.

---

## Abiertas

**Ninguna, a fecha del 2026-09-06.** La última fue T2-29, cerrada ese día; su ficha está en la
tabla de cerradas del Tier 2. Lo que queda sin hacer está en *En suspenso y anuladas*, y son
decisiones tomadas, no trabajo olvidado.

Lo que sigue es la historia del Tier 5, que se abrió y se cerró entero, y se conserva aquí porque
explica de dónde salió.

### Tier 5 — Sistema de diseño

*Abierto el 2026-09-05.* No sale de la auditoría del 2026-08-27 sino de revisar el lenguaje visual
con la interfaz ya construida, y por eso no comparte numeración con los otros tiers.

**Se abrió como un tier de preferencias y no lo era.** Al mirar la interfaz en el navegador, cuatro
de las seis tareas con que nació resultaron ser defectos medibles —texto entre 1,1:1 y 2,77:1, es
decir, ilegible— y no cuestiones de gusto. La quinta, T5-04, sí era una decisión, y se resolvió
dejando el token como estaba, aunque al mirarlo destapó T5-07: cinco desenfoques que no podían
verse. La sexta, T5-03, era el trabajo de fondo, y lo que midió al abrirla habla por sí solo: 39
espaciados, 9 radios y **19 tamaños de texto**, varios separados por un tercio de píxel.

El punto de partida es que **el proyecto ya tiene un sistema de diseño**, no CSS suelto: una capa de
33 tokens con nombres semánticos y una paleta que es la escala `slate` de Tailwind transcrita a
mano, en estilo *soft UI* con superficies esmeriladas. Dos análisis independientes —leer el CSS y
consultar la base de datos de la skill `ui-ux-pro-max`— coinciden en la misma dirección
(*Glassmorphism* sobre neutros fríos), así que **lo que toca es terminarlo, no sustituirlo**.
Migrar a Tailwind o a una librería de componentes serían semanas para llegar al mismo aspecto,
tirando por el camino la accesibilidad ya pagada en T1-16 a T1-23.

**Las doce tareas del tier están cerradas**: nueve el 2026-09-05 —el mismo día que se abrió— y
T5-10, T5-11 y T5-12 el 2026-09-06, las tres abiertas porque el propietario las vio en pantalla.
Ver la tabla de cerradas.

Que el tier siga creciendo después de darse por terminado no es señal de que se cerrara mal: es la
forma que tiene este defecto de aparecer. **La presentación no la ve ninguna prueba de
comportamiento**, así que cada uno de estos hallazgos ha entrado por el mismo sitio —alguien
abriendo la pantalla— y ha salido con una prueba nueva que sí puede verlo la próxima vez.

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

Resumen de las 96 tareas cerradas y verificadas. El detalle de cómo se resolvió cada una está en
[HISTORY.md](HISTORY.md), por sesión; el efecto visible, en [CHANGELOG.md](CHANGELOG.md).

**La fila de T1-13 conserva el rastro de haberse cerrado mal.** Ahora sí está cerrada, pero la fila
dice también que estuvo nueve días dada por buena sin serlo: borrar esa parte escondería justo lo
que hay que recordar.

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
| T1-05 | Dejar de confiar en cualquier proxy para `X-Forwarded-For` | 2026-09-05 · La confianza pasa a ser explícita: nueva sección `ForwardedHeaders` con `KnownProxies` y `KnownNetworks`, **vacías por defecto**, y con las dos vacías el middleware ni se registra. Adelantada desde «antes de publicar» porque servir por LAN ampliaba el escenario. 4 pruebas, y la del criterio del ROADMAP se falsificó restaurando el defecto: 20 logins con la cabecera rotando dejaban de dar 429 |
| T1-06 | Crear el índice de `RefreshTokens.TokenHash` | 2026-08-27 · Índice único |
| T1-07 | Crear un índice utilizable sobre `MediaItems.UserId` | 2026-08-27 · `IX_MediaItems_UserId_CreatedAtUtc`; el que había era parcial y no servía |
| T1-08 | Poner en verde la suite del frontend | 2026-08-27 |
| T1-09 | Simular `matchMedia` en el arranque de los tests | 2026-08-27 |
| T1-10 | Actualizar los tests de `LibraryView` a la interfaz vigente | 2026-08-27 |
| T1-11 | Corregir la aserción errónea del test de Jikan | 2026-08-27 |
| T1-13 | Rotar las credenciales y sacarlas del disco en claro | **2026-09-05** · Base de Neon eliminada y lo desplegado revocado y borrado por el propietario. Verificado en disco: 0 coincidencias de `neon` en los tres `appsettings*.json`. **Se cerró antes por error el 2026-08-27** —la credencial de Neon seguía activa y el 2026-09-04 se crearon y borraron bases remotas con ella sin querer—, y estuvo reabierta del 2026-09-04 al 2026-09-05 |
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
| T2-29 | Avisar de que un JSON ajeno no es una exportación | 2026-09-06 · La causa era un valor por defecto; el aviso además no llegaba a verse |

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
| T4-02 | Añadir PKCE a los flujos OAuth | 2026-09-05 · `Pkce` con S256, `OAuthState.CodeVerifier` y su migración. El verificador se guarda en el servidor y solo su hash viaja: un código interceptado ya no se canjea. Activado en Google; en GitHub queda el interruptor `UsePkce` en `false` porque su OAuth App no documenta soporte. 5 pruebas, una contra el vector del apéndice B del RFC 7636 |
| T4-04 | Añadir pruebas end-to-end | 2026-09-05 · **13 pruebas con Playwright** sobre la aplicación entera: acceso, biblioteca, OAuth y transferencia. Destaparon T2-29 y obligaron a hacer configurables los cupos del rate limiter, que la suite agotaba a mitad de camino. De OAuth se cubren los dos extremos —que la petición al proveedor lleve `state` y el reto de PKCE, y que una vuelta con `state` inválido acabe en el login— porque completar el flujo exigiría autenticarse de verdad contra Google |
| T4-03 | Internacionalizar la interfaz | 2026-09-06 · **Español e inglés**, con i18next y `react-i18next`. El diccionario está tipado a partir del español, así que una clave que falte en inglés o esté mal escrita **no compila**. Se migraron 27 archivos y **463 textos**, y de paso salieron tres cosas: los mapas de etiquetas guardan ahora la clave y no el texto —si guardaran el texto, el idioma quedaría congelado en el de arranque—, los mensajes de Zod se traducen en cada `parse` por la misma razón, y `Anime`, `Manga`, `Jikan` o `AniList` se quedaron fuera del diccionario por ser nombres propios. El interruptor va en la cabecera y también en las pantallas de acceso: quien no lee español tiene que poder cambiarlo antes de entrar |
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
| T5-04 | Decidir qué hacer con `--surface-strong: #ffffff` | 2026-09-05 · **Se queda como está.** El anti-patrón habla del cristal y este token no lo es: sus 4 usuarios son superficies opacas a propósito y ninguno lleva `backdrop-filter`. Además no se vería, porque `--surface-primary` ya compone a **#fdfdfe** sobre el degradado. Queda una prueba que fija la premisa, y T5-07 recoge lo que sí apareció |
| T5-07 | Cinco `backdrop-filter` que no difuminaban nada | 2026-09-05 · Los cuatro emergentes tapaban lo difuminado con un fondo al 0,97–0,98 de alfa, y `.auth-card` con un blanco **opaco**: pasaba entre el 3 % y el 0 %. Se quitó el desenfoque en vez de bajar el alfa, porque son las superficies con más texto y aparecen sobre contenido arbitrario: translúcidas, su contraste dejaría de ser una propiedad del CSS. El cristal se queda donde sí se ve y no lleva texto encima |
| T5-08 | Migrar el resto de las vistas a la escala | 2026-09-05 · **296 medidas migradas**: espaciados a mano de 39 a 1, radios de 9 a 0, tamaños de texto de 19 a 0. 182 encajaron exactas y ninguna se movió más de 2px. La escala ganó cuatro peldaños que le faltaban —`--space-7`, `--space-14`, `--type-4xl` y `--leading-none`—: sin ellos el titular de portada encogía 8px y el contenedor principal 8px, que es rediseñar en vez de migrar. **La prueba se invirtió**: en vez de una lista de prefijos migrados, vigila el archivo entero con dos excepciones documentadas. Revisado en el navegador en los dos temas y en móvil |
| T5-09 | La clase `.field` se usa en el marcado y no existe en el CSS | 2026-09-05 · Hallazgo de T5-08, y **anterior a la migración**: la etiqueta y el campo de «Borrar la cuenta» salían pegados. Era su único uso en todo el frontend, así que se cambió por `control`, el grupo de campo que ya usan los otros cinco formularios de esa pantalla, en vez de definir una clase con un solo usuario |
| T5-10 | Un campo de formulario sin la clase del sistema de diseño | 2026-09-06 · El campo de «Borrar la cuenta» era el único `<input>` del frontend sin `className="input"`, así que el navegador pintaba su control nativo en medio de una pantalla que usa el del sistema; y el botón de debajo quedaba pegado al campo por faltar el contenedor que da el ritmo vertical. **Segundo y tercer defecto del mismo bloque tras T5-09**, los tres invisibles para las pruebas de comportamiento y los tres vistos por el propietario a simple vista. `form-controls.test.ts` revisa desde ahora los 61 campos del frontend |
| T5-11 | La flecha de los `select` pegada al borde derecho | 2026-09-06 · La flecha nativa se dibuja en el filo y **no la mueve `padding-right`**, así que los doce `select` de la aplicación —biblioteca, editor de registros, descubrir y alta rápida— la tenían pegada mientras su texto respetaba el margen del otro lado. Se sustituye por el mismo `ChevronDownIcon` que usan los menús de acciones, colocado con el margen del texto. `select-arrow.test.ts` vigila el efecto secundario del arreglo: quitada la nativa, borrar la imagen del tema oscuro deja los desplegables **sin ningún indicador** |
| T5-12 | El desplegable de color se cortaba dentro del diálogo | 2026-09-06 · Flotaba con `position: absolute` dentro de `.side-panel__content`, que se desplaza: un hijo absoluto no ensancha la caja de su contenedor pero sí cuenta como desbordamiento, de modo que el diálogo sacaba barra y recortaba la fila del color personalizado **con sitio de sobra en pantalla** —medido: el diálogo terminaba en 720 y el desplegable en 725—. Puesto en el flujo, el diálogo pasa de 396 a 527px sin barra. Lo comprueba una prueba end-to-end, la única que puede: jsdom no maqueta |
| T5-03 | La capa de tokens no medía nada, solo pintaba | 2026-09-05 · Los 33 tokens eran todos de color y sombra, así que cada clase inventaba sus medidas: **39 espaciados, 9 radios y 19 tamaños de texto**, seis de ellos indistinguibles entre sí por menos de un píxel. Escala de espaciado en rejilla de 4px, radios, `--type-*` y alturas de línea, más `--control-min-height` para que el objetivo táctil de 44px tenga nombre propio. La pantalla de acceso queda migrada entera como plantilla, con una prueba que falla si vuelve a escribirse una medida a mano |
