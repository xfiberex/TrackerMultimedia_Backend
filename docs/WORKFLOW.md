# Trabajo diario

> Comandos, entorno y la rutina obligatoria antes de cada commit.
> La instalación desde cero y el detalle de cada secreto están en el [`README.md`](../README.md)
> del backend y en el del frontend; aquí solo está lo que se usa a diario.

## Comandos

| Comando | Para qué | Requisitos |
|---|---|---|
| `Start-Service postgresql-x64-17` | Arranca la base de datos si su servicio está en *Manual* | PowerShell como administrador |
| `netstat -an \| grep 543` | Ver en qué puerto escucha PostgreSQL en **este** equipo | — |
| `dotnet run` | Levanta la API en `http://localhost:5218` | Servicio de PostgreSQL arrancado y secretos en user-secrets |
| `dotnet user-secrets list` | Ver la configuración sensible de esta máquina | Ejecutar en la raíz del repositorio de backend |
| `dotnet ef database update` | Aplicar las migraciones pendientes a mano | `dotnet-ef` global. El arranque también las aplica desde el 2026-08-27 |
| `dotnet ef migrations add <Nombre>` | Crear una migración tras cambiar el modelo | **Nunca con `--no-build`**: genera migraciones vacías |
| `dotnet ef migrations list` | Ver qué migraciones existen y cuáles están aplicadas | La comprobación que destapó T0-06 |
| `dotnet ef database drop --force` | **Borra la base local entera.** Solo para comprobar el esquema desde cero | El servicio arrancado |
| `dotnet test TrackerMultimedia_Backend.slnx` | Suite del backend, 171 pruebas | **Necesita PostgreSQL en marcha** |
| `dotnet format whitespace --verify-no-changes` | Comprobar el estilo del `.editorconfig` | — |
| `npm run dev` (en `Frontend/`) | Interfaz en `http://localhost:5173`, **solo en este equipo** | `.env` copiado de `.env.example` |
| `npm run dev:lan` | Lo mismo, accesible desde el móvil u otro equipo de la red | — |
| `npm run test` | Suite del frontend, 205 pruebas | — |
| `npm run test:e2e` | Suite end-to-end, 15 pruebas con Playwright | **PostgreSQL y el backend en marcha**; ver abajo |
| `npm run test:e2e:ui` | Lo mismo, en el modo interactivo de Playwright | Lo mismo |
| `npm run lint` | ESLint. **`npm run build` no lo ejecuta** | — |
| `npm run build` | Build de producción a `dist/`. Incluye `tsc -b` | — |
| `npx prettier --check .` | Estilo del frontend | — |

**Puertos:** backend `5218`, frontend `5173`. El de PostgreSQL cambia según el equipo: ver abajo.

## Base de datos

PostgreSQL 17 **instalado en la máquina**, no en contenedor. En Windows es el servicio
`postgresql-x64-17`, y hay que tenerlo arrancado antes de levantar el backend.

**El puerto depende del equipo, así que compruébalo en vez de suponerlo:** `netstat -an | grep 543`.
El equipo original usaba el **5433**; el actual, el **5432** por defecto. Esta documentación decía
5433 sin matices hasta el 2026-09-04, y es el detalle que más veces se ha escrito mal en una cadena
de conexión.

El nombre de la base, `trackerMultimedia`, lleva mayúscula intercalada: en SQL va **siempre entre
comillas dobles**, porque PostgreSQL pasa a minúsculas todo identificador sin comillar.

**El servicio nativo escucha en `0.0.0.0`**, es decir, en todas las interfaces, no solo en
loopback. En una red doméstica de confianza no es grave, pero conviene saberlo si alguna vez se
sirve la aplicación con `npm run dev:lan`: la base también es alcanzable desde la red, y su única
defensa es la contraseña del rol `postgres`.

*El 2026-08-27 la base pasó brevemente a Docker y se revirtió el mismo día, a petición del
propietario. No volver a proponer el cambio.*

## Secretos

