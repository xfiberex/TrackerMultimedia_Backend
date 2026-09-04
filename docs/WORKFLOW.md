# Trabajo diario

> Comandos, entorno y la rutina obligatoria antes de cada commit.
> La instalación desde cero y el detalle de cada secreto están en el [`README.md`](../README.md)
> del backend y en el del frontend; aquí solo está lo que se usa a diario.

## Comandos

| Comando | Para qué | Requisitos |
|---|---|---|
| `Start-Service postgresql-x64-17` | Arranca la base de datos si su servicio está en *Manual* | PowerShell como administrador |
| `dotnet run` | Levanta la API en `http://localhost:5218` | Servicio de PostgreSQL arrancado y secretos en user-secrets |
| `dotnet user-secrets list` | Ver la configuración sensible de esta máquina | Ejecutar en la raíz del repositorio de backend |
| `dotnet ef database update` | Aplicar las migraciones pendientes a mano | `dotnet-ef` global. El arranque también las aplica desde el 2026-08-27 |
| `dotnet ef migrations add <Nombre>` | Crear una migración tras cambiar el modelo | **Nunca con `--no-build`**: genera migraciones vacías |
| `dotnet ef migrations list` | Ver qué migraciones existen y cuáles están aplicadas | La comprobación que destapó T0-06 |
| `dotnet ef database drop --force` | **Borra la base local entera.** Solo para comprobar el esquema desde cero | El servicio arrancado |
| `dotnet test TrackerMultimedia_Backend.slnx` | Suite del backend, 165 pruebas | **Necesita PostgreSQL en marcha** |
| `dotnet format whitespace --verify-no-changes` | Comprobar el estilo del `.editorconfig` | — |
| `npm run dev` (en `Frontend/`) | Interfaz en `http://localhost:5173`, **solo en este equipo** | `.env` copiado de `.env.example` |
| `npm run dev:lan` | Lo mismo, accesible desde el móvil u otro equipo de la red | — |
| `npm run test` | Suite del frontend, 165 pruebas | — |
| `npm run lint` | ESLint. **`npm run build` no lo ejecuta** | — |
| `npm run build` | Build de producción a `dist/`. Incluye `tsc -b` | — |
| `npx prettier --check .` | Estilo del frontend | — |

**Puertos:** backend `5218`, frontend `5173`, PostgreSQL `5433`.

## Base de datos

PostgreSQL 17 **instalado en la máquina**, no en contenedor. Escucha en el **puerto 5433**, no en
el 5432 por defecto: es el detalle que más veces se olvida al escribir una cadena de conexión. En
Windows es el servicio `postgresql-x64-17` y su arranque está en *Manual*, así que hay que
iniciarlo antes de levantar el backend.

El nombre de la base, `trackerMultimedia`, lleva mayúscula intercalada: en SQL va **siempre entre
comillas dobles**, porque PostgreSQL pasa a minúsculas todo identificador sin comillar.

**El servicio nativo escucha en `0.0.0.0:5433`**, es decir, en todas las interfaces, no solo en
loopback. En una red doméstica de confianza no es grave, pero conviene saberlo si alguna vez se
sirve la aplicación con `npm run dev:lan`: la base también es alcanzable desde la red, y su única
defensa es la contraseña del rol `postgres`.

*El 2026-08-27 la base pasó brevemente a Docker y se revirtió el mismo día, a petición del
propietario. No volver a proponer el cambio.*

## Secretos

Van **solo** en `dotnet user-secrets` desde el 2026-08-27. `appsettings.json` está versionado y
contiene únicamente valores por defecto no sensibles; `appsettings.Local.json` sigue existiendo,
no se versiona y tampoco contiene secretos: es configuración local no sensible.

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

---

## Verificación local antes de cada commit

No hay CI y no la va a haber, así que **esta rutina es la única red de seguridad del proyecto**.
Se ejecuta entera antes de cada commit, no cuando uno se acuerda: los cinco comandos juntos tardan
alrededor de un minuto.

En la raíz del repositorio de backend:

```bash
dotnet test TrackerMultimedia_Backend.slnx     # 165 pruebas. Debe decir "Con error: 0"
dotnet restore                                 # No debe emitir ningún NU1903
```

En `Frontend/`:

```bash
npm run lint                                   # Debe salir sin ningún error
npm run test -- --run                          # 165 pruebas
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
