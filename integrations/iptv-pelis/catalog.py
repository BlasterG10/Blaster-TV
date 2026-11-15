"""Metadata catalog adapter for Blaster-TV.

This module retrieves descriptive metadata only. It never invents video URLs.
Playback URLs must come from a user's authorized M3U/source and be attached by
the application layer after matching a catalog item.
"""
from __future__ import annotations

import os
from typing import Any
from urllib.parse import urlencode

import requests

OMDB_URL = "https://www.omdbapi.com/"
TVMAZE_URL = "https://api.tvmaze.com"
TIMEOUT = 12
USER_AGENT = "Blaster-TV/1.0 (metadata catalog)"


class CatalogError(RuntimeError):
    """Raised when a metadata provider returns an error or invalid response."""


def _get_json(url: str, params: dict[str, Any] | None = None) -> Any:
    try:
        response = requests.get(
            url,
            params=params,
            timeout=TIMEOUT,
            headers={"User-Agent": USER_AGENT},
        )
        response.raise_for_status()
        return response.json()
    except requests.RequestException as exc:
        raise CatalogError(f"Error consultando el proveedor: {exc}") from exc
    except ValueError as exc:
        raise CatalogError("El proveedor devolvió una respuesta JSON inválida.") from exc


def _normalize_movie(item: dict[str, Any]) -> dict[str, Any]:
    return {
        "kind": "movie",
        "title": item.get("Title") or "",
        "year": item.get("Year") or "",
        "poster": item.get("Poster") if item.get("Poster") not in (None, "N/A") else None,
        "imdb_id": item.get("imdbID"),
        "provider": "OMDb",
        "playback_url": None,
    }


def search_movies(query: str, page: int = 1) -> list[dict[str, Any]]:
    """Search OMDb for movies. Requires OMDB_API_KEY in the environment."""
    api_key = os.getenv("OMDB_API_KEY")
    if not api_key:
        raise CatalogError("Falta configurar la variable de entorno OMDB_API_KEY.")

    data = _get_json(
        OMDB_URL,
        {"apikey": api_key, "s": query.strip(), "type": "movie", "page": page},
    )
    if data.get("Response") != "True":
        return []
    return [_normalize_movie(item) for item in data.get("Search", [])]


def movie_details(imdb_id: str) -> dict[str, Any]:
    """Return OMDb details for one movie, without a fabricated stream URL."""
    api_key = os.getenv("OMDB_API_KEY")
    if not api_key:
        raise CatalogError("Falta configurar la variable de entorno OMDB_API_KEY.")

    data = _get_json(OMDB_URL, {"apikey": api_key, "i": imdb_id, "plot": "full"})
    if data.get("Response") != "True":
        raise CatalogError(data.get("Error", "No se encontró la película."))

    return {
        "kind": "movie",
        "title": data.get("Title") or "",
        "year": data.get("Year") or "",
        "overview": data.get("Plot") if data.get("Plot") not in (None, "N/A") else "",
        "poster": data.get("Poster") if data.get("Poster") not in (None, "N/A") else None,
        "imdb_id": data.get("imdbID"),
        "rating": data.get("imdbRating"),
        "runtime": data.get("Runtime"),
        "genres": [part.strip() for part in (data.get("Genre") or "").split(",") if part.strip()],
        "provider": "OMDb",
        "playback_url": None,
    }


def _normalize_show(item: dict[str, Any]) -> dict[str, Any]:
    show = item.get("show", item)
    image = show.get("image") or {}
    return {
        "kind": "series",
        "title": show.get("name") or "",
        "year": (show.get("premiered") or "")[:4],
        "overview": show.get("summary") or "",
        "poster": image.get("original") or image.get("medium"),
        "tvmaze_id": show.get("id"),
        "tvmaze_url": show.get("url"),
        "provider": "TVmaze",
        "playback_url": None,
    }


def search_series(query: str) -> list[dict[str, Any]]:
    """Search TVmaze for series metadata; no API key is required."""
    if not query.strip():
        return []
    data = _get_json(f"{TVMAZE_URL}/search/shows", {"q": query.strip()})
    return [_normalize_show(item) for item in data if item.get("show")]


def series_episodes(tvmaze_id: int) -> list[dict[str, Any]]:
    """List episodes for a TVmaze show. Episode metadata is not video content."""
    data = _get_json(f"{TVMAZE_URL}/shows/{int(tvmaze_id)}/episodes")
    return [
        {
            "kind": "episode",
            "title": episode.get("name") or "",
            "season": episode.get("season"),
            "number": episode.get("number"),
            "overview": episode.get("summary") or "",
            "airdate": episode.get("airdate"),
            "tvmaze_id": episode.get("id"),
            "provider": "TVmaze",
            "playback_url": None,
        }
        for episode in data
    ]
