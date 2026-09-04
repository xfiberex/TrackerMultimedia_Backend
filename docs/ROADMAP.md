# ROADMAP — TrackerMultimedia

> Qué falta por hacer. Plan ejecutable derivado de la auditoría del **2026-08-27**.
> El *qué cambió* está en [CHANGELOG.md](CHANGELOG.md); el *por qué* en [CONTEXT.md](CONTEXT.md).

## Índice

| Tier | Nombre | Tareas | Cerradas | Esfuerzo restante |
|------|--------|--------|----------|-------------------|
| 0 | Crítico / Bloqueante | 6 | 5 | — · T0-05 en suspenso |
| 1 | Alta prioridad | 21 | 20 | — · solo T1-05, reclasificada Bajo |
| 2 | Mejoras sustanciales | 26 | 26 | — |
| 3 | Pulido y mantenimiento | 23 | 23 | — |
| 4 | Futuro / Opcional | 10 | 5 | sin estimar |
| **Total** | | **88** | **79** | |

**Estado (2026-09-04):** 79 tareas cerradas y verificadas. **Cerrados los Tiers 0, 2 y 3; del Tier 1 solo queda T1-05, reclasificada Bajo.** Las
dos suites están en verde —**165/165** backend, **contra PostgreSQL real**, y **165/165** frontend—, con `npm run lint`,
`tsc -b` y `npm run build` limpios.

`npm audit` vuelve a dar **0 vulnerabilidades** tras cerrar T3-21. La frase se había
retirado el mismo día al aparecer 13 avisos —1 crítico y 10 altos— publicados después de la
auditoría del 2026-08-27; se restituye ahora con la fecha en la que se comprobó, porque es
una afirmación que caduca.

### El proyecto pasa a ser de uso local

Decisión del propietario del 2026-08-27: **Render, Neon y Netlify quedan deshabilitados** y
sus credenciales revocadas. La aplicación se usa a través del servidor de desarrollo de Vite,
contra el PostgreSQL 17 instalado en la máquina. Los repositorios siguen en GitHub.

Esto no cierra hallazgos por sí solo: **cambia cuáles están activos**. Lo que dependía de
haber un servicio público queda en suspenso, no resuelto, y **vuelve en el momento en que se
vuelva a desplegar**:

| Tarea | Antes | Ahora |
|---|---|---|
| **T0-05** Política de privacidad y aviso legal | Crítico | En suspenso: sin servicio público no hay tratamiento de datos de terceros |
| **T1-14** Borrado de cuenta (GDPR art. 17) | Alto | Bajo: deja de ser deber legal, sigue siendo carencia funcional |
| **T1-05** `X-Forwarded-For` de cualquier proxy | Alto | Bajo: el escenario era el proxy de Render. **El código no ha cambiado** |

Sigue vigente sin cambios todo lo que no dependía del despliegue, en particular **T1-15**
(licencia) y **T1-20** (229 binarios en el índice de git), porque los repositorios siguen
publicados en GitHub.

---

## Tier 0 — Crítico / Bloqueante

- [x] **[T0-01] Restaurar la aplicación de migraciones en el arranque**
  - **Área:** DevOps · **Severidad:** Crítico
  - **Ubicación:** `TrackerMultimedia_Backend/Program.cs:235`
  - **Qué hacer:** la línea `dbContext.Database.Migrate();` está comentada dentro del bloque `if (shouldApplyMigrations)`. Descomentarla, envolverla en `try/catch` con log explícito del fallo, y decidir si en producción se prefiere migración en el arranque o un paso previo al despliegue. Si se mantiene en el arranque, documentar el riesgo con varias instancias.
  - **Criterio de aceptación:** desplegar sobre una base de datos vacía crea el esquema completo; los logs de arranque muestran las migraciones aplicadas; `Database__ApplyMigrationsOnStartup=false` las omite.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [x] **[T0-02] Poner la suite de tests bajo control de versiones**
  - **Área:** Arquitectura / DevOps · **Severidad:** Crítico
  - **Ubicación:** `TrackerMultimedia_Backend/TrackerMultimedia.Tests/` (24 archivos, 105 tests), `TrackerMultimedia_Backend/TrackerMultimedia_Backend.slnx:3`
  - **Resuelto (2026-08-27):** el proyecto se movió dentro del repositorio de backend y el `.slnx` apunta ya a una ruta interna. Como el `.csproj` del backend está en la raíz del repositorio, sus globs por defecto se tragaban los archivos de test (`error CS0246` en masa): se añadió `DefaultItemExcludes` en `TrackerMultimedia_Backend.csproj:12` para excluir la carpeta, y la misma exclusión en el `.dockerignore`. Verificado: `dotnet test TrackerMultimedia_Backend.slnx` da 105/105, `dotnet publish -c Release` no arrastra nada de los tests, y ninguno de los 24 archivos fuente cae bajo una regla de `.gitignore`.
  - **Qué hacer:** la carpeta de tests, el `README.md`, `netlify.toml` y `render.yaml` de la raíz no pertenecen a ningún repositorio git. Decidir la estrategia (ver T0-03) y mover los tests dentro del repositorio que corresponda; corregir la referencia `../TrackerMultimedia.Tests/...` del `.slnx`, que hoy apunta fuera del repo y deja la solución sin abrir tras un clon limpio.
  - **Criterio de aceptación:** `git clone` del repositorio de backend en una carpeta nueva permite abrir la solución y ejecutar `dotnet test` sin copiar archivos a mano.
  - **Esfuerzo:** medio · **Depende de:** T0-03

- [x] **[T0-03] Alinear los blueprints de despliegue con la estructura real de repositorios**
  - **Área:** DevOps · **Severidad:** Crítico
  - **Ubicación:** `TrackerMultimedia_Backend/render.yaml`, `TrackerMultimedia_Frontend/netlify.toml`
  - **Resuelto (2026-08-27):** elegida la salida **(b)**, dos repositorios independientes. Cada blueprint vive ahora en la raíz de su repositorio con rutas relativas a ella: `render.yaml` pasa a `dockerfilePath: ./Dockerfile` y `dockerContext: .`, y `netlify.toml` pierde el `base` que apuntaba a una carpeta inexistente. La documentación de despliegue, que solo existía en el README sin versionar de la carpeta contenedora, se repartió entre el `README.md` de cada repositorio. **Pendiente de verificación:** que los servicios de Render y Netlify estén hoy configurados por blueprint y no a mano en el panel; eso solo se comprueba entrando en cada panel.
  - **Qué hacer:** `render.yaml` declara `dockerfilePath: TrackerMultimedia_Backend/Dockerfile` y `netlify.toml` declara `base = "TrackerMultimedia_Frontend"`, rutas que solo existen en un monorepo. Los repositorios reales son dos independientes (`xfiberex/TrackerMultimedia_Backend` y `xfiberex/TrackerMultimedia_Frontend`), donde esos prefijos no existen. Elegir una de las dos salidas: **(a)** unificar en un monorepo real y versionar los blueprints en él, o **(b)** mover cada blueprint a su repositorio con rutas relativas a la raíz de ese repositorio. Verificar además cómo están configurados hoy los servicios en los paneles de Render y Netlify.
  - **Criterio de aceptación:** el despliegue se reproduce desde el repositorio versionado sin configuración manual en el panel; un `render.yaml`/`netlify.toml` recién clonado describe el despliegue vigente.
  - **Esfuerzo:** medio · **Depende de:** ninguna

