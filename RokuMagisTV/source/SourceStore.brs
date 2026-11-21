' Persistencia de la configuración en el registro de Roku (roRegistrySection), equivalente a
' CatalogStore.cs de la versión de escritorio: fuentes, favoritos e historial.
' El registro es pequeño (decenas de KB), así que aquí solo se guarda configuración, nunca las
' listas de canales; esas van a la caché de archivos (CacheStore.brs).

function SS_Section() as Object
    return CreateObject("roRegistrySection", "MagisTV")
end function

function SS_ReadJson(key as String, fallback as Dynamic) as Dynamic
    sec = SS_Section()
    if sec.Exists(key)
        data = ParseJson(sec.Read(key))
        if data <> invalid then return data
    end if
    return fallback
end function

sub SS_WriteJson(key as String, value as Dynamic)
    sec = SS_Section()
    sec.Write(key, FormatJson(value))
    sec.Flush()
end sub

function SS_NewId() as String
    di = CreateObject("roDeviceInfo")
    return di.GetRandomUUID()
end function

' Fuente: { id, name, type: "M3UUrl" | "Xtream", address, username, password, enabled }
function SS_LoadSources() as Object
    return SS_ReadJson("sources", [])
end function

sub SS_SaveSources(sources as Object)
    SS_WriteJson("sources", sources)
end sub

function SS_NewM3uSource(url as String) as Object
    name = url
    schemeEnd = Instr(1, url, "://")
    if schemeEnd > 0
        rest = Mid(url, schemeEnd + 3)
        slash = Instr(1, rest, "/")
        if slash > 0 then name = left(rest, slash - 1) else name = rest
    end if
    return { id: SS_NewId(), name: "M3U · " + name, type: "M3UUrl", address: url, username: "", password: "", enabled: true }
end function

function SS_NewXtreamSource(server as String, user as String, pass as String) as Object
    host = server
    schemeEnd = Instr(1, server, "://")
    if schemeEnd > 0 then host = Mid(server, schemeEnd + 3)
    slash = Instr(1, host, "/")
    if slash > 0 then host = left(host, slash - 1)
    return { id: SS_NewId(), name: "Xtream · " + host + " · " + user, type: "Xtream", address: server, username: user, password: pass, enabled: true }
end function

' Favoritos: conjunto de claves "sourceId|url".
function SS_FavoriteKey(item as Object) as String
    return item.sourceId + "|" + item.url
end function

function SS_LoadFavorites() as Object
    favs = {}
    for each key in SS_ReadJson("favorites", [])
        favs[key] = true
    end for
    return favs
end function

sub SS_SaveFavorites(favs as Object)
    keys = []
    for each key in favs
        keys.Push(key)
    end for
    SS_WriteJson("favorites", keys)
end sub

' Historial: últimos 40 elementos reproducidos (solo lo necesario para volver a abrirlos).
function SS_LoadHistory() as Object
    return SS_ReadJson("history", [])
end function

sub SS_AddHistory(item as Object)
    history = SS_LoadHistory()
    key = SS_FavoriteKey(item)
    kept = [{ name: item.name, url: item.url, kind: item.kind, sourceId: item.sourceId, logo: item.logo, category: item.category, source: item.source, seriesKey: item.seriesKey }]
    for each h in history
        if h.sourceId + "|" + h.url <> key and kept.Count() < 40 then kept.Push(h)
    end for
    SS_WriteJson("history", kept)
end sub
