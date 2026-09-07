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
| 5 | Sistema de diseño | 13 | 13 | — |
| 6 | Lo que solo se ve con la aplicación en marcha | 30 | 0 | **30** |
| **Total** | | **132** | **100** | **30 + 2 en suspenso** |

Las siete anuladas —T1-12, T2-16, T2-25, T6-05, T6-16, T6-20 y T6-22— quedan fuera del recuento: ver *En suspenso y anuladas*.
**Los totales de esta tabla estuvieron mal hasta el 2026-09-05**, y de dos maneras: el total decía 98
cuando la suma de la columna da 96, y la sección de cerradas hablaba de 85 cuando eran 88. Corregido
al cerrar T1-13; conviene volver a sumar la columna cada vez que se toque una fila.

**Estado (2026-09-06, tras la re-auditoría de la tarde).** Los tiers 0 a 5 siguen cerrados, salvo
las dos tareas suspendidas por decisión del propietario —T0-05 y T4-06—, que no están resueltas
sino esperando a que el proyecto cambie: ver su ficha para saber qué las reactiva. Por la mañana se
cerraron T4-03 —la interfaz en español e inglés—, T5-10, T2-29, T5-11, T5-12 y T5-13.

**Por la tarde se abrió el Tier 6 con 34 tareas**, salidas de una re-auditoría hecha con la
aplicación levantada y ejercida desde el navegador. **Por la noche quedaron en 30**: al eliminarse
«Descubrir» (ver abajo), cuatro se anularon porque desapareció el código que las producía. Cinco de
las treinta son de severidad **Alta** y se atienden antes que nada de los Tiers 2 y 3, aunque su
número sea mayor: el Tier 6 es una tanda temática, no un escalón de prioridad.

**El 2026-09-06 por la noche se eliminó la pantalla «Descubrir»** y con ella toda dependencia de
catálogos externos, por decisión del propietario. La biblioteca se construye a mano. Es un cambio de
alcance del producto, no una tarea del roadmap; el porqué está en [DECISIONS.md](DECISIONS.md) y lo
que se retiró, en [CHANGELOG.md](CHANGELOG.md).

**Hay tres suites.** Verificadas el 2026-09-06 **después** de retirar «Descubrir», que se llevó
32 pruebas del backend y 8 del frontend:
- **150/150** en el backend sobre PostgreSQL real, 0 omitidas.
- **197/197** en el frontend, 38 archivos.
- **Las end-to-end no dan 15/15 siguiendo el procedimiento documentado.** Con el backend arrancado
  como dice [WORKFLOW.md](WORKFLOW.md) —`dotnet run`, sin más— salen **12 fallos y 3 aciertos**: el
  fixture registra e inicia sesión con una cuenta por prueba, es decir 30 peticiones de la política
  `auth`, cuyo cupo por defecto es **10 por minuto**. Con el cupo subido pasan **15/15**, y así se
  verificaron también tras retirar «Descubrir». El requisito no está escrito en ninguna parte ni lo
  comprueba `global-setup.ts`. Ver **T6-34**.

Con `npm run lint`, `tsc -b` y `npm run build` limpios. `npm audit` da **0 vulnerabilidades** y
`dotnet restore` no emite ningún `NU1903`, comprobado el 2026-09-06; es una afirmación que caduca,
así que lleva fecha —y la de las suites acaba de caducar, que es justo lo que ilustra el punto.

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

### Tier 6 — Lo que solo se ve con la aplicación en marcha

*Abierto el 2026-09-06.* No sale de la auditoría del 2026-08-27 ni del repaso visual del Tier 5,
sino de una re-auditoría con **la aplicación levantada y ejercida desde el navegador**: backend en
`localhost:5218`, frontend en `localhost:5173`, PostgreSQL 17 en el 5433 y una cuenta de prueba
creada por el flujo de registro real.

**Los Tiers 0 a 4 eran de severidad; a partir del 5 son tandas temáticas.** Eso no rebaja la
prioridad de nada: cada tarea lleva su severidad en la ficha, y las cinco primeras son de severidad
**Alta**. Se atienden antes que cualquier cosa de los Tiers 2 y 3, aunque su número sea mayor.

**Por qué existe este tier.** El proyecto lleva dos tandas seguidas —T5-09 a T5-13— descubriendo
defectos por el único camino que los ve: alguien mirando la pantalla. Esta vez el instrumento ha
sido el navegador conducido por herramientas, y el resultado confirma el patrón. De los 34 hallazgos
con que nació, **14 eran imposibles de ver leyendo el código**: un error que se pinta 193 píxeles por
encima del área visible, un botón deshabilitado idéntico al habilitado, 39 píxeles de desplazamiento
lateral en un móvil, una API externa que devolvía 403 desde hacía días. Las tres suites —en verde
ese mismo día— no vieron ninguno, y no por estar mal escritas: **jsdom no maqueta, y ninguna prueba
miraba a un tercero real**.

*Cuatro de esos 34 se anularon esa misma noche al retirarse «Descubrir». Quedan 30.*

**Lo que sí resistió el examen.** El contraste en oscuro cumple AA con holgura —5,71:1 el texto
secundario, medido en el navegador—, el foco es visible en los trece puntos de tabulación, el
diálogo modal gestiona el foco, Lighthouse da **100 en Accesibilidad, Buenas prácticas y SEO**, la
correlación de registro de T4-11 nombró el proveedor caído y su excepción sin que hubiera que
buscarla, y el abanico de proveedores degrada correctamente cuando alguno falla. El trabajo de los
seis tiers anteriores aguanta; lo que sigue es lo que quedaba fuera de su alcance.

---

