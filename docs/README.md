# Documentación de TrackerMultimedia

Estos documentos cubren **los dos repositorios del proyecto**, backend y frontend, y viven
versionados aquí, en `docs/` del repositorio de backend. Antes estaban sueltos en la carpeta
que contiene ambos clones, que no es un repositorio: no tenían copia de seguridad ni historial.
Se movieron dentro el **2026-09-04**.

> Consecuencia práctica: **se editan desde el repositorio de backend y se suben con él.**
> El repositorio de frontend no tiene copia; su `README.md` cubre solo su instalación y sus comandos.

## Qué hay aquí

| Archivo | Responde a |
|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | Qué es el producto, cómo está organizado y qué deliberadamente no hace |
| [DECISIONS.md](DECISIONS.md) | Qué se decidió y por qué; y qué se descartó, para no volver a proponerlo |
| [PITFALLS.md](PITFALLS.md) | Trampas del stack aprendidas a base de fallo. Léelo antes de depurar algo raro |
| [WORKFLOW.md](WORKFLOW.md) | Comandos, base de datos, secretos y la verificación obligatoria antes de cada commit |
| [ROADMAP.md](ROADMAP.md) | Qué falta por hacer |
| [CHANGELOG.md](CHANGELOG.md) | Qué cambió en cada versión |
| [HISTORY.md](HISTORY.md) | Registro de sesiones de trabajo, en orden cronológico |
| `openapi.json` | Contrato de la API (30 rutas), regenerable con el comando del README del backend |

El **onboarding** —requisitos, secretos, arranque, despliegue— no está aquí a propósito: vive
en el [`README.md`](../README.md) del backend y en el del frontend, cada uno con lo suyo.

## Reglas de mantenimiento

- Se actualizan **en el mismo commit** que el cambio que documentan, para que el contexto viaje con el código.
- **Las fechas son siempre absolutas.** Nunca «ayer» ni «hace poco».
- Una decisión revertida **no se borra**: se marca como superada, con la fecha y el hecho que la cambió.
- El **qué** va al changelog; el **por qué**, a `DECISIONS.md`; lo que costó descubrirlo, a `PITFALLS.md`.
- Las rutas son relativas a la raíz del repositorio de backend. Lo del otro repositorio se
  prefija con `Frontend/`.

## Estado del proyecto — 2026-09-04

| | |
|---|---|
| **Alcance** | Uso **personal y local**. No hay servicio público ni usuarios ajenos al propietario. |
| **Repositorios** | [xfiberex/TrackerMultimedia_Backend](https://github.com/xfiberex/TrackerMultimedia_Backend) y [xfiberex/TrackerMultimedia_Frontend](https://github.com/xfiberex/TrackerMultimedia_Frontend), independientes. La carpeta local que los contiene no es un repositorio. |
| **Versión publicada** | Ninguna etiquetada. No hay tags ni releases en ninguno de los dos. |
| **Stack backend** | .NET 10 (`net10.0`), ASP.NET Core, Identity, JWT Bearer, EF Core 10 + Npgsql 10, MailKit 4.16 |
| **Stack frontend** | React 19.2, TypeScript ~6.0, Vite 8, React Router 7, TanStack Query 5, Axios, Zod 4, CSS propio |
| **Base de datos** | PostgreSQL 17 **instalado en la máquina**, puerto **5433**. Servicio Windows `postgresql-x64-17`. |
| **Despliegue** | **Ninguno.** Render, Neon y Netlify deshabilitados el 2026-08-27. Los blueprints se conservan como receta para volver. |
| **Pruebas** | Backend **171/171** (xUnit + `WebApplicationFactory` sobre **PostgreSQL real**, base desechable por clase). Frontend **188/188** (Vitest + Testing Library). |
| **CI** | **No la habrá** (decisión del 2026-08-27). La red de seguridad es la rutina local de [WORKFLOW.md](WORKFLOW.md). |
| **Roadmap** | **79 de 88 tareas cerradas.** Cerrados los Tiers 0, 2 y 3; del Tier 1 solo queda T1-05. |
