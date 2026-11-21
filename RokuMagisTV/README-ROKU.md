# MagisTV para Roku

Canal de Roku (BrightScript + SceneGraph) con las mismas funciones principales que la versión de Windows.

## Instalar (modo desarrollador)

**Opción A — script (Windows):**
```powershell
cd RokuMagisTV
.\instalar-en-roku.ps1 -RokuIp 192.168.1.50 -Password TU_CLAVE
```

**Opción B — a mano:**
1. Comprime el **contenido** de `RokuMagisTV` (`manifest`, `source`, `components`, `images`) en un `.zip`. El `manifest` debe quedar en la raíz del zip, no dentro de una carpeta.
2. En el navegador abre `http://IP_DE_TU_ROKU`, entra con `rokudev` y tu clave.
3. **Upload** → elige el zip → **Install**.

Cada instalación reemplaza a la anterior. Las fuentes y favoritos se conservan (están en el registro del Roku).

## Uso con el control remoto

| Dónde | Tecla | Acción |
|---|---|---|
| Listas | ◀ ▶ | Moverse entre menú, categorías y contenido |
| Contenido | OK / ▶ | Reproducir (o abrir la serie) |
| Contenido | ✱ | Añadir o quitar de favoritos |
| Fuentes | OK | Activar o desactivar una fuente |
| Fuentes | ✱ dos veces | Eliminar una fuente |
| Reproductor | OK o ◀ (TV en vivo) | Abrir la guía de canales |
| Reproductor | ▲ ▼ | Canal anterior / siguiente |
| Reproductor | ◀ ▶ / ◀◀ ▶▶ (películas) | Retroceder 15 s / adelantar 30 s / ±60 s |
| Reproductor | ✱ | Favorito |
| Reproductor | Atrás | Ocultar barra y guía; otra vez: salir |

La barra flotante y la guía se ocultan **juntas** a los 6 segundos sin usar el control (solo mientras el vídeo se reproduce).

## Fuentes

- **Lista M3U por URL.**
- **Cuenta Xtream:** servidor, usuario y contraseña en tres pasos. También puedes pegar el enlace `get.php?username=...&password=...` completo en el primer paso y se rellenan solos.

## Qué incluye

- TV en vivo, Películas y Series agrupadas por categoría.
- Detección de películas y series en listas M3U (`/movie/`, `/series/`, `.mp4/.mkv`, `S01E02`, `1x02`), igual que en Windows.
- Series con episodios por temporada (M3U y Xtream; en Xtream se piden al abrir la serie). Al terminar un episodio pasa al siguiente.
- Guía EPG: Xtream con `get_short_epg` (solo para los canales en pantalla) y XMLTV de listas M3U (`url-tvg`).
- Favoritos, «Continuar viendo» (últimos 40) y búsqueda.
- Caché de 6 h en el Roku: si no hay conexión se muestra la última copia guardada.
- Hasta 3 cargas a la vez para no saturar el dispositivo.

## Límites propios de Roku

Un Roku tiene mucha menos memoria que un PC, así que:

- Listas M3U de más de **20 MB** no se cargan (para esas, usa la cuenta Xtream del mismo proveedor).
- Guías XMLTV de más de **12 MB** se omiten; solo se guardan las próximas 8 horas.
- Cada categoría muestra hasta **1.500** elementos; el resto se encuentra con «Buscar».
- No se añaden las 92 listas públicas automáticamente, ni hay descargas ni efecto de cristal (Roku no tiene desenfoque); se usan los mismos colores con transparencia.
- Las listas M3U desde archivo local no aplican: en Roku se añaden por URL.

## Estructura

| Windows (WPF) | Roku |
|---|---|
| `Services/M3uParser.cs` | `source/M3uParser.brs` |
| `Services/MediaKindClassifier.cs` | `source/MediaKindClassifier.brs` |
| `Services/XtreamClient.cs` | `source/XtreamClient.brs` |
| `Services/XmlTvService.cs` | `source/EpgService.brs` |
| `Services/SourceCache.cs` | `source/CacheStore.brs` |
| `Services/CatalogStore.cs` | `source/SourceStore.brs` |
| `Task.Run` / carga en segundo plano | `components/tasks/LoaderTask` |
| `MainWindow` | `components/MainScene` |
| `PlayerWindow` + barra + guía | `components/screens/VideoScreen` |

## Depurar

Si algo falla, la consola del Roku muestra el error exacto y la línea:
```
telnet IP_DE_TU_ROKU 8085
```
(En Windows: activa «Cliente Telnet» en «Características de Windows», o usa PuTTY en modo Telnet, puerto 8085.)