- [ ] **[T6-01] Cambiar la contraseña no cierra ninguna sesión**
  - **Área:** Seguridad · **Severidad:** Alto
  - **Ubicación:** `Controllers/AuthController.cs:329-351`
  - **Problema:** `ChangePassword` devuelve 204 y no toca `RefreshTokens`. `ResetPassword` sí los
    revoca todos (`AuthController.cs:566-568`): el mismo riesgo, dos comportamientos distintos.
    Identity actualiza el `SecurityStamp`, pero este backend no lo valida en ninguna parte, así que
    no tiene efecto.
  - **Verificado en vivo el 2026-09-06:** login → cookie guardada → `POST /auth/change-password`
    (204) → `POST /auth/refresh` **con la cookie anterior al cambio** → **200 y token nuevo**.
  - **Impacto:** cambiar la contraseña es justo lo que hace quien sospecha que le han robado la
    sesión, y es la única acción que no le sirve de nada: el token robado sigue rotando siete días.
  - **Qué hacer:** revocar los tokens del usuario al final de `ChangePassword`, con el mismo
    `ExecuteUpdateAsync` que ya usa `ResetPassword`, y emitir una sesión nueva para quien hizo el
    cambio para no echarlo de su propio navegador.
  - **Criterio de aceptación:** una prueba de integración que repita el experimento de arriba y
    exija **401** en el refresco posterior al cambio. Debe falsificarse quitando la revocación.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [ ] **[T6-02] El error de validación del servidor se pinta fuera de la pantalla**
  - **Área:** UI/UX · **Severidad:** Alto
  - **Ubicación:** `src/features/media-items/components/MediaItemEditorForm.tsx` (bloque con clase
    `auth-error`, al principio del formulario)
  - **Problema:** el editor de biblioteca es alto y se desplaza. El bloque de error va arriba del
    todo y nadie lo trae a la vista, así que al pulsar «Guardar nuevo registro» desde el final del
    formulario **no ocurre nada visible**: ni el diálogo se cierra, ni aparece un aviso, ni se
    desplaza a ninguna parte.
  - **Verificado en vivo el 2026-09-06:** el nodo existe y está pintado, en
    `top: -193px, bottom: -146px` con la ventana en 800px de alto. Lleva `role="alert"`, **así que
    un lector de pantalla sí lo anuncia**: el fallo es exclusivamente para quien mira.
  - **Impacto:** un guardado que falla en silencio. Es el mismo patrón que T2-29 —«el aviso además
    no llegaba a verse»— en otro sitio, y por eso conviene resolverlo donde no pueda repetirse.
  - **Qué hacer:** al recibir un error del servidor, llevar el foco al bloque de error
    (`ref.current?.focus()` con `tabIndex={-1}`) y no solo desplazarlo: el foco arrastra la vista
    **y** sitúa al usuario de teclado. Considerar además un toast, que ya existe en el proyecto.
  - **Criterio de aceptación:** prueba end-to-end que envíe el formulario con un valor que el
    servidor rechace y compruebe que el mensaje está **dentro** de la ventana
    (`expect(locator).toBeInViewport()`). Es la única suite que puede: jsdom no maqueta.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [ ] **[T6-03] La aplicación se desplaza 39 px en horizontal en un móvil de 360 px**
  - **Área:** Diseño responsivo · **Severidad:** Alto
  - **Ubicación:** `src/index.css:302-311`
  - **Problema:** `.hero-panel::after` es un resplandor decorativo de 220×220 px colocado con
    `inset: auto -3rem -5rem auto`, es decir **48 px por fuera del panel por la derecha**, y
    `.hero-panel` no lleva `overflow: hidden`. A 1280 px el margen lateral lo absorbe; a 360 px el
    panel ocupa casi toda la ventana y esos 48 px caen fuera.
  - **Verificado en vivo el 2026-09-06:** `scrollWidth` 384 frente a `clientWidth` 345, y
    `window.scrollTo(9999,0)` mueve la página **39 px**. Es el único pseudo-elemento de todo el
    frontend con un inset negativo.
  - **Impacto:** la aplicación se sirve por LAN **para usarla desde el móvil** (`npm run dev:lan`).
    Toda la interfaz baila lateralmente al arrastrar el dedo.
  - **Qué hacer:** `overflow: hidden` en `.hero-panel`. **Comprobado en el navegador durante la
    auditoría:** el `scrollWidth` baja de 384 a 345 y el desplazamiento de 39 px a **0**, sin que se
    pierda el resplandor visible dentro del panel.
  - **Criterio de aceptación:** prueba end-to-end a 360×640 que exija
    `scrollWidth <= clientWidth`. Vigila el desbordamiento en general, no esta regla en concreto.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [ ] **[T6-04] El envío de correo bloquea la petición, sin plazo y sin reintento**
  - **Área:** Arquitectura (resiliencia) · **Severidad:** Alto
  - **Ubicación:** `Services/SmtpEmailService.cs:44-48`
  - **Problema:** `ConnectAsync`, `AuthenticateAsync`, `SendAsync` y `DisconnectAsync` se esperan
    **dentro de la petición HTTP**, y no se configura `SmtpClient.Timeout`: el valor por defecto de
    MailKit es **120 segundos**. Afecta a `/register`, `/forgot-password` y `/resend-confirmation`.
  - **Impacto:** con Mailtrap lento o caído, registrarse tarda hasta dos minutos con la pantalla
    parada. `SendEmailSafelyAsync` protege del 500 (T1-03) pero no de la espera. Diez peticiones así
    —el cupo de la política `auth`— retienen diez hilos.
  - **Qué hacer:** fijar `client.Timeout` a un valor explícito (5-10 s) y sacar el envío del camino
    de la petición: basta una cola en memoria con `Channel<T>` y un `BackgroundService`, que ya hay
    precedente en `ExpiredDataCleanupService`. El correo es asíncrono por naturaleza; nadie espera
    a que salga.
  - **Criterio de aceptación:** con un servidor SMTP que no responde, `/register` contesta en menos
    de un segundo y el fallo del envío queda registrado. Prueba con un `IEmailService` que tarde.
  - **Esfuerzo:** medio · **Depende de:** ninguna

