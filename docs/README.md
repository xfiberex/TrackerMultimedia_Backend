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
| [CONTEXT.md](CONTEXT.md) | **Empieza por aquí.** Estado, decisiones vigentes y registro de sesión, con enlaces al resto |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Qué es el producto, cómo está organizado y qué deliberadamente no hace |
| [DECISIONS.md](DECISIONS.md) | Qué se decidió y por qué; y qué se descartó, para no volver a proponerlo |
| [PITFALLS.md](PITFALLS.md) | Trampas del stack aprendidas a base de fallo. Léelo antes de depurar algo raro |
| [WORKFLOW.md](WORKFLOW.md) | Comandos, base de datos, secretos y la verificación obligatoria antes de cada commit |
| [ROADMAP.md](ROADMAP.md) | Qué falta por hacer |
| [CHANGELOG.md](CHANGELOG.md) | Qué cambió en cada versión |
| [HISTORY.md](HISTORY.md) | Registro de sesiones de trabajo, en orden cronológico |
| `openapi.json` | Contrato de la API (27 rutas), regenerable con el comando del README del backend |

El **onboarding** —requisitos, secretos, arranque, despliegue— no está aquí a propósito: vive
en el [`README.md`](../README.md) del backend y en el del frontend, cada uno con lo suyo.

## Reglas de mantenimiento

- Se actualizan **en el mismo commit** que el cambio que documentan, para que el contexto viaje con el código.
- **Las fechas son siempre absolutas.** Nunca «ayer» ni «hace poco».
- Una decisión revertida **no se borra**: se marca como superada, con la fecha y el hecho que la cambió.
- El **qué** va al changelog; el **por qué**, a `DECISIONS.md`; lo que costó descubrirlo, a `PITFALLS.md`.
- Las rutas son relativas a la raíz del repositorio de backend. Lo del otro repositorio se
  prefija con `Frontend/`.

## Estado del proyecto

**Esta tabla se retiró el 2026-09-06.** Repetía cifras que ya viven —con su fecha— en
[CONTEXT.md](CONTEXT.md), y en su última versión las tres estaban caducadas a la vez: decía 171 y
188 pruebas cuando eran otras, «79 de 88 tareas» cuando el roadmap iba por otro sitio, y presentaba
los blueprints de despliegue como «receta para volver» cuando la base de Neon llevaba un día
eliminada.

**La causa no era descuido, era duplicación:** la misma cifra escrita en tres archivos se separa
sola. El estado vigente está en un único sitio, [CONTEXT.md](CONTEXT.md), y desde aquí se enlaza en
vez de copiarse. Es la mitad de T6-17 que se puede cerrar sin tocar código.
