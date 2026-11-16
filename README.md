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

## Aplicación nativa para Windows

El código fuente WPF de Windows está en [`desktop/MagisWindowsNative`](desktop/MagisWindowsNative/README.md). Este paquete incluye reproducción LibVLC, fuentes M3U/Xtream y el catálogo abierto de películas y series. Compila con .NET 10 SDK en Windows:

```powershell
dotnet build desktop/MagisWindowsNative/MagisWindowsNative.csproj -c Release
```

El catálogo abierto usa feeds de Archive Watch y PublicDomainM3U. La app conserva los enlaces originales y refresca sus fuentes cada seis horas mientras está abierta. Las fuentes describen sus obras como de dominio público o libres para compartir; verifica los derechos aplicables antes de redistribuir.
