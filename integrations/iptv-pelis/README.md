# Integración de catálogo IPTV-PELIS para Blaster-TV

Esta integración adapta la idea del proyecto público [IPTV-PELIS](https://github.com/IPTV-Danilo/IPTV-PELIS) usando sus proveedores de metadatos, sin copiar su implementación ni asumir que el repositorio fuente contiene vídeos reproducibles.

## Qué ofrece

- **Películas:** búsqueda y ficha descriptiva mediante OMDb.
- **Series:** búsqueda y temporadas/episodios mediante TVmaze.
- **Metadatos:** título, año, sinopsis, póster, identificadores y datos de episodios.
- **Reproducción:** el adaptador devuelve `playback_url: null` intencionalmente. La URL de reproducción debe proceder de una lista M3U o fuente autorizada del usuario, no de OMDb o TVmaze.

## Requisitos

- Python 3.10 o superior.
- Dependencia: `requests`.
- Variable de entorno `OMDB_API_KEY` para búsquedas y detalles de películas.
- TVmaze no requiere API key para sus endpoints públicos.

Instala la dependencia:

```bash
python -m pip install -r integrations/iptv-pelis/requirements.txt
```

Configura OMDb en PowerShell (solo para la sesión actual):

```powershell
$env:OMDB_API_KEY = "TU_API_KEY"
```

## Uso

Desde la raíz del repositorio:

```python
from integrations.iptv_pelis.catalog import (
    search_movies,
    movie_details,
    search_series,
    series_episodes,
)

movies = search_movies("The Matrix")
movie = movie_details("tt0133093")
series = search_series("Breaking Bad")

if series:
    episodes = series_episodes(series[0]["tvmaze_id"])
```

## Integración con el reproductor

La capa de interfaz debe consumir estos resultados para poblar las secciones Películas y Series. Cuando el usuario importe una lista M3U autorizada, la aplicación podrá asociar un elemento del catálogo con una entrada de esa lista mediante identificadores/título/año y ofrecer el botón Reproducir solo si existe una URL válida.

No se debe:
- Construir direcciones ficticias como `YOUR_SERVER/...`.
- Tratar una ficha de metadatos como si fuera un stream.
- Publicar claves API en Git.
- Descargar ni redistribuir contenido protegido sin autorización.

## Proveedores y atribución

- OMDb: https://www.omdbapi.com/
- TVmaze: https://www.tvmaze.com/api
- Proyecto de referencia: https://github.com/IPTV-Danilo/IPTV-PELIS

TVmaze publica sus datos bajo CC BY-SA; la aplicación debe mantener la atribución correspondiente y cumplir la condición ShareAlike cuando distribuya datos derivados. Consulta los términos vigentes de cada proveedor antes de distribuir la aplicación.
