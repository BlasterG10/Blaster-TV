# Magis Windows Native — proyecto inicial

Aplicación de escritorio WPF para Windows 11, con catálogo M3U/M3U8 y conexión Xtream Codes compatible, y reproducción mediante LibVLCSharp.

## Qué incluye este primer build
- Importar listas M3U/M3U8 desde archivo local o URL HTTP/HTTPS.
- Analizar `#EXTINF`, nombre, `group-title` y `tvg-logo`.
- Conectar a un servidor Xtream autorizado con URL, usuario y contraseña.
- Consultar categorías de TV en vivo, películas y series mediante las acciones Xtream correspondientes.
- Abrir la ficha de episodios de una serie y reproducir el episodio seleccionado.
- Reproducir enlaces compatibles con VLC.
- Guardar opcionalmente las credenciales con DPAPI de Windows (perfil del usuario actual).
- Cifrar con DPAPI el catálogo local, incluidos favoritos e historial, para no dejar URLs Xtream con credenciales en texto plano.
- Mostrar tarjetas compactas con logotipos/carteles cuando el proveedor los suministra; la lista usa virtualización y reciclaje de controles.
- Buscar por nombre o categoría.
- Filtrar por categoría.
- Favoritos persistentes.
- Historial de reproducción (últimos 100 elementos) guardado localmente.
- Interfaz oscura de TV de escritorio, inspirada en el flujo de navegación observado, pero con identidad visual propia.

## Requisitos
- Windows 11 x64.
- Visual Studio 2026 o .NET 10 SDK con carga de trabajo **Desarrollo de escritorio de .NET**.
- Acceso a Internet para restaurar paquetes NuGet.
- Un servicio IPTV/lista y enlaces que tengas derecho a utilizar.

## Abrir y compilar
1. Extrae el ZIP.
2. Abre `MagisWindowsNative.csproj` en Visual Studio.
3. Permite restaurar los paquetes NuGet.
4. Selecciona configuración `Release` y plataforma `x64`.
5. Ejecuta con F5 para probar.

También desde PowerShell en la carpeta del proyecto:
```powershell
dotnet restore
dotnet build -c Release
dotnet run
```

## Publicar como aplicación Windows
Con .NET 10 SDK instalado:
```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```
El ejecutable se genera dentro de `bin\\Release\\net10.0-windows\\win-x64\\publish\\`.

## Conexión M3U
Pulsa **Abrir M3U** y selecciona tu archivo `.m3u` o `.m3u8`. También puedes importar una lista remota con **M3U por URL**. Las URL HTTP/HTTPS/RTSP/RTMP que contenga se agregan al catálogo.

## Conexión Xtream
Pulsa **Conectar Xtream** e introduce:
- URL base del proveedor, por ejemplo `https://servidor-ejemplo.tld:8080`
- Usuario
- Contraseña

Puedes marcar **Recordar credenciales**. Se guardan cifradas con DPAPI y solo el mismo usuario de Windows puede descifrarlas. El historial y favoritos también se guardan cifrados porque las URL Xtream suelen incluir usuario y contraseña.

## Mejoras de esta iteración (ligereza y fluidez)
- Se conserva WPF nativo y LibVLCSharp; no se agrega otro framework de interfaz ni otro motor multimedia.
- Lista con virtualización y reciclaje de controles para no crear todas las filas visuales simultáneamente.
- Tarjetas compactas con imágenes de proveedor a tamaño reducido y placeholder visual cuando no hay logo/cartel.
- Timeout de red de 25 segundos y tolerancia a servidores que no publiquen categorías, sin bloquear la interfaz durante las llamadas asíncronas.
- Historial y favoritos cifrados localmente con DPAPI; se migra automáticamente el antiguo `catalog.json` JSON a formato cifrado al abrir.

## Alcance y limitaciones de este build
- Las series abren una ventana ligera con la lista de episodios devuelta por el proveedor. La estructura de temporadas depende de los datos que entregue el servidor.
- La importación de EPG/XMLTV está disponible desde archivo. La sincronización automática de EPG remota queda pendiente.
- Las listas M3U se pueden importar desde archivo o URL HTTP/HTTPS.
- El APK inspeccionado incluye librerías Android ARM de VLC/OpenVPN y referencias a TMDB, EPG y Xtream. No se copian su código, recursos de marca, endpoints privados, claves, ni componentes Android a este proyecto.
- No incluye VPN ni técnicas para saltar bloqueos, DRM o controles de acceso. La reproducción depende de la compatibilidad del stream y de los permisos del proveedor.

