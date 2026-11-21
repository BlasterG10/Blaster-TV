' Puerto de Services/XtreamClient.cs a BrightScript. Las llamadas son síncronas (roUrlTransfer)
' porque siempre se ejecutan dentro de LoaderTask (componente Task), que ya corre en su propio
' hilo: bloquear aquí no congela la interfaz.
'
' BrightScript es estricto con los tipos (comparar Integer con Boolean, o sumar String + Integer,
' lanza un error en tiempo de ejecución), por eso todo pasa por XC_Str / XC_IdStr / XC_ToInt.

function XC_BaseUrl(server as String) as String
    s = server.Trim()
    while Len(s) > 0 and right(s, 1) = "/"
        s = left(s, Len(s) - 1)
    end while
    return s
end function

function XC_Encode(s as String) as String
    xfer = CreateObject("roUrlTransfer")
    return xfer.Escape(s)
end function

function XC_ApiUrl(server as String, user as String, pass as String, action as String, extra as String) as String
    url = XC_BaseUrl(server) + "/player_api.php?username=" + XC_Encode(user) + "&password=" + XC_Encode(pass)
    if Len(action) > 0 then url = url + "&action=" + action
    if Len(extra) > 0 then url = url + "&" + extra
    return url
end function

function XC_Get(url as String) as String
    xfer = CreateObject("roUrlTransfer")
    xfer.SetCertificatesFile("common:/certs/ca-bundle.crt")
    xfer.InitClientCertificates()
    xfer.SetUrl(url)
    xfer.AddHeader("User-Agent", "MagisTV-Roku/1.0")
    xfer.EnableEncodings(true)
    body = xfer.GetToString()
    if body = invalid then return ""
    return body
end function

function XC_Str(v as Dynamic) as String
    if v = invalid then return ""
    if GetInterface(v, "ifString") <> invalid then return v
    return ""
end function

function XC_IdStr(v as Dynamic) as String
    if v = invalid then return ""
    if GetInterface(v, "ifString") <> invalid then return v
    if GetInterface(v, "ifInt") <> invalid or GetInterface(v, "ifFloat") <> invalid or GetInterface(v, "ifDouble") <> invalid
        return Str(v).Trim()
    end if
    return ""
end function

function XC_ToInt(v as Dynamic) as Integer
    if v = invalid then return 0
    if GetInterface(v, "ifInt") <> invalid then return v
    if GetInterface(v, "ifFloat") <> invalid or GetInterface(v, "ifDouble") <> invalid then return Int(v)
    if GetInterface(v, "ifString") <> invalid then return v.ToInt()
    return 0
end function

' Valida las credenciales contra player_api.php (sin acción: devuelve user_info/server_info).
function XC_Login(server as String, user as String, pass as String) as Object
    result = { ok: false, message: "" }
    body = XC_Get(XC_ApiUrl(server, user, pass, "", ""))
    if Len(body) = 0
        result.message = "No se pudo conectar con el servidor."
        return result
    end if
    data = ParseJson(body)
    if data = invalid or data.user_info = invalid
        result.message = "Respuesta inesperada del servidor."
        return result
    end if
    auth = data.user_info.auth
    authOk = false
    if auth <> invalid
        if GetInterface(auth, "ifBoolean") <> invalid
            authOk = auth
        else if GetInterface(auth, "ifString") <> invalid
            authOk = (auth = "1")
        else
            authOk = (XC_ToInt(auth) = 1)
        end if
    end if
    if authOk
        result.ok = true
    else
        result.message = "Usuario o contraseña incorrectos."
    end if
    return result
end function

function XC_GetCategoryMap(server as String, user as String, pass as String, action as String) as Object
    map = {}
    data = ParseJson(XC_Get(XC_ApiUrl(server, user, pass, action, "")))
    if data <> invalid
        for each row in data
            id = XC_IdStr(row.category_id)
            if Len(id) > 0 then map[id] = XC_Str(row.category_name)
        end for
    end if
    return map
end function

function XC_CategoryName(map as Object, id as Dynamic) as String
    key = XC_IdStr(id)
    if Len(key) > 0 and map.DoesExist(key) then return map[key]
    return "General"
end function