- [ ] **[T6-06] `/api/auth/confirm-email` dice si un correo tiene cuenta**
  - **Área:** Seguridad · **Severidad:** Medio
  - **Ubicación:** `Controllers/AuthController.cs:587-591`
  - **Problema:** el `EmailConfirmed` se comprueba **antes** de validar el token, así que la
    respuesta depende solo del correo. Verificado con el mismo token basura en los dos casos:
    - registrado y confirmado → **200** `{"message":"El correo ya estaba confirmado…"}`
    - no registrado → **400** `"El enlace de confirmación no es válido o ha expirado."`
  - **Impacto:** T1-04 cerró la enumeración en registro y login; este endpoint quedó fuera y la
    reabre entera, sin autenticación. El límite de 10/min por IP la hace lenta, no imposible.
  - **Qué hacer:** validar el token primero y responder lo mismo en los dos casos, como ya hacen
    `/register`, `/login` y `/forgot-password`.
  - **Criterio de aceptación:** una prueba que exija código y cuerpo **idénticos** para un correo
    registrado y uno inexistente con el mismo token inválido.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [ ] **[T6-07] Las cabeceras de seguridad desaparecen justo en las respuestas 500**
  - **Área:** Seguridad · **Severidad:** Medio
  - **Ubicación:** `Program.cs:376` (manejador de excepciones) y `Program.cs:447-453` (cabeceras)
  - **Problema:** el middleware que añade `X-Content-Type-Options`, `X-Frame-Options` y
    `Referrer-Policy` está registrado **por debajo** de `UseExceptionHandler`. Cuando este captura
    una excepción limpia la respuesta —cabeceras incluidas— y la reescribe desde su propia posición.
  - **Verificado el 2026-09-06:** un 404 llega con las tres cabeceras; un 500 llega **sin ninguna**.
  - **Qué hacer:** mover el bloque de cabeceras por encima de `UseExceptionHandler`, o escribirlas
    con `context.Response.OnStarting(...)` para que sobrevivan a la reescritura.
  - **Criterio de aceptación:** una prueba que provoque un 500 y exija las tres cabeceras. Hoy falla.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [ ] **[T6-08] El despliegue real no envía ninguna cabecera de seguridad**
  - **Área:** Seguridad / DevOps · **Severidad:** Medio
  - **Ubicación:** `netlify.toml:22-32`; `index.html:19-20` (el comentario que lo afirma)
  - **Problema:** las seis cabeceras del frontend —`frame-ancestors 'none'`, `X-Frame-Options`,
    `X-Content-Type-Options`, `Referrer-Policy`, `Permissions-Policy` y
    `Cross-Origin-Opener-Policy`— viven en `netlify.toml`. **Netlify se retiró el 2026-09-05.** La
    aplicación se sirve con `vite dev`, que no envía ninguna de ellas.
  - **Verificado el 2026-09-06:** `curl -D - http://localhost:5173/` devuelve `Vary`,
    `Content-Type`, `Cache-Control`, `Etag` y nada más.
  - **Lo grave no es el riesgo —es una LAN doméstica— sino que la documentación afirme una
    protección que no existe.** `index.html` dice literalmente que `frame-ancestors` «va como
    cabecera en netlify.toml», y quien lea el archivo creerá que la aplicación no se puede enmarcar.
  - **Qué hacer:** decidir y dejarlo escrito. O bien se asume que en LAN no hay cabeceras y se
    corrige el comentario de `index.html` para que no prometa lo que no hay, o bien se sirve el
    build con algo que sí las mande. Lo segundo solo tiene sentido si se vuelve a desplegar.
  - **Criterio de aceptación:** ningún archivo del repositorio afirma una cabecera que el despliegue
    vigente no envíe.
  - **Esfuerzo:** bajo · **Depende de:** T6-30

- [ ] **[T6-09] `?page=2147483647` devuelve un 500**
  - **Área:** Código · **Severidad:** Medio
  - **Ubicación:** `Contracts/MediaItems/GetMediaItemsRequest.cs:38` y
    `Services/MediaItemsService.cs:138`
  - **Problema:** `[Range(1, int.MaxValue)]` admite el máximo, y `Skip((Page - 1) * PageSize)`
    desborda el entero sin marcar: `(2147483646) * 100` da negativo y EF Core emite un `OFFSET`
    negativo.
  - **Verificado el 2026-09-06:** HTTP 500, y en el log
    `Npgsql.PostgresException 2201X: OFFSET no debe ser negativo`.
  - **Impacto:** ninguna fuga de datos —el `ProblemDetails` es correcto y lleva `traceId`— pero es
    una excepción no controlada que provoca cualquier usuario autenticado desde la barra de
    direcciones, y además pierde las cabeceras de T6-07.
  - **Qué hacer:** acotar `Page` a un máximo razonable (`[Range(1, 100000)]`) o calcular el salto en
    `long` y recortarlo. Lo primero es más honesto: nadie pagina hasta ahí.
  - **Criterio de aceptación:** `?page=2147483647` responde 400 de validación, no 500.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [ ] **[T6-10] 122 validaciones sin mensaje propio: el usuario lee inglés y nombres de C#**
  - **Área:** Código / Redacción · **Severidad:** Medio
  - **Ubicación:** `Contracts/**` y `Domain/**` — 122 atributos `[Required]`, `[Range]`,
    `[StringLength]`, `[EmailAddress]` y `[EnumDataType]`, **ninguno** con `ErrorMessage`
  - **Problema visto en pantalla el 2026-09-06,** con la interfaz en español:
    `The field CurrentSeason must be between 1 and 2147483647.` Tres defectos en una frase: está en
    inglés, nombra la propiedad de C# en vez del campo que el usuario ve («Temporada»), y enseña
    `int.MaxValue`.
  - **Relación con T2-26:** aquella tarea unificó el idioma de los mensajes **escritos a mano** en
    servicios y controladores. La capa de anotaciones nunca entró en su alcance, así que no es un
    cierre en falso, pero sí el trozo que quedó fuera y que es el que ve el usuario.
  - **Qué hacer:** empezar por los atributos de los contratos que llegan a la interfaz, con
    `ErrorMessage` en español y el nombre visible del campo. Para el resto, valorar un
    `IValidationMetadataProvider` que traduzca por convención en vez de 122 cadenas a mano.
  - **Criterio de aceptación:** una prueba que recorra los tipos de `Contracts/` y falle si un
    atributo de validación no declara `ErrorMessage`. Fija la regla en vez de la lista.
  - **Esfuerzo:** medio · **Depende de:** ninguna

