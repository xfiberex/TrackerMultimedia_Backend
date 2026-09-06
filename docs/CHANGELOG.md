# Changelog

Todos los cambios notables de TrackerMultimedia se documentan en este archivo.

El formato sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y el proyecto se adhiere a [Versionado Semántico](https://semver.org/lang/es/).

> **Por qué** se tomó cada decisión: [DECISIONS.md](DECISIONS.md).
> **Qué queda pendiente**: [ROADMAP.md](ROADMAP.md).
> **Cómo se llegó hasta aquí**: [HISTORY.md](HISTORY.md).

> ⚠️ **Reconstrucción aproximada.** Ninguno de los dos repositorios tiene etiquetas de git ni releases publicados, así que las versiones anteriores a `[Sin publicar]` se han reconstruido a partir de los mensajes y fechas de commit (13 en el backend y 15 en el frontend, del 2026-05-07 al 2026-05-23). Las fechas son fiables; el reparto por versión es una interpretación. A partir de ahora, cada corte de versión debe etiquetarse en git y anotarse aquí en el momento de publicarse.

---

## [Sin publicar]

### Añadido

- **La aplicación habla español e inglés.** El interruptor está en la cabecera, junto al del tema, y también en las pantallas de acceso: quien no lee español tiene que poder cambiar de idioma **antes** de entrar, no después. El cambio es inmediato, no recarga la página y no pierde lo que tengas a medio escribir. La elección se recuerda en el navegador; la primera vez se parte del idioma que ya tenga configurado. Las fechas siguen al idioma elegido, así que una pantalla en inglés no muestra «05 sept 2026» (T4-03).

- **Ya te puedes descargar todos tus datos.** Desde el perfil, un botón entrega un archivo con todo lo que se guarda de ti: los datos de tu cuenta, las cuentas de Google o GitHub que tengas vinculadas, tus sesiones abiertas, tus formatos y tu biblioteca completa con sus categorías. No incluye contraseñas ni claves de sesión. La descarga de la biblioteca sigue estando donde estaba y sirve para otra cosa: es la que se puede volver a importar (T4-08).
- **Ya se puede borrar la cuenta.** Desde el perfil, escribiendo la contraseña —o tu propia dirección de correo, si entraste con Google o GitHub y no tienes ninguna— y confirmando después en un aviso. Se borra todo: biblioteca, categorías, formatos y sesiones. No hay forma de recuperarlo (T1-14).
- **El proyecto tiene licencia: MIT.** Hasta ahora estaba publicado en GitHub sin ninguna, lo que significa «todos los derechos reservados» y que nadie podía usarlo ni contribuir legalmente (T1-15).

- Los botones de «Continuar con Google» y «Continuar con GitHub» se pueden ocultar desde la configuración del frontend, para entrar con correo y contraseña. Sirve sobre todo al abrir la aplicación desde el móvil: Google solo sabe volver a `localhost` o a un dominio público, así que desde otro dispositivo ese camino termina en un error. Ocultarlos es solo una máscara de la interfaz —quien decide qué proveedores se aceptan sigue siendo el servidor—.

### Corregido

- **Un archivo que no es una copia de tu biblioteca ya no se importa «sin cambios».** Al elegir un JSON cualquiera, la aplicación respondía «Importación JSON completada sin cambios» con el tono de haber ido bien, así que quien se equivocara de archivo podía concluir que su copia de seguridad estaba vacía. Ahora avisa de que ese archivo no es una exportación de TrackerMultimedia y explica qué hacer. Una exportación de verdad que no traiga nada nuevo —una biblioteca vacía, o una que ya estaba entera— sigue siendo un éxito sin cambios, que es lo que es (T2-29).
- **Los avisos de error del servidor ya se leen.** Todos los mensajes de validación que escribe el backend —el tamaño máximo de un archivo, una categoría sin nombre, un progreso negativo— llegaban a la pantalla convertidos en «One or more validation errors occurred.», la frase genérica y en inglés que acompaña siempre a esas respuestas. La aplicación la leía antes que el mensaje de verdad, así que ninguno de esos textos se había visto nunca. Salió a la luz al cerrar T2-29: su aviso nuevo habría muerto ahí (T2-29).
- **«1 resultado listos para importar» ya concuerda.** El singular estaba resuelto a medias en varios contadores —se cambiaba el sustantivo pero no el adjetivo—, y aparecía en Descubrir, en Biblioteca, en los filtros y en Categorías. Ahora la concordancia la decide la capa de idiomas, que también sabe hacerla en inglés (T4-03).
- **El campo para confirmar el borrado de la cuenta se veía distinto a todos los demás.** Era el único campo de la aplicación sin el estilo del sistema de diseño, así que el navegador pintaba el suyo propio; y el botón de debajo quedaba pegado al campo. Es el mismo bloque donde T5-09 ya había corregido otro problema de presentación (T5-10).
- **El atajo «Saltar al contenido» no se leía en modo oscuro.** Es el enlace que aparece al pulsar el tabulador para ir directo al contenido sin recorrer la cabecera. Salía en gris claro sobre blanco: 1,48 veces de contraste. Quien lo usa es justamente quien navega con el teclado (T5-06).
- **La pantalla de inicio de sesión era casi ilegible en modo oscuro.** El nombre de la aplicación, el título «Iniciar sesión», los tres enlaces del pie y el mensaje de error salían en azul marino casi negro sobre una tarjeta azul marino, y el botón de acceso no se distinguía del fondo de la tarjeta. La pantalla estaba escrita entera con los colores del tema claro: lo que sí se veía era por casualidad, no por diseño (T5-05).
- **El botón de borrar no se leía en modo oscuro.** Su texto estaba en el mismo tono rojo que usa la etiqueta «Abandonado», pensado para fondo claro, sobre el fondo oscuro del propio botón: **2,47 veces** de contraste, cuando el mínimo accesible es 4,5. Era, de todos los controles, el que menos convenía que costara leer (T5-01).
- **Las cifras ya no bailan al recorrer la lista.** El progreso, el año y la puntuación se leen en columna, pero cada dígito ocupaba un ancho distinto, así que los números no quedaban alineados entre filas. Ahora sí (T5-02).
- **El texto gris claro ya se lee.** Buena parte de los textos secundarios de la aplicación —descripciones, etiquetas opcionales, pies de formulario— estaban en un gris demasiado claro sobre el fondo: **1,96 veces** de contraste cuando el mínimo accesible es 4,5. Con buena luz y buena vista pasaba; con reflejos en la pantalla, un monitor mal calibrado o simplemente vista cansada, no. Ahora todos los colores de texto superan ese mínimo en los dos temas (T1-23).
- **La pestaña activa del catálogo, en modo oscuro, no se distinguía de las demás.** Estaba pintada con un color pensado para fondos, no para texto: 1,41 de contraste, prácticamente invisible. No había forma de saber en qué pestaña estabas (T1-23).
- **El identificador de los errores no servía para nada.** Cuando algo falla, la respuesta trae un código para poder relacionar lo que viste con lo que quedó registrado (T2-05). Resulta que el código que se mostraba en pantalla y el que se escribía en el registro **eran dos distintos**, así que buscar el primero no encontraba nunca el segundo: la función existía desde hacía meses y no había funcionado ni una vez. Ahora es el mismo valor, y además queda marcado en todas las líneas de esa petición, no solo en la del error (T4-11).
- **Si un catálogo externo se caía, la búsqueda devolvía menos resultados sin decir nada.** Buscar consulta varios catálogos a la vez y está pensado para que la caída de uno no estropee la búsqueda entera, lo cual sigue igual. Lo que no ocurría era dejar constancia: el resultado incompleto era indistinguible de uno completo, y podía estar así semanas sin que nadie se enterara. Ahora cada fallo queda registrado con el catálogo concreto (T4-11).
- **La sesión dejaba de valer sola tras recargar la página.** Al arrancar, la aplicación pedía dos veces a la vez la renovación de la sesión, y el servidor interpretaba la segunda como que alguien había copiado la sesión: cerraba todas por seguridad. No se veía en el momento —la pantalla seguía funcionando— sino en la recarga siguiente, que devolvía al inicio de sesión sin explicación. El defecto era anterior y no se había detectado nunca (T4-01).
- La biblioteca vuelve a funcionar en instalaciones nuevas. Una migración mal creada hacía que a la tabla de elementos le faltara la columna del formato, así que cualquier base de datos creada desde cero devolvía un error del servidor al abrir la biblioteca. El fallo no se veía en la base ya existente, solo al desplegar de nuevo o al empezar de cero (T0-06).
- El perfil ya muestra las cuentas de Google y GitHub vinculadas. Antes la lista aparecía siempre vacía aunque la vinculación hubiera funcionado (T1-01, T1-02).
- Registrarse ya no falla cuando el servidor de correo no responde. Antes la cuenta se creaba igualmente pero la pantalla mostraba un error, y al reintentar decía que el correo ya existía: se quedaba sin poder entrar ni recibir la confirmación (T1-03).
- La biblioteca y el inicio de sesión responden con normalidad al crecer la base de datos: faltaban dos índices y cada consulta recorría la tabla entera (T1-06, T1-07).
- Se puede saltar directamente al contenido con el teclado, sin recorrer antes toda la cabecera en cada página (T1-18).
- Los grupos de categorías, colores y proveedores ya se anuncian con su nombre en los lectores de pantalla; antes eran listas de casillas sin título (T1-17).
- El enlace que abre la ficha externa de un resultado de búsqueda ya dice a dónde lleva. Antes se anunciaba solo como «enlace» (T1-22).
- Los paneles y las ventanas de confirmación ya se pueden usar con el teclado. Al abrirse, el cursor entra en el panel; el tabulador se queda dentro en lugar de irse al listado del fondo; y al cerrarlo, el cursor vuelve al botón desde el que se abrió. Antes había que buscarlo a ciegas (T1-16).
- En los avisos que borran algo, la opción preseleccionada pasa a ser «Cancelar». Antes lo estaba el botón de borrar, así que pulsar Intro sin leer bastaba para perder el elemento (T1-16).
- Un fallo de la interfaz ya no deja la pantalla en blanco: aparece un aviso con la opción de recargar o volver al inicio, y con la garantía de que no se ha perdido nada de lo guardado (T2-21).
- Cuando algo falla en el servidor, la respuesta incluye un identificador. Sirve para relacionar lo que vio el usuario con lo que quedó registrado, en vez de un error vacío (T2-05).
- Importar un archivo con más de 5.000 elementos avisa en lugar de intentarlo y agotar la memoria (T2-17).
- Entrar con Google o GitHub ya no falla con un error del servidor cuando el enlace de acceso ha caducado, que es lo que pasa si tardas en completar el paso o vuelves atrás en el navegador. Ahora te devuelve a la pantalla de inicio de sesión con un aviso. Lo mismo con cualquier corte de red mientras se habla con el proveedor (T2-06).
- Guardar un elemento de AniList o MangaDex sin identificador ya avisa. Solo se comprobaba en los de MyAnimeList, así que los de los otros dos catálogos se guardaban a medias y además esquivaban la comprobación de duplicados (T2-18).
- Guardar un elemento con un formato que no existe o que es de otra cuenta ya avisa. Antes se guardaba sin formato y la pantalla decía que había ido bien: el dato se perdía en silencio (T2-19).
- Los formatos por defecto se crean al darse de alta, no la primera vez que se abre la pantalla de formatos. Con dos pestañas abiertas a la vez, la segunda daba error del servidor (T2-20).
- La búsqueda en catálogos externos ya no falla entera cuando MangaDex devuelve un elemento con el identificador mal formado. Antes, uno solo defectuoso se llevaba por delante todos los resultados buenos de la misma respuesta (T4-09).
- Los avisos de error del servidor están todos en español. Quedaban varios en inglés mezclados con el resto, entre ellos el que ves cuando falla la búsqueda en catálogos externos (T2-26).
- Los paneles ya no parpadean al cerrarse. El formulario se reiniciaba durante la fracción de segundo que dura la animación de salida (T1-16).
- Quien tiene activado el ajuste de movimiento reducido del sistema deja de ver también los desplazamientos y fundidos, no solo las animaciones (T2-24).

### Cambiado

- **La aplicación pasa a usarse solo en local.** Los servicios de Render, Neon y Netlify quedan deshabilitados. La interfaz se abre con el servidor de desarrollo de Vite y la base de datos es el PostgreSQL instalado en el equipo. Los archivos de despliegue se conservan por si algún día vuelve a publicarse, marcados como inactivos.
- La dirección del backend se configura ahora con una ruta relativa (`/api`) en lugar de una URL fija a `localhost`. Gracias a eso, la aplicación se puede abrir desde el móvil o desde otro ordenador de la misma red sin cambiar nada, con `npm run dev:lan`.

- `npm run dev` vuelve a servir la aplicación **solo en este equipo**. El script llevaba `vite --host`, que la dejaba accesible a cualquiera conectado al mismo router en cada arranque, sin pedirlo y sin avisar.
- Nuevos scripts `npm run dev:lan` y `npm run preview:lan` para abrir la aplicación desde el móvil u otro ordenador de la misma red. Es lo mismo que antes hacía `dev` sin decirlo, pero ahora se ve en la orden que se escribe.

- **Los espaciados de toda la interfaz se han igualado.** Márgenes, separaciones, redondeos y tamaños de letra estaban escritos uno a uno por toda la hoja de estilos: había 39 espaciados distintos, 9 redondeos y 19 tamaños de texto, varios de ellos separados por menos de un píxel, es decir, imposibles de distinguir. Ahora salen todos de una escala común. **Lo que se nota es poco y a propósito:** ningún elemento se mueve más de 2 píxeles, y dos de cada tres no se mueven nada. Lo que gana es que a partir de ahora una pantalla nueva se parece a las demás sin que nadie tenga que acordarse de con qué medidas (T5-08).
- Las fechas se muestran con el formato del dispositivo de quien mira, en vez de estar fijadas a la configuración de República Dominicana (T4-03).

### Seguridad

- **Entrar con Google es más difícil de interceptar.** Al iniciar el proceso, el servidor se guarda un secreto de un solo uso y solo manda su huella; al terminar, tiene que enseñar el secreto. Así, si alguien lograra hacerse con el código que devuelve Google a medio camino, no le serviría de nada. Con GitHub queda desactivado a la espera de confirmar que su sistema lo admite (T4-02).
- **El freno a los intentos de acceso ya no se puede esquivar.** El servidor limita a 10 los intentos de entrar por minuto, para que nadie pueda ir probando contraseñas. Ese freno se apoya en saber de qué dispositivo viene cada intento, y hasta ahora el propio atacante podía decirle al servidor que cada intento venía de un sitio distinto: el contador se reiniciaba en cada vuelta y no llegaba a saltar nunca. Ahora esa información solo se acepta de servidores intermedios declarados de antemano, y en el uso local y por LAN no se acepta de nadie (T1-05).
- **La sesión guardada deja de ser accesible desde la propia página.** El identificador de larga vida que mantiene la sesión entre recargas pasa a una cookie que el navegador guarda y envía él solo, y que el código de la página no puede leer. Antes vivía en un almacén que cualquier script de la página podía consultar: bastaba una inyección para llevarse la sesión y seguir usándola durante días desde otro sitio. **Efecto al actualizar: se cierran todas las sesiones abiertas y hay que volver a entrar una vez** (T4-01).
- **Entrar con Google o GitHub ya no deja ningún dato de sesión en la barra de direcciones.** Antes la dirección de vuelta llevaba las credenciales colgando, y de ahí pasaban al historial del navegador y a cualquier sitio donde se pegara ese enlace. Ahora solo lleva a qué pantalla volver. Se nota en que entrar tarda una fracción de segundo más (T4-01).
- La política de seguridad del navegador deja de permitir conexiones a tres servicios externos con los que la aplicación nunca habla —los consulta el servidor— y a un dominio de despliegue fijado a mano. Añadidas además las cabeceras que impiden que la aplicación se incruste en otra página (T2-14).

- Actualizadas las dependencias con avisos de seguridad conocidos: de 13 (uno crítico) a ninguno. Ningún cambio de versión mayor, así que no cambia nada de cómo funciona la aplicación (T3-21).

- Ya no se puede averiguar qué direcciones de correo tienen cuenta. El registro y el inicio de sesión responden ahora siempre lo mismo, exista la cuenta o no, esté bloqueada o sin confirmar. Si alguien intenta registrarse con un correo ya dado de alta, el aviso se envía al titular de esa dirección en lugar de mostrarse a quien hizo la petición (T1-04).
- Actualizadas dos dependencias con vulnerabilidades conocidas de severidad alta (T0-04).
- Rotadas todas las credenciales del proyecto y deshabilitados los servicios a los que daban acceso (T1-13).
- Si alguien copia la sesión de otra persona y la usa, ahora se detecta: en cuanto se reutiliza una sesión ya renovada, se cierran **todas** las del titular y hay que volver a entrar con la contraseña (T2-12).
- Bloquear una cuenta o dejarla sin confirmar surte efecto de inmediato. Antes seguía renovando su sesión hasta que caducara, y podía entrar con Google o GitHub aunque estuviera bloqueada para la contraseña (T2-10, T2-12).
- La página de inicio de sesión ya no muestra el texto que le pongan en la dirección web. Bastaba con enviar un enlace preparado para que apareciera cualquier mensaje —por ejemplo un aviso falso con un teléfono— sobre el sitio auténtico (T2-27).
- Los detalles técnicos de un fallo con Google o GitHub dejan de aparecer en la barra de direcciones, y con ella en el historial del navegador (T2-11).
- El archivo CSV exportado ya no puede ejecutar nada al abrirlo en Excel. Un título como `=1+1` se interpretaba como fórmula (T2-09).
- Las sesiones cerradas y los intentos de inicio de sesión con Google o GitHub dejan de acumularse para siempre en la base de datos. Se borran solos: las sesiones caducadas a los 7 días, y los intentos de OAuth —que guardan el correo de la cuenta externa— al día siguiente (T1-21).
- El correo del usuario deja de aparecer entero en el registro del servidor. Donde hay cuenta se anota su identificador, y donde todavía no la hay —un alta que falla, un aviso enviado a alguien sin cuenta— se anota la dirección parcialmente tapada. El registro es un sitio con su propia conservación: una dirección completa ahí sobrevive al borrado de la cuenta (T2-15).
- Las credenciales de desarrollo salen de los archivos de configuración y pasan al almacén de secretos de .NET, fuera de la carpeta del proyecto. Se eliminaron además dos que sobraban: la cadena de conexión de la base de datos de producción, que ningún código leía y solo servía para tener esa credencial en el portátil, y una clave de API que tampoco usaba nadie (T1-13, parcial: falta revocar las antiguas en cada proveedor).

- Eliminado el panel de estadísticas de la biblioteca. Estaba escrito de punta a punta pero no se mostraba en ninguna pantalla, así que nadie llegó a verlo nunca; se retira de los dos proyectos junto con el endpoint y los estilos que solo él usaba. Decisión del propietario (T2-08).

- El modo oscuro ya no parpadea al cargar: antes se veía un destello claro en cada carga porque el tema se aplicaba con el primer dibujo de la interfaz (T3-18).
- Si no has elegido tema a mano, la aplicación sigue al del sistema **también cuando cambia**, sin recargar. Antes lo miraba una sola vez y, además, guardaba una preferencia sin que la hubieras elegido: bastaba abrir la aplicación una vez para que dejara de seguir al sistema para siempre (T3-18).
- Cerrar sesión con la red caída ya avisa. Se sale igualmente de este dispositivo, pero ahora se dice que el servidor no llegó a enterarse y qué hacer si te preocupa (T3-16).
- Una variable de configuración mal escrita ya no deja la pantalla en blanco: aparece un aviso que dice cuál es y qué corregir. Además, dejar `VITE_API_URL` vacío en el archivo de configuración vuelve a usar el valor por defecto, en vez de impedir que la aplicación arranque (T3-17).
- Una contraseña desmesuradamente larga en el inicio de sesión se rechaza al llegar, sin llegar a compararse. Antes el servidor gastaba tiempo de CPU cifrándola solo para acabar diciendo que no (T3-20).

### Interno

- **La interfaz pasa por una capa de idiomas** (`i18next` + `react-i18next`), con el diccionario tipado a partir del español: una clave que falte en inglés, o mal escrita en un `t(...)`, no compila. Siete pruebas nuevas vigilan lo que el compilador no ve: que las interpolaciones sobrevivan a la traducción, que los plurales tengan sus dos formas en los dos idiomas y que no vuelva a incrustarse texto en un componente. Esa última regla encontró seis textos que la propia migración se había dejado en el catálogo (T4-03).
- **Hay una regla que revisa los 61 campos de formulario del frontend** y falla si alguno se queda sin la clase del sistema de diseño. Es la que habría evitado T5-09 y T5-10, dos defectos de presentación en el mismo bloque que ninguna prueba de comportamiento podía ver (T5-10).
- La configuración de Vite usaba `__dirname`, que no existe en un módulo ESM: Vite lo emulaba y avisaba en cada arranque de que iba a dejar de hacerlo. Sustituido por `import.meta.dirname`.
- **`vitest` recogía por error las pruebas de Playwright.** Sus archivos acaban en `.spec.ts`, que es parte del patrón por defecto, así que `npm test` daba cuatro archivos en rojo que no tenían nada roto. Pasó inadvertido en T4-04 porque cada suite se comprobó con su propio comando y nunca las dos a la vez.
- **Hay una tercera suite de pruebas: 13 pruebas end-to-end con Playwright** (T4-04). A diferencia de las otras dos, hablan con la aplicación entera —navegador, servidor de Vite, backend y PostgreSQL— y cubren registro, acceso, el ciclo completo de un registro de la biblioteca, el inicio de los flujos de Google y GitHub y la exportación con su reimportación. Necesitan PostgreSQL y el backend en marcha; ver `docs/WORKFLOW.md`.
- **Los cupos del limitador de peticiones se configuran**, con los mismos valores de siempre por defecto (T4-04). Hacía falta para las pruebas end-to-end, que agotaban el de autenticación a mitad de ejecución, y sirve igual en casa: servida por LAN, la aplicación hace que todos los dispositivos compartan el mismo cupo de accesos.

- **La comprobación de salud del servidor por fin comprueba algo.** Antes respondía «todo bien» siempre, incluso con la base de datos caída, que es justo la avería que deja la aplicación inservible. Ahora hay dos: una dice si el proceso está en pie y otra si además la base de datos responde (T4-10).
- La especificación de la API queda publicada en el repositorio, así que se puede consultar sin arrancar nada. Al servirla desde la aplicación en marcha hace falta activarla a propósito fuera de desarrollo (T4-07).
- Se mide la cobertura de las pruebas. La primera medición destapó que **dos de los tres catálogos externos —AniList y MangaDex— no tenían ni una sola prueba**, y tampoco el modificar y borrar formatos. Veinte pruebas nuevas (T4-05).
- **Las pruebas del backend se ejecutan ahora contra PostgreSQL de verdad**, no contra una base de datos distinta en memoria. La diferencia no era teórica: la búsqueda de la biblioteca fallaba en las pruebas —usa una función que solo existe en PostgreSQL— y por eso no tenía ninguna. Ahora tiene cinco (T2-02, T2-04).
- Las pruebas aplican las migraciones en lugar de construir el esquema por su cuenta, así que una migración rota se detecta. Era justo lo que faltaba para haber pillado antes el fallo de la biblioteca (T2-03).
- Corregido el fallo intermitente de la suite que aparecía sin relación con el test en ejecución: desapareció al dejar de compartir una sola conexión SQLite entre todos los `DbContext` (T2-28).

- Corregido un comentario que describía una protección inexistente: decía que la sesión guardada en el navegador estaba protegida con `SameSite=Strict`, que es un atributo de *cookie* y no se aplica a `localStorage`. Ahora enumera las defensas que sí existen y deja escrito que esa no está (T2-13).
- Añadido Prettier con el estilo que ya seguía el código, para que el editor deje de reformatear archivos al guardarlos (T3-22).
- Un espacio de más alrededor de la dirección del backend en el archivo de configuración ya no impide que la aplicación arranque (T3-23).
- Añadidos `robots.txt` y `sitemap.xml`. Las pantallas privadas y los enlaces con token quedan fuera de los buscadores; solo el inicio de sesión y el registro son indexables (T3-19).
- Los árboles de carpetas de los dos README vuelven a coincidir con el repositorio, y con ellos dos recuentos de pruebas que se habían quedado atrás (T3-06).
- Unificados en un solo tipo los cuatro resultados de validación del backend, que tenían la misma forma (T3-11).
- Añadido un archivo de estilo (`.editorconfig`) en los dos repositorios y normalizada la sangría de 67 archivos del backend, que mezclaban tabuladores y espacios dentro del mismo bloque. Sin él, cada editor imponía lo suyo al guardar (T3-15).
- La lectura del identificador de usuario deja de estar copiada en cuatro controladores (T3-12).
- Eliminado código que no usaba nadie: un documento de diseño de otro producto (190 líneas), el módulo de saneado de HTML y sus dos dependencias, tres campos de configuración y un contrato de OAuth que ninguna respuesta devolvía (T3-07 a T3-10).
- Corregido un segundo comentario que describía algo que el código no hace: decía que el acceso con Google valida un `id_token`, cuando pide el perfil a otro endpoint (T3-14).
- Documentado cómo verificar el esquema de la base de datos partiendo de cero. Es la comprobación que destapó el fallo de la biblioteca descrito arriba, y ninguna prueba automática puede sustituirla.
- Restaurada la aplicación de migraciones al arrancar, que estaba desactivada por una línea comentada. Ahora registra en el log qué migraciones aplica y un fallo queda anotado como error sin tumbar el servicio (T0-01).
- Eliminada una clave foránea duplicada en `AspNetUserLogins` que EF Core había generado por una navegación mal configurada, junto con su columna y su índice (T1-01).
- Las dos suites de pruebas pasan a estar en verde por primera vez: **105/105** en el backend y **124/124** en el frontend. Se corrigió una aserción mal escrita en el backend y se alinearon con la interfaz vigente los tests del frontend que habían quedado atrás (T1-08 a T1-11, T2-01).
- El linter comprueba ahora reglas de accesibilidad. Su ausencia era la razón de que las siete etiquetas sin control asociado sobrevivieran hasta la auditoría (T2-22).
- Resuelto el único error de ESLint del proyecto, que el build no detectaba porque `npm run build` no ejecuta el linter (T2-23).
- La suite de pruebas del backend pasa a estar bajo control de versiones. Vivía en una carpeta que no pertenecía a ningún repositorio, de modo que un clon limpio no traía ni un solo test y la solución no llegaba a abrirse (T0-02).
- Cada blueprint de despliegue vive ahora en la raíz del repositorio que describe, con rutas relativas a ella. Los anteriores declaraban prefijos de carpeta propios de un monorepo que no existe, así que ni Render ni Netlify podían reproducir el despliegue desde el repositorio (T0-03).
- La documentación de despliegue, que solo existía en un archivo sin versionar, se repartió entre el `README.md` del backend y el del frontend.
- Descartada la integración continua por decisión del propietario: proyecto de un solo desarrollador, verificación en local. La rutina que la sustituye —cinco comandos, alrededor de un minuto— queda documentada en `docs/WORKFLOW.md` (T1-12, anulada).
- El repositorio del backend vuelve a contener solo código: se sacaron del control de versiones 227 archivos que eran salida de compilación y caché del editor, 68 MB en total. Los archivos siguen en el disco; simplemente dejan de subirse (T1-20).
- Un clon del repositorio ya arranca siguiendo el README. Faltaba el archivo de configuración base, que además estaba excluido del control de versiones pese a que la documentación decía lo contrario (T1-19).
- Primera auditoría técnica completa del proyecto (13 áreas, profundidad exhaustiva, normativa GDPR y WCAG 2.2 AA), y los tres documentos vivos: `ROADMAP.md`, `CHANGELOG.md` y `CONTEXT.md`.
- **La documentación común deja de estar fuera de todo repositorio.** Vivía suelta en la carpeta que contiene los dos clones, que no es un repositorio: sin copia de seguridad ni historial. Ahora está versionada en `docs/` del repositorio de backend, reorganizada por responsabilidad —arquitectura, decisiones, trampas del stack, trabajo diario, roadmap, changelog e historial— y con las afirmaciones que habían envejecido mal corregidas: la suite ya no corre sobre SQLite, `appsettings.json` sí existe, y los recuentos de pruebas y de migraciones estaban desfasados.

### Conocido y sin corregir

Si algún día la aplicación vuelve a publicarse:

- Sin política de privacidad. **Deja de ser un problema legal mientras el uso sea personal y local**, pero vuelve a serlo el día que esté accesible para otras personas (T0-05). El borrado de cuenta, que aparecía aquí, **ya está hecho** (T1-14).
- **Al desplegar detrás de un proxy hay que declararlo.** El arreglo de T1-05 hace que la cabecera `X-Forwarded-For` se ignore mientras no haya proxies en `ForwardedHeaders:KnownProxies` o `KnownNetworks`. Es lo correcto en local y por LAN, pero si se publica detrás de un proxy sin declararlo, el límite de intentos contará a todo el mundo como si fuera el mismo dispositivo.
- El esquema de la base de datos de producción no llegó a comprobarse y ya no es comprobable: la base se eliminó el 2026-09-05. Un despliegue nuevo construye el esquema desde cero con las migraciones actuales, que sí están verificadas desde vacío (T0-06).

---

## [1.0.0] — 2026-05-23

Primera versión funcional completa. *Reconstruida a partir del historial de git; nunca se etiquetó ni se publicó formalmente.*

### Añadido

- **Biblioteca personal de contenido multimedia**: alta, edición, borrado y listado paginado de elementos, con estado de seguimiento, progreso por episodios, capítulos, páginas, horas o pistas, temporada actual, puntuación personal, notas y fechas de inicio y finalización.
- **Búsqueda y filtrado** de la biblioteca por título, formato, estado, origen, categorías, rango de fechas y rango de puntuación, con ordenación por título, puntuación, año de publicación o fecha de alta.
- **Descubrimiento en catálogos externos**: búsqueda simultánea en Jikan (MyAnimeList), AniList y MangaDex, con alta rápida del resultado en la biblioteca. Si un proveedor falla, los resultados de los demás siguen mostrándose.
- **Categorías propias** con nombre y color personalizables, y asignación múltiple por elemento.
- **Formatos propios** con orden personalizable; al crear la cuenta se ofrecen diez formatos base (Anime, Serie, Película, Manga, Cómic, Libro, Videojuego, Podcast, Álbum, Otro).
- **Importación y exportación de la biblioteca** en JSON y CSV, con detección de duplicados para no crear elementos repetidos al reimportar.
- **Cuentas de usuario** con registro por correo y contraseña, confirmación de la dirección por email, recuperación de contraseña, cambio de contraseña y edición del nombre visible.
- **Inicio de sesión con Google y con GitHub**. Si el correo del proveedor ya tiene una cuenta con contraseña, se pide la contraseña para confirmar la vinculación en lugar de crear una cuenta duplicada.
- **Gestión de sesiones**: cierre de sesión en el dispositivo actual y cierre de todas las sesiones abiertas desde el perfil.
- **Modo oscuro** con detección de la preferencia del sistema y conmutador manual persistente.
- **Panel de estadísticas de la biblioteca** (recuentos por estado, formato y origen, medias de puntuación y actividad del mes). *Nota: en el estado actual del código este panel no está conectado a ninguna pantalla; ver T2-08.*

### Seguridad

- Autenticación por JWT de vida corta con tokens de refresco opacos, almacenados en la base de datos solo como hash SHA-256 y rotados en cada uso.
- Bloqueo temporal de la cuenta tras 5 intentos fallidos de acceso durante 15 minutos.
- Límites de peticiones por minuto: 10 en los endpoints de autenticación, 30 en la búsqueda externa y 200 en el resto de endpoints autenticados.
- Parámetro `state` anti-CSRF persistido en base de datos para los flujos OAuth, con caducidad y un solo uso.
- Aislamiento estricto por usuario: toda consulta de elementos, categorías y formatos filtra por el propietario, cubierto por pruebas de aislamiento dedicadas.
- Cabeceras de seguridad en las respuestas del backend (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`), HSTS fuera de desarrollo y Content Security Policy en el frontend.
- Validación del secreto JWT al arrancar: la aplicación se niega a iniciarse si falta o tiene menos de 32 bytes.

### Interno

- Backend ASP.NET Core 10 con Entity Framework Core sobre PostgreSQL, organizado en `Controllers` / `Services` / `Domain` / `Contracts`.
- Frontend React 19 con Vite 8, TypeScript, TanStack Query, React Router 7 y validación de contratos con Zod, organizado por *features*.
- Suite de 102 pruebas de integración del backend con xUnit y `WebApplicationFactory`, y 124 pruebas de componente del frontend con Vitest y Testing Library.
- Empaquetado del backend en Docker con imagen de runtime y usuario sin privilegios, listo para Render; frontend preparado como SPA para Netlify.

---

## [0.1.0] — 2026-05-07

### Añadido

- Estructura inicial del monorepo con los proyectos de backend y frontend por separado.
- Esqueleto de autenticación y del modelo de datos de la biblioteca.
