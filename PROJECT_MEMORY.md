# PROJECT_MEMORY.md — Blaster-TV

> **Protocolo obligatorio de continuidad del proyecto.**
> Antes de CADA acción que consulte, edite, genere, pruebe, configure o publique algo en este proyecto, el asistente debe leer este archivo completo y seguir sus reglas. Si se detecta que este archivo está desactualizado, primero se debe verificar el repositorio y luego actualizar esta memoria con hechos comprobados.

## 1. Identidad y objetivo

- **Repositorio:** https://github.com/BlasterG10/Blaster-TV
- **Rama principal:** `main`
- **Proyecto:** aplicación IPTV / reproductor multimedia.
- **Objetivo:** construir una aplicación para reproducir listas IPTV M3U del usuario y fuentes legítimas, con secciones de TV en vivo, películas y series.
- **Idioma de interacción y documentación:** español, salvo que una interfaz o requisito técnico necesite inglés.

## 2. Reglas obligatorias antes de actuar

1. Leer este archivo completo antes de cada acción relacionada con el proyecto.
2. Consultar el estado actual de GitHub y los archivos relevantes antes de proponer o aplicar cambios. No confiar únicamente en conversaciones antiguas.
3. Identificar rama, archivos y dependencias afectadas antes de modificar.
4. Hacer cambios pequeños y enfocados; no reescribir ni eliminar funcionalidades existentes sin una razón técnica clara.
5. Preservar el trabajo existente del usuario. Nunca sobrescribir archivos sin leerlos primero.
6. No afirmar que algo funciona, compila o fue probado si no se ejecutó la prueba correspondiente.
7. Después de cada modificación, verificar el resultado disponible (contenido publicado, diff, pruebas o compilación según el cambio).
8. Actualizar este archivo después de cada cambio significativo: registrar fecha, archivos, objetivo, resultado y tareas pendientes.
9. Mantener secretos y claves API fuera del repositorio. Usar variables de entorno o configuración local ignorada por Git.
10. No incorporar listas, vídeos, credenciales ni enlaces de reproducción de origen no autorizado. El reproductor debe aceptar fuentes que el usuario tenga derecho a utilizar.
11. Separar metadatos de catálogo de las URL reales de reproducción. Una ficha, póster o sinopsis no demuestra que exista un flujo reproducible.
12. En caso de duda, inspeccionar primero y evitar cambios destructivos.

## 3. Estado verificado del repositorio al crear esta memoria

- El repositorio está accesible y la conexión GitHub dispone de permisos de lectura y escritura.
- La rama principal detectada es `main`.
- El repositorio estaba prácticamente vacío.
- El único archivo visible en la raíz era `README.md`, con el texto:
  `# Blaster-TV`
  `Proyecto de iptv`
- No se encontró código de aplicación, configuración de compilación, pruebas ni estructura de carpetas durante esta inspección inicial.
- Por tanto, todavía no se puede confirmar framework, lenguaje, plataforma destino ni comandos de ejecución.

## 4. Requisitos funcionales conocidos

### 4.1 TV en vivo
- Importar listas M3U proporcionadas por el usuario.
- Permitir cargar listas públicas o propias solo cuando su uso y distribución sean legítimos.
- Parsear canales y agruparlos por categoría cuando la información esté disponible.
- Permitir cambiar de canal desde la interfaz sin cerrar la ventana del reproductor.
- Investigar y corregir el error reportado al buscar canales importados desde los apartados de TV en vivo / series.

### 4.2 Reproductor
- Controles de adelantar y retroceder 15 segundos cuando el tipo de flujo lo permita.
- Barra de controles que se oculte aproximadamente después de 7 segundos sin interacción.
- Volver a mostrar los controles al mover el cursor o pausar con el teclado.
- Añadir controles para cambiar al canal anterior y siguiente sin salir del reproductor.
- Adaptar controles a las capacidades del medio: los flujos en vivo normalmente no permiten buscar hacia delante o atrás.

### 4.3 Películas y series
- Añadir catálogo separado para películas y series, con búsqueda y fichas visuales.
- Explorar como referencia el repositorio público https://github.com/IPTV-Danilo/IPTV-PELIS.
- Según su README, ese proyecto se describe como generador M3U con metadatos y menciona OMDb para películas, TVMaze para series y Fanart.tv para pósteres, con salida `output/playlist.m3u`.
- Esa descripción no confirma que proporcione vídeo ni URL de reproducción operativas.
- La inspección inicial encontró que `main.py` importa `series.py`, aunque el listado visible de archivos no incluía dicho archivo; construye URLs de ejemplo con `YOUR_SERVER`. Hay que volver a verificar antes de integrar.
- Usar metadatos y carátulas como complemento; reproducir únicamente enlaces de vídeo válidos y autorizados, aportados por el usuario o por servicios que permitan su uso.

