# Magis Windows Native — iteración ligera Xtream + interfaz moderna

## Cambios
- Mantiene WPF nativo y LibVLCSharp: no añade un framework visual ni un segundo reproductor.
- Catálogo con tarjetas compactas, miniaturas del proveedor y `VirtualizingStackPanel` en modo Recycling.
- Las llamadas Xtream siguen siendo asíncronas; timeout HTTP de 25 segundos.
- Resuelve IDs de categoría con `get_live_categories`, `get_vod_categories` y `get_series_categories`; si el servidor no publica categorías, continúa cargando el catálogo.
- Series: doble clic, Enter o botón Reproducir abre episodios usando `get_series_info`; el episodio elegido se envía al reproductor existente.
- Historial y favoritos cifrados con DPAPI de Windows. El archivo JSON de catálogo de versiones anteriores se migra al formato cifrado al abrir.
- La opción de recordar credenciales ya utiliza DPAPI en el perfil actual de Windows.

## Probar con tu cuenta
1. Extrae el ZIP en una carpeta nueva.
2. Abre `MagisWindowsNative.csproj` en Visual Studio.
3. Restaura NuGet y compila en Release x64.
4. Ejecuta y pulsa **Conectar Xtream**.
5. Introduce la URL base, usuario y contraseña que te dio el proveedor. No incluyas `/player_api.php` en la URL base.
6. Pulsa TV en vivo, Películas o Series. En Series, abre un título para ver sus episodios.

## Estado de validación
- Validación XML realizada para todos los XAML y el `.csproj`.
- No se pudo ejecutar `dotnet build` en el entorno de generación porque no tiene instalado el SDK .NET. La compilación final debe confirmarse en Visual Studio/Windows.
- Compatibilidad real de reproducción depende de los formatos y permisos de la cuenta/proveedor.

## Iteración de organización de canales — grupos, países y servidores
- Se añade selector de vista para organizar el catálogo por categoría del proveedor, país detectado, host/servidor de origen o grupos personalizados.
- Xtream usa `get_live_categories` / `get_vod_categories` / `get_series_categories` y enlaza `category_id` con el nombre de categoría devuelto por el proveedor.
- M3U conserva `group-title`, `tvg-country` y `tvg-logo`; cada canal se etiqueta también con el host de su URL para agrupar por servidor.
- Xtream etiqueta los elementos con el host de conexión, sin mostrar usuario ni contraseña.
- Se pueden crear grupos propios y asignar uno o varios canales con Ctrl/Shift. La asignación se conserva cifrada junto con favoritos e historial mediante el almacenamiento DPAPI existente.
- La lista mantiene virtualización WPF; la nueva organización se realiza con filtros en memoria y no agrega librerías ni servicios externos.

## Navegación por Inicio, TV en vivo y Catálogo — guía v3
- La tarjeta destacada/área de inicio se conserva únicamente en **Inicio**; se oculta en TV en vivo, Catálogo, favoritos e historial para dar más espacio al contenido.
- Se añade una vista **Catálogo** unificada. Al abrirla, si hay conexión Xtream, carga bajo demanda los tipos que falten: TV en vivo, películas y series. Evita volver a solicitar los catálogos ya cargados durante la sesión.
- El Catálogo agrupa primero por tipo de contenido y después por país o región. El selector permite filtrar por país/región, categoría, servidor o grupos personalizados sin alterar el orden jerárquico principal.
- **TV en vivo** ahora abre una guía en la ventana principal, con canales agrupados por país y categoría; no muestra la tarjeta destacada ni una vista de reproducción previa. El canal se reproduce al seleccionarlo y pulsar Reproducir o hacer doble clic.
- Se amplía la guía de canales para mostrar programa actual y siguiente cuando se importa un XMLTV local. La asociación prioriza `tvg-id`/`epg_channel_id` y recurre al nombre del canal cuando no hay coincidencia por ID.
- Se reconocen etiquetas de región frecuentes (Latinoamérica, Caribe, Centroamérica, Sudamérica, Norteamérica, Europa, África, Asia e Internacional) cuando el país no está especificado.
- Se conserva la virtualización WPF y no se añaden paquetes NuGet ni motores de reproducción adicionales.
- Validación de esta entrega: todos los XAML pasan el análisis XML, no hay `x:Name` duplicados y los manejadores XAML referenciados existen. No se pudo ejecutar `dotnet build` en el entorno de empaquetado porque no dispone del SDK .NET; compilar y probar en Windows antes de usar como versión estable.

## Fuentes múltiples v4
- Nuevo panel Administrar fuentes.
- Varias listas M3U/M3U8 por URL o archivo local pueden coexistir.
- Varias conexiones Xtream independientes se conservan con credenciales cifradas DPAPI.
- Las fuentes activas se combinan en la guía de TV y el catálogo; se pueden activar, desactivar y eliminar.
- Actualización de fuentes bajo demanda y carga inicial de las fuentes activas guardadas.
- El filtro Servidor muestra el nombre de la fuente, no solo el host de la URL de stream.
- Las series mantienen el vínculo con la conexión Xtream de origen para obtener sus episodios.
- La lista de administración oculta query strings de URLs M3U para no exponer tokens en pantalla.
- La barra lateral pasa a tener scroll para conservar accesibilidad en ventanas más bajas.
