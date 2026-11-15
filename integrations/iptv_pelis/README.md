# Catálogo IPTV-PELIS para Blaster-TV

Adaptador compatible con el enfoque del repositorio de referencia [IPTV-PELIS](https://github.com/IPTV-Danilo/IPTV-PELIS), utilizando proveedores de metadatos mediante una implementación propia.

## Funciones

- **Películas:** búsqueda y detalles mediante OMDb.
- **Series:** búsqueda y episodios mediante TVmaze.
- **Datos normalizados:** título, año, sinopsis, póster, identificadores y datos de episodios.
- **Reproducción:** `playback_url` queda en `null`. OMDb y TVmaze aportan metadatos, no URLs de vídeo.

## Requisitos

- Python 3.10 o superior.
- Instalar dependencias desde la raíz del repositorio:

```bash
python -m pip install -r integrations/iptv_pelis/requirements.txt
```

- Configurar `OMDB_API_KEY` para las funciones de películas. TVmaze dispone de endpoints públicos sin clave.

PowerShell:

```powershell
$env:OMDB_API_KEY = "TU_API_KEY"
```

## Ejemplo

Desde la raíz del repositorio:

```python
from integrations.iptv_pelis.catalog import (
    search_movies, movie_details, search_series, series_episodes,
)

movies = search_movies("The Matrix")
details = movie_details("tt0133093")
shows = search_series("Breaking Bad")
episodes = series_episodes(shows[0]["tvmaze_id"]) if shows else []
```

## Conexión con las listas M3U

La interfaz debe mostrar los metadatos en las secciones Películas y Series. El botón **Reproducir** solo debe activarse cuando el elemento haya sido asociado a una entrada M3U o fuente de reproducción válida que el usuario tenga derecho a utilizar.

No generar URLs de ejemplo como `YOUR_SERVER/...`, no confundir pósteres/sinopsis con vídeos y no subir claves API al repositorio.

## Atribución y fuentes

- OMDb: https://www.omdbapi.com/
- TVmaze API: https://www.tvmaze.com/api
- Proyecto de referencia: https://github.com/IPTV-Danilo/IPTV-PELIS

TVmaze indica que sus datos se publican bajo CC BY-SA. La aplicación debe atribuir a TVmaze y cumplir las condiciones ShareAlike aplicables a datos derivados.