Van **solo** en `dotnet user-secrets` desde el 2026-08-27. `appsettings.json` está versionado y
contiene únicamente valores por defecto no sensibles; `appsettings.Local.json` no se versiona y
tampoco debería contener secretos: es configuración local no sensible.

En este equipo se cumple desde el 2026-09-04. Al mover el proyecto de máquina los user-secrets no
viajaron —viven fuera del repositorio, que es justamente su razón de ser— y todo acabó en
`appsettings.Local.json`: `Jwt:Secret`, `Smtp:Password`, los *client secret* de Google y GitHub y una
cadena de conexión a Neon. Ya está corregido: los ocho secretos están en user-secrets y el archivo
local solo contiene configuración no sensible.

> ⚠️ **Al cambiar de equipo, los secretos se vuelven a poner a mano.** No hay ningún mecanismo que
> los transporte, y ese es el precio de que no estén en el repositorio. La plantilla
> `appsettings.Local.example.json` dice qué claves hacen falta; los valores salen del gestor de
> contraseñas o se regeneran. **Copiarlos a `appsettings.Local.json` "hasta que haya tiempo" es
> exactamente cómo ocurrió la vez anterior.**

De paso se eliminaron dos restos que solo existían en esta máquina: `Security:ApiKey`, que T3-02 dio
por eliminada y ningún código lee desde entonces, y una carpeta `artifacts/` de mayo con tres copias
en claro de los mismos secretos. Ambas estaban ignoradas por git, así que nunca llegaron a un commit.

> ✅ **La credencial de Neon estuvo en claro en esta máquina y ya está revocada.** El 2026-09-05 se
> eliminó la base entera y se revocó y borró todo lo desplegado (T1-13). Sacarla del repositorio no
> la invalidaba: entre el 2026-08-27 y esa fecha, la documentación la daba por revocada mientras el
> rol `neondb_owner` seguía abierto. La lección de fondo está en [PITFALLS.md](PITFALLS.md).

**La suite de tests no lee `appsettings.Local.json`.** Solo mira `TRACKERMULTIMEDIA_TEST_POSTGRES`
y los user-secrets. Sin ninguno de los dos no arranca, con un mensaje que lo explica.

**user-secrets no cifra nada.** Guarda un `secrets.json` en claro en
`%APPDATA%\Microsoft\UserSecrets\`. Lo que aporta es que ese archivo vive fuera de la carpeta del
repositorio, así que no puede colarse en un commit ni en el contexto de build de Docker. Frente a
quien tenga acceso a la sesión de Windows, no protege.

## Pruebas del backend

Cada clase de test recibe **su propia base desechable**. Las migraciones se aplican una vez por
ejecución sobre una plantilla y cada clase la copia con `CREATE DATABASE ... TEMPLATE`. Al
terminar, cada base se borra; los restos de una ejecución cancelada los limpia la siguiente.

La cadena de conexión sale de `TRACKERMULTIMEDIA_TEST_POSTGRES` o, si no está definida, de los
mismos user-secrets que usa la aplicación. **La base de la aplicación no se toca**: de esa cadena
solo se reutilizan el servidor y las credenciales.

Para cobertura, ver la sección *Cobertura* del README del backend. **Mira la cobertura de ramas,
no la de líneas:** la primera medición dio 82,8 % de líneas y 48,4 % de ramas, y lo que enseñó no
fue el porcentaje sino qué estaba a cero.

## Pruebas end-to-end (T4-04)

Hablan con **la aplicación entera**: navegador real, Vite, backend y PostgreSQL. No
sustituyen a las otras dos suites; cubren lo que ninguna ve, que es que las piezas encajen.

**Playwright levanta el servidor de Vite, pero no el backend ni la base**, a propósito: un
fallo suyo saldría como un timeout críptico en vez de como el error que de verdad ocurrió.
Los comprueba al arrancar y, si faltan, lo dice con lo que hay que hacer.

```bash
# 1. PostgreSQL, si su servicio está en Manual (PowerShell como administrador)
Start-Service postgresql-x64-17

