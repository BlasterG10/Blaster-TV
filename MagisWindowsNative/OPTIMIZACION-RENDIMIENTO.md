# Optimización de rendimiento (sobre MagisTV Logo/Tema v5)

La interfaz gráfica NO cambia: mismos XAML, colores, tamaños y textos. El único cambio en XAML es que
la imagen de cada tarjeta se enlaza mediante `ThumbnailConverter` (mismo aspecto, menos memoria/CPU).

## Carga más rápida
- **Caché local cifrada (DPAPI)** de cada fuente (`%LocalAppData%\MagisWindowsNative\cache`): al abrir la app el
  catálogo aparece al instante y solo se vuelve a descargar lo que tiene más de 6 horas.
  "Actualizar fuentes" fuerza la descarga (incluye películas/series ya cargadas).
- Todas las fuentes se descargan **en paralelo**; categorías y streams de Xtream se piden a la vez.
- Descarga con compresión (gzip/brotli) y lectura en *streaming* (M3U línea a línea, JSON directo del socket).
- Todo el parseo ocurre en hilos de trabajo, no en el hilo de la interfaz.
- `CountryResolver` ya no crea ~60 expresiones regulares por canal (era el mayor cuello de botella).
- LibVLC se inicializa en segundo plano después de mostrar la ventana (antes bloqueaba el arranque).
- Guía XMLTV leída en *streaming* en un hilo aparte, con parseo de fechas sin `TryParseExact`.
- Publicación con **ReadyToRun** (menos JIT al iniciar). Requiere internet la primera vez que se publica.

## Más fluidez
- El catálogo se ordena una sola vez por sección (antes se reordenaba al insertar cada elemento).
- Una sola reconstrucción de la lista por acción (antes hasta 5 al cambiar de sección).
- Búsqueda con pequeña espera (180 ms) para no filtrar en cada tecla.
- `CountryGroup` se calcula una vez por elemento.
- Miniaturas decodificadas a tamaño de tarjeta y sin excepciones por logos vacíos al hacer scroll.
- Guardado de favoritos/historial en segundo plano (ya no congela al reproducir).
- Cola de reproducción sin copiar la lista; indicador de buffering sin saturar la interfaz.
- Índice EPG construido una vez por guía importada.

## Verificación pendiente
Este paquete se editó sin SDK de .NET disponible: compílalo en Windows (`dotnet build -c Release`) y prueba antes de usarlo.

# Guía de canales dentro del reproductor
- Abrir/cerrar: botón **☰ Guía** de la barra de controles, o las teclas **G / Enter / Tab** en el reproductor.
- Filtros: tipo (Todo, TV en vivo, Películas, Series) y país·categoría; búsqueda instantánea (basta con empezar a escribir).
- Navegación: ↑↓ para moverse, **Enter** o doble clic para reproducir, **Esc** para cerrar, Ctrl+F para buscar.
- El elemento en reproducción aparece marcado como AHORA; con la guía abierta se puede cambiar de canal sin perder el foco.
- Con una guía XMLTV importada, cada canal muestra el programa actual y el siguiente.
- CH − / CH + y Re Pág / Av Pág recorren la lista que estés viendo en la guía.

# Guía sincronizada con la barra flotante
- La guía aparece y se oculta **en el mismo instante** que la barra de controles (el reproductor avisa con el evento `ControlsVisibilityChanged`).
- Mientras se usa la guía (ratón, teclado, rueda) la barra se mantiene visible; al quedar inactivo, ambas se ocultan juntas.
- Si la guía estaba abierta y se ocultó con la barra, vuelve con ella al mover el ratón o pulsar una tecla; **G / Enter / Tab** la recuperan o la cierran.
- Reaparece sin quitar el teclado al reproductor (`ShowActivated="False"`).

# Efecto cristal (liquid glass / blur)
- Mismos colores de siempre: solo cambia la opacidad. Cada color tiene su par opaco `*.Solid` en `Themes/Glass.xaml`.
- `Services/Glass.cs` aplica el material del sistema (propiedad adjunta `svc:Glass.Backdrop`):
  - Windows 11 22H2+: Mica (ventana principal) y Acrílico (barra, guía y diálogos), con esquinas redondeadas y barra de título oscura.
  - Windows 10 / 11 antiguo: acrílico clásico en barra y guía; el resto usa los colores opacos originales.
  - Con "Efectos de transparencia" desactivados en Windows, o si algo falla, cada ventana usa sus colores opacos: nunca queda transparente.
- La barra y la guía ya no usan `AllowsTransparency` (ventana en capas, que no admite blur): usan `WindowChrome` + DWM.
- Estructura: `Themes/Glass.xaml` (superficies, bordes, estilos de ventana) y `Themes/Controls.xaml` (botones de navegación y píldora) se cargan desde `App.xaml`.
- `GroupNameDialog` ahora usa la misma paleta morada que el resto (antes tenía azules sueltos).

