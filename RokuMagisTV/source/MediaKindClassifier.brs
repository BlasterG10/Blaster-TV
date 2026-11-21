' Puerto de Services/MediaKindClassifier.cs (versión Windows) a BrightScript.
' Una lista M3U no dice si una entrada es un canal en vivo, una película o un episodio de
' serie: se decide por la propia entrada, igual que en la versión de escritorio:
'   • direcciones al estilo Xtream  .../movie/usuario/clave/123.mp4  y  .../series/usuario/clave/456.mkv
'   • archivos de vídeo (.mp4, .mkv, .avi...) en vez de un flujo en vivo (.m3u8, .ts, sin extensión)
'   • marcadores de episodio en el título: "Serie S01E02", "Serie 1x02"
' Los canales en vivo 24/7 (incluidas las categorías "Películas/Series" de listas públicas, que
' son flujos .m3u8) se quedan como TV en vivo: no son vídeo bajo demanda.

function MK_VideoExtensions() as Object
    return [".mp4", ".m4v", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".mpg", ".mpeg"]
end function

' Devuelve un objeto { kind, seriesTitle, season, episode }. kind es "Live", "Movie" o "Episode".
function MK_Classify(url as String, name as String) as Object
    result = { kind: "Live", seriesTitle: "", season: 0, episode: 0 }

    path = MK_PathOf(url)
    isXtreamMovie = instr(1, path, "/movie/") > 0
    isXtreamSeries = instr(1, path, "/series/") > 0
    isVideoFile = MK_HasVideoExtension(path)

    episode = MK_TryEpisode(name)
    if episode <> invalid
        result.kind = "Episode"
        result.seriesTitle = episode.title
        result.season = episode.season
        result.episode = episode.episode
        return result
    end if

    if isXtreamSeries
        result.kind = "Episode"
        result.seriesTitle = name.Trim()
        result.season = 1
        result.episode = 0
        return result
    end if

    if isXtreamMovie or isVideoFile
        result.kind = "Movie"
        return result
    end if

    return result
end function

function MK_PathOf(url as String) as String
    schemeEnd = instr(1, url, "://")
    if schemeEnd = 0 then return ""
    start = instr(schemeEnd + 3, url, "/")
    if start = 0 then return ""
    path = mid(url, start)
    q = instr(1, path, "?")
    if q > 0 then path = left(path, q - 1)
    h = instr(1, path, "#")
    if h > 0 then path = left(path, h - 1)
    return LCase(path)
end function

function MK_HasVideoExtension(path as String) as Boolean
    for each ext in MK_VideoExtensions()
        extLen = Len(ext)
        if Len(path) >= extLen and LCase(right(path, extLen)) = ext
            return true
        end if
    end for
    return false
end function

' SxxEyy / S1 E2 / T1E2 (regex con roRegex, insensible a mayúsculas).
function MK_TryEpisode(name as String) as Dynamic
    reSxE = CreateObject("roRegex", "^(.*?)[\s._\-:]*[\[(]?\b[st]\s*(\d{1,3})\s*[._\-]?\s*e[p]?\.?\s*(\d{1,4})\b", "i")
    match = reSxE.Match(name)
    if match.Count() >= 4
        return MK_BuildEpisodeMatch(match[1], match[2], match[3])
    end if

    ' 1x02 / 12x034
    reNxN = CreateObject("roRegex", "^(.*?)[\s._\-:]+(\d{1,2})x(\d{2,3})\b", "i")
    match = reNxN.Match(name)
    if match.Count() >= 4
        return MK_BuildEpisodeMatch(match[1], match[2], match[3])
    end if

    return invalid
end function

function MK_BuildEpisodeMatch(rawTitle as String, rawSeason as String, rawEpisode as String) as Dynamic
    title = MK_TrimEdges(rawTitle)
    if Len(title) = 0 then return invalid
    return { title: title, season: rawSeason.ToInt(), episode: rawEpisode.ToInt() }
end function

function MK_TrimEdges(s as String) as String
    chars = [" ", "-", ".", "_", ":", "[", "("]
    result = s.Trim()
    changed = true
    while changed and Len(result) > 0
        changed = false
        for each c in chars
            if left(result, 1) = c
                result = mid(result, 2)
                changed = true
            end if
        end for
        result = result.Trim()
    end while
    return result
end function
