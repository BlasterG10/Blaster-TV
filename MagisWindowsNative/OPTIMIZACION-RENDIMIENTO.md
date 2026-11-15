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
