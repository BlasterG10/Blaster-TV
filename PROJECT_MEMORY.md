

## 11. Asistente local de configuración — 2026-11-14

- **Objetivo:** simplificar la configuración de OMDb y el primer inicio de la aplicación para Windows sin pedir al usuario que comparta su clave por chat.
- **Archivos añadidos al ZIP de la aplicación:**
  - `Configurar-y-Ejecutar.ps1`: solicita la clave OMDb mediante entrada oculta, la guarda en la variable de entorno del usuario Windows (no en archivos del proyecto), comprueba la presencia de .NET 10 SDK, ejecuta restore/build y solo inicia la aplicación si compila.
  - `INICIAR-BLASTER-TV.bat`: lanzador de doble clic que ejecuta el asistente PowerShell con bypass limitado al proceso.
- **Archivo actualizado:** `README.md` con instrucciones del asistente.
- **Seguridad:** clave opcional; TVmaze continúa disponible sin OMDb. La clave se persiste en el entorno del usuario Windows y no se incluye en ZIP ni GitHub. No pedir ni almacenar la clave en la conversación.
- **Verificación:** ZIP creado en `/mnt/data/MagisWindowsNative_Autoconfig_OMDb_TVmaze.zip`; `unzip -t` confirmó que no hay errores en el archivo. No se ejecutó el script en Windows ni se compiló .NET en este entorno.
- **Limitación:** esto es un asistente local de configuración/compilación/inicio, no un MSI ni un EXE independiente firmado. Requiere .NET 10 SDK y conexión a NuGet.
- **Siguiente paso:** el usuario descarga, extrae y hace doble clic en `INICIAR-BLASTER-TV.bat`; si el SDK no está instalado, debe instalar .NET 10 SDK y volver a ejecutar.


## 12. Integración Jellyfin en Blaster-TV — 2026-11-14

- **Base de trabajo:** ZIP funcional WPF .NET 10 Windows con OMDb/TVmaze y asistente local; conservar M3U, Xtream, LibVLC, favoritos, historial y controles existentes.
- **Nuevos archivos:** `Services/JellyfinClient.cs`, `JellyfinDialog.xaml`, `JellyfinDialog.xaml.cs`.
- **Archivos modificados:** `MainWindow.xaml`, `MainWindow.xaml.cs`, `Models/MediaItem.cs` (campo `ExternalId`), `README.md`.
- **Capacidades añadidas:** conexión Jellyfin mediante URL/usuario/contraseña; autenticación `Users/AuthenticateByName`; carga de películas, series y episodios; doble clic en una serie consulta sus episodios; películas y episodios abren URL de streaming Jellyfin en el reproductor LibVLC existente.
- **Separación de fuentes:** Jellyfin tiene controles independientes. Conectar Jellyfin no reemplaza listas M3U ni la sesión Xtream. Los botones originales Películas/Series siguen usando Xtream; los botones Jellyfin cargan el catálogo del servidor.
- **Seguridad:** token Jellyfin en memoria durante la sesión; no guardar contraseñas/token en archivos, GitHub ni ZIP. No pedir credenciales por chat.
- **Verificación realizada:** XAML parseado como XML válido; balance estático de llaves C#; ZIP `/mnt/data/BlasterTV_Jellyfin_M3U_Xtream_Integrated.zip` pasa `unzip -t`.
- **No verificado:** no hay .NET SDK en este entorno, así que no se ejecutó `dotnet build`; tampoco se probó contra un servidor Jellyfin real. No afirmar que la compilación o reproducción real está confirmada.
- **Siguiente paso del usuario:** extraer ZIP en una carpeta aparte, compilar/ejecutar en Windows con .NET 10 SDK, conectar su servidor Jellyfin y reportar cualquier error concreto.


## 14. MagisTV para Roku — 2026-11-21

- **Pedido del usuario:** versión para Roku con alcance «completo»: fuentes M3U/Xtream, TV en vivo, películas, series con episodios, guía EPG, favoritos. El usuario ya tiene modo desarrollador activo y la IP de su Roku.
- **Ubicación:** carpeta `RokuMagisTV/` en la rama `magistv-roku` (creada desde `main`; independiente de la línea WPF `guia-y-cristal` de la sección 13 y de Jellyfin de la sección 12).
- **Tecnología:** BrightScript + SceneGraph, resolución de diseño 1280x720 (`ui_resolutions=hd`). Instalación por sideload (`instalar-en-roku.ps1` o instalador web en `http://IP`).
- **Puertos desde Windows:** `M3uParser`, `MediaKindClassifier`, `XtreamClient`, EPG (`get_short_epg` para Xtream; XMLTV con `url-tvg` para M3U), caché de 6 h con copia vieja si no hay red (`cachefs:`), favoritos/historial en `roRegistrySection`. Red y parseo siempre en `LoaderTask` (máx. 3 a la vez).
- **Interfaz:** menú | categorías | contenido (`MainScene`); reproductor con barra flotante y guía que se ocultan juntas (`VideoScreen`). Mismos colores que Windows.
- **Límites por memoria del Roku:** M3U ≤ 20 MB, XMLTV ≤ 12 MB (ventana de 8 h), 1.500 elementos visibles por categoría (el resto vía Buscar). Sin listas públicas automáticas, sin descargas, sin desenfoque. Símbolos limitados a Latin-1 por la fuente del sistema.
- **Verificación:** XML de componentes válido; todas las funciones llamadas existen en los scripts incluidos por cada componente; callbacks de `observeField`/`onChange` existen; bloques `sub/function/if/for/while` cuadran.
- **No verificado:** no hay compilador de BrightScript ni Roku en este entorno; no se ha instalado ni ejecutado. Si falla, pedir al usuario la salida de `telnet IP 8085` (consola de depuración del Roku) con el error y la línea.
- **2026-11-21 — Corrección «abre y se cierra»:** el canal no compilaba por usar palabras reservadas de BrightScript como identificadores (`stop` y `next` en `EpgService.brs`, `m` como variable) y una variable `isFav` con el mismo nombre que la función `IsFav` (BrightScript no distingue mayúsculas). Renombrados: `stop`→`ends`/`stopTime`, `next`→`upcoming`, campo de fuente `type`→`sourceType`, `m`→`minutes`, `isFav`→`nowFav`. Al revisar código BrightScript, comprobar siempre palabras reservadas y choques variable/función.