function XC_NewItem(name as String, url as String, category as String, logo as String, epgId as String, kind as String, sourceId as String, sourceName as String) as Object
    return {
        name: name
        url: url
        category: category
        logo: logo
        epgId: epgId
        source: sourceName
        sourceId: sourceId
        kind: kind
        seriesKey: ""
        season: 0
        episode: 0
        isFavorite: false
    }
end function

function XC_GetLiveStreams(server as String, user as String, pass as String, sourceId as String, sourceName as String) as Object
    categories = XC_GetCategoryMap(server, user, pass, "get_live_categories")
    data = ParseJson(XC_Get(XC_ApiUrl(server, user, pass, "get_live_streams", "")))
    result = []
    if data = invalid then return result
    base = XC_BaseUrl(server) + "/live/" + XC_Encode(user) + "/" + XC_Encode(pass) + "/"
    for each row in data
        id = XC_IdStr(row.stream_id)
        if Len(id) > 0
            item = XC_NewItem(XC_Str(row.name), base + id + ".m3u8", XC_CategoryName(categories, row.category_id), XC_Str(row.stream_icon), XC_Str(row.epg_channel_id), "Live", sourceId, sourceName)
            item.streamId = id
            result.Push(item)
        end if
    end for
    return result
end function

function XC_GetVodStreams(server as String, user as String, pass as String, sourceId as String, sourceName as String) as Object
    categories = XC_GetCategoryMap(server, user, pass, "get_vod_categories")
    data = ParseJson(XC_Get(XC_ApiUrl(server, user, pass, "get_vod_streams", "")))
    result = []
    if data = invalid then return result
    base = XC_BaseUrl(server) + "/movie/" + XC_Encode(user) + "/" + XC_Encode(pass) + "/"
    for each row in data
        id = XC_IdStr(row.stream_id)
        if Len(id) > 0
            ext = XC_Str(row.container_extension)
            if Len(ext) = 0 then ext = "mp4"
            result.Push(XC_NewItem(XC_Str(row.name), base + id + "." + ext, XC_CategoryName(categories, row.category_id), XC_Str(row.stream_icon), "", "Movie", sourceId, sourceName))
        end if
    end for
    return result
end function

' Las series se listan sin episodios (igual que en la versión de escritorio); los episodios se
' piden solo cuando el usuario abre la serie, con XC_GetSeriesEpisodes.
function XC_GetSeries(server as String, user as String, pass as String, sourceId as String, sourceName as String) as Object
    categories = XC_GetCategoryMap(server, user, pass, "get_series_categories")
    data = ParseJson(XC_Get(XC_ApiUrl(server, user, pass, "get_series", "")))
    result = []
    if data = invalid then return result
    for each row in data
        id = XC_IdStr(row.series_id)
        if Len(id) > 0
            item = XC_NewItem(XC_Str(row.name), "xtream-series://" + sourceId + "/" + id, XC_CategoryName(categories, row.category_id), XC_Str(row.cover), "", "Series", sourceId, sourceName)
            item.seriesKey = id
            result.Push(item)
        end if
    end for
    return result
end function

function XC_GetSeriesEpisodes(server as String, user as String, pass as String, seriesId as String, sourceId as String, sourceName as String) as Object
    data = ParseJson(XC_Get(XC_ApiUrl(server, user, pass, "get_series_info", "series_id=" + XC_Encode(seriesId))))
    result = []
    if data = invalid or data.episodes = invalid then return result
    base = XC_BaseUrl(server) + "/series/" + XC_Encode(user) + "/" + XC_Encode(pass) + "/"

    for each seasonKey in data.episodes
        for each ep in data.episodes[seasonKey]
            id = XC_IdStr(ep.id)
            if Len(id) > 0
                ext = XC_Str(ep.container_extension)
                if Len(ext) = 0 then ext = "mp4"
                seasonNum = XC_ToInt(ep.season)
                if seasonNum = 0 then seasonNum = Int(Val(seasonKey))
                epNum = XC_ToInt(ep.episode_num)
                title = XC_Str(ep.title)
                if Len(title) = 0 then title = "Episodio " + Str(epNum).Trim()
                item = XC_NewItem(title, base + id + "." + ext, "", "", "", "Episode", sourceId, sourceName)
                item.seriesKey = seriesId
                item.season = seasonNum
                item.episode = epNum
                result.Push(item)
            end if
        end for
    end for
    return result
end function
