# TrackerMultimedia — carpeta de trabajo

> ⚠️ **Esta carpeta no es un repositorio.** Es solo el contenedor local donde conviven
> los dos repositorios reales del proyecto. Nada de lo que hay aquí se sube a ningún
> sitio salvo que lo muevas dentro de uno de ellos.

## Los dos repositorios

| Carpeta | Repositorio | Despliegue |
|---|---|---|
| `TrackerMultimedia_Backend/` | `xfiberex/TrackerMultimedia_Backend` | Render (`render.yaml` en su raíz) |
| `TrackerMultimedia_Frontend/` | `xfiberex/TrackerMultimedia_Frontend` | Netlify (`netlify.toml` en su raíz) |

La base de datos es PostgreSQL en Neon, provisionada fuera de ambos repositorios.

Cada repositorio se clona por separado y es autosuficiente: la suite de pruebas del
backend vive dentro del repositorio de backend, y cada blueprint de despliegue vive en
la raíz del repositorio al que describe. Las instrucciones de instalación, secretos y
despliegue están en el `README.md` de cada uno; este archivo no las duplica a propósito.

## Los documentos de esta carpeta

Cubren los dos repositorios a la vez, así que no pertenecen del todo a ninguno y de
momento viven aquí, **sin control de versiones**:

- [`ROADMAP.md`](ROADMAP.md) — qué falta por hacer y en qué orden.
- [`CHANGELOG.md`](CHANGELOG.md) — qué cambió en cada versión.
- [`CONTEXT.md`](CONTEXT.md) — por qué el proyecto es como es.

Que estén fuera de git significa que no hay copia de seguridad ni historial de sus
cambios. Está pendiente decidir dónde acaban.