## Notas de seguridad
Usa solo credenciales y fuentes de contenido propias o autorizadas. No compartas capturas que muestren usuario, contraseña o tokens.


## Interfaz visual moderna
La actualización visual incorpora navegación lateral, barra superior de búsqueda y categorías, panel destacado, reproductor integrado en la cabecera, catálogo con tarjetas compactas, estados hover/selección, paleta oscura azul-violeta y controles consistentes. Se mantiene una identidad visual propia, sin copiar marcas ni recursos de Disney o Netflix.

La interfaz fue editada en XAML, pero debe compilarse y probarse en Windows 11. No se ha validado visualmente en este entorno.


## Versión consolidada: catálogo, historial y EPG
- La interfaz moderna se conserva.
- La guía EPG permite importar archivos XMLTV (`.xml`, `.xmltv` y `.gz`) y muestra canal, horario, título y descripción. El horario se convierte a la zona local de Windows.
- Se añade la sección **Continuar viendo**, alimentada por el historial local de reproducción.
- Se mantienen favoritos, filtro de categorías, búsqueda, M3U local/remoto y conexión Xtream.
- El historial guarda hasta 100 elementos y se almacena en `%LOCALAPPDATA%\\MagisWindowsNative\\catalog.json`.
- Las credenciales Xtream se almacenan con DPAPI en el perfil de Windows del usuario.

## Publicación
Desde PowerShell en la carpeta del `.csproj`:
```powershell
dotnet restore
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```
Comprueba el directorio `bin\\Release\\net10.0-windows\\win-x64\\publish\\`. Mantén junto al EXE cualquier archivo de runtime de VLC que el publish genere.

## Límites conocidos de esta entrega
- La guía XMLTV se importa desde archivo local; la descarga automática desde URL y el emparejamiento automático de `tvg-id` con XMLTV no están implementados.
- El apartado de series obtiene el catálogo Xtream; la navegación completa por temporadas y episodios requiere la siguiente integración de `get_series_info`.
- No se incluye una clave TMDB. Las imágenes/portadas remotas y metadatos TMDB requieren una clave propia y su integración posterior.
- No se incluye contenido, listas, credenciales, claves ni servidores de proveedores. Usa únicamente fuentes y servicios autorizados.
- Este código fuente debe compilarse y probarse en Windows 11 antes de considerarlo un instalador final. No se pudo ejecutar una compilación Windows en el entorno de generación.


## Iteración Premium — series y controles de reproducción
Nuevas funciones añadidas en esta iteración:
- Detalle de episodios Xtream mediante `get_series_info`, con agrupación por temporadas y construcción de enlaces de episodio.
- Ventana de selección de episodios; doble clic o botón de reproducción abre el episodio seleccionado en LibVLC.
- Pantalla completa de la ventana con `F11`; `Esc` restaura la ventana.
- Reproducción centralizada en un método común que actualiza el historial local (máximo 100 elementos).
- Se conserva la guía XMLTV local, favoritos, filtros, búsqueda, M3U local/remoto y credenciales DPAPI.

## Verificación en Windows requerida
Esta entrega es código fuente actualizado. No se ha compilado en este entorno porque no tiene instalado el SDK de .NET. En Windows 11, desde la carpeta del proyecto, ejecutar:
```powershell
dotnet restore
dotnet build -c Release
dotnet run -c Release
```
Pruebas manuales recomendadas:
1. Importar la M3U de demostración y reproducir.
2. Conectar un servidor Xtream autorizado.
3. Abrir Series, seleccionar una serie y pulsar Episodios.
4. Probar reproducción de un episodio.
5. Probar F11 y Esc durante reproducción.
6. Importar un XMLTV local y comprobar horarios.
7. Cerrar y volver a abrir para comprobar favoritos y credenciales guardadas.

## Funciones aún no terminadas
- Integración TMDB completa: requiere que el usuario configure su propia API key; esta entrega no incluye una clave.
- Descarga remota/automática y sincronización EPG por `tvg-id`.
- Sincronización de progreso exacto (posición/duración) y reanudación automática; el historial actual registra elementos reproducidos, no el segundo exacto.
- Pistas de audio y selección de subtítulos desde la interfaz.
- Perfiles múltiples y controles parentales.
- Instalador firmado, actualizaciones automáticas y pruebas de rendimiento en distintos equipos.

No se debe describir esta entrega como producto final validado hasta completar las pruebas anteriores en Windows 11.


