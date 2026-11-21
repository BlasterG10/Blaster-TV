sub init()
    m.top.functionName = "runRequest"
end sub

' request.mode:
'   "source"    { source, kind: "M3U"|"Live"|"Movie"|"Series", force }
'   "episodes"  { source, seriesId }
'   "epgBatch"  { source, channels: [ { url, streamId } ] }
'   "epgXmltv"  { url, ids: [ "id en minúsculas" ] }
'   "login"     { server, username, password }
sub runRequest()
    req = m.top.request
    out = { mode: req.mode, sourceId: "", kind: "", items: [], epgUrl: "", error: "", warning: "", fromCache: false, extra: {} }
    if req.source <> invalid then out.sourceId = req.source.id

    if req.mode = "source"
        LT_LoadSource(req, out)
    else if req.mode = "episodes"
        s = req.source
        out.items = XC_GetSeriesEpisodes(s.address, s.username, s.password, req.seriesId, s.id, s.name)
        if out.items.Count() = 0 then out.error = "Esta serie no tiene episodios disponibles."
    else if req.mode = "epgBatch"
        s = req.source
        for each ch in req.channels
            out.extra[ch.url] = EPG_XtreamShort(s.address, s.username, s.password, ch.streamId)
        end for
    else if req.mode = "epgXmltv"
        wanted = {}
        for each id in req.ids
            wanted[id] = true
        end for
        loaded = EPG_LoadXmltv(req.url, wanted, 8)
        out.extra = loaded.programs
        out.error = loaded.error
    else if req.mode = "login"
        login = XC_Login(req.server, req.username, req.password)
        out.extra = login
        if not login.ok then out.error = login.message
    end if

    m.top.result = out
end sub

sub LT_LoadSource(req as Object, out as Object)
    source = req.source
    kind = req.kind
    out.kind = kind

    cached = CS_Load(source.id, kind)
    if not req.force and CS_IsFresh(cached)
        out.items = cached.items
        out.epgUrl = XC_Str(cached.epgUrl)
        out.fromCache = true
        return
    end if

    items = invalid
    epgUrl = ""
    if source.sourceType = "Xtream"
        if kind = "Live"
            items = XC_GetLiveStreams(source.address, source.username, source.password, source.id, source.name)
        else if kind = "Movie"
            items = XC_GetVodStreams(source.address, source.username, source.password, source.id, source.name)
        else if kind = "Series"
            items = XC_GetSeries(source.address, source.username, source.password, source.id, source.name)
        end if
        if items <> invalid and items.Count() = 0 then items = invalid
    else
        downloaded = LT_DownloadM3u(source.address)
        if downloaded.error <> ""
            out.error = downloaded.error
        else
            items = M3U_Parse(downloaded.text, source.name, source.id)
            epgUrl = M3U_HeaderEpgUrl(downloaded.text)
            downloaded = invalid
        end if
    end if

    if items <> invalid
        out.items = items
        out.epgUrl = epgUrl
        CS_Save(source.id, kind, items, epgUrl)
        return
    end if

    ' Sin red o el servidor falló: se usa la copia guardada aunque esté vieja, como en Windows.
    if cached <> invalid
        out.items = cached.items
        out.epgUrl = XC_Str(cached.epgUrl)
        out.fromCache = true
        out.warning = "Sin conexión con " + source.name + ": se muestra la copia guardada."
        out.error = ""
        return
    end if
    if out.error = ""
        label = "el contenido"
        if kind = "Live" then label = "los canales"
        if kind = "Movie" then label = "las películas"
        if kind = "Series" then label = "las series"
        out.error = "No se pudo cargar " + label + " de " + source.name + " (sin conexión o sin contenido)."
    end if
end sub

' Descarga a tmp: y comprueba el tamaño antes de cargarlo en memoria: una lista enorme
' agotaría la memoria de un Roku (mucho menor que la de un PC).
function LT_DownloadM3u(url as String) as Object
    result = { text: "", error: "" }
    path = "tmp:/magistv_source.m3u"
    xfer = CreateObject("roUrlTransfer")
    xfer.SetCertificatesFile("common:/certs/ca-bundle.crt")
    xfer.InitClientCertificates()
    xfer.SetUrl(url)
    xfer.AddHeader("User-Agent", "MagisTV-Roku/1.0")
    xfer.EnableEncodings(true)
    code = xfer.GetToFile(path)
    if code <> 200
        result.error = "La lista respondió con código " + Str(code).Trim() + "."
        return result
    end if

    fs = CreateObject("roFileSystem")
    stat = fs.Stat(path)
    if stat <> invalid and stat.size <> invalid and stat.size > LT_MaxM3uBytes()
        fs.Delete(path)
        result.error = "La lista es demasiado grande para Roku (" + Str(Int(stat.size / 1048576)).Trim() + " MB). Usa una lista más pequeña o una cuenta Xtream."
        return result
    end if

    result.text = ReadAsciiFile(path)
    fs.Delete(path)
    if Len(result.text) = 0 then result.error = "La lista está vacía."
    return result
end function

function LT_MaxM3uBytes() as Integer
    return 20 * 1024 * 1024
end function
