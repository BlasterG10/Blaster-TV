# Blaster-TV

Aplicación IPTV en desarrollo para gestionar listas M3U y reproducir fuentes de contenido autorizadas.

## Integración de catálogo

Se añadió un adaptador de metadatos basado en la idea del proyecto [IPTV-PELIS](https://github.com/IPTV-Danilo/IPTV-PELIS):

- OMDb: búsqueda y fichas de películas.
- TVmaze: búsqueda de series y episodios.
- Paquete de integración: [integrations/iptv_pelis](integrations/iptv_pelis/README.md).

Esta integración proporciona metadatos, no alojamiento de vídeos ni URLs de reproducción. Las fuentes reproducibles deben proceder de listas M3U o servicios autorizados por el usuario.

## Configuración del catálogo

Instalar dependencias:

```bash
python -m pip install -r integrations/iptv_pelis/requirements.txt
```

Para consultar películas, configurar la variable de entorno `OMDB_API_KEY`. Las búsquedas públicas de TVmaze no requieren clave.

## Memoria obligatoria del proyecto

Antes de cada intervención técnica, leer [PROJECT_MEMORY.md](PROJECT_MEMORY.md). Este documento mantiene las decisiones, el estado, los requisitos y el historial del proyecto.