# 2. El backend, con el cupo de peticiones subido: la suite registra una cuenta por
#    prueba y todas llegan desde la misma dirección, así que con el cupo normal de 10
#    por minuto se agota a mitad de camino y los fallos salen como errores confusos.
cd TrackerMultimedia_Backend

# En bash:
RateLimiting__Auth__PermitLimit=500 dotnet run

# En PowerShell, la misma línea es un error de sintaxis; y **tiene que ser el mismo
# proceso** el que reciba la variable y arranque el backend. Lanzarlo aparte con
# `Start-Process` no se la lleva, el backend arranca con el cupo normal y las pruebas
# fallan a media suite como si el cambio hubiera roto algo:
$env:RateLimiting__Auth__PermitLimit = "500"; dotnet run

# 3. Las pruebas (en el repositorio de frontend)
npm run test:e2e
```

**Cada prueba estrena su cuenta**, con el sufijo `@e2e.test`. El registro exige confirmar
el correo (T2-10) y el correo va a un buzón de Mailtrap que no se puede leer desde ahí, así
que `e2e/db.ts` confirma la cuenta en la base directamente. **Es lo único para lo que se
toca la base**: lo que las pruebas afirman se afirma contra la interfaz o contra la API, y
una aserción que mirase la base dejaría de ser end-to-end.

La limpieza borra **solo** las cuentas con ese sufijo, y se hace **al empezar y no al
terminar**, para poder mirar el estado que dejó un fallo.

**Lo que no cubren, y por qué:** completar un flujo de Google o GitHub exigiría
autenticarse de verdad contra el proveedor, con una cuenta real y su segundo factor. Se
cubren los dos extremos que sí son nuestros —que la petición lleve el `state` y el reto de
PKCE, y que una vuelta con `state` inválido acabe en el login con aviso—.

## Leer el log

En desarrollo la salida es texto legible. Cada línea de una petición lleva su ámbito delante, y ahí
está el identificador que también recibe el usuario cuando algo falla:

```text
=> SpanId:d5ff15a5bea4e05a, TraceId:6ebc4753fd242bab1e03caa1b64eaf24, ParentId:0000000000000000
   => ConnectionId:0HNOATN8Q8LHV => RequestPath:/health/ready
```

**Si alguien reporta un error, pide el `traceId` de la respuesta** —32 caracteres hexadecimales— y
búscalo tal cual: aparece en *todas* las líneas de esa petición, no solo en la del error. Ojo con no
confundirlo con `RequestId`, que sale al lado, tiene la forma `0HNOATN8Q8LHV:00000001` y es otra
cosa: usar ese era precisamente el fallo que corrigió T4-11.

Fuera de desarrollo la salida es una línea JSON por evento, y el `TraceId` es un campo con nombre
dentro de `Scopes`, así que se filtra sin expresiones regulares:

```bash
jq 'select(.Scopes[]?.TraceId == "6ebc4753fd242bab1e03caa1b64eaf24")' registro.log
```

**Métricas:** no hay ninguna propia y es deliberado mientras el uso sea local (T4-06, en suspenso).
Los medidores que ya publican ASP.NET Core, EF Core y el runtime se leen en vivo sin tocar el código:

```bash
dotnet-counters monitor -n TrackerMultimedia --counters Microsoft.AspNetCore.Hosting
```

---

## Textos de la interfaz (T4-03)

La interfaz está en **español e inglés**. Ningún texto visible se escribe en un componente: todos
viven en `src/shared/i18n/es.ts` y `src/shared/i18n/en.ts`, y se pintan con `t('seccion.clave')`.

### Añadir un texto

1. Añade la clave en `es.ts`, dentro de la sección que le corresponda.
2. Añade **la misma clave** en `en.ts`. Si se te olvida, `tsc -b` falla: `en.ts` está tipado a
   partir de `es.ts` y no admite que falte ni que sobre ninguna.
3. Úsala con `t('seccion.clave')`. El editor autocompleta y una clave mal escrita no compila.

**Si el texto lleva un dato dentro**, va con `{{marcador}}` en los dos idiomas:

```ts
// es.ts
borradoPideCorreo: 'Escribe {{correo}} para continuar',
// en el componente
t('perfil.borradoPideCorreo', { correo: user.email })
```

Los valores interpolados tienen que ser `string`; el tipado rechaza un `number` o un
`string | null`, que es como se descubrió que una de esas frases podía llegar a decir
«Escribe null para continuar».

**Si el texto lleva una cuenta**, usa las dos formas de plural y deja que i18next elija:

```ts
registros_one: '{{count}} registro',
registros_other: '{{count}} registros',
// en el componente
t('biblioteca.registros', { count: total })
```

**Si el texto lleva marcado dentro** —una palabra en negrita, un enlace—, se usa `<Trans>` en vez
de partir la frase en trozos. Partirla ata el orden de las palabras al español.

### Añadir un idioma

1. Añádelo a `IDIOMAS` y a `NOMBRE_DE_IDIOMA` en `src/shared/i18n/idiomas.ts`.
2. Crea su archivo de textos copiando el tipo de `en.ts` (`typeof es`).
3. Regístralo en `resources`, en `src/shared/i18n/index.ts`.

`LanguageToggle` es un interruptor de dos posiciones porque hay dos idiomas; con un tercero hay que
cambiarlo por un desplegable.

### Lo que vigilan las pruebas

`src/config/i18n.test.ts` comprueba tres cosas que el compilador **no** puede ver:

- que las interpolaciones sobrevivan a la traducción —una frase inglesa que pierda su `{{correo}}`
  compila igual y llega al usuario sin el dato—;
- que toda clave con plural tenga sus dos formas en los dos idiomas;
- que no quede texto incrustado en ningún componente. Esta última encontró seis textos que la
  propia migración se había dejado, así que no es teórica.

Hay dos excepciones declaradas, y cada una explica por qué: `TrackerMultimedia`, que es una marca,
y `#336699`, que es un ejemplo de color. Añadir una tercera exige poder explicarla igual.