- [ ] **[T6-11] Un botón deshabilitado se ve exactamente igual que uno pulsable**
  - **Área:** UI/UX · **Severidad:** Medio
  - **Ubicación:** `src/index.css` — hay `:disabled` para `.auth-shell .button--primary`
    (`index.css:1655`) y para `.action-menu__item` (`:417`), **y para nada más**
  - **Problema medido el 2026-09-06** con la biblioteca vacía, comparando «Exportar» (deshabilitado)
    con «Importar» (activo): mismo `color` `rgb(241,245,249)`, mismo fondo `rgb(30,41,59)`, mismo
    borde, `opacity: 1` en los dos y —lo que remata— **`cursor: pointer` en el deshabilitado**.
  - **Impacto:** el usuario pulsa y no pasa nada, sin explicación. Ocurre en los dos temas.
  - **Qué hacer:** una regla `:disabled` para `.button` en la capa base —opacidad reducida y
    `cursor: not-allowed`—, no dentro de `.auth-shell`. Y en «Exportar» conviene además decir *por
    qué* está apagado: no hay nada que exportar.
  - **Criterio de aceptación:** ampliar `form-controls.test.ts`, que ya recorre los 61 campos del
    frontend, para exigir que `.button:disabled` se distinga del habilitado en al menos una
    propiedad visible.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [ ] **[T6-12] Las devtools de TanStack Query se sirven en el uso real**
  - **Área:** DevOps / UI · **Severidad:** Medio
  - **Ubicación:** `src/main.tsx:166`
  - **Problema:** el guardado `import.meta.env.DEV` es correcto **para un build**, pero la forma de
    uso documentada del proyecto es `npm run dev:lan`, que es precisamente un servidor de
    desarrollo. Así que el botón flotante se sirve siempre.
  - **Verificado el 2026-09-06:** visible en la esquina inferior derecha de todas las pantallas y,
    a 360 px, **encima de los controles «Importar» y «Exportar»** (ver captura
    `03-movil-360-scroll-horizontal.png`). Es además el único elemento enfocable de la página sin
    indicador de foco.
  - **Qué hacer:** es un síntoma de algo mayor —servir desarrollo como si fuera producción, ver
    T6-30—, pero se puede atajar ya: condicionar las devtools a una variable propia
    (`VITE_DEVTOOLS`) en vez de a `DEV`, apagada en el `.env` de uso normal.
  - **Criterio de aceptación:** con el `.env` de uso habitual, `npm run dev:lan` no pinta el botón.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [ ] **[T6-13] «Temporada» admite 0 en el navegador y el servidor exige 1**
  - **Área:** Validación · **Severidad:** Medio
  - **Ubicación:** `src/features/media-items/components/MediaItemEditorForm.tsx` (`min="0"`) frente a
    `Domain/Entities/MediaItem.cs:73` (`[Range(1, int.MaxValue)]`)
  - **Problema:** el campo acepta 0 sin protestar y el servidor lo rechaza. Es el desajuste que
    destapó T6-02 y T6-10, porque los tres se ven en la misma pantalla.
  - **Qué hacer:** decidir cuál es la regla —¿existe la temporada 0?— y escribirla **una vez**. Si
    el mínimo es 1, `min="1"` en el campo y en el esquema de Zod; si es 0, corregir el `[Range]` del
    dominio. Lo que no puede quedarse es una regla distinta en cada lado.
  - **Criterio de aceptación:** ningún valor aceptado por el formulario es rechazado por el
    servidor. Revisar de paso «Episodios», que tiene el mismo `min="0"`.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [ ] **[T6-14] El enlace al perfil mide 17 px de alto**
  - **Área:** Accesibilidad · **Severidad:** Medio
  - **Ubicación:** `src/layouts/AppLayout.tsx` (enlace con el nombre de usuario en la cabecera)
  - **Problema:** medido en el navegador, **105 × 17 px**. WCAG 2.2 AA, criterio 2.5.8 *Target Size
    (Minimum)*, pide 24 × 24 CSS px. No le aplica la excepción de «enlace en línea dentro de un
    texto»: es un control suelto de la cabecera.
  - **Impacto:** es el único acceso al perfil —y por tanto a exportar los datos y a borrar la
    cuenta— desde la aplicación, y se usa desde el móvil.
  - **Qué hacer:** darle el `--control-min-height` que T5-03 creó justamente para esto, o padding
    vertical hasta 24 px como mínimo (44 px sería lo deseable en táctil).
  - **Criterio de aceptación:** prueba end-to-end a 360×640 que recorra los controles interactivos y
    exija 24 × 24 en todos. Cubre el resto de la interfaz de paso.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [ ] **[T6-15] Los clientes de Google y GitHub no tienen plazo**
  - **Área:** Arquitectura (resiliencia) · **Severidad:** Medio
  - **Ubicación:** `Program.cs:238` y `Program.cs:242`
  - **Problema:** los tres catálogos externos sí declaran `Timeout` (10-15 s, `Program.cs:301-323`);
    los dos `AddHttpClient` de OAuth no, así que heredan los **100 segundos** por defecto de
    `HttpClient`. El callback de OAuth hace dos llamadas seguidas (token y perfil), y GitHub tres.
  - **Impacto:** un proveedor lento deja al usuario en una pantalla en blanco durante minutos, y el
    `catch` de `OAuthController.cs:159-164` —que ya contempla `TaskCanceledException`— no llega a
    entrar hasta que expira ese plazo.
  - **Qué hacer:** declarar `client.Timeout` igual que en los catálogos. Los valores razonables aquí
    son más bajos: 10 s de sobra para un canje de código.
  - **Criterio de aceptación:** los cinco clientes HTTP salientes del proyecto declaran plazo
    explícito. Una prueba que recorra el `IHttpClientFactory` lo fija.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [ ] **[T6-17] Cinco cifras de la documentación no coinciden con la realidad**
  - **Área:** Documentación · **Severidad:** Medio
  - **Ubicación:** `docs/WORKFLOW.md` (tabla de comandos y sección «Base de datos»);
    `docs/README.md` (tabla «Estado del proyecto — 2026-09-04»)
  - **Problema, todo comprobado el 2026-09-06:**

    | Dónde | Dice | Es |
    |---|---|---|
    | `WORKFLOW.md`, comandos | «Suite del backend, **171** pruebas» | **182**, 0 omitidas |
    | `WORKFLOW.md`, base de datos | «el actual, el **5432** por defecto» | **5433** (`netstat` y la cadena de user-secrets) |
    | ~~`README.md`, estado~~ | ~~Tres cifras caducadas a la vez~~ | **Resuelto el 2026-09-06**: la tabla se retiró y ahora enlaza a CONTEXT.md |

  - **Por qué importa más de lo que parece:** el propio `WORKFLOW.md` avisa de que el puerto «es el
    detalle que más veces se ha escrito mal en una cadena de conexión». **La nota que existe para
    evitar ese error es la que está equivocada** — y encima se contradice con `README.md`, que sí
    dice 5433. Con dos documentos discrepando, quien los lea elegirá mal la mitad de las veces.
  - **La causa no es descuido, es estructura:** las mismas cifras están escritas en tres archivos.
    Corregirlas hoy no impide que vuelvan a separarse mañana.
  - **Qué hacer:** dejar cada cifra **en un solo sitio**. Las de estado viven ya en
    [CONTEXT.md](CONTEXT.md) con su fecha; los otros documentos deberían enlazar en vez de repetir.
    Retirar o corregir la tabla de estado de `docs/README.md`, que es la más antigua de las tres.
  - **Hecho a medias el 2026-09-06:** corregidas las dos cifras de `WORKFLOW.md` y retirada la
    tabla de estado de `docs/README.md`, que era la peor de las tres. **Queda la parte estructural:**
    los recuentos de pruebas siguen escritos en `WORKFLOW.md` y en `CONTEXT.md` a la vez, así que
    pueden volver a separarse.
  - **Criterio de aceptación:** ninguna cifra medida aparece en dos archivos a la vez, y las que
    quedan coinciden con lo que devuelven los comandos.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [ ] **[T6-18] No hay ni una etiqueta de git, y el changelog habla de versiones**
  - **Área:** DevOps · **Severidad:** Medio
  - **Ubicación:** los dos repositorios; `docs/CHANGELOG.md:11`
  - **Problema:** `git tag` no devuelve nada en ninguno de los dos. El aviso del CHANGELOG lo
    reconoce y se compromete: «a partir de ahora, cada corte de versión debe etiquetarse en git». Ese
    «a partir de ahora» es del 2026-08-27 y sigue sin cumplirse; mientras tanto `[Sin publicar]` ha
    acumulado la mayor parte del trabajo de cuatro semanas.
  - **Impacto:** no se puede responder «qué había el día que esto funcionaba», que es la pregunta
    para la que existe una etiqueta. Y el `package.json` dice `1.0.0` desde siempre.
  - **Qué hacer:** cortar `v1.0.0` en los dos repositorios sobre el commit actual, mover
    `[Sin publicar]` a esa versión con su fecha, y dejar el corte de versión escrito como paso de
    `WORKFLOW.md`.
  - **Criterio de aceptación:** `git tag` devuelve al menos una etiqueta en cada repositorio y el
    CHANGELOG tiene una sección con esa versión y su fecha.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [ ] **[T6-19] Ninguna de las tres suites puede ver los defectos de este tier**
  - **Área:** QA · **Severidad:** Medio
  - **Ubicación:** `src/**/*.test.tsx` (jsdom), `e2e/*.spec.ts`
  - **Problema:** las tres suites estaban en verde mientras la aplicación tenía un error invisible al
    guardar, 39 px de desplazamiento lateral en móvil y un botón deshabilitado indistinguible. No es
    un fallo de escritura: **jsdom no maqueta**, así que ninguna prueba de componente puede medir
    posición, desbordamiento ni visibilidad, y las end-to-end existentes comprueban flujos, no
    geometría.
  - **El proyecto ya sabe esto** —T5-12 lo dice con estas palabras— pero la conclusión no se
    generalizó: se escribió una prueba end-to-end para *aquel* defecto en vez de una familia de
    comprobaciones para *esa clase* de defecto.
  - **Qué hacer:** un archivo end-to-end nuevo, `e2e/presentacion.spec.ts`, que recorra las cinco
    pantallas en 360, 768 y 1280, en los dos temas, y compruebe tres invariantes: sin desplazamiento
    horizontal, todo control interactivo de 24×24 como mínimo, y todo mensaje de error dentro de la
    ventana. Las tareas T6-02, T6-03, T6-11 y T6-14 aportan cada una su criterio; esta es la casa
    donde viven.
  - **Criterio de aceptación:** el archivo existe, falla contra el código de hoy y pasa cuando las
    cuatro tareas anteriores estén cerradas.
  - **Esfuerzo:** medio · **Depende de:** T6-02, T6-03, T6-11, T6-14