## 5. Arquitectura y decisiones técnicas

- **Framework / lenguaje:** desconocido; determinar tras inspeccionar el repositorio cuando se incorpore el código.
- **Motor de reproducción:** desconocido; elegir según plataforma y formatos requeridos, después de conocer el framework.
- **Persistencia de listas y favoritos:** pendiente de diseño.
- **Modelo recomendado:** separar importación/parsing de M3U, catálogo/metadatos, fuentes de reproducción, interfaz y motor de vídeo.
- Mantener una fuente de reproducción asociada explícitamente a cada elemento. No generar URLs ficticias ni asumir que OMDb/TVMaze alojan medios.

## 6. Registro de cambios

### 2026-11-14 — Creación de memoria inicial
- **Archivo:** `PROJECT_MEMORY.md`
- **Acción:** crear el documento de continuidad y las reglas para futuras intervenciones.
- **Estado:** se creó desde cero tras revisar la raíz del repositorio y el README.
- **Verificación:** pendiente de confirmar la lectura del archivo publicado en GitHub.

## 7. Pendientes prioritarios

1. Inspeccionar la estructura real de Blaster-TV cuando se añada el código de la aplicación.
2. Identificar framework, plataforma destino, motor de reproducción y comandos de compilación.
3. Registrar cómo importar y conservar listas M3U.
4. Reproducir y corregir el error de búsqueda en TV en vivo / series reportado por el usuario.
5. Implementar navegación entre canales dentro del reproductor.
6. Ajustar la barra de controles: ocultar a los 7 segundos, mostrar ante interacción/pausa, saltos de 15 segundos cuando sean compatibles.
7. Conectar el adaptador de metadatos ya añadido con la interfaz de la aplicación cuando exista código de interfaz.
8. Añadir pruebas y documentar cómo ejecutar la aplicación.

## 8. Plantilla para actualizar el registro

Para cada cambio significativo, añadir una entrada con:

- Fecha:
- Objetivo:
- Archivos modificados:
- Motivo técnico:
- Verificación ejecutada:
- Resultado:
- Riesgos / limitaciones:
- Próximo paso:

---
**Regla de inicio para futuras sesiones:** leer `PROJECT_MEMORY.md`, consultar la rama actual y revisar los archivos relevantes antes de realizar cualquier cambio.


## 9. Integración IPTV-PELIS — 2026-11-14

- **Repositorio de referencia:** https://github.com/IPTV-Danilo/IPTV-PELIS
- **Inspección:** el árbol del repositorio de referencia contiene README, config.py, m3u.py, metadata.py y movies.py; no aparece series.py ni un servicio real de streaming. El código original construía URLs de ejemplo con YOUR_SERVER. No se copió código del repositorio de referencia.
- **Decisión:** integrar proveedores de metadatos mediante un adaptador propio y mantener las fuentes de reproducción separadas.
- **Archivos añadidos:**
  - `integrations/iptv_pelis/catalog.py`: búsqueda y detalles de películas con OMDb; búsqueda de series y episodios con TVmaze.
  - `integrations/iptv_pelis/README.md`: instalación, uso, limitaciones y atribución.
  - `integrations/iptv_pelis/requirements.txt`: dependencia requests.
  - `integrations/iptv_pelis/__init__.py`: paquete Python.
- **Archivo actualizado:** `README.md` de la raíz con descripción y enlace a la integración.
- **Seguridad/precisión:** el adaptador devuelve `playback_url: None`; nunca crea direcciones de vídeo falsas. OMDb requiere `OMDB_API_KEY` en variable de entorno. TVmaze ofrece endpoints públicos sin clave y sus datos requieren atribución y cumplimiento CC BY-SA.
- **Verificación realizada:** los archivos fueron publicados en GitHub y se confirmaron commits exitosos. No se ejecutó una prueba de integración contra APIs ni una compilación porque el repositorio todavía no contiene la aplicación, pruebas o configuración de ejecución.
- **Limitación actual:** la integración de backend de metadatos está añadida, pero aún no está conectada a una interfaz de películas/series ni al reproductor; falta incorporar el código real de la aplicación.
- **Próximo paso:** añadir o localizar la aplicación base y conectar sus vistas Películas/Series a este adaptador, asociando reproducción únicamente con entradas válidas de listas M3U autorizadas.