- [x] **[T0-04] Actualizar las dependencias con vulnerabilidad conocida de severidad alta**
  - **Área:** Seguridad · **Severidad:** Crítico
  - **Ubicación:** `TrackerMultimedia_Backend/TrackerMultimedia_Backend.csproj:19`, `TrackerMultimedia.Tests/TrackerMultimedia.Tests.csproj:12`
  - **Qué hacer:** `dotnet restore` reporta `NU1903` para `Microsoft.OpenApi` 2.0.0 (GHSA-v5pm-xwqc-g5wc, llega como transitiva de `Microsoft.AspNetCore.OpenApi`) y `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 (GHSA-2m69-gcr7-jv3q, solo en tests). Subir las versiones o fijar la transitiva con una `PackageReference` explícita.
  - **Criterio de aceptación:** `dotnet restore` no emite ningún `NU1903`; el resultado de `npm audit` del frontend queda registrado en el mismo commit.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [x] **[T0-06] Reparar la migración que EF Core nunca ejecutaba**
  - **Área:** Datos · **Severidad:** Crítico
  - **Ubicación:** `TrackerMultimedia_Backend/Migrations/20260519030000_AddUserFormatIdToMediaItems.cs` (eliminada)
  - **Qué pasaba:** hallazgo **nuevo**, aparecido al crear por primera vez una base de datos vacía. La migración que añade `MediaItems.UserFormatId` estaba escrita a mano y **no tenía archivo `.Designer.cs`**, que es donde vive el atributo `[Migration("...")]`. EF Core identifica las migraciones por ese atributo, no por el nombre del archivo: la clase se compilaba, se veía en la carpeta y **no existía** para EF. `dotnet ef migrations list` solo mostraba tres migraciones de las cuatro presentes. Peor aún, el *snapshot* del modelo sí incluía `UserFormatId`, así que EF daba el modelo por representado y **tampoco iba a generarla nunca**: el arranque informaba «Esquema de base de datos al día: no hay migraciones pendientes» sobre una base a la que le faltaba la columna.
  - **Impacto verificado:** sobre una base creada desde las migraciones, `GET /api/media-items` devuelve **500** con `Npgsql.PostgresException 42703: column m.UserFormatId does not exist`. Es decir, **toda la biblioteca —la funcionalidad central del producto— está rota en cualquier despliegue nuevo**. Ningún test lo detectaba porque la suite usa `EnsureCreated()` sobre SQLite, que construye el esquema desde el modelo y se salta las migraciones (T2-03).
  - **Resuelto (2026-08-27):** eliminada la migración huérfana y regeneradas con `dotnet ef migrations add` en una sola migración correcta, `20260827232632_AddUserFormatIdFixLoginsFkAndIndexes`, que añade `UserFormatId` con su índice y su FK, elimina la FK sombra de T1-01 y crea los índices de T1-06 y T1-07. Verificado sobre PostgreSQL 17 en Docker desde una base vacía: las tres migraciones aplican, la columna existe, `AspNetUserLogins` tiene una sola FK, y el ciclo completo de alta y lectura de un elemento con formato devuelve 201 y 200.
  - **⚠️ Antes de desplegar esto en producción:** comprobar el estado real del esquema en Neon. Si la columna `UserFormatId` ya existe allí pero `__EFMigrationsHistory` no registra ninguna migración que la cree, aplicar esta migración fallará con «la columna ya existe». En ese caso hay que insertar a mano la fila correspondiente en `__EFMigrationsHistory` en lugar de ejecutar el `Up()`. **Pendiente de verificación:** no se ha inspeccionado la base de producción.
  - **Esfuerzo:** medio · **Depende de:** ninguna

- [~] **[T0-05] ~~Publicar política de privacidad y aviso legal~~ — EN SUSPENSO (deja de aplicar mientras el uso sea local)**
  - **Área:** Legal · **Severidad:** Crítico → *no aplica hoy* · *Requiere revisión legal*
  - **En suspenso desde el 2026-08-27:** la aplicación dejó de estar publicada. Sin servicio accesible a terceros no hay tratamiento de datos personales de otras personas, y la obligación que motivaba esta tarea decae. **No se ha borrado ni resuelto: se reactiva íntegra en el momento en que la aplicación vuelva a estar accesible para alguien que no seas tú**, aunque sea a un grupo reducido. Servirla con `vite --host` a otros dispositivos de tu red no cambia nada mientras las cuentas sigan siendo tuyas; darle acceso a otras personas, sí.
  - **No es asesoramiento jurídico.** Que el uso sea personal y local es la razón por la que hoy no aplica, pero dónde está exactamente la frontera —qué cuenta como «doméstico» a efectos del GDPR, y qué obligaciones subsisten— **requiere revisión legal** si alguna vez deja de ser evidente.
  - **Ubicación:** no existe (`TrackerMultimedia_Frontend/src/router.tsx`, `src/layouts/AppLayout.tsx`)
  - **Qué hacer:** la aplicación es pública, tiene cuentas de usuario y trata datos personales (email, nombre visible, IP en el rate limiting, identificadores y email de Google/GitHub). Bajo GDPR necesita: identificación del responsable y contacto, categorías de datos, base legal, finalidad, plazos de retención, destinatarios (Neon, Render en Virginia, Netlify, Google, GitHub, el proveedor SMTP), transferencias internacionales y derechos del interesado. Crear las vistas `/privacidad` y `/aviso-legal` y enlazarlas desde el pie del layout y desde el registro.
  - **Criterio de aceptación:** ambas páginas son accesibles sin iniciar sesión y están enlazadas desde el formulario de registro y desde el layout autenticado; un asesor legal ha revisado el texto.
  - **Esfuerzo:** medio · **Depende de:** ninguna

---

## Tier 1 — Alta prioridad

- [x] **[T1-01] Corregir la doble clave foránea en `AspNetUserLogins`**
  - **Área:** Arquitectura / Código · **Severidad:** Alto
  - **Ubicación:** `TrackerMultimedia_Backend/Domain/Entities/ApplicationUser.cs:20`, `Data/ApplicationDbContext.cs`, `Migrations/20260519010320_InitialCreate.cs:127`
  - **Qué hacer:** la navegación `ExternalLogins` no está configurada en `OnModelCreating`, así que EF crea una relación adicional con una FK sombra `ApplicationUserId` **junto a** la `UserId` que configura `IdentityDbContext`. `UserManager.AddLoginAsync` solo rellena `UserId`, por lo que `ApplicationUserId` queda a NULL y la navegación nunca devuelve nada. Configurar explícitamente `modelBuilder.Entity<IdentityUserLogin<Guid>>().HasOne<ApplicationUser>().WithMany(u => u.ExternalLogins).HasForeignKey(l => l.UserId)` y añadir una migración que elimine la columna y la FK sobrantes.
  - **Criterio de aceptación:** `AspNetUserLogins` tiene una sola FK; un usuario con Google vinculado recibe `linkedProviders: ["google"]` en `GET /api/auth/me`.
  - **Esfuerzo:** medio · **Depende de:** T0-01

- [x] **[T1-02] Devolver realmente los proveedores vinculados en `/api/auth/me`**
  - **Área:** Código · **Severidad:** Alto
  - **Ubicación:** `TrackerMultimedia_Backend/Controllers/AuthController.cs:208`
  - **Qué hacer:** `await userManager.GetLoginsAsync(user);` descarta su resultado y no puebla la navegación (el store proyecta a `UserLoginInfo`, no materializa entidades rastreadas). Sustituir por una carga explícita desde el `DbContext` o construir `UserResponse` a partir del valor devuelto por `GetLoginsAsync`. Aplicar lo mismo en `AuthSessionService.CreateSessionAsync`, que sufre el mismo vacío.
  - **Criterio de aceptación:** un test de integración crea un usuario con login externo y comprueba que `linkedProviders` no está vacío en `/me` y en la respuesta de login.
  - **Esfuerzo:** bajo · **Depende de:** T1-01

- [x] **[T1-03] No romper el registro cuando falla el envío de correo**
  - **Área:** Código · **Severidad:** Alto
  - **Ubicación:** `TrackerMultimedia_Backend/Controllers/AuthController.cs:63`, `:290`, `Services/SmtpEmailService.cs:44-47`
  - **Qué hacer:** `SmtpEmailService.SendAsync` propaga cualquier excepción de MailKit. En `Register` el usuario ya se ha creado cuando se envía el correo, así que un fallo SMTP devuelve 500 y deja una cuenta creada, sin confirmar y sin explicación para el usuario. Capturar el fallo, registrarlo y devolver 201 con un aviso, u ofrecer el reenvío. Igual en `ForgotPassword`.
  - **Criterio de aceptación:** con SMTP caído, `POST /api/auth/register` devuelve 201 y el log registra el fallo de envío; existe un test que lo cubre.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [x] **[T1-04] Eliminar la enumeración de cuentas en registro y login**
  - **Área:** Seguridad · **Severidad:** Alto
  - **Ubicación:** `TrackerMultimedia_Backend/Controllers/AuthController.cs:53-60`, `:88-99`
  - **Qué hacer:** el comentario de la línea 80 afirma que no se distingue entre usuario inexistente y contraseña incorrecta, pero tres caminos sí lo revelan: `Register` devuelve el código `DuplicateEmail` de Identity, y `Login` responde "Cuenta bloqueada temporalmente" y "Debes confirmar tu dirección de correo" solo cuando el email existe. Unificar la respuesta de login y, en registro, responder siempre 201 enviando un correo distinto ("ya tienes cuenta") si el email ya existe.
  - **Criterio de aceptación:** registrar un email existente y uno nuevo devuelve respuestas indistinguibles en cuerpo, código y tiempo aproximado; login con cuenta bloqueada, no confirmada e inexistente devuelve el mismo 401.
  - **Esfuerzo:** medio · **Depende de:** ninguna

- [ ] **[T1-05] Dejar de confiar en cualquier proxy para `X-Forwarded-For`**
  - **Área:** Seguridad · **Severidad:** Alto → **Bajo** mientras el uso sea local
  - **Reclasificada el 2026-08-27:** el escenario de ataque era Internet a través del proxy de Render, que ya no existe. En local el backend solo escucha en `localhost` y solo lo alcanza el proxy de Vite. **El defecto sigue en el código sin cambios**, así que vuelve a ser Alto en cuanto haya despliegue: es de los primeros que hay que resolver antes de volver a publicar.
  - **Ubicación:** `TrackerMultimedia_Backend/Program.cs:53-54`
  - **Qué hacer:** `KnownIPNetworks.Clear()` y `KnownProxies.Clear()` hacen que ASP.NET Core acepte la cabecera `X-Forwarded-For` de cualquier origen. Como el rate limiter particiona por `RemoteIpAddress` (`Program.cs:72`, `:84`) después de `UseForwardedHeaders`, un atacante puede rotar la cabecera y saltarse por completo el límite de 10 peticiones/minuto que protege login y registro contra fuerza bruta. Restringir a las redes de Render o, si se mantiene el `Clear()`, documentar el riesgo y añadir una partición secundaria por email en los endpoints de autenticación.
  - **Criterio de aceptación:** enviar 50 peticiones a `/api/auth/login` con una `X-Forwarded-For` distinta en cada una acaba devolviendo 429.
  - **Esfuerzo:** medio · **Depende de:** ninguna

- [x] **[T1-06] Crear el índice de `RefreshTokens.TokenHash`**
  - **Área:** Rendimiento · **Severidad:** Alto
  - **Ubicación:** `TrackerMultimedia_Backend/Data/ApplicationDbContext.cs`, `Migrations/20260519010320_InitialCreate.cs:384`
  - **Qué hacer:** la tabla solo tiene `IX_RefreshTokens_UserId`. `Refresh` y `Logout` buscan por `TokenHash` (`AuthController.cs:131`, `:160`), lo que provoca un recorrido secuencial completo de la tabla en cada renovación de sesión y en cada cierre. Añadir `HasIndex(t => t.TokenHash).IsUnique()` y su migración.
  - **Criterio de aceptación:** el `EXPLAIN` de la consulta de refresh usa un índice; la migración está aplicada en producción.
  - **Esfuerzo:** bajo · **Depende de:** T0-01

- [x] **[T1-07] Crear un índice utilizable sobre `MediaItems.UserId`**
  - **Área:** Rendimiento · **Severidad:** Alto
  - **Ubicación:** `TrackerMultimedia_Backend/Data/ApplicationDbContext.cs:77-80`
  - **Qué hacer:** el único índice que empieza por `UserId` es parcial (`filter: "ExternalId" IS NOT NULL AND "ExternalMediaKind" IS NOT NULL`), y PostgreSQL no lo usa para el `WHERE "UserId" = @p` que ejecutan todas las consultas de biblioteca, estadísticas, exportación e importación. Añadir un índice no filtrado sobre `(UserId, CreatedAtUtc)` que cubra también la ordenación por defecto.
  - **Criterio de aceptación:** `EXPLAIN ANALYZE` de `GET /api/media-items` muestra un *index scan* en lugar de un *seq scan*.
  - **Esfuerzo:** bajo · **Depende de:** T0-01

- [x] **[T1-08] Poner en verde la suite del frontend**
  - **Área:** QA · **Severidad:** Alto
  - **Ubicación:** `TrackerMultimedia_Frontend/src/layouts/AppLayout.test.tsx`, `src/features/media-items/views/LibraryView.test.tsx`, `src/features/categories/views/CategoriesView.test.tsx`, `src/features/search/views/DiscoverView.test.tsx`, `src/features/search/components/SearchResultCard.test.tsx`
  - **Qué hacer:** `npm run test` da 17 fallos de 124 repartidos en 5 archivos. Las causas verificadas están desglosadas en T1-09, T1-10, T2-01 y T1-17. Esta tarea es el cierre: comprobar que no queda ningún fallo tras aplicarlas.
  - **Criterio de aceptación:** `npm run test` termina con 0 fallos.
  - **Esfuerzo:** bajo · **Depende de:** T1-09, T1-10, T1-17, T2-01

- [x] **[T1-09] Simular `matchMedia` en el arranque de los tests del frontend**
  - **Área:** QA · **Severidad:** Alto
  - **Ubicación:** `TrackerMultimedia_Frontend/src/test/setup.ts:1-7`
  - **Qué hacer:** jsdom no implementa `window.matchMedia`, y `useDarkMode` lo llama sin protección (`src/shared/hooks/useDarkMode.ts:14`). Eso tumba los 2 tests de `AppLayout` con `TypeError: window.matchMedia is not a function`. Añadir el stub en `setup.ts` y, en el hook, una comprobación defensiva.
  - **Criterio de aceptación:** `AppLayout.test.tsx` pasa sus 2 tests sin tocar las aserciones.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [x] **[T1-10] Actualizar los tests de `LibraryView` a la interfaz vigente**
  - **Área:** QA · **Severidad:** Alto
  - **Ubicación:** `TrackerMultimedia_Frontend/src/features/media-items/views/LibraryView.test.tsx:195`, `:205`
  - **Qué hacer:** 8 de los 11 tests del archivo asumen una interfaz anterior: filtros siempre visibles, cuando hoy `LibraryFilters` se renderiza tras `isFiltersOpen` (`LibraryView.tsx:529`), y una llamada a `MediaItemsApi.getStats` que la vista ya no hace. Abrir el panel de filtros dentro del test antes de buscar sus controles y resolver la aserción de `getStats` según lo que se decida en T2-08.
  - **Criterio de aceptación:** los 11 tests pasan y cubren el flujo real de apertura de filtros.
  - **Esfuerzo:** medio · **Depende de:** T2-08

- [x] **[T1-11] Corregir la aserción errónea del test de Jikan**
  - **Área:** QA · **Severidad:** Alto
  - **Ubicación:** `TrackerMultimedia.Tests/Services/JikanSearchServiceTests.cs:113`, `:114`
  - **Qué hacer:** el único test del backend en rojo espera `"Activo"` para un elemento cuyo mock devuelve `"status": "Finished Airing"` (`:43`), que el código mapea correctamente a `"Finalizado"`. La aserción está mal, no el código. La línea 114 tiene el mismo problema: el mock envía `"Hiatus"` y `MapExternalStatus` solo reconoce `"On Hiatus"`, que es el valor real de la API de Jikan. Corregir ambas aserciones y alinear el mock con los valores reales de Jikan.
  - **Criterio de aceptación:** `dotnet test` termina con 102 pruebas superadas y 0 fallos.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [~] **[T1-12] ~~Montar integración continua en ambos repositorios~~ — ANULADA**
  - **Área:** DevOps · **Severidad:** Alto
  - **Anulada el 2026-08-27** por decisión del propietario: no habrá CI, ni workflows de GitHub, ni comprobaciones de fusión. El proyecto lo desarrolla una sola persona y la verificación se hace en local. Ver *Decisiones cerradas*. El identificador no se reutiliza.
  - **El riesgo no desaparece con la tarea.** Lo que la CI iba a cubrir es exactamente lo que ya había pasado: 18 pruebas llevaban tiempo en rojo sin que nadie se enterara. Sin comprobación automática, esa red pasa a depender de ejecutar la verificación local **antes de cada commit**, no cuando uno se acuerda. La rutina está en `CONTEXT.md`, sección *Tareas comunes*.

- [x] **[T1-13] Rotar la credencial de Neon y sacarla del disco en claro**
  - **Área:** Seguridad · **Severidad:** Alto
  - **Ubicación:** `TrackerMultimedia_Backend/appsettings.Local.json:4`
  - **Qué hacer:** el archivo contiene una cadena de conexión de Neon con usuario y contraseña en texto plano. Está en `.gitignore` y en `.dockerignore`, y se ha verificado que **nunca** entró en el historial de git, así que la exposición se limita al disco local. Aun así, el proyecto ya tiene `UserSecretsId` configurado y su propio README manda usar `dotnet user-secrets`. Rotar la contraseña en Neon, mover el valor a user-secrets y borrar la clave del archivo.
  - **Avance (2026-08-27):** hecha la parte que no depende de terceros. `appsettings.Local.json` ya no contiene ningún secreto y las ocho claves están en `dotnet user-secrets`. Se **eliminó** además `ConnectionStrings:DefaultConnectionPro` —la cadena de producción de Neon, que ningún código leía y solo servía para tener la credencial de producción en el portátil— y `Security:ApiKey`, igualmente muerta. `Jwt:Secret` se regeneró de cero. *(La base local pasó brevemente a Docker el mismo día; revertido a la instalación nativa a petición del propietario.)*
  - **Lo que falta, y solo puedes hacerlo tú:** rotar en cada panel la contraseña de Neon, los *client secret* de Google y GitHub, y la contraseña de Mailtrap. Los valores actuales siguen siendo válidos: mientras no se revoquen, la rotación no ha ocurrido. Comandos y enlaces en la sección *Rotar credenciales* del README del backend.
  - **Cerrada (2026-08-27):** rotadas todas las credenciales de proveedor —verificado por hash contra los valores anteriores en el caso de Google y GitHub— y, además, **deshabilitados los servicios de Neon, Render y Netlify**. Las credenciales antiguas no solo se revocaron: ya no hay servicio al que pudieran dar acceso.
  - **Criterio de aceptación:** `appsettings.Local.json` no contiene credenciales ✅; la aplicación arranca en local leyendo de user-secrets ✅; la contraseña antigua ya no es válida en Neon ✅.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [x] **[T1-14] Implementar el borrado de cuenta**
  - **Área:** Legal · **Severidad:** Alto → **Bajo** mientras el uso sea local
  - **Reclasificada el 2026-08-27:** desaparecida la obligación del GDPR art. 17 al dejar de haber servicio público, esto pasa de deber legal a carencia funcional: sigue sin haber forma de borrar una cuenta y sus datos. Vuelve a ser Alto si la aplicación se publica.
  - **Ubicación:** `TrackerMultimedia_Backend/Controllers/AuthController.cs`, `TrackerMultimedia_Frontend/src/features/auth/views/ProfileView.tsx`
  - **Qué hacer:** no existe ninguna vía para eliminar la cuenta, lo que incumple el derecho de supresión del GDPR (art. 17). Añadir `DELETE /api/auth/account` con reautenticación por contraseña (o confirmación por correo en cuentas solo-OAuth), que borre el usuario y, por cascada, sus `MediaItems`, `UserCategories`, `UserFormats`, `RefreshTokens` y logins externos. Añadir el botón en el perfil con doble confirmación.
  - ****Resuelto (2026-09-02):** `DELETE /api/auth/account` con reautenticación aunque el JWT sea válido: es la única operación del sistema sin vuelta atrás y un token puede quedar abierto en un equipo prestado. Dos formas, porque hay dos clases de cuenta: con contraseña se comprueba **por el mismo camino que el login** —bloqueo, contar el fallo, reiniciar al acertar—, porque con `CheckPasswordAsync` a secas el endpoint sería un oráculo de contraseñas sin freno para quien tuviera un token robado; las cuentas de Google o GitHub escriben su propia dirección, que no prueba identidad pero sí que la acción es deliberada. **Lo que la cascada no cubría:** los `OAuthStates` no tienen `UserId` y su columna `PendingEmail` guarda la dirección, así que sin borrarlos a mano el correo sobrevivía a la cuenta hasta la purga del día siguiente. Todo dentro de una transacción. En el perfil, confirmación doble: primero se escribe la credencial y solo entonces se habilita el botón que abre el diálogo. `deleteAccount` limpia la sesión local **solo si el servidor confirma** —al revés que `logout`—, porque limpiarla igualmente dejaría a quien escribe mal la contraseña en el login creyendo que borró su cuenta. Ocho tests que cuentan filas en vez de fiarse del 204, y **comprobado contra PostgreSQL real**: cuenta con 1 elemento, 10 formatos y 1 refresh token; contraseña incorrecta → 400 y la cuenta sigue; correcta → 204 y **cero filas huérfanas** en las cuatro tablas.**
  - **Criterio de aceptación:** tras el borrado no queda ninguna fila del usuario en ninguna tabla; un test de integración lo verifica.
  - **Esfuerzo:** medio · **Depende de:** ninguna

- [x] **[T1-15] Declarar una licencia para el proyecto**
  - **Área:** Legal · **Severidad:** Alto
  - **Ubicación:** no existe `LICENSE` en ninguno de los dos repositorios
  - **Qué hacer:** ambos repositorios están publicados en GitHub sin archivo de licencia, lo que por defecto significa "todos los derechos reservados": nadie puede usar, copiar ni contribuir legalmente. Elegir licencia (MIT es lo habitual para un proyecto de portafolio), añadir `LICENSE` a cada repositorio y declararla en los README.
  - ****Resuelto (2026-09-02):** **MIT**, elegida por el propietario. `LICENSE` en los dos repositorios y declarada en ambos README, con las dos consecuencias prácticas escritas —uso libre conservando el aviso, y sin garantía de ningún tipo— y la nota de que el aviso nombra la identidad de git (`xfiberex`) y conviene sustituirla por el nombre legal si la autoría tiene que poder acreditarse. **No es asesoramiento jurídico:** si el proyecto llega a tener valor comercial o colaboradores, requiere revisión legal.**
  - **Criterio de aceptación:** GitHub muestra la licencia detectada en ambos repositorios.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [x] **[T1-16] Añadir gestión de foco a los diálogos modales**
  - **Área:** Accesibilidad · **Severidad:** Alto · **WCAG 2.4.3, 2.1.2**
  - **Ubicación:** `TrackerMultimedia_Frontend/src/shared/components/SidePanelDialog.tsx:97-104`, `src/shared/components/ConfirmDialog.tsx:97-99`
  - **Qué hacer:** ambos componentes declaran `role="dialog"`/`alertdialog` con `aria-modal="true"` pero no mueven el foco al abrirse, no lo devuelven al cerrarse y no atrapan el tabulador (no hay ni una llamada a `.focus()` en toda la aplicación). Un usuario de teclado abre el panel y sigue tabulando por el contenido de fondo, que además los lectores de pantalla ya han ocultado por `aria-modal`. Implementar el patrón de diálogo modal de ARIA APG en un hook compartido: foco inicial, ciclo de tabulación acotado y restauración del foco previo.
  - **Resuelto (2026-08-27):** hook compartido `src/shared/hooks/useModalDialog.ts` con el patrón de ARIA APG —foco inicial, tabulador acotado, devolución del foco y Escape—, aplicado a `SidePanelDialog` y `ConfirmDialog`. Diez tests nuevos lo cubren.
  - **Tres defectos que aparecieron al implementarlo, ninguno previsto en el hallazgo original:**
    1. **El fondo era el único control de cierre de dos paneles.** Era un `<button>` con `aria-label` colocado *fuera* del diálogo: el teclado llegaba a él mientras los lectores de pantalla lo ocultaban por `aria-modal`. Pasa a ser un `<div aria-hidden>`; los cuatro paneles ya tenían su botón «Cancelar» dentro, y Escape sigue funcionando.
    2. **Los diálogos se desmontaban y volvían a montarse al cerrarse.** `isClosing` se activaba en un efecto, así que había un render intermedio con `open` a false e `isClosing` aún a false donde `isRendered` daba false. Además de tirar el foco al `<body>`, reiniciaba el formulario durante la animación de salida. Ahora el cambio de `open` se procesa en el render, con estado y no con una ref (`react-hooks/refs` prohíbe leerlas ahí).
    3. **`autoFocus` ganaba la carrera al hook.** React lo aplica durante el commit, antes de los efectos, y cuatro formularios autoenfocan su primer campo. El hook guardaba como «foco anterior» un campo de dentro del propio diálogo, que al cerrarse ya no existía. Se resuelve con un seguimiento del último foco fuera de cualquier diálogo, instalado al importar el módulo. Cubierto por un test específico.
  - **Y una decisión de producto:** en los diálogos destructivos el foco inicial va a «Cancelar», no a la acción de confirmar, que es donde estaba el `autoFocus`. Quien pulsa Intro por inercia no debería borrar nada.
  - **Criterio de aceptación:** con el panel abierto, `Tab` y `Shift+Tab` recorren solo los controles del diálogo ✅; al cerrar, el foco vuelve al botón que lo abrió ✅; verificado con teclado ✅ y con un lector de pantalla ⬜ *(pendiente de verificación: no se ha probado con NVDA ni con VoiceOver; lo comprobado son las relaciones de foco y los roles ARIA)*.
  - **Esfuerzo:** medio · **Depende de:** ninguna

- [x] **[T1-17] Asociar las etiquetas huérfanas a sus controles**
  - **Área:** Accesibilidad · **Severidad:** Alto · **WCAG 1.3.1, 3.3.2**
  - **Ubicación:** `TrackerMultimedia_Frontend/src/features/categories/views/CategoriesView.tsx:374`, `src/features/catalog/views/CatalogView.tsx:385`, `src/features/media-items/components/LibraryFilters.tsx:170`, `src/features/media-items/components/MediaItemEditorForm.tsx:243`, `src/features/search/components/SearchQuickAddForm.tsx:150`, `src/features/search/views/DiscoverView.tsx:293`
  - **Qué hacer:** siete `<label>` no tienen `htmlFor` ni envuelven ningún control ("Color" ×2, "Categorías" ×3, "Proveedores"). Para un lector de pantalla son texto suelto: el grupo de casillas de categorías no tiene nombre accesible. Como etiquetan grupos, la solución correcta es `<fieldset><legend>` o `role="group"` con `aria-labelledby`. Los tests de `CategoriesView` (`:60`, `:84`) ya lo señalan en rojo con "no form control was found associated to that label".
  - **Criterio de aceptación:** ningún `<label>` queda sin control o grupo asociado; los tests de `CategoriesView` pasan sin modificar sus aserciones.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [x] **[T1-18] Añadir un enlace para saltar al contenido**
  - **Área:** Accesibilidad · **Severidad:** Alto · **WCAG 2.4.1 (nivel A)**
  - **Ubicación:** `TrackerMultimedia_Frontend/src/layouts/AppLayout.tsx:37-98`
  - **Qué hacer:** no existe *skip link*. Cada página obliga a tabular por la marca, el enlace de perfil, el conmutador de tema, el botón de salir y los tres enlaces de navegación antes de llegar al contenido. Añadir un enlace visible al recibir foco que apunte a `<main id="contenido" tabIndex={-1}>`.
  - **Criterio de aceptación:** el primer `Tab` tras cargar muestra "Saltar al contenido" y activarlo mueve el foco a `<main>`.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [x] **[T1-19] Reparar el onboarding documentado en los README**
  - **Área:** Documentación · **Severidad:** Alto
  - **Ubicación:** `TrackerMultimedia_Backend/.gitignore:8`, `TrackerMultimedia_Backend/README.md`, `TrackerMultimedia_Frontend/README.md`
  - **Avance parcial (2026-08-27):** al cerrar T0-02 y T0-03 se eliminó la afirmación falsa de los tres README (el de la raíz se reescribió por completo y el del backend ya no lista el archivo en su árbol). Lo sustancial sigue pendiente: **el archivo no existe y sigue en el `.gitignore`**, así que un clon limpio no arranca sin configurar antes `Cors:AllowedOrigins`, que `Program.cs:36-37` exige lanzando `InvalidOperationException`.
  - **Qué hacer:** los tres README afirman que `appsettings.json` contiene la configuración no sensible ya preparada. El archivo **no existe** y además está listado en `.gitignore:8`. Un desarrollador nuevo que siga el README no arranca: `Cors:AllowedOrigins` es obligatorio y su ausencia lanza una excepción en `Program.cs:37`. Crear un `appsettings.json` versionado con los valores no sensibles y quitarlo del `.gitignore`, o reescribir los README para reflejar que todo se configura por user-secrets y variables de entorno.
  - **Resuelto (2026-08-27):** creado `appsettings.json` versionado con los valores no sensibles y retirada la línea del `.gitignore`. Solo quedan dos valores obligatorios, y están documentados: la cadena de conexión y `Jwt:Secret`, ambos en user-secrets.
  - **Verificado, no supuesto:** se exportaron los archivos versionados a una carpeta vacía y se arrancó en `Production` aportando únicamente esos dos por variable de entorno. `/health` respondió `Healthy` sin ningún error de configuración.
  - **Criterio de aceptación:** seguir el README desde un clon limpio arranca el backend sin pasos no documentados ✅.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [x] **[T1-20] Eliminar del repositorio los binarios y artefactos versionados**
  - **Área:** Refactorización / DevOps · **Severidad:** Alto
  - **Ubicación:** `TrackerMultimedia_Backend/artifacts/` (219 archivos), `TrackerMultimedia_Backend/.vs/`, `TrackerMultimedia_Backend/.codegraph/codegraph.db` (2,5 MB)
  - **Qué hacer:** 229 de los 352 archivos versionados del repositorio de backend son DLL, PDB, caché de Visual Studio y la base de datos de codegraph. `artifacts/verify-build-oauth/` contiene incluso una copia anidada de `artifacts/verify-build/`. La caché de Visual Studio incluye `DocumentLayout.json`, que expone rutas locales de la máquina. Añadir `artifacts/`, `.vs/` y `.codegraph/*.db` al `.gitignore` y sacarlos del índice con `git rm -r --cached`.
  - **Resuelto (2026-08-27):** `artifacts/` y `.vs/` añadidos al `.gitignore` y sacados del índice con `git rm -r --cached`, que los deja en el disco. De **378 archivos versionados a 151**, sin ningún `.dll`, `.pdb`, `.exe`, `.bin` ni `.vsidx`. Los 105 tests siguen pasando, lo que confirma que nada dependía de la salida publicada.
  - **Corrección al hallazgo original:** decía que `.codegraph/codegraph.db` (2,5 MB) estaba versionado y **no lo estaba**: `.codegraph/` trae su propio `.gitignore` que ya lo excluye. Lo que sobraba era `artifacts/` (219 archivos, 68 MB) y `.vs/` (8), no 229 incluyendo la base de codegraph.
  - **Criterio de aceptación:** `git ls-files` no devuelve ningún binario ✅; el repositorio queda por debajo de 150 archivos — **151**, un archivo por encima del umbral que se fijó a ojo en la auditoría.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [x] **[T1-21] Purgar tokens de refresco y estados OAuth caducados**
  - **Área:** Seguridad / Legal · **Severidad:** Alto
  - **Ubicación:** `TrackerMultimedia_Backend/Domain/Entities/RefreshToken.cs`, `Domain/Entities/OAuthState.cs`
  - **Qué hacer:** ninguna de las dos tablas se limpia nunca. Cada login añade una fila a `RefreshTokens` que sobrevive revocada y caducada para siempre, y cada intento de OAuth deja un `OAuthState`. Es crecimiento sin límite y, bajo GDPR, retención indefinida de datos vinculados a una persona. Añadir un `BackgroundService` que borre periódicamente lo caducado y documentar el plazo en la política de privacidad.
  - **Resuelto (2026-08-27):** `ExpiredDataCleanupService` ejecuta la purga al arrancar y cada `Cleanup:IntervalHours` (6 h). Borra tokens de refresco caducados hace más de 7 días y estados OAuth caducados hace más de 1 día; ambos plazos son configurables. La lógica vive en `ExpiredDataCleaner`, separada del temporizador, para poder ejercitarla sin esperar: dos tests cubren que se va lo caducado, que se queda lo vigente —incluidos los tokens revocados pero no caducados, a propósito— y que ejecutarla sin nada que borrar es inocuo. Un fallo se registra y se reintenta, no tumba la aplicación.
  - **Por qué los plazos son distintos:** un token de refresco caducado ya se rechaza al usarse, así que conservarlo no aporta nada; los 7 días son margen por si se añade detección de reutilización. `OAuthStates`, en cambio, guarda el email y el nombre del perfil externo y su vida útil real son diez minutos: es dato personal sin motivo para retenerlo.
  - **Dependencia resuelta de otra forma:** dependía de T0-05 para documentar el plazo en la política de privacidad. Con T0-05 en suspenso, el plazo queda documentado en el README del backend, que es donde puede consultarse hoy.
  - **Criterio de aceptación:** existe un proceso programado ✅; el plazo está documentado ✅.
  - **Esfuerzo:** medio · **Depende de:** ~~T0-05~~

- [x] **[T1-22] Dar nombre accesible al enlace externo de los resultados de búsqueda**
  - **Área:** Accesibilidad · **Severidad:** Alto · **WCAG 2.4.4, 4.1.2**
  - **Ubicación:** `TrackerMultimedia_Frontend/src/features/search/components/SearchResultCard.tsx:45`
  - **Qué hacer:** hallazgo **nuevo**, aparecido al actualizar los tests de T2-01. El enlace a la ficha externa contenía únicamente `ArrowTopRightOnSquareIcon`, que Heroicons emite con `aria-hidden="true"`: el enlace se quedaba sin ningún nombre accesible y un lector de pantalla lo anunciaba como «enlace» a secas. El test `SearchResultCard.test.tsx` lo señalaba en rojo con "Unable to find an accessible element with the role link and name /abrir/i" y se había interpretado como test desactualizado. Añadido `aria-label` con el título y el proveedor.
  - **Criterio de aceptación:** el enlace expone un nombre accesible del tipo «Abrir Naruto en Jikan»; cubierto por test. ✅
  - **Esfuerzo:** bajo · **Depende de:** ninguna

---

## Tier 2 — Mejoras sustanciales

- [x] **[T2-01] Alinear los tests de `CategoriesView`, `DiscoverView` y `SearchResultCard`**
  - **Área:** QA · **Ubicación:** `src/features/categories/views/CategoriesView.test.tsx:60,84`, `src/features/search/views/DiscoverView.test.tsx:157`, `src/features/search/components/SearchResultCard.test.tsx`
  - **Qué hacer:** los fallos restantes buscan un botón "Preparar importación", un enlace accesible con nombre `/abrir/i` y el texto "Sin título alternativo" que la interfaz actual ya no ofrece con esos nombres. Revisar caso por caso si lo desactualizado es el test o la interfaz.
  - **Criterio de aceptación:** los 3 archivos pasan en verde. · **Esfuerzo:** medio · **Depende de:** T1-17

- [x] **[T2-02] Ejecutar los tests del backend contra PostgreSQL**
  - **Área:** QA · **Ubicación:** `TrackerMultimedia.Tests/Helpers/AppFactory.cs:30`, `:115`
  - **Qué hacer:** la suite sustituye Npgsql por SQLite en memoria, así que nada específico del proveedor real se comprueba: `EF.Functions.ILike` (`MediaItemsService.cs:83`) es exclusivo de Npgsql, el índice único parcial usa sintaxis de PostgreSQL y las colaciones difieren. Migrar a Testcontainers o a una base de datos PostgreSQL de test.
  - ****Resuelto (2026-09-02):** cada clase de test recibe una base **PostgreSQL desechable**. La cadena sale de `TRACKERMULTIMEDIA_TEST_POSTGRES` o, si no está, de los mismos user-secrets del backend: ni una credencial más que rotar ni un archivo de tests con una contraseña dentro; de esa cadena solo se reutilizan servidor y credenciales. **El sondeo previo confirmó que no era teórico:** `GET /api/media-items?search=...` devolvía **500** sobre SQLite porque `EF.Functions.ILike` solo existe en Npgsql. **Un fallo grave por el camino:** quitar el bloque de `RemoveAll` del `AppFactory` pareció redundante y no lo era —`Program.cs` lee la cadena de `builder.Configuration` al componer los servicios, antes de que se apliquen las fuentes de la factoría de tests, así que la aplicación se quedaba con la de los user-secrets: **la base real**, donde una tanda de tests escribió 199 usuarios—. Verificado tras el arreglo: 199 antes y 199 después de una ejecución completa. **133/133**, estable en tres pasadas.**
  - **Criterio de aceptación:** la suite corre sobre PostgreSQL y cubre la búsqueda por texto. · **Esfuerzo:** alto · **Depende de:** ninguna

- [x] **[T2-03] Ejercitar las migraciones en los tests**
  - **Área:** QA · **Ubicación:** `TrackerMultimedia.Tests/Helpers/AppFactory.cs:63`
  - **Qué hacer:** `EnsureCreatedAsync()` construye el esquema desde el modelo y salta las migraciones por completo, que es la razón de que nadie detectara que `Migrate()` estaba comentado. Cambiar a `MigrateAsync()`.
  - ****Resuelto (2026-09-02):** la plantilla se construye con `Migrate()`, no con `EnsureCreated()`. Las migraciones se aplican **una vez por ejecución** sobre una base plantilla y cada clase la copia con `CREATE DATABASE ... TEMPLATE`, que tarda milisegundos: así se ejercitan de verdad sin repetirlas veinte veces. Una migración rota ya hace fallar la suite entera, que es exactamente lo que no ocurría cuando `Migrate()` estaba comentado y nadie se enteró (T0-01).**
  - **Criterio de aceptación:** una migración rota hace fallar la suite. · **Esfuerzo:** medio · **Depende de:** T2-02

- [x] **[T2-04] Cubrir con tests la búsqueda de la biblioteca**
  - **Área:** QA · **Ubicación:** `TrackerMultimedia_Backend/Services/MediaItemsService.cs:74-85`
  - **Qué hacer:** ningún test usa el parámetro `search`. El escapado de `%`, `_` y `\` y la insensibilidad a mayúsculas no están cubiertos en una funcionalidad visible del producto.
  - ****Resuelto (2026-09-02):** cinco tests, ya posibles tras T2-02: coincidencia parcial, insensibilidad a mayúsculas, título alternativo, aislamiento entre usuarios y el escapado de los tres comodines de `LIKE`. El de escapado lleva **un señuelo por comodín** —`50%` junto a `50 sin símbolo`, `Archivo_final` junto a `ArchivoXfinal`, y una ruta con barra invertida—, de modo que si el escapado se rompiera cada consulta traería dos resultados en vez de uno. Pasaron a la primera: el escapado estaba bien, simplemente no estaba comprobado.**
  - **Criterio de aceptación:** tests que cubren coincidencia parcial, mayúsculas y los tres caracteres escapados. · **Esfuerzo:** bajo · **Depende de:** T2-02

- [x] **[T2-05] Añadir un manejador global de excepciones con ProblemDetails**
  - **Área:** Código · **Ubicación:** `TrackerMultimedia_Backend/Program.cs:247-266`
  - **Qué hacer:** no hay `UseExceptionHandler`. Cualquier excepción no controlada sale como 500 sin cuerpo útil y sin identificador de correlación. Añadir el manejador, devolver `ProblemDetails` y registrar un `traceId`.
  - **Resuelto (2026-08-27):** `UseExceptionHandler` como primer middleware del pipeline, con `ProblemDetails` (RFC 9457) y `traceId`. El detalle de la excepción se queda en el log; al cliente le llega un texto fijo y el identificador. `UseStatusCodePages` da la misma forma a las respuestas sin cuerpo, como un 404 de ruta desconocida. Tres tests.
  - **Criterio de aceptación:** una excepción provocada devuelve `application/problem+json` con `traceId` y sin detalles internos. · **Esfuerzo:** bajo · **Depende de:** ninguna

- [x] **[T2-06] Capturar los fallos de red y de formato en los servicios OAuth**
  - **Área:** Código · **Ubicación:** `Services/GitHubAuthService.cs:55,67,116`, `Services/GoogleAuthService.cs:54`
  - **Qué hacer:** `OAuthController.cs:147` solo captura `InvalidOperationException`, pero `EnsureSuccessStatusCode()` lanza `HttpRequestException` y `json.GetProperty("access_token")` lanza `KeyNotFoundException` cuando GitHub responde 200 con `{"error":"bad_verification_code"}`, que es exactamente lo que ocurre con un código caducado. El usuario recibe un 500 en vez de volver al login con un mensaje.
  - ****Resuelto (2026-09-02):** el `catch` del callback pasa a cubrir `HttpRequestException`, `JsonException`, `KeyNotFoundException` y `TaskCanceledException` además de `InvalidOperationException`. Este endpoint es una redirección de navegador: cualquier fallo hablando con el proveedor tiene que acabar en `/login` con un código, no en una página de error. Se atacó además la causa concreta: `GitHubAuthService` comprueba ahora si la respuesta 200 trae `error` —lo que devuelve GitHub con un código caducado— y lo traduce a `InvalidOperationException` registrando el motivo; `GoogleAuthService` usa `TryGetProperty` en lugar de `GetProperty` para `access_token`. Dos tests: uno recorre los cuatro tipos de fallo contra el callback, otro reproduce el 200-con-error de GitHub. El texto del proveedor se queda en el log y no llega a la URL.**
  - **Criterio de aceptación:** un código caducado redirige a `/login?oauth_error=...`; tests que simulan ambos fallos. · **Esfuerzo:** bajo · **Depende de:** ninguna

- [x] **[T2-07] No reutilizar el `state` OAuth en el flujo de vinculación**
  - **Área:** Seguridad · **Ubicación:** `TrackerMultimedia_Backend/Controllers/OAuthController.cs:200`
  - **Qué hacer:** tras marcar el estado como consumido en `:133`, el caso de vinculación lo revive con `storedState.IsUsed = false` y le da 15 minutos más, contradiciendo el "solo se puede consumir una vez" que documenta el propio modelo (`OAuthState.cs:27`). Crear una fila nueva para la vinculación y dejar el `state` original consumido.
  - ****Resuelto (2026-08-27):** la vinculación pendiente pasa a una fila nueva de `OAuthStates` con su propio `StateValue`; el `state` original se queda consumido. El test que existía afirmaba justo el comportamiento incorrecto —que `IsUsed` volvía a `false`— y se actualizó.**
  - **Criterio de aceptación:** repetir el callback con el mismo `state` devuelve `state_mismatch` siempre. · **Esfuerzo:** bajo · **Depende de:** ninguna

- [x] **[T2-08] Decidir el futuro del panel de estadísticas**
  - **Área:** Refactorización · **Ubicación:** `src/features/media-items/components/MediaStatsPanel.tsx`, `src/features/media-items/api/MediaItemsAPI.ts:39`, `TrackerMultimedia_Backend/Services/MediaItemsService.cs:151-261`
  - **Qué hacer:** la funcionalidad está huérfana de punta a punta: `MediaStatsPanel` no se importa en ninguna vista, `MediaItemsApi.getStats` no se llama desde ningún sitio, `queryKeys.mediaItems.stats` no se usa, y en el backend siguen vivos `GET /api/media-items/stats`, `GetStatsAsync` (8 consultas, ~110 líneas) y seis contratos de respuesta. Reconectarlo a una vista o eliminarlo de ambos lados.
  - ****Resuelto (2026-09-02):** decisión del propietario: eliminarlo. Comprobado antes de borrar que seguía huérfano —`MediaStatsPanel` no se importa desde ninguna vista, `getStats` no se llama desde ningún sitio y `queryKeys.mediaItems.stats` tampoco—; los únicos usos vivos eran los propios tests y un mock vestigial en `LibraryView.test.tsx`. Fuera del backend: el endpoint, `GetStatsAsync` (~110 líneas), el ayudante `GetStatusCount`, los cinco contratos y sus tests. Fuera del frontend: el componente, su test, `MediaItemsApi.getStats`, cinco interfaces del esquema, la clave de consulta y **106 líneas de CSS** que quedaban sin dueño (`stats-grid`, `stat-card*`, `breakdown-*`, `panel--dense`), comprobando una por una que ninguna se usaba en otro sitio.**
  - **Criterio de aceptación:** o el panel se muestra en la aplicación, o no queda código de estadísticas en ninguno de los dos proyectos. · **Esfuerzo:** medio · **Depende de:** ninguna

- [x] **[T2-09] Sanear el CSV exportado contra inyección de fórmulas**
  - **Área:** Seguridad · **Ubicación:** `TrackerMultimedia_Backend/Services/MediaItemsService.cs:725-731`
  - **Qué hacer:** `EscapeCsvCell` entrecomilla `;`, `"`, CR y LF pero no neutraliza las celdas que empiezan por `=`, `+`, `-`, `@` o tabulador. Un título como `=cmd|'/c calc'!A1` se ejecuta al abrir el archivo en Excel. Prefijar esas celdas con un apóstrofo.
  - ****Resuelto (2026-08-27):** `EscapeCsvCell` antepone un apóstrofo a las celdas que empiezan por `=`, `+`, `-`, `@`, tabulador o retorno de carro. Cubierto por un test con cuatro cargas típicas, entre ellas `=cmd|'/c calc'!A1`.**
  - **Criterio de aceptación:** un elemento con título `=1+1` se exporta sin ejecutarse en Excel; test que lo cubre. · **Esfuerzo:** bajo · **Depende de:** ninguna

- [x] **[T2-10] Exigir email confirmado y cuenta no bloqueada en todos los caminos de acceso**
  - **Área:** Seguridad · **Ubicación:** `Controllers/OAuthController.cs:157-163`, `:237-245`
  - **Qué hacer:** `Login` bloquea a los usuarios sin email confirmado (`AuthController.cs:95`), pero el caso A de OAuth emite sesión sin comprobar el bloqueo y `LinkConfirm` la emite sin comprobar `EmailConfirmed`. La regla de negocio se puede rodear por dos caminos.
  - ****Resuelto (2026-08-27):** el callback de OAuth y `LinkConfirm` comprueban ahora bloqueo y correo confirmado, igual que el login con contraseña. Sin esto la cuenta quedaba cerrada por un camino y abierta por otro. El callback devuelve el código `account_unavailable`. Dos tests nuevos.**
  - **Criterio de aceptación:** los tres caminos aplican las mismas comprobaciones; tests que lo verifican. · **Esfuerzo:** bajo · **Depende de:** ninguna

- [x] **[T2-11] Dejar de propagar mensajes de excepción internos al frontend**
  - **Área:** Seguridad · **Ubicación:** `Controllers/OAuthController.cs:150`
  - **Qué hacer:** `BuildErrorRedirect(frontendBase, "profile_error", ex.Message)` mete el mensaje de la excepción en la barra de direcciones del usuario. Usar códigos de error y dejar el detalle solo en el log.
  - ****Resuelto (2026-08-27):** `BuildErrorRedirect` pierde el parámetro de mensaje libre; el detalle de la excepción se registra en el log y al usuario le llega solo un código. Un test comprueba que ni el mensaje ni el nombre del motor de base de datos aparecen en la redirección.**
  - **Criterio de aceptación:** ninguna redirección de error contiene texto de excepción. · **Esfuerzo:** bajo · **Depende de:** ninguna

- [x] **[T2-28] Aislar la conexión SQLite compartida en la suite del backend**
  - **Área:** QA · **Severidad:** Medio
  - **Ubicación:** `TrackerMultimedia_Backend/TrackerMultimedia.Tests/Helpers/AppFactory.cs:30`
  - **Hallazgo nuevo (2026-08-27)**, aparecido dos veces durante esta sesión. `AppFactory` comparte **una única `SqliteConnection`** entre todos los `DbContext` para que la base en memoria sobreviva entre scopes. El proveedor de SQLite de EF Core registra una colación en esa conexión cada vez que construye un `DbContext`, escribiendo en una colección que no es segura entre hilos. Cuando dos lo hacen a la vez, el fallo llega como `System.InvalidOperationException: Operations that change non-concurrent collections must have exclusive access` desde `SqliteConnection.CreateCollation`, **sin ninguna relación aparente con lo que se esté probando**. Ahí está el coste real: quien lo vea pensará que su cambio rompió algo.
  - **Lo comprobado:** es intermitente. La suite completa pasa de forma estable (117/117 en tres pasadas seguidas), y aparece sobre todo al ejecutar con `--filter`, que reparte el trabajo de otra forma. **Desactivar el paralelismo de xUnit no lo arregla** —probado con `xunit.runner.json` y con `[assembly: CollectionBehavior(DisableTestParallelization = true)]`—, lo que sitúa la concurrencia dentro del host de la aplicación, no en el ejecutor de tests.
  - **Qué hacer:** dar a cada `DbContext` su propia conexión sobre el mismo archivo compartido (`DataSource=file:memdb?mode=memory&cache=shared`) en lugar de reutilizar una única instancia, o serializar la construcción del `DbContext` en el factory.
  - ****Resuelto (2026-09-02):** resuelto **de rebote** al cerrar T2-02: desaparece la `SqliteConnection` compartida entre todos los `DbContext`, que era la causa. Comprobado en el escenario donde saltaba: `dotnet test --filter` sobre `OAuthControllerTests`, tres pasadas seguidas, 14/14 cada una.**
  - **Criterio de aceptación:** ejecutar cualquier test con `--filter` veinte veces seguidas no produce ningún fallo de colección concurrente.
  - **Esfuerzo:** medio · **Depende de:** ninguna

- [x] **[T2-27] Impedir que la página de login muestre texto arbitrario de la URL**
  - **Área:** Seguridad · **Severidad:** Alto
  - **Ubicación:** `TrackerMultimedia_Frontend/src/features/auth/utils/authErrors.ts:120-133`, `src/features/auth/views/LoginView.tsx:38-42`
  - **Hallazgo nuevo (2026-08-27)**, aparecido al corregir la mitad de servidor de T2-11. `getOAuthErrorMessage` aceptaba un segundo parámetro, `oauth_error_message`, leído de la barra de direcciones, y **si venía lo mostraba tal cual y con prioridad sobre el código de error**. Eso convertía la página de login en un lienzo para cualquiera capaz de que alguien pinche un enlace: `?oauth_error_message=Tu+cuenta+fue+suspendida,+llama+al+900123456` pintaba ese texto en el sitio legítimo, con su dominio y su candado. No hace falta comprometer nada; basta con enviar el enlace. La auditoría solo había señalado la mitad de servidor del problema.
  - **Resuelto:** la función recibe únicamente el código y lo traduce contra la tabla de códigos conocidos, con un mensaje genérico como respaldo. Dos tests: uno comprueba que el texto libre se ignora y otro que un código desconocido no rompe nada.
  - **Criterio de aceptación:** ningún texto procedente de la URL llega a la interfaz ✅.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [x] **[T2-12] Detectar la reutilización de tokens de refresco**
  - **Área:** Seguridad · **Ubicación:** `Controllers/AuthController.cs:129-144`
  - **Qué hacer:** la rotación revoca el token usado, pero presentar uno ya revocado solo devuelve 401 sin más consecuencias. Con detección de reutilización, ese intento debe revocar toda la familia de tokens del usuario, porque indica que alguien copió el token. Falta además comprobar el estado del usuario (bloqueado, email sin confirmar) al refrescar.
  - ****Resuelto (2026-08-27):** presentar un token ya rotado revoca **todas** las sesiones activas del usuario y queda registrado. El refresco comprueba además el estado de la cuenta: antes, bloquear a alguien no surtía efecto hasta que caducara su token, así que podía seguir renovando durante días. Todos los fallos devuelven la misma respuesta. Tres tests.**
  - **Criterio de aceptación:** reutilizar un token revocado invalida todas las sesiones del usuario y queda registrado. · **Esfuerzo:** medio · **Depende de:** ninguna

- [x] **[T2-13] Corregir el comentario de política de seguridad de `tokenStore`**
  - **Área:** Seguridad / Redacción · **Ubicación:** `src/shared/api/tokenStore.ts:13`
  - **Qué hacer:** el comentario afirma que el refresh token se persiste "en localStorage con SameSite=Strict". `SameSite` es un atributo de cookie y no existe en `localStorage`: el texto documenta una mitigación inexistente y puede llevar a subestimar el riesgo ante XSS. Corregirlo y dejar clara la exposición real.
  - ****Resuelto (2026-09-02):** el comentario dice ahora que el token es legible por cualquier JS del origen y enumera las mitigaciones que sí existen —hash en servidor, rotación en cada uso y revocación de todas las sesiones ante reutilización (T2-12)—, dejando escrito que la de cookie **no** existe. Un comentario que inventa una defensa es peor que no tener comentario: hace que nadie busque la que falta.**
  - **Criterio de aceptación:** el comentario describe solo mitigaciones vigentes. · **Esfuerzo:** bajo · **Depende de:** ninguna

- [x] **[T2-14] Revisar la CSP y añadir cabeceras de seguridad en Netlify**
  - **Área:** Seguridad · **Ubicación:** `TrackerMultimedia_Frontend/index.html:8-21`, `netlify.toml`
  - **Qué hacer:** el `connect-src` permite `https://*.jikan.moe`, `https://*.googleapis.com` y `https://api.github.com`, a los que el frontend no llama nunca (los consulta el backend), y fija el host de Render, lo que romperá con un dominio propio. Además `netlify.toml` no define ninguna cabecera: no hay HSTS, `X-Frame-Options` ni `frame-ancestors`, y `frame-ancestors` no funciona desde `<meta>`, como reconoce el propio comentario del HTML. Mover la CSP a cabeceras de Netlify y recortar los orígenes.
  - ****Resuelto (2026-09-02):** `connect-src` queda en `'self'`. Los tres hosts externos —Jikan, Google, GitHub— no los llama el navegador: esos catálogos y los proveedores OAuth los consulta el **backend**; y el cuarto fijaba el dominio de Render, que habría roto la aplicación al cambiar de despliegue. Una CSP que permite orígenes sin usar no protege de menos: **describe mal el sistema**. Las cabeceras que un `<meta>` no puede declarar van en `netlify.toml` —`frame-ancestors`, `X-Frame-Options`, `X-Content-Type-Options`, `Referrer-Policy`, `Permissions-Policy` y COOP—, dejando la política de contenido en un solo sitio para que el hash del script en línea no pueda desincronizarse. Comprobado en Chrome con el backend en marcha: sin una sola violación en consola y `fetch('/api/auth/methods')` devuelve 200. **No se cierra el criterio de securityheaders.com**, que exige un despliegue público: queda anotado para cuando lo haya.**
  - **Criterio de aceptación:** securityheaders.com da al menos una B; la CSP no incluye orígenes sin usar. · **Esfuerzo:** medio · **Depende de:** ninguna

- [x] **[T2-15] Minimizar los datos personales en los logs**
  - **Área:** Seguridad / Legal · **Ubicación:** `Controllers/AuthController.cs:55,62,84,90,97,105,280,418`, `Services/SmtpEmailService.cs:32,49`
  - **Qué hacer:** el email del usuario se registra en once puntos, varios con nivel `Warning` en flujos fallidos. Bajo GDPR eso es tratamiento de datos personales en un destino con su propia retención. Registrar el `UserId` y, cuando el usuario aún no existe, un hash o un email parcialmente enmascarado.
  - ****Resuelto (2026-09-02):** al revisarlo, `AuthController` ya registraba `UserId` en todos los puntos —eso se corrigió al pasar por T2-12—, así que el hallazgo estaba parcialmente desactualizado y quedaban cuatro sitios: `OAuthController` (alta fallida y vinculación pendiente) y las dos trazas de `SmtpEmailService`. La vinculación pasa a registrar el `UserId`, que ahí sí existe. Para los dos casos en los que todavía no hay usuario se añade `Infrastructure/Logging/PersonalData.MaskEmail`, que conserva la primera letra y el dominio (`a***@ejemplo.com`): sirve para reconocer un correo que ya se conoce, no para descubrir uno nuevo. Comprobado con `grep`: no queda ninguna plantilla de log con la dirección completa.**
  - **Criterio de aceptación:** ningún log de nivel `Information` o superior contiene una dirección de correo completa. · **Esfuerzo:** bajo · **Depende de:** ninguna

- [x] **[T2-16] Optimizar `GetStatsAsync`** — *anulada*
  - **Área:** Rendimiento · **Ubicación:** `Services/MediaItemsService.cs:151-261`
  - **Qué hacer:** ocho consultas independientes sobre la misma tabla, y `scoredItems` (`:187`) trae a memoria todas las puntuaciones del usuario solo para calcular una media que PostgreSQL puede hacer con `AVG`. Agrupar en menos consultas y calcular la media en la base de datos.
  - **Anulada el 2026-09-02:** dependía de T2-08 y esa se resolvió eliminando la funcionalidad. `GetStatsAsync` ya no existe, así que no hay nada que optimizar. **La consulta más barata es la que no se ejecuta.** El identificador no se reutiliza. Si algún día vuelve el panel, este hallazgo vuelve con él: ocho consultas sobre la misma tabla y una media calculada en memoria trayendo todas las puntuaciones del usuario.
  - **Criterio de aceptación:** el endpoint ejecuta 3 consultas o menos y no materializa listas completas. · **Esfuerzo:** medio · **Depende de:** T2-08

- [x] **[T2-17] Limitar el tamaño de las importaciones**
  - **Área:** Rendimiento · **Ubicación:** `Services/MediaItemsService.cs:311-412`, `Controllers/MediaItemsController.cs:62`
  - **Qué hacer:** hay un tope de 10 MB de archivo, pero no de número de elementos. Un JSON de 10 MB puede contener decenas de miles de elementos que se cargan en memoria junto con la biblioteca completa del usuario y se insertan en un solo `SaveChanges`. Añadir un máximo de elementos y procesar por lotes.
  - **Resuelto (2026-08-27):** máximo de **5.000 elementos** por importación, con un mensaje que nombra las dos cifras. El tope de 10 MB acotaba el archivo pero no el trabajo. Cubierto por un test.
  - **Criterio de aceptación:** superar el máximo devuelve 400 con un mensaje claro; test incluido. · **Esfuerzo:** medio · **Depende de:** ninguna

- [x] **[T2-18] Validar `ExternalId` para todos los proveedores externos**
  - **Área:** Código · **Ubicación:** `Services/MediaItemsService.cs:1398-1413`
  - **Qué hacer:** `ValidateExternalSource` solo exige `ExternalId` y `ExternalMediaKind` cuando `SourceType == Jikan`, pero el enum también tiene `AniList` y `MangaDex` (`Domain/Enums/MediaItemSourceType.cs:7-8`). Un elemento de AniList sin identificador pasa la validación y escapa además al índice único que evita duplicados.
  - ****Resuelto (2026-09-02):** la condición se invierte: la comprobación va contra `Manual`, no contra `Jikan`. Así cualquier proveedor que se añada al enum queda cubierto desde el primer día sin tocar la función — que es justo lo que falló cuando se añadieron AniList y MangaDex. Dos tests: uno recorre los tres proveedores por ambas carencias (identificador y tipo de medio), otro fija que un elemento manual sigue sin exigir nada.**
  - **Criterio de aceptación:** la validación cubre todo `SourceType` distinto de `Manual`; tests por proveedor. · **Esfuerzo:** bajo · **Depende de:** ninguna

- [x] **[T2-19] No descartar en silencio un formato inválido**
  - **Área:** Código / UX · **Ubicación:** `Services/MediaItemsService.cs:1295-1304`
  - **Qué hacer:** `ResolveUserFormatIdAsync` devuelve `null` cuando el formato no existe o no pertenece al usuario, así que el elemento se guarda sin formato y la interfaz muestra un guardado correcto. `ResolveCategoriesAsync` sí devuelve error en el mismo caso. Unificar el comportamiento devolviendo un error de validación.
  - ****Resuelto (2026-09-02):** `ResolveUserFormatIdAsync` devuelve ahora `ServiceResult<Guid?>` y los dos llamantes (alta y edición) propagan el error. `Guid.Empty` se sigue tratando como "sin formato" para no romper a los clientes que lo mandan en lugar de omitir el campo. Dos tests: formato de otro usuario y formato inexistente.**
  - **Criterio de aceptación:** enviar un `UserFormatId` ajeno devuelve 400 con el campo señalado. · **Esfuerzo:** bajo · **Depende de:** ninguna

- [x] **[T2-20] Sacar el sembrado de formatos del endpoint GET**
  - **Área:** Arquitectura · **Ubicación:** `Services/FormatsService.cs:34-37`, `:117-133`
  - **Qué hacer:** `GET /api/formats` inserta los diez formatos por defecto cuando el usuario no tiene ninguno. Un GET que escribe rompe la semántica HTTP y, con dos peticiones concurrentes, ambas ven cero formatos y la segunda viola el índice único, devolviendo 500. Sembrar al crear la cuenta.
  - ****Resuelto (2026-09-02):** `GetAllAsync` ya no escribe; el sembrado vive en `FormatsService.EnsureDefaultFormatsAsync`, idempotente, y lo llaman los dos únicos caminos que crean cuentas: `AuthController.Register` y el caso B del callback de OAuth. Dos tests: un alta real por el endpoint recibe los diez formatos, y un GET de una cuenta sin formatos devuelve lista vacía **y deja la tabla a cero**. Riesgo de arrastre comprobado, no supuesto: cuentas ya existentes que nunca hubieran llamado al GET se quedarían sin formatos, así que consulté la base local —`SELECT count(*)` sobre `AspNetUsers`— y hay **0 usuarios**, luego no hay nada que rellenar. En una base con usuarios haría falta un `INSERT` puntual antes de desplegar esto.**
  - **Criterio de aceptación:** `GET /api/formats` no escribe nunca; los formatos existen desde el registro. · **Esfuerzo:** medio · **Depende de:** ninguna

- [x] **[T2-21] Añadir `ErrorBoundary` al frontend**
  - **Área:** Código / UX · **Ubicación:** `src/App.tsx`, `src/main.tsx`
  - **Qué hacer:** no hay ningún límite de error, así que cualquier excepción durante el renderizado deja la pantalla en blanco, sin mensaje y sin forma de recuperarse. Añadir un `ErrorBoundary` con opción de recargar.
  - **Resuelto (2026-08-27):** `ErrorBoundary` montado **por fuera de los proveedores**, para que un fallo en el contexto de sesión también quede capturado; con acciones de recargar y volver al inicio, y la traza visible solo en desarrollo. Cuatro tests.
  - **Criterio de aceptación:** una excepción provocada en una vista muestra una pantalla de error con acción de recuperación. · **Esfuerzo:** bajo · **Depende de:** ninguna

- [x] **[T2-22] Añadir `eslint-plugin-jsx-a11y`**
  - **Área:** Accesibilidad / DevOps · **Ubicación:** `eslint.config.js:12-17`
  - **Qué hacer:** la configuración solo extiende `js`, `typescript-eslint`, `react-hooks` y `react-refresh`. Ninguna regla de accesibilidad se comprueba, que es la razón de que siete etiquetas sin control asociado (T1-17) pasaran desapercibidas.
  - **Resuelto (2026-08-27):** `eslint-plugin-jsx-a11y` con su configuración recomendada. Verificado contra un archivo con defectos a propósito: detecta la etiqueta sin asociar de T1-17, un `alt` ausente, un `onClick` sin equivalente de teclado y un `href` inválido.
  - **Un obstáculo y una mejora de paso:** el plugin declara compatibilidad hasta ESLint 9 y el proyecto usa el 10. Funciona —comprobado ejecutándolo—, así que el *peer* se fuerza con un `override` en lugar de instalar con `--legacy-peer-deps`, que reordena el árbol entero: el primer intento dejó fuera `@testing-library/dom` y rompió los tipos de todos los tests. La única regla que saltó sobre el código existente fue `no-autofocus`, once veces: cuatro estaban dentro de diálogos y pasan a `data-dialog-autofocus`, que gestiona `useModalDialog` —mejor que antes, porque React aplica `autoFocus` durante el commit y le ganaba la carrera al hook—; las seis de páginas de autenticación conservan `autoFocus` con una excepción documentada, que es justo el caso para el que existe la excepción de la regla.
  - **Criterio de aceptación:** `npm run lint` señala una etiqueta sin asociar introducida a propósito. · **Esfuerzo:** bajo · **Depende de:** T1-17

- [x] **[T2-23] Corregir el error de ESLint y hacer que el build ejecute el lint**
  - **Área:** Código · **Ubicación:** `src/features/auth/views/ProfileView.tsx:18`, `package.json:8`
  - **Qué hacer:** `npm run lint` devuelve un error (`react-hooks/set-state-in-effect`: `setDisplayName` dentro de un efecto provoca renderizados en cascada). Como `npm run build` solo ejecuta `tsc -b && vite build`, Netlify despliega con el error presente. Derivar `displayName` del usuario o usar `key`, y añadir el lint a la comprobación de despliegue.
  - **Criterio de aceptación:** `npm run lint` sale limpio y forma parte de la verificación local previa al commit. · **Esfuerzo:** bajo · **Depende de:** ninguna

- [x] **[T2-24] Extender `prefers-reduced-motion` a las transiciones**
  - **Área:** Accesibilidad · **Ubicación:** `src/index.css:1174-1187`
  - **Qué hacer:** el bloque anula `animation` en nueve selectores, pero la hoja declara 31 animaciones y transiciones y ninguna `transition` se desactiva. Quien pide movimiento reducido sigue viendo desplazamientos y fundidos. Ampliar la regla con un selector global que anule también `transition`.
  - **Criterio de aceptación:** con movimiento reducido activo en el sistema operativo, ningún elemento anima ni transiciona. · **Esfuerzo:** bajo · **Depende de:** ninguna

- [~] **[T2-25] Marcar la página activa en la navegación** — ~~ANULADA el 2026-08-27~~
  - **Área:** Accesibilidad · **Ubicación:** `src/layouts/AppLayout.tsx:80-89`
  - **Por qué se anula:** el hallazgo original era incorrecto. Al ir a implementarlo se
    comprobó en `node_modules/react-router/dist/` que `NavLink` **ya emite
    `aria-current="page"` por defecto** cuando la ruta está activa. No había nada que
    corregir. El ID queda reservado y no se reutiliza.

- [x] **[T2-26] Unificar el idioma de los mensajes de error del backend**
  - **Área:** Ortografía y redacción · **Ubicación:** `Services/MediaItemsService.cs:537,614,1407,1410`, `Services/CategoriesService.cs:77,118`, `Services/FormatsService.cs:89`, `Controllers/SearchController.cs:30,38,45`
  - **Qué hacer:** el producto está en español pero varios mensajes que llegan al usuario están en inglés: "The Title field cannot be empty.", "The Name field cannot be empty.", "Not found", "ExternalId is required when SourceType is Jikan.", "External search is unavailable.". Traducirlos y fijar la convención.
  - ****Resuelto (2026-09-02):** traducidos los de `MediaItemsService`, `CategoriesService`, `FormatsService` y los tres `Problem(...)` de `SearchController`. Dos matices sobre el hallazgo original, comprobados en el código: **«Not found» nunca llegaba al usuario** —los tres controladores lo descartan y devuelven `NotFound()` sin cuerpo—, así que se tradujo por coherencia, no porque se viera; y los de `ExternalId` desaparecieron al reescribirlos en T2-18. Se cambió además «Type o ContentKind es obligatorio», que estaba en español pero nombraba dos campos internos de la API, por «Debes indicar el tipo de contenido». Repasado con `grep` sobre todas las cadenas de `Fail(`, `title:` y `detail:`.**
  - **Criterio de aceptación:** ningún mensaje devuelto al cliente está en inglés. · **Esfuerzo:** bajo · **Depende de:** ninguna

---

## Tier 3 — Pulido y mantenimiento

- [x] **[T3-01] Unificar los cinco valores documentados de `VITE_API_URL`** · **Cerrada (2026-08-27):** un único valor, `/api`, en `.env`, `.env.example`, el README y el valor por defecto de `src/config/env.ts`. Se eligió **ruta relativa** y no URL absoluta porque es la única que funciona igual en `localhost` y con `vite --host` desde otro dispositivo: con una absoluta a `localhost`, el otro dispositivo hablaría con su propio localhost. El esquema de Zod acepta ahora rutas además de URLs, se añadió el mismo proxy en `preview` (sin él `/api` daba 404 al previsualizar el build) y se eliminó `apiBaseWithoutPath`, que no usaba nadie. ~~Hallazgo original:~~ · Documentación · `README.md:97`, `TrackerMultimedia_Backend/TODO.md`, `TrackerMultimedia_Frontend/.env.example:7`, README del frontend, `src/config/env.ts:11`. Hay tres valores distintos (`/api`, `http://localhost:5173/api`, `http://localhost:5218/api`) y dos nombres de variable (`VITE_API_URL` y `VITE_API_BASE_URL`). Dejar una única fuente. · **Criterio:** un solo valor documentado, coherente con el proxy de Vite. · **Esfuerzo:** bajo · **Depende de:** ninguna
- [x] **[T3-02] Eliminar o implementar `Security:ApiKey`** · **Cerrada (2026-08-27):** eliminada de los dos README, de `appsettings.Local.example.json` y de user-secrets. No existía en el código. ~~Hallazgo original:~~ · Documentación / Limpieza · ambos README y `appsettings.Local.example.json:7`. Se documenta como "header X-Api-Key para rutas administrativas" pero no existe en el código. · **Criterio:** la clave desaparece de la documentación y de los ejemplos. · **Esfuerzo:** bajo · **Depende de:** ninguna
- [x] **[T3-03] Eliminar `DefaultConnectionPro`** · **Cerrada (2026-08-27):** eliminada junto con T1-13. Era la cadena de producción de Neon y su único efecto era tener esa credencial en el portátil. ~~Hallazgo original:~~ · Limpieza · `appsettings.Local.example.json:4`. Segunda cadena de conexión que ningún código lee. · **Criterio:** no queda rastro de la clave. · **Esfuerzo:** bajo · **Depende de:** T1-13
- [x] **[T3-04] Sustituir `TODO.md` por documentación vigente** · **Cerrada (2026-08-27):** eliminado. Describía paso a paso el despliegue en Render, que ya no existe; lo que seguía siendo útil vive en el README del backend. ~~Hallazgo original:~~ · Documentación · `TrackerMultimedia_Backend/TODO.md`. Contradice al README raíz en rutas de build y nombres de variables, y describe pasos ya ejecutados. · **Criterio:** el archivo se elimina o se reescribe como guía de despliegue correcta. · **Esfuerzo:** bajo · **Depende de:** T0-03
- [x] **[T3-05] Documentar cómo ejecutar las pruebas** · **Cerrada (2026-08-27):** los dos README documentan `dotnet test TrackerMultimedia_Backend.slnx` y `npm run test`, y el del frontend perdió la mención a `playwright-report`. Se añadió además la comprobación de esquema desde cero con `dotnet ef database drop`, que es lo que las pruebas no pueden cubrir. ~~Hallazgo original:~~ · Documentación · ambos README. Ninguno explica `dotnet test` ni `npm run test`, y el del frontend menciona `playwright-report` cuando no hay Playwright en el proyecto. · **Criterio:** cada README tiene una sección de pruebas con comandos reales. · **Esfuerzo:** bajo · **Depende de:** ninguna
- [x] **[T3-06] Actualizar el árbol de estructura de los README** · **Cerrada (2026-09-02):** los dos árboles coinciden ya con el repositorio. Faltaban `Categories/` y `Formats/` en los contratos, `catalog/` y `categories/` en las features, `Infrastructure/Http/` y `Logging/`, once servicios y los archivos añadidos en esta tanda (`robots.txt`, `sitemap.xml`, los dos `.editorconfig`, `appsettings.json`). Corregido además que `Domain/Validation/` no contiene «constantes» sino un atributo de validación, y dos recuentos de pruebas que se habían quedado atrás: 140→148 en el frontend y **105→124** en el backend. ~~Hallazgo original:~~ · Documentación · ambos README. Omiten `TrackerMultimedia.Tests/` y las features `categories` y `catalog`, y describen `Domain/Validation/` como "constantes" cuando contiene un atributo de validación. · **Criterio:** el árbol coincide con el repositorio. · **Esfuerzo:** bajo · **Depende de:** ninguna
- [x] **[T3-07] Eliminar `Compliance-Platform-DESIGN.md`** · **Cerrada (2026-09-02):** borrado. 190 líneas describiendo el sistema de diseño de otro producto ("Compliance Platform Dashboard"); comprobado con `grep` que nada lo referenciaba. ~~Hallazgo original:~~ · Limpieza · `TrackerMultimedia_Frontend/Compliance-Platform-DESIGN.md`. 190 líneas que describen el sistema de diseño de otro producto ("Compliance Platform Dashboard"), sin relación con TrackerMultimedia. · **Criterio:** el archivo ya no está versionado. · **Esfuerzo:** bajo · **Depende de:** ninguna
- [x] **[T3-08] Eliminar `sanitize.ts` y las dependencias de DOMPurify** · **Cerrada (2026-09-02):** borrados `sanitize.ts`, sus tres tests y las dependencias `dompurify` y `@types/dompurify`. Comprobado antes: ninguna de las cuatro funciones se usaba fuera de su propio test y no hay un solo `dangerouslySetInnerHTML` en el proyecto. Se usó `npm uninstall`, no `--legacy-peer-deps`, y se verificó que el árbol quedaba intacto: el diff del lockfile son 28 líneas y `@testing-library/dom` sigue instalado. ~~Hallazgo original:~~ · Limpieza · `src/shared/utils/sanitize.ts`, `package.json`. Ninguna de las cuatro funciones se usa en la aplicación (solo en su propio test) y no hay ningún `dangerouslySetInnerHTML`. Se verificó que DOMPurify no llega al bundle, así que es deuda de dependencias, no de peso. `@types/dompurify` está además obsoleto porque DOMPurify 3 trae sus propios tipos. · **Criterio:** el módulo y las dos dependencias desaparecen, o se documenta su uso previsto. · **Esfuerzo:** bajo · **Depende de:** ninguna
- [x] **[T3-09] Eliminar los campos sin uso de `env`** · **Cerrada (2026-09-02):** `env` expone solo `apiUrl`. `apiBaseWithoutPath` ya había caído en T3-01; `dev`, `prod` e `isProduction` no los leía nadie —comprobado: el único archivo que importa `env` es `axios.ts` y solo usa `apiUrl`—. Quien necesite el modo usa `import.meta.env.DEV`, que además Vite sustituye en compilación. ~~Hallazgo original:~~ · Limpieza · `src/config/env.ts:18-21`. `dev`, `isProduction` y `apiBaseWithoutPath` no se usan en ningún sitio. · **Criterio:** el objeto solo expone lo que se consume. · **Esfuerzo:** bajo · **Depende de:** ninguna
- [x] **[T3-10] Eliminar `OAuthLinkRequiredResponse`** · **Cerrada (2026-09-02):** borrado `OAuthLinkRequiredResponse`. El controlador redirige en lugar de devolverlo, así que el tipo no aparecía en ninguna firma. ~~Hallazgo original:~~ · Limpieza · `Contracts/Auth/OAuthContracts.cs:13-19`. El contrato no se usa: el controlador redirige en lugar de devolverlo. · **Criterio:** el tipo desaparece. · **Esfuerzo:** bajo · **Depende de:** ninguna
- [x] **[T3-11] Unificar los tipos de resultado equivalentes** · **Cerrada (2026-09-02):** un solo tipo, `ServiceResult<T>`. Los tres `record struct` privados —`NormalizeTitleResult`, `ResolveDomainResult`, `NormalizeTextResult`— tenían exactamente la misma forma y desaparecen. Se añadió `ToFailure<TOther>()` para propagar el error entre tipos, que era el motivo real de que existieran por separado: cada punto de propagación repetía `Fail(x.ErrorField!, x.ErrorMessage!)` con sus dos `!`; ahora hay 15 usos. Un detalle que salió al hacerlo: en `ServiceResult<ContentKind>` la propiedad `Value` **no** es `ContentKind?` —`T?` con un `T` sin restringir no envuelve en `Nullable` para tipos valor—, así que seis `contentKindResult.Value!.Value` se quedaron en `.Value`, más claros que antes. Refactor sin cambio de comportamiento: 124/124. ~~Hallazgo original:~~ · Refactorización · `Contracts/Common/ServiceResult.cs`, `Services/MediaItemsService.cs:1503,1510`, `Services/CategoriesService.cs:136`. Cuatro estructuras con la misma forma (`Value`/`ErrorField`/`ErrorMessage`). · **Criterio:** un único tipo de resultado. · **Esfuerzo:** medio · **Depende de:** ninguna
- [x] **[T3-12] Extraer `GetUserId()` a una clase base o extensión** · **Cerrada (2026-09-02):** `ClaimsPrincipalExtensions` en `Infrastructure/Http/`, con dos formas porque había dos comportamientos distintos y no uno copiado cuatro veces: `GetUserId()` lanza —para los tres controladores CRUD, donde la ausencia del claim significa un token que este backend no pudo emitir— y `TryGetUserId(out)` devuelve `false`, que es lo que `AuthController` necesitaba en sus cuatro endpoints para responder 401. Ningún controlador vuelve a mencionar `FindFirstValue`; se quitaron además los `using` que dejaron de hacer falta. Refactor sin cambio de comportamiento: 124/124. ~~Hallazgo original:~~ · Refactorización · `Controllers/MediaItemsController.cs:22`, `CategoriesController.cs:17`, `FormatsController.cs:17`, `AuthController.cs:181`. El mismo método está copiado cuatro veces. · **Criterio:** una sola implementación compartida. · **Esfuerzo:** bajo · **Depende de:** ninguna
- [x] **[T3-13] Corregir la errata y los comentarios contradictorios de `Program.cs`** · **Cerrada (2026-09-02):** **ya estaba resuelto y no por esta tarea.** El comentario («Uitlizar DefaultConnection para desarrollo» / «Utilizar … para producción») desapareció en el commit `2b2dc76` al reescribirse el bloque de migraciones. Se deja constancia en vez de marcarla como trabajo hecho aquí. ~~Hallazgo original:~~ · Redacción · `Program.cs:222-223`. Dice "Uitlizar" y las dos líneas seguidas afirman lo mismo para desarrollo y para producción sin aportar nada. · **Criterio:** el comentario desaparece o explica algo cierto. · **Esfuerzo:** bajo · **Depende de:** ninguna
- [x] **[T3-14] Corregir el comentario de `GoogleAuthService`** · **Cerrada (2026-09-02):** el comentario decía «con validación de id_token para obtener el perfil»; el servicio pide el perfil al endpoint `userinfo` con el access token y nunca valida un `id_token`. Ahora describe lo que hace y añade que esa validación, que ahorraría una llamada de red, habría que implementarla. ~~Hallazgo original:~~ · Redacción · `Services/GoogleAuthService.cs:12`. Afirma "con validación de id_token para obtener el perfil"; el código usa el endpoint `userinfo` con el access token y nunca valida un `id_token`. · **Criterio:** el comentario describe la implementación real. · **Esfuerzo:** bajo · **Depende de:** ninguna
- [x] **[T3-15] Normalizar la indentación** · **Cerrada (2026-09-02):** `.editorconfig` en los dos repositorios y `dotnet format whitespace` aplicado: **67 archivos**, con los tabuladores de `ApplicationDbContext` y las sangrías descuadradas de `OAuthController` corregidos. `dotnet format whitespace --verify-no-changes` sale limpio. Sobre la codificación: se eligió `charset = utf-8` **sin** BOM porque era lo que ya tenían 86 de los 112 archivos —contados, no supuestos—, así que la opción contraria habría tocado el triple de archivos. En el frontend el `.editorconfig` cubre sangría y fin de línea, pero **no** comillas ni punto y coma: para eso hace falta Prettier, registrado aparte como T3-22. ~~Hallazgo original:~~ · Estilo · `Data/ApplicationDbContext.cs:13-15` mezcla tabuladores y espacios dentro del mismo bloque; `Controllers/OAuthController.cs:296,302-303` tiene sangrías descuadradas. Añadir un `.editorconfig` y aplicar `dotnet format`. · **Criterio:** `dotnet format --verify-no-changes` sale limpio. · **Esfuerzo:** bajo · **Depende de:** ninguna
- [x] **[T3-16] Evitar el rechazo no gestionado al cerrar sesión** · **Cerrada (2026-09-02):** la sesión local se limpia igualmente —`logout` lo hace en un `finally`—, así que lo que había que contar no era el fallo sino su consecuencia: el servidor no se enteró de que revocara el token de refresco. `AppLayout` captura y muestra un aviso que dice justo eso y remite a «cerrar todas las sesiones» del perfil. `logoutAll` ya estaba bien gestionada. Un test que fuerza el rechazo. ~~Hallazgo original:~~ · Código · `src/features/auth/context/AuthContext.tsx:86-95`, `src/layouts/AppLayout.tsx:67`. `logout` usa `try/finally` sin `catch`, y `onClick={() => void logout()}` descarta la promesa: un fallo de red produce un *unhandled rejection*. · **Criterio:** el fallo se captura y se avisa con un toast. · **Esfuerzo:** bajo · **Depende de:** ninguna
- [x] **[T3-17] Evitar la pantalla en blanco por variable de entorno inválida** · **Cerrada (2026-09-02):** `safeParse` en lugar de `parse`, y en el fallo se pinta un aviso legible directamente en el documento —sin React, porque esto ocurre antes de que React monte nada y ni el límite de error lo ve— con el nombre de la variable, el motivo y qué hacer. Se construye con `append` y no con `innerHTML`: el valor viene de la configuración, pero pintarlo como markup convertiría un error de configuración en un punto de inyección. **No se cae a un valor por defecto a propósito**: taparlo haría que la aplicación hablara con un backend que no es. Tres tests. ~~Hallazgo original:~~ · Código · `src/config/env.ts:7-9`. `envSchema.parse` se ejecuta al importar el módulo: un `VITE_API_URL` mal formado en Netlify deja la aplicación en blanco sin ningún mensaje. · **Criterio:** una variable inválida muestra un error legible. · **Esfuerzo:** bajo · **Depende de:** ninguna
- [x] **[T3-18] Reaccionar a los cambios de tema del sistema y evitar el destello** · **Cerrada (2026-09-02):** dos defectos, y el segundo hacía inútil arreglar el primero. **(1)** El hook no se suscribía a `prefers-color-scheme`; ahora escucha el evento `change` y se da de baja al desmontarse. **(2)** El efecto escribía en `localStorage` **al montar**, así que desde la primera visita siempre había preferencia guardada y el modo «seguir al sistema» dejaba de existir sin que nadie lo desactivara: ahora solo se persiste al pulsar el interruptor, y el estado tiene tres valores (`dark`, `light`, `null` = seguir al sistema). **El destello** se corrige con un script en el `<head>` que aplica el atributo antes del primer pintado. La CSP prohíbe scripts en línea, así que se declara por hash SHA-256. Cinco tests, y comprobado además en Chrome: sin violación de CSP, `data-theme=dark` ya puesto al cargar y **nada escrito en `localStorage` tras la carga**, que es la prueba de (2). ~~Hallazgo original:~~ · UI/UX · `src/shared/hooks/useDarkMode.ts:14,26-30`, `index.html`. El hook lee `prefers-color-scheme` una vez y no se suscribe a los cambios; además aplica el tema en el primer render de React, así que en modo oscuro hay un destello claro al cargar. · **Criterio:** el tema se aplica antes de la hidratación y sigue los cambios del sistema. · **Esfuerzo:** bajo · **Depende de:** T1-09
- [x] **[T3-21] Resolver los 13 avisos de `npm audit` del frontend** · **Cerrada (2026-09-02):** `npm audit fix` sin `--force`: de **13 avisos a 0**. Todo cabía dentro de los rangos `^` que ya había, así que `package.json` **no cambia** y el diff es solo del lockfile: vitest 3.2.4→3.2.7, vite 8.0.10→8.2.2, axios 1.16.0→1.20.0, react-router-dom 7.14.2→7.18.3. Copia previa de `package.json` y `package-lock.json` antes de ejecutarlo, y comprobado después que el árbol seguía entero —`@testing-library/dom` instalado, tipos, lint, 148/148 y build— porque un `--legacy-peer-deps` ya lo rompió una vez en esta sesión. ~~Hallazgo original:~~ · Seguridad / Dependencias · `TrackerMultimedia_Frontend/package.json`. **Hallazgo nuevo del 2026-09-02**, aparecido al desinstalar DOMPurify y no causado por ese cambio. `npm audit` da **1 crítico, 10 altos y 2 bajos**: `vitest <3.2.6` (lectura y ejecución de archivos arbitrarios con el servidor de UI escuchando), `axios`, `vite`/`launch-editor`, `react-router`, `ws`, `postcss`, `nanoid`, `form-data`, `browserslist` y `brace-expansion`. **`npm audit fix --dry-run` los cubre los 13 sin `--force`**, es decir, con actualizaciones compatibles con semver. Contexto que rebaja la urgencia sin anularla: el proyecto no está desplegado y el crítico es de una herramienta de desarrollo que solo escucha si se lanza la UI de Vitest, cosa que este proyecto no hace. **Ejecutar con cuidado**: `npm audit fix` rehace el lockfile, y en esta misma sesión un `--legacy-peer-deps` dejó fuera `@testing-library/dom` y rompió los tipos de todos los tests; hacer copia de `package.json` y `package-lock.json` antes, y después comprobar suite, `tsc -b`, lint y build. · **Criterio:** `npm audit` sin avisos altos ni críticos, con las dos suites y el build en verde. · **Esfuerzo:** bajo · **Depende de:** ninguna

- [x] **[T3-22] Añadir Prettier al frontend** · **Cerrada (2026-09-02):** Prettier con el estilo que **ya seguía el código** —sin punto y coma, comillas simples, comas finales, ancho 100—, no con sus valores por defecto: el objetivo era fijar lo que hay. Configuración y pasada de formateo en commits separados. `.md` queda fuera, porque reflowar los párrafos convertiría cualquier cambio futuro de una frase en un diff de página entera, y también `.agents/`, `.codegraph/`, `.mcp.json` y `skills-lock.json`, que los gestionan otras herramientas. **La pasada destapó a la primera lo que T3-18 había dejado advertido**: Prettier reformateó el script en línea de `index.html` y el hash SHA-256 de la CSP dejó de cuadrar —fallo mudo: el navegador bloquea el script y vuelve el destello sin que nada falle a la vista—. No bastaba con recalcularlo, así que van dos medidas: un `<!-- prettier-ignore -->` **suelto** (Prettier solo reconoce la directiva si el comentario no lleva nada más, cosa aprendida probándola con la explicación dentro y viendo que no surtía efecto) y un test que recalcula el hash y dice cuál es el correcto. Comprobado que el test falla de verdad quitándole un punto y coma al script. ~~Hallazgo original:~~ · Estilo · `TrackerMultimedia_Frontend/`. **Hallazgo nuevo del 2026-09-02.** El proyecto no tiene Prettier ni ninguna otra herramienta que fije comillas, punto y coma o ancho de línea, así que el formateador por defecto del editor decide: guardar `vite.config.ts` desde VS Code lo reformateó entero —cuatro espacios, comillas dobles, punto y coma— contra el estilo del resto del proyecto. El `.editorconfig` añadido en T3-15 tapa la sangría y el fin de línea, que es la mitad del problema; la otra mitad necesita Prettier. Conviene una sola pasada de formateo en su propio commit, para que no se mezcle con cambios de comportamiento. · **Criterio:** `npx prettier --check .` sale limpio y el editor no reformatea al guardar. · **Esfuerzo:** bajo · **Depende de:** ninguna

- [x] **[T3-23] Revisar el resto de validaciones de entorno tras el caso de la cadena vacía** · **Cerrada (2026-09-02):** quedaba un caso de la misma familia que la cadena vacía: `apiUrl` hace `.trim()` diez líneas más abajo, pero el esquema validaba el valor **sin recortar**, así que un `VITE_API_URL= /api` con un espacio de más tumbaba la aplicación entera antes de que React montara. Ahora se valida el valor recortado, que es el que se usa. Tres tests más —variables ausentes de verdad, valores con espacios, e inválido con espacios que sigue rechazándose—, y comprobado que el de los espacios falla con el código anterior. Corregido de paso el mensaje del interruptor booleano, que decía «debe ser "true" o "false"» mientras el esquema acepta además «1» y «0». ~~Hallazgo original:~~ · Código · `TrackerMultimedia_Frontend/src/config/env.ts`. **Hallazgo nuevo del 2026-09-02, destapado por el test de T3-17 y no por la auditoría.** El esquema de Zod rechazaba `VITE_API_URL` vacío, mientras que el código que resuelve `apiUrl` diez líneas más abajo sí contemplaba ese caso (`length > 0`). Es decir: una línea `VITE_API_URL=` sin valor en el `.env` —lo más parecido a «no la he configurado»— tumbaba la aplicación entera antes de que React montara. Corregido ya en el propio T3-17 aceptando la cadena vacía. Queda pendiente lo general: **revisar que ningún otro punto de validación contradiga al código que consume el valor**, que es la clase de fallo que ningún test detecta si nadie prueba el caso vacío. · **Criterio:** cada variable de entorno tiene un test del caso ausente y del caso vacío. · **Esfuerzo:** bajo · **Depende de:** ninguna

- [x] **[T3-19] Añadir `robots.txt` y `sitemap.xml`** · **Cerrada (2026-09-02):** `robots.txt` y `sitemap.xml` en `public/`. El motivo concreto está escrito en el propio archivo: la reescritura SPA hace que **cualquier** ruta devuelva 200 con el HTML de la aplicación, también `/library` o `/profile`, así que para un rastreador «existen». Se excluyen la zona privada y las pantallas que solo tienen sentido con un token en la URL —indexarlas expondría en el buscador enlaces que caducan—; quedan permitidas `/login` y `/register`, que son las dos del sitemap. Queda dicho que `robots.txt` pide, no impide: el control de acceso lo hace el backend. Comprobado con `npm run preview`: 200 y `text/plain` / `text/xml`, o sea que la regla SPA no se los traga. ~~Hallazgo original:~~ · SEO · `TrackerMultimedia_Frontend/public/`. No existe ninguno de los dos. Aunque casi toda la aplicación está tras autenticación, conviene declarar explícitamente qué se puede indexar y evitar que las rutas privadas aparezcan en buscadores. · **Criterio:** ambos archivos se sirven en producción y las rutas privadas están excluidas. · **Esfuerzo:** bajo · **Depende de:** ninguna
- [x] **[T3-20] Poner longitud máxima a la contraseña de login** · **Cerrada (2026-09-02):** `Password` limitado a 100 caracteres y `Email` a 256, los mismos topes que el registro impone **desde el primer commit**, así que ninguna cuenta existente puede quedarse fuera —se comprobó en el historial de git antes de ponerlo—. No se declara mínimo a propósito: el login comprueba, no define la política. Un test con 5.000 caracteres. ~~Hallazgo original:~~ · Seguridad · `Contracts/Auth/LoginRequest.cs:7`. `Password` no tiene `StringLength`, así que una cadena enorme llega a `CheckPasswordAsync` y consume CPU en el hashing PBKDF2. · **Criterio:** el contrato limita a 100 caracteres, igual que registro. · **Esfuerzo:** bajo · **Depende de:** ninguna

---

## Tier 4 — Futuro / Opcional

- [ ] **[T4-01] Migrar el refresh token a una cookie `httpOnly`** · Seguridad. Es la mitigación real contra el robo por XSS que el propio `tokenStore.ts:19-24` ya propone. Implica cookies, CORS con credenciales y protección CSRF. · **Esfuerzo:** alto
- [ ] **[T4-02] Añadir PKCE a los flujos OAuth** · Seguridad. Con cliente confidencial no es obligatorio, pero es defensa en profundidad frente a la interceptación del código de autorización. · **Esfuerzo:** medio
- [ ] **[T4-03] Internacionalizar la interfaz** · UI/UX. Todos los textos están incrustados en español y `formatDate` fija la configuración regional `es-DO` (`src/shared/utils/index.ts:33`) en lugar de la del usuario. · **Esfuerzo:** alto
- [ ] **[T4-04] Añadir pruebas end-to-end** · QA. Playwright sobre los flujos de registro, login, OAuth, CRUD e importación/exportación. El `.gitignore` del frontend ya menciona `playwright-report` sin que exista Playwright. · **Esfuerzo:** alto
- [x] **[T4-05] Medir y publicar la cobertura de pruebas** · **Cerrada (2026-09-02):** `ReportGenerator` declarado en `.config/dotnet-tools.json` —no instalado globalmente, para que la versión sea la misma en cualquier equipo— y el procedimiento en el README. La primera medición dio **82,8 % de líneas y 48,4 % de ramas**, pero lo útil no fue el porcentaje sino **qué estaba a cero, que no se veía leyendo el código**: `AniListSearchService` y `MangaDexSearchService` completos (374 líneas, dos de los tres proveedores de catálogo) y el actualizar/borrar de formatos. Veinte tests nuevos; cobertura a **87,4 % / 56,1 %**. Y un defecto real salió al escribirlos, registrado abajo. ~~Hallazgo original:~~ · QA. `coverlet.collector` ya está en el proyecto de tests pero no se genera ningún informe. · **Esfuerzo:** medio

- [x] **[T4-09] Un identificador mal formado de MangaDex tumbaba la búsqueda entera** · **Hallazgo nuevo del 2026-09-02, encontrado al escribir los tests de T4-05, y cerrado el mismo día.** `MangaDexSearchService` derivaba el identificador con `Guid.Parse`, que lanza `FormatException` si el UUID viene mal formado. Esa excepción **no** está en el filtro del `catch` de `SearchAsync` —que solo cubre `HttpRequestException`, `TaskCanceledException` y `JsonException`—, así que un único elemento defectuoso devolvía un 500 y se llevaba por delante los resultados buenos de la misma respuesta. Mismo patrón que T2-06. Ahora ese elemento se descarta, igual que ya se descartaba uno sin título, con su test.
- [ ] **[T4-06] Observabilidad: logging estructurado y métricas** · DevOps. Hoy solo hay `ILogger` con la configuración por defecto: sin correlación de peticiones, sin exportación y sin alertas. **Sigue abierta**: de este punto solo se ha resuelto la sonda de salud, que se ha separado como T4-10 por ser un defecto concreto y verificable, mientras que la correlación y las métricas son trabajo de otra naturaleza. · **Esfuerzo:** medio

- [x] **[T4-10] La sonda de salud no comprobaba nada** · **Hallazgo separado de T4-06 y cerrado el 2026-09-04.** `AddHealthChecks()` sin comprobaciones registradas devuelve **siempre** `Healthy`: lo único que demostraba `/health` era que el proceso responde, que es lo que ya se sabe por haber recibido la petición. Con PostgreSQL caído seguía diciendo que todo iba bien. Ahora hay **dos** sondas, y la separación no es ceremonia: `/health` sigue siendo la de vida —es la que apunta `healthCheckPath` en `render.yaml`, y meter ahí la base de datos provocaría un bucle de reinicios, porque reiniciar el proceso no levanta una base caída— y `/health/ready` comprueba además la conexión. Ninguna devuelve el detalle del fallo: el mensaje de Npgsql lleva host, puerto y usuario, y eso se queda en el log. Cuatro tests, uno de ellos con la base **de verdad inalcanzable**, que es el que da sentido a la tarea: comprueba que `/health` responde 200 y `/health/ready` devuelve 503. · **Verificado, no supuesto:** con el API en marcha contra el PostgreSQL local, `/health` → `200` y `/health/ready` → `Healthy`.

- [x] **[T4-07] Publicar la especificación OpenAPI** · **Cerrada (2026-09-04):** dos medidas, porque el problema tenía dos mitades. **(1)** `docs/openapi.json` queda versionado —**30 rutas**—, así que el contrato se consulta sin arrancar nada ni desplegar nada; se le quita `servers`, que apunta al puerto de la máquina donde se generó y no describe el API. **(2)** En ejecución, la especificación se sirve siempre en desarrollo y fuera de él solo con `OpenApi__Exposed=true`. No se publica por defecto a propósito: no contiene secretos ni habilita nada, pero entrega el mapa completo de rutas y parámetros, y eso es una decisión por despliegue. Dos tests, y el negativo hizo falta de verdad: el host de pruebas arranca en `Development`, así que sin forzar `Production` el test del interruptor habría pasado igual aunque el documento se publicara siempre. ~~Hallazgo original:~~ · Documentación · `MapOpenApi` solo se activa en `Development`. · **Esfuerzo:** bajo

- [x] **[T4-08] Exportación completa de datos personales** · **Cerrada (2026-09-04):** `GET /api/auth/account/export` devuelve un JSON con todo lo guardado sobre la cuenta —sus campos, los proveedores vinculados, las sesiones abiertas, los formatos y la biblioteca entera con sus categorías— y un botón en el perfil, delante del de borrar, que es el orden en que se usan. **No amplía la exportación de biblioteca, que es un formato distinto con otro propósito**: el suyo es el que sabe leer la importación, y meterle los datos de cuenta la habría roto. Lo que queda fuera, a propósito: contraseñas, tokens de refresco —ni siquiera su hash, porque son credenciales en activo— y el `ProviderKey` de los logins externos, que identifica a la persona dentro de Google o GitHub y no le aporta nada a quien se lleva sus datos. Seis tests de backend y dos de frontend; el que comprueba que el token no sale se verificó **mutando el servicio para filtrarlo** y viendo que falla. De paso, los formatos personalizados quedan por fin exportables: no viajan en el archivo de biblioteca, así que hasta ahora no estaban en ninguna exportación. · **Verificado, no supuesto:** cuenta real creada contra el PostgreSQL local, exportación descargada y revisada campo a campo, y cuenta borrada después dejando la base a cero. ~~Hallazgo original:~~ · Legal · La exportación de biblioteca cubre parte de la portabilidad (art. 20) pero no incluye los datos de la cuenta. · **Esfuerzo:** medio

> Sobre el encaje legal: esto cubre el **contenido** que pide la portabilidad y se entrega en un formato estructurado y legible por máquina, pero **no emito asesoramiento jurídico**. Si la aplicación vuelve a publicarse, si el plazo de respuesta, la identificación del solicitante y el alcance exacto de «datos personales» cumplen con la normativa aplicable **requiere revisión legal**, igual que T0-05.

---

## Progreso

| Fecha | Tarea | Nota |
|-------|-------|------|
| 2026-08-27 | — | Roadmap creado a partir de la primera auditoría. 80 tareas abiertas. |
| 2026-08-27 | T0-01 | Migraciones restauradas con `MigrateAsync`, registro de las pendientes y `try/catch` que no tumba el arranque. |
| 2026-08-27 | T0-04 | `Microsoft.OpenApi` fijado a 2.12.2 y `SQLitePCLRaw.lib.e_sqlite3` a 2.1.13. `dotnet restore` sin ningún `NU1903`. **Ojo:** la 3.x de OpenApi rompe el generador de ASP.NET Core, hay que quedarse en la rama 2.x. |
| 2026-08-27 | T1-01 | FK sombra `ApplicationUserId` eliminada. Migración `FixExternalLoginsFkAndAddQueryIndexes`. |
| 2026-08-27 | T1-02 | Nuevo `AuthSessionService.GetLinkedProvidersAsync`. Cubierto por `Me_ReturnsLinkedExternalProviders`. |
| 2026-08-27 | T1-03 | Envío de correo aislado en `SendEmailSafelyAsync`: un SMTP caído ya no rompe el registro. |
| 2026-08-27 | T1-04 | Registro y login con respuesta única. Nueva plantilla `AccountAlreadyExists` y dos tests que comprueban que los casos son indistinguibles. |
| 2026-08-27 | T1-06, T1-07 | Índices `IX_RefreshTokens_TokenHash` (único) e `IX_MediaItems_UserId_CreatedAtUtc`, en la misma migración que T1-01. |
| 2026-08-27 | T1-08 a T1-11 | Las dos suites en verde: 105/105 backend y 124/124 frontend. |
| 2026-08-27 | T1-17 | Las siete etiquetas huérfanas pasan a `role="group"` + `aria-labelledby`, y el selector de color se nombra desde su disparador. |
| 2026-08-27 | T1-18 | Enlace «Saltar al contenido» y `<main id="contenido" tabIndex={-1}>`. |
| 2026-08-27 | T1-22 | Hallazgo nuevo: el enlace externo de los resultados no tenía nombre accesible. Corregido. |
| 2026-08-27 | T2-01, T2-23, T2-24 | Tests alineados con la interfaz, error de ESLint resuelto y movimiento reducido extendido a las transiciones. |
| 2026-08-27 | T2-25 | **Anulada**: el hallazgo era incorrecto, `NavLink` ya emite `aria-current`. |
| 2026-08-27 | T0-02 | Suite de pruebas movida dentro del repositorio de backend. `DefaultItemExcludes` en el `.csproj` para que los globs del SDK no se la traguen. 105/105 desde un `.slnx` con rutas internas. |
| 2026-08-27 | T0-03 | Decididos **dos repositorios independientes**. `render.yaml` y `netlify.toml` movidos a la raíz de cada uno con rutas relativas, y documentación de despliegue repartida entre los dos README. |
| 2026-08-27 | T1-13 | **Cerrada.** Todas las credenciales rotadas y, además, Render, Neon y Netlify deshabilitados: las antiguas ya no dan acceso a nada. |
| 2026-08-27 | T0-05, T1-14, T1-05 | Reclasificadas al pasar el proyecto a uso local. T0-05 en suspenso; las otras dos bajan a Bajo. Ninguna resuelta: vuelven si se redespliega. |
| 2026-08-27 | T2-05, T2-17, T2-21, T2-22 | `ProblemDetails` con `traceId`, tope de elementos en la importación, `ErrorBoundary` y reglas de accesibilidad en el linter. 117 tests en el backend, 140 en el frontend. |
| 2026-08-27 | T2-28 | **Hallazgo nuevo:** carrera intermitente en la suite del backend por compartir una sola `SqliteConnection`. Documentado, no corregido. |
| 2026-08-27 | T2-07, T2-09, T2-10, T2-11, T2-12 | Tanda de seguridad: detección de reutilización de tokens, mismas comprobaciones en los tres caminos de acceso, `state` OAuth no reutilizable, sin mensajes de excepción en la URL y CSV saneado contra inyección de fórmulas. |
| 2026-08-27 | T2-27 | **Hallazgo nuevo:** la página de login mostraba texto arbitrario tomado de la URL. Corregido. |
| 2026-08-27 | T1-19 | `appsettings.json` versionado. Verificado exportando lo versionado a una carpeta vacía y arrancándolo con solo los dos secretos obligatorios. |
| 2026-08-27 | T1-20 | De 378 a 151 archivos versionados, 0 binarios. El hallazgo se equivocaba con `codegraph.db`: ya estaba excluido. |
| 2026-08-27 | T1-21 | Purga periódica de tokens de refresco y estados OAuth caducados, con los plazos documentados. 107 tests en el backend. |
| 2026-08-27 | T1-16 | Gestión de foco en los diálogos, con hook compartido. Aparecieron por el camino tres defectos no previstos: el fondo como único cierre, el desmontaje al cerrar y `autoFocus` ganándole la carrera al hook. 134 tests. |
| 2026-08-27 | — | **Revertida la base a PostgreSQL nativo** en el puerto 5433, a petición del propietario: el proyecto arrancó así. Docker eliminado por completo. Se conserva la comprobación de esquema desde cero, ahora con `dotnet ef database drop`. |
| 2026-08-27 | T3-05 | Ambos README documentan cómo ejecutar las pruebas y cómo verificar el esquema desde cero. |
| 2026-08-27 | T3-01 a T3-04 | `VITE_API_URL` unificada en `/api`, `Security:ApiKey` y `DefaultConnectionPro` eliminadas, `TODO.md` borrado. |
| 2026-08-27 | T0-06 | **Hallazgo crítico nuevo.** Una migración sin `.Designer.cs` era invisible para EF: la columna `MediaItems.UserFormatId` no se creaba nunca y toda la biblioteca fallaba con 500 en cualquier base nueva. Regenerada y verificada sobre PostgreSQL en Docker. |
| 2026-08-27 | T1-13 | Secretos movidos a user-secrets, `appsettings.Local.json` limpio, base local en Docker. Falta rotar en los paneles de Neon, Google, GitHub y Mailtrap. |
| 2026-08-27 | T1-12 | **Anulada** por decisión del propietario: no habrá CI ni workflows. La verificación se hace en local antes de cada commit. |
| 2026-08-27 | T1-19 | Avance parcial: los README ya no afirman que exista `appsettings.json`. El archivo sigue sin existir y sigue en el `.gitignore`; la tarea queda abierta. |

---

## Decisiones cerradas

Lo que se ha revisado y se ha decidido **no** hacer. No volver a proponerlo sin un hecho nuevo.

| Decisión | Motivo |
|---|---|
| **No añadir banner de consentimiento de cookies** | Revisado el 2026-08-27: la aplicación no usa cookies, ni analítica, ni ningún rastreador (verificado en `index.html` y en todo `src/`). Solo usa `localStorage` para el token de refresco, el tema y los proveedores de búsqueda seleccionados, todos estrictamente necesarios para el servicio que el usuario solicita. No requiere consentimiento previo, aunque sí debe describirse en la política de privacidad (T0-05). |
| **No eliminar `ReactQueryDevtools` del árbol de `main.tsx`** | Revisado el 2026-08-27: parecía peso muerto en producción, pero se comprobó sobre `dist/assets/index-*.js` que Vite lo elimina por completo en el build. No hay nada que arreglar. |
| **No sustituir la lectura de tokens desde el fragmento de URL en el callback OAuth** | Revisado el 2026-08-27: el patrón mantiene los tokens fuera de los logs del servidor, que es exactamente lo que documenta `OAuthCallbackView.tsx:7`. Su sustitución real es T4-01 (cookie `httpOnly`), no un parche intermedio. |
| **No tratar `navigate(returnPath)` del callback como redirección abierta** | Revisado el 2026-08-27: `returnPath` llega sin revalidar desde el fragmento (`OAuthCallbackView.tsx:55,79`), pero `history.pushState` rechaza destinos de otro origen, así que no se ha podido construir un caso explotable. Queda como defensa en profundidad, no como vulnerabilidad. |
| **No añadir `aria-hidden` a los iconos de Heroicons** | Revisado el 2026-08-27: se comprobó en `node_modules/@heroicons/react/24/outline/esm/MoonIcon.js` que la librería ya emite `aria-hidden="true"` por defecto. No hay nada que corregir. |
| **No añadir `aria-current` a mano en la navegación (T2-25)** | Revisado el 2026-08-27 al ir a implementarlo: `NavLink` de React Router **ya emite `aria-current="page"` por defecto** en el enlace activo, verificado en `node_modules/react-router/dist/`. El hallazgo de la auditoría era erróneo y la tarea queda anulada. |
| **El proyecto no se despliega: uso local a través de Vite** | Decidido por el propietario el 2026-08-27. Render, Neon y Netlify deshabilitados y credenciales revocadas. Los blueprints (`render.yaml`, `netlify.toml`) y el `Dockerfile` **se conservan** como receta para volver, marcados como inactivos en cada README: borrarlos no ganaría nada y perdería el trabajo hecho. Lo que **no** hay que hacer es tratar los hallazgos de seguridad de producción como resueltos —T1-05 sigue en el código tal cual—, ni dar por cerrado lo legal: T0-05 está en suspenso, no hecha. |
| **`VITE_API_URL` es una ruta relativa, no una URL absoluta** | Decidido el 2026-08-27 al cerrar T3-01. `/api` se resuelve contra el host desde el que el navegador cargó la página, así que funciona igual en `localhost` y al servir con `vite --host` desde otro dispositivo de la red. Una URL absoluta a `localhost` rompe el segundo caso sin avisar: el otro dispositivo hablaría con su propio localhost. El esquema de Zod tuvo que ampliarse para aceptar rutas, porque `z.string().url()` las rechaza. |
| ~~**La base de datos de desarrollo va en Docker**~~ · **REVERTIDA el 2026-08-27** | El propietario decidió volver a la instalación nativa de PostgreSQL 17 en el puerto 5433, que es como arrancó el proyecto. Contenedor, volumen y red eliminados; `docker-compose.yml`, `.env` y `.env.example` borrados del repositorio. **Lo que sí hay que conservar es la práctica que Docker introdujo:** recrear la base desde cero antes de dar por buena una migración, ahora con `dotnet ef database drop --force && dotnet ef database update`. Eso es lo que destapó T0-06, un fallo crítico invisible durante meses, y no depende de Docker sino de que alguien construya el esquema desde cero alguna vez. ~~Decisión original:~~ `docker-compose.yml` levanta PostgreSQL 17 en `127.0.0.1:5433`, con la contraseña en `.env` fuera del control de versiones. El puerto no es el 5432 para no chocar con una instalación nativa, y el contenedor no se expone a la red local. La ventaja que zanjó la discusión no es la comodidad: es que `docker compose down -v && docker compose up -d` da una base vacía en segundos, y **eso es lo que destapó T0-06**, un fallo crítico que llevaba meses invisible porque nadie había creado nunca un esquema desde cero. |
| **No montar integración continua (T1-12)** | Decidido por el propietario el 2026-08-27: proyecto de un solo desarrollador, sin pull requests ni revisores, y la verificación se hace en local. Se descartan GitHub Actions, los workflows y las comprobaciones obligatorias de fusión. **La contrapartida hay que asumirla explícitamente:** la CI existía para detectar que las suites se ponen en rojo, y eso ya había ocurrido —18 pruebas llevaban tiempo fallando sin que nadie lo notara— precisamente porque nada las ejecutaba salvo la voluntad de hacerlo. Con la decisión tomada, la única red que queda es la rutina de verificación local previa al commit documentada en `CONTEXT.md`. No volver a proponer CI salvo que entre otra persona al proyecto. |
| **No unificar los dos repositorios en un monorepo** | Decidido por el propietario el 2026-08-27 al resolver T0-03: se mantienen `TrackerMultimedia_Backend` y `TrackerMultimedia_Frontend` como repositorios independientes. La carpeta que los contiene en local **no** es un repositorio y no debe volver a comportarse como si lo fuera: nada que deba sobrevivir puede quedarse en su raíz. Consecuencia directa: los blueprints de despliegue usan rutas relativas a la raíz de su propio repositorio, y la suite de pruebas del backend vive dentro del repositorio de backend. |
| **No subir `Microsoft.OpenApi` a la rama 3.x** | Probado el 2026-08-27: la 3.10.2 corrige el aviso de seguridad pero rompe la compilación, porque el generador de código de `Microsoft.AspNetCore.OpenApi` 10.0.7 asigna a `IOpenApiMediaType.Example`, que en la 3.x es de solo lectura (`error CS0200`). La versión válida es **2.12.2**, que corrige el aviso y compila. |