## Corrección de compilación — noviembre 2026
Se restauraron los métodos que faltaban en `MainWindow.xaml.cs`:
- `PlayMediaItem(MediaItem)`: punto único de reproducción para elementos del catálogo y episodios; valida la URL y registra historial.
- `Window_KeyDown`: gestiona los atajos de teclado.
- `ToggleFullscreen`: alterna pantalla completa con F11 y restaura con Esc.
El evento XAML `PreviewKeyDown="Window_KeyDown"` coincide con el método implementado.


## Corrección Premium Fix 2 — controles de pantalla, audio y subtítulos
- Se añade un botón visible de pantalla completa al panel del reproductor; también se mantienen `F11` y `Esc`. Esta acción maximiza la ventana de la aplicación y restaura su tamaño anterior.
- Se añaden selectores de pistas de audio y subtítulos integrados en el stream, consultados desde LibVLC al comenzar la reproducción.
- El selector de subtítulos incluye la opción **Desactivados**.
- La disponibilidad y los nombres de pistas dependen de lo que publique cada stream; algunas listas no ofrecen pistas alternativas o subtítulos seleccionables.

**Pendiente de validación en Windows:** compilar y probar con streams que tengan varias pistas de audio y subtítulos. Esta iteración no implementa todavía carga de archivos externos `.srt`/`.ass`, sincronización/retardo de subtítulos, ni selección de dispositivos de salida de audio.


## Reproductor independiente

Al iniciar un canal, película o episodio se abre una ventana de reproducción independiente maximizada. Incluye pausa, detener, barra de progreso con búsqueda, volumen, selección de pistas de audio y subtítulos, y pantalla completa con F11/Esc. Cerrar la ventana detiene la reproducción y devuelve el control a la biblioteca.


## Catálogo externo de metadatos (OMDb + TVmaze)

La opción **Descubrir catálogo** abre una ventana independiente para consultar películas y series sin alterar las listas M3U/Xtream ni el reproductor.

- **Películas:** búsqueda y fichas completas mediante OMDb. Requiere una clave API propia configurada en la variable de entorno `OMDB_API_KEY`.
- **Series:** búsqueda, sinopsis, pósteres y episodios mediante la API pública de TVmaze.
- **Importante:** OMDb y TVmaze aportan metadatos, no archivos de vídeo ni URL de reproducción. Los episodios mostrados desde TVmaze son informativos; para reproducir se necesita una fuente legítima M3U/Xtream que ya proporcione el enlace.
- **Atribución:** los datos de TVmaze deben atribuirse a TVmaze y están sujetos a sus condiciones/licencia CC BY-SA. Consulta https://www.tvmaze.com/api. OMDb: https://www.omdbapi.com/.

### Configurar OMDb en Windows PowerShell

Obtén tu propia clave desde https://www.omdbapi.com/apikey.aspx y configura la variable para tu usuario:

```powershell
setx OMDB_API_KEY "TU_CLAVE_OMDB"
```

Cierra y vuelve a abrir Visual Studio Code para que la aplicación reciba la variable. TVmaze no requiere clave para sus endpoints públicos. No guardes claves API en el código ni las subas a GitHub.

## Configuracion automatica (Windows)

Para configurar OMDb de forma local y compilar/iniciar la aplicacion sin escribir comandos:

1. Instala **.NET 10 SDK** para Windows si aun no esta instalado: https://dotnet.microsoft.com/download/dotnet/10.0
2. Haz doble clic en `INICIAR-BLASTER-TV.bat`.
3. Cuando lo solicite, pega tu clave OMDb. La entrada se muestra oculta y el script guarda la variable `OMDB_API_KEY` en el entorno de tu usuario de Windows; no escribe la clave en el proyecto ni en el ZIP.
4. El script ejecuta `dotnet restore`, `dotnet build -c Release` y, si compila correctamente, inicia la aplicacion.

La clave OMDb es opcional: si se omite, la consulta de series mediante TVmaze sigue disponible, pero la busqueda de peliculas OMDb no podra autenticarse. No compartas la clave por chat ni la subas a GitHub. Si cambias la clave, vuelve a ejecutar el archivo BAT.

El script no es un instalador MSI ni publica un EXE independiente; es un asistente local de configuracion, compilacion e inicio.

