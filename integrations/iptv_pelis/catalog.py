"""Blaster-TV metadata catalog adapter for OMDb and TVmaze."""
from __future__ import annotations

import os
from typing import Any

import requests

OMDB_URL = "https://www.omdbapi.com/"
TVMAZE_URL = "https://api.tvmaze.com"
TIMEOUT = 12
USER_AGENT = "Blaster-TV/1.0 (metadata catalog)"


class CatalogError(RuntimeError):
    """Provider/network/configuration error."""


def _get_json(url: str, params: dict[str, Any] | None = None) -> Any:
    try:
        response = requests.get(
            url, params=params, timeout=TIMEOUT,
            headers={"User-Agent": USER_AGENT},
        )
        response.raise_for_status()
        return response.json()
    except requests.RequestException as exc:
        raise CatalogError(f"Error consultando el proveedor: {exc}") from exc
    except ValueError as exc:
        raise CatalogError("El proveedor devolvió JSON inválido.") from exc


def _omdb_key() -> str:
    key = os.getenv("OMDB_API_KEY")
    if not key:
        raise CatalogError("Configura la variable de entorno OMDB_API_KEY.")
    return key


def search_movies(query: str, page: int = 1) -> list[dict[str, Any]]:
    """Search movie metadata. Requires OMDB_API_KEY."""
    if not query.strip():
        return []
    data = _get_json(OMDB_URL, {
        "apikey": _omdb_key(), "s": query.strip(),
        "type": "movie", "page": max(1, int(page)),
    })
    if data.get("Response") != "True":
        return []
    return [{
        "kind": "movie",
        "title": item.get("Title") or "",
        "year": item.get("Year") or "",
        "poster": item.get("Poster") if item.get("Poster") not in (None, "N/A") else None,
        "imdb_id": item.get("imdbID"),
        "provider": "OMDb",
        "playback_url": None,
    } for item in data.get("Search", [])]


def movie_details(imdb_id: str) -> dict[str, Any]:
    """Fetch descriptive movie metadata, never a stream URL."""
    data = _get_json(OMDB_URL, {
        "apikey": _omdb_key(), "i": imdb_id, "plot": "full",
    })
    if data.get("Response") != "True":
        raise CatalogError(data.get("Error", "Película no encontrada."))
    return {
        "kind": "movie",
        "title": data.get("Title") or "",
        "year": data.get("Year") or "",
        "overview": data.get("Plot") if data.get("Plot") not in (None, "N/A") else "",
        "poster": data.get("Poster") if data.get("Poster") not in (None, "N/A") else None,
        "imdb_id": data.get("imdbID"),
        "rating": data.get("imdbRating"),
        "runtime": data.get("Runtime"),
        "genres": [g.strip() for g in (data.get("Genre") or "").split(",") if g.strip()],
        "provider": "OMDb",
        "playback_url": None,
    }


def search_series(query: str) -> list[dict[str, Any]]:
    """Search TVmaze public show metadata; no API key required."""
    if not query.strip():
        return []
    results = _get_json(f"{TVMAZE_URL}/search/shows", {"q": query.strip()})
    normalized = []
    for result in results:
        show = result.get("show") or {}
        image = show.get("image") or {}
        normalized.append({
            "kind": "series",
            "title": show.get("name") or "",
            "year": (show.get("premiered") or "")[:4],
            "overview": show.get("summary") or "",
            "poster": image.get("original") or image.get("medium"),
            "tvmaze_id": show.get("id"),
            "tvmaze_url": show.get("url"),
            "provider": "TVmaze",
            "playback_url": None,
        })
    return normalized


def series_episodes(tvmaze_id: int) -> list[dict[str, Any]]:
    """Fetch episode metadata. Episode records are not video streams."""
    results = _get_json(f"{TVMAZE_URL}/shows/{int(tvmaze_id)}/episodes")
    return [{
        "kind": "episode",
        "title": episode.get("name") or "",
        "season": episode.get("season"),
        "number": episode.get("number"),
        "overview": episode.get("summary") or "",
        "airdate": episode.get("airdate"),
        "tvmaze_id": episode.get("id"),
        "provider": "TVmaze",
        "playback_url": None,
    } for episode in results]