# Listas públicas incluidas (iptv-org y Free-TV)
- `Assets/public-playlists.json` trae 92 listas M3U públicas (canales por categoría, país e idioma, más películas y series).
- En el **primer inicio** se añaden una sola vez como fuentes normales («Público · …»), activas, y se cargan al arrancar. En «Administrar fuentes» puedes desactivarlas o eliminarlas; no se vuelven a añadir solas.
- Las descargas corren **de 6 en 6** (no las 92 a la vez) y cada lista se guarda en la caché cifrada, así que los siguientes inicios muestran el catálogo guardado al instante y solo refrescan lo que tiene más de 6 h.
- Las listas se solapan mucho (el índice completo ya incluye cada categoría, país e idioma): cada canal se conserva **una sola vez**, en la primera lista pública que lo aporta.
- El primer arranque con las 92 listas descarga bastante (decenas de MB) y puede tardar; la ventana y la interfaz siguen fluidas mientras tanto.

# Películas y series en listas M3U (igual que con Xtream)
- Antes: toda entrada de una lista M3U se trataba como TV en vivo, y los botones Películas/Series solo funcionaban con Xtream.
- Ahora `Services/MediaKindClassifier.cs` clasifica cada entrada M3U:
  - direcciones tipo Xtream `…/movie/usuario/clave/123.mp4` → **película**; `…/series/…` → **episodio**;
  - archivos de vídeo (`.mp4 .mkv .avi .mov .wmv .flv .webm .m4v .mpg .mpeg`) → **película**;
  - títulos con `S01E02`, `S1 E2`, `T1E2` o `1x02` → **episodio** de esa serie;
  - todo lo demás (`.m3u8`, `.ts`, sin extensión) sigue siendo **TV en vivo**.
- Los episodios de una misma serie se agrupan en una entrada **Serie**; al abrirla sale la lista de episodios (por temporada y número), igual que con Xtream. Los episodios sueltos no se listan por separado.
- **Películas** y **Series** del menú cargan ahora M3U y Xtream juntos; solo avisan si ninguna fuente activa trae ese tipo.
- La guía del reproductor también filtra por Películas y Series de listas M3U.
- Las listas públicas de iptv-org «Películas» y «Series» son canales 24/7 en directo (`.m3u8`), así que siguen en TV en vivo; no son vídeos bajo demanda.
- Las copias guardadas antes de este cambio se ignoran una vez (se vuelven a descargar) para reclasificarlas.
- Prueba rápida: `Ejemplos/prueba-peliculas-series.m3u` (direcciones de ejemplo, no reproducen) → «Añadir archivo M3U» y mira Películas/Series.

# Mejoras incorporadas desde IPTVnator (código abierto) y catálogo nuevo
Se revisó https://github.com/4gray/iptvnator (reproductor de IPTV de código abierto) para ver qué faltaba en MagisTV. No reparte canales ni credenciales — solo es un reproductor — así que no aportaba contenido, pero sí ideas de funciones:

- **Descargas (`Services/DownloadManager.cs`):** botón «⇩ Descargar» para películas y episodios. Se guardan en la carpeta **Vídeos\MagisTV** del usuario (o en los datos de la app si esa carpeta no está disponible), con progreso en pantalla y sin bloquear la interfaz. Hay una sección nueva **Descargas** en el menú para verlas y reproducirlas sin conexión. La TV en vivo no se puede descargar porque no tiene un final.
- **Reproductor externo (`OpenExternal_Click`):** botón «⧉ Externo» que abre el canal, película o episodio seleccionado con el reproductor predeterminado de Windows (por ejemplo VLC, si está asociado a flujos de red). Si no tienes ninguno asociado, Windows puede abrir el navegador en su lugar.
- No se incorporó timeshift/catch-up ni Stalker/Ministra: dependen de que el proveedor Xtream los ofrezca y necesitan más interfaz (línea de tiempo); queda pendiente si lo quieres más adelante.

## Catálogo de películas de dominio público (Archive.org)
Pedido: "busca para las películas y series". Se investigó qué fuentes de contenido son legales y de verdad accesibles:

- **`Services/ArchiveOrgCatalog.cs`** consulta en vivo la API pública de archive.org (Internet Archive), sin credenciales ni scraping: solo pide ítems de la colección `feature_films` marcados explícitamente como dominio público (`licenseurl` con "publicdomain"). Se añade una sola vez como fuente «Dominio público · Archive.org (películas)», activa, y se recarga igual que cualquier otra fuente (caché de 6 h incluida).
- **No se incluyeron series:** no se encontró una colección de archive.org clara y fiablemente de dominio público para series de TV completas — la colección genérica "television" del sitio mezcla grabaciones subidas sin licencia clara, así que no se usó para evitar contenido de procedencia dudosa.
- Para series reales, sigue siendo necesario tu propio Xtream o una lista M3U con series (ya detectadas automáticamente, ver sección anterior).
- Servicios gratuitos con anuncios como Pluto TV o Tubi no publican una API o lista M3U oficial para integrarlos así; hacerlo requeriría depender de listas no oficiales de terceros, que no es algo que pueda verificar como legítimo.
