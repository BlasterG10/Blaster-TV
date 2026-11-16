# Hallazgos técnicos del APK suministrado

Archivo analizado: `Magistv+.apk` (31,679,909 bytes).

Hallazgos verificables en la estructura del APK y cadenas de `classes.dex`:
- Existe `classes.dex`.
- Namespace/clases visibles: `com.rebrandmx.magicstv.data.CatalogStore` y `ExternalEpgRepo`.
- Referencias de acciones Xtream: `get_live_categories`, `get_live_streams`, `get_vod_categories`, `get_vod_streams`, `get_vod_info`, `get_series_categories`, `get_series`, `get_series_info`.
- Referencia a `player_api.php`.
- Referencias a EPG externo: `epg_listings`, `epg_channel_id`, `external_epg.json`.
- Referencias TMDB: `https://api.themoviedb.org/3`, `https://image.tmdb.org/t/p/`.
- URL codificada encontrada: `https://indieraw.mx/2/magistv` (no se usa en el proyecto).
- Bibliotecas Android ARM `libvlc.so`, `libvlcjni.so` y bibliotecas OpenVPN.

Este informe describe indicios estáticos, no demuestra que los servicios externos sigan activos ni que cada función esté operativa. El proyecto Windows se implementa de forma independiente y no reutiliza endpoints privados, credenciales, claves o recursos protegidos.