- [ ] **[T6-21] La tipografía se descarga de Google en cada carga**
  - **Área:** Legal / Rendimiento · **Severidad:** Bajo · *Requiere revisión legal si se despliega*
  - **Ubicación:** `index.html:47-52`
  - **Problema:** Inter se pide a `fonts.googleapis.com` y `fonts.gstatic.com`. Cada visita
    transmite la IP del usuario a un tercero, cosa que en la UE ha sido objeto de litigio. Además
    contradice el espíritu del `connect-src 'self'` que el propio archivo defiende con tanto
    cuidado, y añade dos orígenes al camino crítico de renderizado.
  - **Hoy no aplica** por la misma razón que T0-05: el uso es doméstico y el único usuario es el
    propietario. **Se reactiva si se vuelve a desplegar.**
  - **Qué hacer:** alojar Inter en `public/` como `woff2` y servirla desde el propio origen. Se
    ahorran dos conexiones y desaparece el tercero.
  - **Criterio de aceptación:** ninguna petición de la aplicación sale hacia un dominio de Google.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [ ] **[T6-23] Dos restos sueltos en el árbol de archivos**
  - **Área:** Limpieza · **Severidad:** Bajo
  - **Ubicación:** `TrackerMultimedia_Frontend/__pycache__/anexar.cpython-314.pyc`;
    `TrackerMultimedia/TrackerMultimedia.Tests/obj/`
  - **Problema:** un `.pyc` de Python dentro del repositorio de React —de un script `anexar.py` que
    ya no está—, y una carpeta `obj/` huérfana en la carpeta padre, **fuera de los dos
    repositorios**, superviviente del movimiento de la suite que hizo T0-02.
  - **Nota:** ninguno está versionado (los dos árboles de git están limpios), así que es limpieza de
    disco, no del historial.
  - **Qué hacer:** borrar los dos y añadir `__pycache__/` al `.gitignore` del frontend.
  - **Criterio de aceptación:** no quedan.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [ ] **[T6-24] `MediaItemsService` tiene 1.561 líneas y más de cincuenta métodos**
  - **Área:** Refactorización · **Severidad:** Bajo
  - **Ubicación:** `Services/MediaItemsService.cs`
  - **Problema:** es el 22 % del backend en un archivo, y hace tres trabajos distintos: consultas de
    biblioteca (CRUD y filtros), **serialización de exportación** (JSON y CSV, con su escapado) y
    **análisis de importación** (lector de CSV a mano, 14 funciones de conversión y normalización).
    Los tres últimos bloques no dependen del `DbContext`: son estáticos.
  - **Impacto:** ninguno funcional —está bien escrito y probado—, pero es el archivo que más cuesta
    abrir y donde más caro sale equivocarse.
  - **Qué hacer:** extraer `LibraryExportWriter` y `LibraryImportReader` como clases propias. Son
    métodos estáticos ya: la extracción es mecánica y las pruebas existentes deberían seguir en
    verde sin tocarlas.
  - **Criterio de aceptación:** ningún archivo del backend pasa de 600 líneas y la suite sigue en
    150/150.
  - **Esfuerzo:** medio · **Depende de:** ninguna

- [ ] **[T6-25] El tema oscuro se sostiene sobre 67 excepciones por componente**
  - **Área:** Refactorización / Sistema de diseño · **Severidad:** Bajo
  - **Ubicación:** `src/index.css:2603-3111`
  - **Problema medido:** `:root` define **55 tokens**; el bloque `[data-theme='dark']` redefine
    **20**. Los otros 35 se compensan con **67 reglas `[data-theme='dark'] .algo`**, una por
    componente.
  - **Por qué importa:** es la causa mecánica de T5-01, T5-05, T5-06 y T5-11. Mientras el tema
    oscuro no salga entero de la capa de tokens, **cada componente nuevo nace con un fallo latente**
    —el que se le olvide su regla— y ese fallo es invisible para toda prueba de comportamiento. Los
    cuatro defectos anteriores no fueron descuidos: son lo que produce esta estructura.
  - **Qué hacer:** no es una tarea de una sesión. Empezar por medir qué 35 tokens faltan por tematizar
    y llevar a la capa de tokens los grupos que más overrides concentran (superficies, bordes,
    campos). Cada grupo migrado retira sus reglas por componente.
  - **Criterio de aceptación:** el número de reglas `[data-theme='dark'] .clase` baja de 67 a menos
    de 20, y una prueba lo fija como techo para que no vuelva a crecer.
  - **Esfuerzo:** alto · **Depende de:** ninguna

