' Guía de programación (equivalente a XmlTvService.cs), adaptada a la memoria de un Roku:
'  • Xtream: get_short_epg por canal (JSON pequeño, títulos en base64), pedido solo para los
'    canales que se están viendo en pantalla.
'  • M3U con url-tvg / x-tvg-url: XMLTV descargado una vez y recortado a las próximas horas y a
'    los canales que existen en las listas. Si el archivo es demasiado grande para un Roku, se
'    omite y se avisa (un XMLTV de decenas de MB agota la memoria del dispositivo).

function EPG_MaxXmltvBytes() as Integer
    return 12 * 1024 * 1024
end function

function EPG_Now() as Integer
    return CreateObject("roDateTime").AsSeconds()
end function

' "20261121183000 +0100" -> segundos UTC desde 1970.
function EPG_XmltvTime(s as String) as Integer
    if Len(s) < 14 then return 0
    iso = Mid(s, 1, 4) + "-" + Mid(s, 5, 2) + "-" + Mid(s, 7, 2) + "T" + Mid(s, 9, 2) + ":" + Mid(s, 11, 2) + ":" + Mid(s, 13, 2) + "Z"
    dt = CreateObject("roDateTime")
    dt.FromISO8601String(iso)
    secs = dt.AsSeconds()
    rest = Mid(s, 15).Trim()
    if Len(rest) >= 5
        sign = left(rest, 1)
        hours = Mid(rest, 2, 2).ToInt()
        mins = Mid(rest, 4, 2).ToInt()
        offset = hours * 3600 + mins * 60
        if sign = "+" then secs = secs - offset
        if sign = "-" then secs = secs + offset
    end if
    return secs
end function

function EPG_Base64(s as Dynamic) as String
    if s = invalid or GetInterface(s, "ifString") = invalid or Len(s) = 0 then return ""
    ba = CreateObject("roByteArray")
    ba.FromBase64String(s)
    return ba.ToAsciiString()
end function

' Xtream: ahora y siguiente de un canal. Devuelve un roArray de { title, start, ends }.
function EPG_XtreamShort(server as String, user as String, pass as String, streamId as String) as Object
    result = []
    body = XC_Get(XC_ApiUrl(server, user, pass, "get_short_epg", "stream_id=" + XC_Encode(streamId) + "&limit=3"))
    data = ParseJson(body)
    if data = invalid or data.epg_listings = invalid then return result
    for each p in data.epg_listings
        start = XC_ToInt(p.start_timestamp)
        stopTime = XC_ToInt(p["stop_timestamp"])
        title = EPG_Base64(p.title)
        if start > 0 and stopTime > start and Len(title) > 0
            result.Push({ title: title, start: start, ends: stopTime })
        end if
    end for
    return result
end function

' XMLTV: devuelve { channelId(minúsculas): [ { title, start, ends } ] } solo para los ids pedidos
' y para programas entre ahora-1h y ahora+windowHours.
function EPG_LoadXmltv(url as String, wantedIds as Object, windowHours as Integer) as Object
    result = { programs: {}, error: "" }
    path = "tmp:/magistv_epg.xml"
    xfer = CreateObject("roUrlTransfer")
    xfer.SetCertificatesFile("common:/certs/ca-bundle.crt")
    xfer.InitClientCertificates()
    xfer.SetUrl(url)
    xfer.EnableEncodings(true)
    code = xfer.GetToFile(path)
    if code <> 200
        result.error = "No se pudo descargar la guía (código " + Str(code).Trim() + ")."
        return result
    end if

    fs = CreateObject("roFileSystem")
    stat = fs.Stat(path)
    if stat = invalid or stat.size = invalid
        result.error = "No se pudo leer la guía descargada."
        return result
    end if
    if stat.size > EPG_MaxXmltvBytes()
        fs.Delete(path)
        result.error = "La guía XMLTV es demasiado grande para Roku; se omite."
        return result
    end if

    text = ReadAsciiFile(path)
    fs.Delete(path)
    xml = CreateObject("roXMLElement")
    if not xml.Parse(text)
        result.error = "La guía XMLTV no es válida."
        return result
    end if
    text = invalid

    now = EPG_Now()
    windowStart = now - 3600
    windowEnd = now + windowHours * 3600
    children = xml.GetChildElements()
    if children = invalid then return result
    for each node in children
        if node.GetName() = "programme"
            attrs = node.GetAttributes()
            channel = LCase(XC_Str(attrs.channel))
            if wantedIds.DoesExist(channel)
                stopTime = EPG_XmltvTime(XC_Str(attrs["stop"]))
                start = EPG_XmltvTime(XC_Str(attrs["start"]))
                if stopTime > windowStart and start < windowEnd
                    titles = node.GetNamedElements("title")
                    title = ""
                    if titles.Count() > 0 then title = titles[0].GetText()
                    if not result.programs.DoesExist(channel) then result.programs[channel] = []
                    result.programs[channel].Push({ title: title, start: start, ends: stopTime })
                end if
            end if
        end if
    end for

    for each channel in result.programs
        result.programs[channel].SortBy("start")
    end for
    return result
end function

' { now: programa|invalid, upcoming: programa|invalid }
function EPG_NowNext(programs as Dynamic, now as Integer) as Object
    result = { now: invalid, upcoming: invalid }
    if programs = invalid then return result
    for each p in programs
        if p.start <= now and p.ends > now
            result.now = p
        else if p.start > now and result.upcoming = invalid
            result.upcoming = p
        end if
    end for
    return result
end function

function EPG_FormatTime(secs as Integer) as String
    dt = CreateObject("roDateTime")
    dt.FromSeconds(secs)
    dt.ToLocalTime()
    h = dt.GetHours()
    minutes = dt.GetMinutes()
    mm = Str(minutes).Trim()
    if minutes < 10 then mm = "0" + mm
    return Str(h).Trim() + ":" + mm
end function

function EPG_Describe(nowNext as Object) as String
    text = ""
    if nowNext.now <> invalid
        text = "Ahora: " + nowNext.now.title + "  (" + EPG_FormatTime(nowNext.now.start) + "-" + EPG_FormatTime(nowNext.now.ends) + ")"
    end if
    if nowNext.upcoming <> invalid
        if Len(text) > 0 then text = text + "   ·   "
        text = text + "Luego: " + nowNext.upcoming.title + " " + EPG_FormatTime(nowNext.upcoming.start)
    end if
    return text
end function
