

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


## 13. Optimización, guía de canales y efecto cristal — 2026-11-15

- **Ubicación:** código de la aplicación WPF en `MagisWindowsNative/` (base: ZIP v5 "Logo_Tema_Publicacion_Ligera"). Esta línea de trabajo **no incluye** todavía la integración Jellyfin/OMDb de la sección 12; hay que fusionarlas.
- **Rendimiento:** `CountryResolver` sin Regex, parser M3U por líneas, XMLTV en streaming en segundo plano, caché cifrada de fuentes (`SourceCache`, TTL 6 h), guardado de catálogo en segundo plano, LibVLC inicializado tras mostrar la ventana, miniaturas decodificadas a tamaño de tarjeta, ordenación y filtrado una sola vez por acción. Detalle en `MagisWindowsNative/OPTIMIZACION-RENDIMIENTO.md`.
- **Guía de canales:** `PlayerGuideWindow` (ventana propia sobre el reproductor por el problema de airspace de LibVLC); filtros por tipo/categoría, búsqueda, teclado (G/Enter/Tab, Esc). Se oculta y reaparece junto con la barra flotante (`PlayerWindow.ControlsVisibilityChanged`).
- **Efecto cristal:** `Services/Glass.cs` (Mica/acrílico vía DWM, con alternativa opaca), `Themes/Glass.xaml` y `Themes/Controls.xaml`. Mismos colores; solo cambia la opacidad. La barra y la guía usan `WindowChrome` en lugar de `AllowsTransparency`.
- **No verificado:** sin SDK de .NET en el entorno de edición; no se compiló ni se probó en Windows. El usuario compiló una versión anterior (sin guía ni cristal) con éxito.
- **Siguiente paso:** compilar en Windows (`dotnet build -c Release` tras borrar `obj`/`bin`), probar guía/blur y reportar errores concretos; después fusionar con la integración Jellyfin.

### 13b. Listas públicas al iniciar — 2026-11-15
- **Pedido del usuario:** cargar al iniciar las listas públicas M3U de `public-playlists.json` (iptv-org y Free-TV, 92 listas).
- **Implementación:** `Assets/public-playlists.json` (recurso), `Services/PublicPlaylists.cs` (alta única como fuentes `Público · …` con id estable `pub…`), indicador `CatalogState.PublicPlaylistsSeeded`, carga de 6 en 6 (`_loadGate`) y deduplicación por URL entre fuentes públicas (`DropStreamsAlreadyLoaded`).
- **Riesgo conocido:** el primer arranque descarga mucho; las listas se solapan (por eso la deduplicación). No verificado en Windows.

### 13c. Películas y series desde listas M3U — 2026-11-15
- **Pedido del usuario:** comprobar que hay películas y series y que se accede a ellas por M3U igual que por Xtream.
- **Hallazgo:** el parser marcaba todo como `Live`; Películas/Series exigían una cuenta Xtream.
- **Implementación:** `Services/MediaKindClassifier.cs` (rutas `/movie/` y `/series/`, extensiones de vídeo, marcadores `S01E02`/`1x02`), `MediaItem.SeriesKey/Season/EpisodeNumber`, entradas `Series` generadas por el parser con sus episodios (`Kind="Episode"`, ocultos en las listas), `OpenSeriesEpisodesAsync` resuelve episodios M3U localmente, caché M3U `M3uAll`, un refresco de fuente M3U reemplaza todos sus tipos.
- **Límite:** las listas públicas iptv-org «Películas/Series» son canales en directo (.m3u8) y siguen como TV. Ejemplo de prueba: `MagisWindowsNative/Ejemplos/prueba-peliculas-series.m3u`.
- **No verificado:** sin compilar ni probar en Windows.

### 13d. Descargas, reproductor externo y catálogo de dominio público (Archive.org) — 2026-11-15
- **Pedido del usuario:** revisar https://github.com/4gray/iptvnator e incorporar sus mejoras más útiles; buscar contenido real de películas y series para completar la app.
- **Implementación:** `Services/DownloadManager.cs` (descarga a Vídeos\MagisTV, sin bloquear la UI, sección «Descargas» nueva); botón «⧉ Externo» (`Process.Start` con el reproductor predeterminado de Windows); `Services/ArchiveOrgCatalog.cs` (películas de dominio público de archive.org vía su API pública, colección `feature_films`, filtradas por `licenseurl` de dominio público).
- **Decisión:** no se integraron series desde archive.org (sin colección claramente de dominio público) ni timeshift/Stalker (requieren más interfaz). No se usaron fuentes no oficiales de Pluto TV/Tubi por falta de garantía de legitimidad.
- **No verificado:** sin compilar ni probar en Windows.

### 13e. Publicación automática para Windows — 2026-11-23
- `.github/workflows/windows-release.yml` (rama `guia-y-cristal`): en cada push que toque `MagisWindowsNative/` compila en `windows-latest` con .NET 10, hace `dotnet publish -r win-x64 --self-contained` y crea una versión en Releases (`windows-v1.0.N`) con `MagisTV-Windows-x64.zip` (~148 MB, incluye LibVLC y `LEEME-INSTALAR.txt`).
- **Primera compilación real del código WPF:** `windows-v1.0.1` compiló sin errores (commit 960f654). Las secciones 13–13d quedan así confirmadas como compilables; falta probarlas en uso.
- Enlace fijo a la última versión: https://github.com/BlasterG10/Blaster-TV/releases/latest