- [ ] **[T6-26] Un texto de la interfaz habla en jerga de implementación**
  - **Área:** Redacción · **Severidad:** Bajo
  - **Ubicación:** `src/features/auth/views/ProfileView.tsx` (panel «Sesiones»)
  - **Problema:** dice «Revoca todos los **refresh tokens**», jerga de implementación en una interfaz
    que en todo lo demás está escrita en un castellano llano y muy cuidado.
  - **Reducida el 2026-09-06.** Nació con dos textos; el segundo era el error de «Descubrir»
    —«Verifica tu conexión» cuando quien fallaba era AniList— y se fue con la pantalla.
  - **Qué hacer:** «Cierra la sesión en todos tus dispositivos».
  - **Criterio de aceptación:** ningún texto de la interfaz nombra un concepto interno.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [ ] **[T6-27] Dos mecanismos para no refrescar dos veces**
  - **Área:** Código · **Severidad:** Bajo
  - **Ubicación:** `src/shared/api/axios.ts:61-72` y `:76-136`
  - **Problema:** conviven `inFlightRefresh` —que evita dos llamadas simultáneas a `/auth/refresh`— y
    la pareja `isRefreshing` + `failedQueue` —que encola las peticiones que esperan—. Cada uno
    resuelve una mitad, pero se solapan y hay que leer los dos para entender el flujo. Además la vía
    encolada (`:112-117`) reintenta **sin marcar `_retry`**, de modo que un segundo 401 en esa
    petición puede disparar un refresco extra.
  - **Impacto:** ninguno observado —el `_retry` de la segunda vuelta corta el bucle— y el comentario
    de `:47-59` explica muy bien por qué existe el single-flight. Es deuda de legibilidad en el
    archivo con el razonamiento más delicado del frontend.
  - **Qué hacer:** unificar en un solo mecanismo: `refreshSession()` ya devuelve una promesa
    compartida, así que las peticiones en espera pueden `await`-earla directamente y sobra la cola.
    Marcar `_retry` antes de reencolar.
  - **Criterio de aceptación:** los tests de `axios.test.ts` siguen en verde y ninguna petición
    provoca más de un refresco.
  - **Esfuerzo:** medio · **Depende de:** ninguna