**No pasa por el diccionario, a propósito:** el mensaje de error de `src/config/env.ts`. Se pinta
cuando las variables de entorno no son válidas, es decir, antes de que i18n exista, y habla con
quien configura el proyecto, no con quien lo usa.

---

## Verificación local antes de cada commit

No hay CI y no la va a haber, así que **esta rutina es la única red de seguridad del proyecto**.
Se ejecuta entera antes de cada commit, no cuando uno se acuerda: los cinco comandos juntos tardan
alrededor de un minuto.

En la raíz del repositorio de backend:

```bash
dotnet test TrackerMultimedia_Backend.slnx     # 182 pruebas. Debe decir "Con error: 0"
dotnet restore                                 # No debe emitir ningún NU1903
```

En `Frontend/`:

```bash
npm run lint                                   # Debe salir sin ningún error
npm run test -- --run                          # 205 pruebas
npm run build                                  # Incluye tsc -b; falla si hay error de tipos
```

Ninguno de los cinco sobra, y hay dos razones concretas para ello:

- **`npm run build` no ejecuta el linter.** Solo hace `tsc -b && vite build`, así que un error de
  ESLint llega a producción sin que el build se queje (T2-23).
- **`tsc -b` no ejecuta los tests, y los tests no comprueban los tipos.** Un test puede pasar con
  un error de tipos delante, y al revés.

### Y una comprobación que no es diaria

Cada vez que se toca el modelo o una migración, **recrear la base desde cero**:

```bash
dotnet ef database drop --force && dotnet ef database update
```

Eso **borra los datos locales**; si hay algo que conservar, la comprobación se hace sobre una base
aparte, como explica el README del backend. Es lo único que detecta un esquema incompleto, y así
apareció T0-06 —un fallo crítico que llevaba meses invisible— después de meses sin que nadie
construyera el esquema desde cero.

### Los dos repositorios se clonan por separado

No hay ningún comando que opere sobre los dos a la vez, y ninguna ruta puede cruzar de uno a otro:
lo que esté fuera de `Backend/` o de `Frontend/` no existe para nadie que clone. Esta
documentación se subió al repositorio de backend justamente por eso.