## Organizar canales por grupos, país o servidor
En la barra superior de la biblioteca:
1. En el primer selector elige **Categoría**, **País**, **Servidor** o **Mis grupos**.
2. En el segundo selector elige el valor que quieres mostrar o **Todos**.
3. Para crear una colección propia, pulsa **＋ Grupo** y escribe un nombre.
4. Selecciona uno o varios canales (Ctrl/Shift permite selección múltiple), elige el grupo de destino y pulsa **Asignar selección**.
5. Para quitar canales de un grupo, elige **Sin asignar** como destino y vuelve a pulsar **Asignar selección**.

Las categorías de Xtream se obtienen de las acciones de categorías del proveedor y se relacionan mediante `category_id`. Las listas M3U utilizan `group-title` y `tvg-country`; el selector **Servidor** agrupa por el host de cada URL de stream. El país se infiere de los metadatos y nombres reconocibles; si no hay datos suficientes se muestra **Sin país identificado**. Puedes completar esa organización manualmente con **Mis grupos**.

## Navegación de Inicio, TV en vivo y Catálogo
- **Inicio** conserva el panel destacado/área de entrada de la aplicación. Ese panel se oculta en las demás secciones para liberar espacio.
- **TV en vivo** abre una guía dentro de la ventana principal; los canales se agrupan por país y categoría y muestran el programa actual/siguiente si se ha importado un XMLTV compatible. Selecciona un canal y pulsa Reproducir o haz doble clic para abrir el reproductor independiente.
- **Catálogo** reúne TV en vivo, películas y series. Si estás conectado a Xtream, carga bajo demanda los tipos que todavía no se han solicitado en la sesión y los agrupa primero por tipo y después por país/región.
- En Catálogo, el selector superior permite filtrar por país/región, categoría, servidor o grupos propios sin cambiar la agrupación principal tipo → país/región.
- La guía XMLTV sigue siendo una importación local desde el apartado **Guía EPG**. El emparejamiento usa `tvg-id` o `epg_channel_id` cuando existe y, como alternativa, el nombre del canal.
- Si el proveedor no aporta país/EPG, la aplicación indica región no identificada o guía no cargada en lugar de inventar esos datos.


## Fuentes múltiples (M3U/M3U8 y Xtream)
**Esta sección reemplaza el flujo anterior de una sola lista/conexión.**
La biblioteca admite varias fuentes a la vez. Abre **Administrar fuentes** en el menú lateral y usa **M3U por URL**, **Archivo M3U** o **Xtream Codes** para registrar tantas fuentes como necesites.

- Todas las fuentes activas se combinan en la misma guía de TV y catálogo.
- Cada fuente puede activarse o desactivarse sin borrarla, o eliminarse permanentemente.
- **Actualizar fuentes** vuelve a descargar las listas activas y actualiza los canales.
- Las conexiones Xtream se mantienen separadas por fuente; las series consultan episodios contra el servidor del que proceden.
- Las fuentes, incluidos los datos de conexión Xtream y las URL M3U que puedan incluir tokens, se guardan dentro de `catalog.json`, cifrado con DPAPI para el usuario actual de Windows.
- Las listas M3U/M3U8 se cargan como TV en vivo. Las películas y series se solicitan a cada conexión Xtream cuando se abre el catálogo o su sección.

Para probar: registra dos listas M3U y/o dos cuentas Xtream, déjalas activas, pulsa **Actualizar fuentes** y abre **TV en vivo** o **Catálogo**. El filtro **Servidor** permite aislar el contenido de cada fuente.


## Catálogo abierto de películas y series
Al iniciar, la app añade una vez tres listas M3U: Archive Watch y listas de películas y series de dominio público. Archive Watch mantiene su feed actualizado; la app lo vuelve a consultar al iniciar cuando la caché tiene más de seis horas. También puedes usar **Actualizar fuentes**. Las tres fuentes se administran como cualquier otra lista y pueden desactivarse o eliminarse.

Las fuentes declaran sus obras de dominio público o libres para compartir. Los derechos dependen del título y el territorio; verifica antes de redistribuir. La app usa enlaces a los alojamientos originales y no aloja los vídeos.

## Inicio más rápido
La primera ejecución carga cinco listas generales. Las listas regionales y de categoría redundantes siguen disponibles en **Administrar fuentes**, pero quedan desactivadas para evitar 90 descargas repetidas al arrancar. Las listas M3U vacías también se guardan en caché, evitando repetir una descarga sin contenido en cada inicio.

## Identidad visual MagisTV
- Logo e icono de aplicación integrados en la barra lateral, panel principal, icono de ventana y ejecutable.
- Paleta de marca morado oscuro, violeta y magenta.
- El nombre del ejecutable publicado es `MagisTV.exe`.
- Las imágenes de marca están en `Assets/`.