- [ ] **[T6-28] La ordenación no tiene desempate único y la consulta va partida**
  - **Área:** Código · **Severidad:** Bajo
  - **Ubicación:** `Services/MediaItemsService.cs:1390-1433` y `:140`
  - **Problema:** ninguna de las siete ordenaciones termina en una columna única. Ordenando por
    título, el desempate es `CreatedAtUtc`; dos registros con el mismo título creados en la misma
    importación —que comparten marca de tiempo— quedan en orden indefinido. Con `AsSplitQuery()` y
    `Skip/Take`, EF Core advierte de que eso puede duplicar o perder filas entre páginas.
  - **Impacto:** improbable y nunca observado, pero silencioso: nadie se entera de que le falta un
    elemento en la página 3.
  - **Qué hacer:** añadir `.ThenBy(item => item.Id)` como último desempate en las siete ramas.
  - **Criterio de aceptación:** una prueba que cree varios elementos con el mismo título y la misma
    fecha, pagine de dos en dos y compruebe que la unión de las páginas es el conjunto entero.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [ ] **[T6-29] El `link_token` viaja en la barra de direcciones**
  - **Área:** Seguridad · **Severidad:** Bajo
  - **Ubicación:** `Controllers/OAuthController.cs:259-263`
  - **Problema:** la redirección a `/link-account` lleva `link_token`, `provider` y `email` en la
    **cadena de consulta**, que sí se manda al servidor y queda en el historial. T4-01 movió los
    tokens del éxito al fragmento precisamente por esto, y el comentario de `:350-360` lo argumenta
    bien; este camino se quedó atrás.
  - **Atenuante:** el `link_token` solo no basta —`LinkConfirm` exige además la contraseña de la
    cuenta— y caduca en 15 minutos, así que la exposición es baja.
  - **Qué hacer:** pasarlo por el fragmento, como ya hace `BuildSuccessRedirect`.
  - **Criterio de aceptación:** ninguna redirección del flujo OAuth lleva credenciales en la cadena
    de consulta.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [ ] **[T6-30] Se sirve un servidor de desarrollo como si fuera el producto**
  - **Área:** DevOps · **Severidad:** Bajo *(pero es la raíz de T6-08 y T6-12)*
  - **Ubicación:** `docs/WORKFLOW.md` (`npm run dev:lan` como forma de uso); `netlify.toml`;
    `render.yaml`; `Dockerfile`
  - **Problema:** la forma de uso documentada es `vite dev`, con módulos sin empaquetar, sin
    minificar, con sourcemaps, con las devtools dentro y sin ninguna cabecera. Y a la vez el
    repositorio conserva tres blueprints —Netlify, Render y Docker— de un despliegue retirado el
    2026-09-05, que describen un mundo que ya no existe.
  - **No es un defecto en sí**: para uso doméstico `vite dev` es una decisión razonable, y tiene la
    ventaja de la recarga en caliente. **El defecto es que no está decidido ni escrito**, así que
    medio proyecto asume una cosa y el otro medio, otra.
  - **Qué hacer:** escribir la decisión en `DECISIONS.md`. Si se queda `dev:lan`, decirlo y
    marcar los tres blueprints como históricos —o retirarlos— para que nadie los lea como vigentes.
    Si se prefiere `npm run preview` sobre el build, medirlo y documentarlo: el proxy de `/api` ya
    está configurado para el 4173 (`vite.config.ts:33-40`).
  - **Criterio de aceptación:** `DECISIONS.md` dice cómo se sirve la aplicación y por qué, y no
    queda ningún archivo de configuración que contradiga esa decisión sin decir que es histórico.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [ ] **[T6-31] La búsqueda dentro de la biblioteca no puede usar índice**
  - **Área:** Rendimiento · **Severidad:** Bajo
  - **Ubicación:** `Services/MediaItemsService.cs:82-84`
  - **Problema:** `EF.Functions.ILike(item.Title, "%término%")` con comodín inicial obliga a
    PostgreSQL a recorrer la tabla entera. El escapado de `%`, `_` y `\` está bien hecho.
  - **Impacto: hoy ninguno**, y conviene decirlo así de claro. Con una biblioteca personal de cientos
    o pocos miles de filas, el recorrido secuencial es más rápido que cualquier índice. **No se ha
    medido** ninguna lentitud: se anota como algo que vigilar, no como un problema que exista.
  - **Qué hacer:** nada por ahora. Si alguna vez se nota, la salida es un índice GIN con `pg_trgm`.
    Queda escrito para no volver a investigarlo desde cero.
  - **Criterio de aceptación:** no aplica hasta que haya una medición que lo justifique.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [ ] **[T6-32] El login tarda distinto según exista la cuenta** · *pendiente de verificación*
  - **Área:** Seguridad · **Severidad:** Bajo
  - **Ubicación:** `Controllers/AuthController.cs:128-132`
  - **Problema, leído en el código y no medido:** cuando el correo no existe, `Login` responde de
    inmediato; cuando existe, ejecuta antes `CheckPasswordAsync`, que es un PBKDF2 de decenas de
    milisegundos. Esa diferencia es medible desde fuera y reabriría por vía temporal la enumeración
    que T1-04 cerró en el contenido de la respuesta. Lo mismo, y más marcado, en `ForgotPassword`
    (`:518-522`), donde el camino con cuenta genera un token y **envía un correo** (ver T6-04).
  - **Por qué queda pendiente:** medirlo bien exige muchas peticiones cronometradas, y la política
    `auth` corta a 10 por minuto. No se ha hecho, así que **no se afirma que exista**: se afirma que
    el código tiene la forma que lo produciría.
  - **Qué hacer:** primero medirlo, subiendo el cupo en una configuración de prueba. Si la diferencia
    es apreciable, la corrección habitual es calcular un hash señuelo en la rama sin cuenta.
  - **Criterio de aceptación:** una medición con su fecha que confirme o descarte la diferencia. Si
    la confirma, que las dos ramas queden dentro del mismo margen.
  - **Esfuerzo:** medio · **Depende de:** T6-04

- [ ] **[T6-33] La base de datos local no tiene copia de seguridad**
  - **Área:** DevOps · **Severidad:** Bajo
  - **Ubicación:** `docs/WORKFLOW.md`, sección «Base de datos»
  - **Problema:** desde que Neon se eliminó (2026-09-05), **la única copia de la biblioteca está en
    el PostgreSQL de este equipo**. No hay copia programada ni documentada, y `WORKFLOW.md` sí
    documenta `dotnet ef database drop --force`, que la borra entera.
  - **Atenuante:** existe la exportación de biblioteca y la de datos personales (T4-08), así que hay
    un camino manual. Nadie lo ejecuta solo.
  - **Qué hacer:** una línea de `pg_dump` en la tabla de comandos de `WORKFLOW.md`, y decidir si se
    programa. Para uso personal, un volcado semanal a una carpeta sincronizada basta.
  - **Criterio de aceptación:** `WORKFLOW.md` explica cómo se hace y cómo se restaura una copia, y
    consta cuándo se probó la restauración por última vez.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [ ] **[T6-34] La suite end-to-end no pasa siguiendo el procedimiento documentado**
  - **Área:** QA / Documentación · **Severidad:** Alto
  - **Ubicación:** `docs/WORKFLOW.md` (tabla de comandos); `e2e/global-setup.ts`;
    `e2e/fixtures.ts:45-58`; `Infrastructure/Options/RateLimitingOptions.cs:27`
  - **Problema:** el fixture `email` tiene `auto: true`, así que **cada** prueba registra una cuenta
    e inicia sesión: 2 peticiones de la política `auth` × 15 pruebas = **30 peticiones**, en serie,
    en poco más de un minuto. El cupo por defecto de esa política es **10 por minuto**.
  - **Medido el 2026-09-06, dos ejecuciones sobre el mismo código:**
    - Backend arrancado como dice `WORKFLOW.md` (`dotnet run`, sin más) → **12 fallos, 3 aciertos**.
    - Backend con `RateLimiting__Auth__PermitLimit=1000` → **15 aciertos en 55 s**.
  - **La suite está bien; lo que falta es el requisito.** `RateLimitingOptions` documenta en su
    propio comentario que los cupos se hicieron configurables en T4-04 *porque* la suite los
    agotaba —así que esto se supo—, pero **el valor necesario no está escrito en ningún sitio**:
    ni en `WORKFLOW.md`, ni en `playwright.config.ts`, ni en el README. `global-setup.ts` comprueba
    con cuidado que estén PostgreSQL y el backend, y no comprueba lo único que además hace falta.
  - **Por qué es Alto y no Medio:** el ROADMAP viene afirmando **15/15** como hecho verificado. Es
    cierto, pero solo bajo una condición que nadie escribió, así que cualquiera que siga la
    documentación concluye que la suite está rota. Una cifra que no se puede reproducir con el
    procedimiento publicado deja de ser una comprobación y pasa a ser una creencia.
  - **Qué hacer:** las dos mitades. (a) Que `global-setup.ts` consulte el cupo efectivo y **falle
    con un mensaje que diga qué exportar**, igual que ya hace con el backend apagado. (b) Escribir
    el requisito en la fila de `npm run test:e2e` de `WORKFLOW.md`. Alternativa mejor si se quiere
    evitar la variable: que el fixture reutilice una cuenta sembrada en vez de registrar una por
    prueba, con lo que la suite dejaría de depender del cupo.
  - **Criterio de aceptación:** partiendo de un backend arrancado con la documentación en la mano,
    `npm run test:e2e` termina en 15/15 o falla diciendo exactamente qué falta.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

---

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

**Las trece tareas del tier están cerradas**: nueve el 2026-09-05 —el mismo día que se abrió— y
T5-10 a T5-13 el 2026-09-06, las cuatro abiertas porque el propietario las vio en pantalla. Ver la
tabla de cerradas.

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

### T6-05, T6-16, T6-20 y T6-22 — ANULADAS por la retirada de «Descubrir»

Anuladas el **2026-09-06**, el mismo día que se abrieron, al eliminarse la búsqueda en catálogos
externos por decisión del propietario. **No se resolvieron: desapareció el código que las
producía.** Los identificadores no se reutilizan.

| ID | Era | Por qué ya no aplica |
|---|---|---|
| T6-05 | «Descubrir» venía con AniList marcado y AniList devolvía 403 | No hay pantalla, ni proveedores, ni valor por defecto que corregir |
| T6-16 | El abanico de proveedores esperaba al más lento | No queda ninguna operación en abanico en el proyecto |
| T6-20 | El nombre accesible de las casillas de proveedor cambiaba con su estado | Esas casillas eran las únicas de la aplicación |
| T6-22 | Jikan, AniList y MangaDex no estaban atribuidos | Ya no se consume ninguno, así que no hay nada que atribuir |

**Lo que sí conviene no perder de vista.** T6-16 describía un problema de diseño —esperar a *todos*
cuando basta con los que contesten— que volvería con cualquier operación en abanico futura, y T6-05
dejó la lección más cara de las cuatro: **la arquitectura degradaba correctamente y un valor por
defecto de la interfaz lo anulaba**. Ninguna de las dos cosas es específica de los catálogos.

De propina, el cambio dejó **vacía** la lista de excepciones de `form-controls.test.ts`: la única que
tenía era la casilla de proveedor. Ya no hay ni un campo del frontend sin la clase del sistema de
diseño.

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
| T5-11 | La flecha de los `select` pegada al borde derecho | 2026-09-06 · La flecha nativa se dibuja en el filo y **no la mueve `padding-right`**, así que los doce `select` de la aplicación —biblioteca, editor de registros, descubrir y alta rápida— la tenían pegada mientras su texto respetaba el margen del otro lado. Se sustituye por el mismo `ChevronDownIcon` que usan los menús de acciones, colocado con el margen del texto. `select-arrow.test.ts` vigila el efecto secundario del arreglo, que costó dos intentos: el fondo de tema oscuro se pintaba con `background` en forma corta, que **no reinicia solo la imagen sino también repetición, tamaño y posición**. Reponer únicamente la imagen dejó los campos empapelados de flechas gigantes en oscuro. Arreglado de raíz —los fondos se pintan con `background-color`— y la prueba pasó a comprobar las cuatro propiedades en los dos temas |
| T5-12 | El desplegable de color se cortaba dentro del diálogo | 2026-09-06 · Flotaba con `position: absolute` dentro de `.side-panel__content`, que se desplaza: un hijo absoluto no ensancha la caja de su contenedor pero sí cuenta como desbordamiento, de modo que el diálogo sacaba barra y recortaba la fila del color personalizado **con sitio de sobra en pantalla** —medido: el diálogo terminaba en 720 y el desplegable en 725—. Puesto en el flujo, el diálogo pasa de 396 a 527px sin barra. Lo comprueba una prueba end-to-end, la única que puede: jsdom no maqueta |
| T5-13 | El fondo ambiental se movía solo y con el cursor | 2026-09-06 · A petición del propietario, en dos pasos: primero el `drift` que seguía al puntero —mover el ratón arremolinaba el humo **detrás de lo que estás leyendo**— y después la deriva por tiempo. Sin ninguna de las dos sobra el bucle: se va un `pointermove` global que disparaba en cada píxel y **un `requestAnimationFrame` que repintaba la pantalla entera sesenta veces por segundo, para siempre**. Se pinta una vez, y eso traslada al código la obligación de repintar al cambiar de tamaño y de tema, que el bucle hacía gratis. Medido: **0 fotogramas en 1,5 s de reposo**. `e2e/fondo.spec.ts` comprueba las tres cosas que fallarían calladas |
| T5-03 | La capa de tokens no medía nada, solo pintaba | 2026-09-05 · Los 33 tokens eran todos de color y sombra, así que cada clase inventaba sus medidas: **39 espaciados, 9 radios y 19 tamaños de texto**, seis de ellos indistinguibles entre sí por menos de un píxel. Escala de espaciado en rejilla de 4px, radios, `--type-*` y alturas de línea, más `--control-min-height` para que el objetivo táctil de 44px tenga nombre propio. La pantalla de acceso queda migrada entera como plantilla, con una prueba que falla si vuelve a escribirse una medida a mano |

---

## Progreso

Una línea por sesión de cierre, con fecha absoluta. El detalle de **cómo** se resolvió cada tarea
va en [HISTORY.md](HISTORY.md); aquí solo queda el recuento, para poder mirar la evolución sin leer
nada más.

| Fecha | Cerradas en la sesión | Abiertas al terminar | Nota |
|---|---|---|---|
| 2026-08-27 | 46 | 50 | Auditoría inicial. Tiers 0 a 4 |
| 2026-09-02 | 30 | 20 | — |
| 2026-09-04 | 8 | 12 | T1-13 se reabre: estaba cerrada en falso |
| 2026-09-05 | 12 | 6 | Se abre y casi se cierra entero el Tier 5 |
| 2026-09-06 (mañana) | 6 | 0 | T4-03, T5-10 a T5-13 y T2-29 |
| 2026-09-06 (tarde) | 0 | 34 | **Re-auditoría con la aplicación en marcha. Se abre el Tier 6** |
| 2026-09-06 (noche) | 0 | **30** | Se elimina «Descubrir»: 4 tareas anuladas, no resueltas. Suites: 150 · 197 · 15 |

**Cómo se cierra una tarea.** Se marca `[x]` con la fecha absoluta **el día que se verifica su
criterio de aceptación**, no el día que se escribe el código. Si el criterio exige el navegador o la
aplicación levantada, se usan; si no se puede verificar, la tarea se queda abierta con la razón
escrita. T1-13 está en este archivo como recordatorio de lo que cuesta lo contrario: se dio por
cerrada el 2026-08-27 y estuvo nueve días sin estarlo.

## Decisiones cerradas

Lo que se decidió **no** hacer, para que no se vuelva a proponer. El detalle de cada una está en
*En suspenso y anuladas* y en [DECISIONS.md](DECISIONS.md).

| Qué | Cuándo | Por qué no |
|---|---|---|
| Montar integración continua (T1-12) | 2026-08-27 | Decisión del propietario. La sustituye la rutina previa a cada commit de [WORKFLOW.md](WORKFLOW.md) |
| Mover PostgreSQL a Docker | 2026-08-27 | Se probó y se revirtió el mismo día, a petición del propietario |
| Migrar a Tailwind o a una librería de componentes | 2026-09-05 | Semanas de trabajo para llegar al mismo aspecto, tirando la accesibilidad ya pagada en T1-16 a T1-23 |
| Cambiar `--surface-strong` (T5-04) | 2026-09-05 | El anti-patrón habla del cristal y ese token no lo es. Hay una prueba que fija la premisa |
| Añadir OpenTelemetry (T4-06) | 2026-09-04 | En suspenso, no anulada: sin servicio desplegado no hay dónde exportar ni a quién alertar. **Se reactiva al desplegar** |
| Publicar política de privacidad (T0-05) | 2026-08-27 | En suspenso, no anulada: sin terceros no hay tratamiento de sus datos. **Se reactiva en cuanto entre alguien que no seas tú** |

> **Ojo con las dos últimas.** No son decisiones cerradas del todo, sino aplazadas con una condición
> escrita. La diferencia importa: una decisión cerrada no se vuelve a mirar; una aplazada hay que
> mirarla el día que su condición se cumpla. Las tareas T6-08, T6-21 y T6-22 dependen de la misma
> condición —volver a desplegar— y están anotadas igual.
