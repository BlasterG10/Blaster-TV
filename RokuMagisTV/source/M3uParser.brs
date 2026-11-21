' Puerto de Services/M3uParser.cs a BrightScript. Convierte el texto de una lista M3U/M3U8 en
' un roArray de asociativos tipo MediaItem. Los episodios de una misma serie (detectados por
' MediaKindClassifier.brs) se agrupan en una entrada "Series" con sus episodios ("Episode"),
' igual que en la versión de escritorio.

function M3U_Parse(text as String, sourceName as String, sourceId as String) as Object
    result = []
    seriesTitles = {} ' seriesKey (minúsculas) -> título legible de la serie

    pendingName = "Untitled"
    pendingCategory = "General"
    pendingLogo = ""
    pendingEpgId = ""
    hasPending = false

    for each rawLine in text.Split(Chr(10))
        line = M3U_StripCr(rawLine).Trim()
        if Len(line) > 0
            if left(line, 8) = "#EXTINF:"
                cat = M3U_ExtractAttr(line, "group-title")
                if Len(cat) = 0 then cat = "General"
                pendingCategory = cat
                pendingLogo = M3U_ExtractAttr(line, "tvg-logo")
                pendingEpgId = M3U_ExtractAttr(line, "tvg-id")
                pendingName = M3U_NameAfterAttrs(line)
                hasPending = true
            else if left(line, 1) <> "#"
                if hasPending
                    url = line
                    classified = MK_Classify(url, pendingName)
                    item = {
                        name: pendingName
                        url: url
                        category: pendingCategory
                        logo: pendingLogo
                        epgId: pendingEpgId
                        source: sourceName
                        sourceId: sourceId
                        kind: classified.kind
                        seriesKey: ""
                        season: 0
                        episode: 0
                        isFavorite: false
                    }
                    if classified.kind = "Episode"
                        key = LCase(classified.seriesTitle)
                        item.seriesKey = key
                        item.season = classified.season
                        item.episode = classified.episode
                        if seriesTitles.DoesExist(key) = false
                            seriesTitles[key] = classified.seriesTitle
                        end if
                    end if
                    result.Push(item)
                end if
                pendingName = "Untitled"
                pendingCategory = "General"
                pendingLogo = ""
                pendingEpgId = ""
                hasPending = false
            end if
        end if
    end for

    M3U_AddSeriesEntries(result, seriesTitles, sourceName, sourceId)
    return result
end function

function M3U_StripCr(s as String) as String
    if Len(s) > 0 and right(s, 1) = Chr(13)
        return left(s, Len(s) - 1)
    end if
    return s
end function

' Busca key="valor" sin depender de expresiones regulares (los nombres de atributo M3U son fijos).
function M3U_ExtractAttr(line as String, key as String) as String
    needle = key + "=" + Chr(34)
    idx = Instr(1, LCase(line), LCase(needle))
    if idx = 0 then return ""
    startIdx = idx + Len(needle)
    endIdx = Instr(startIdx, line, Chr(34))
    if endIdx = 0 then return ""
    return Mid(line, startIdx, endIdx - startIdx)
end function

' El nombre visible va después de los atributos: toma lo que sigue a la última comilla y su coma.
function M3U_NameAfterAttrs(line as String) as String
    lastQuote = 0
    idx = Instr(1, line, Chr(34))
    while idx > 0
        lastQuote = idx
        idx = Instr(idx + 1, line, Chr(34))
    end while

    if lastQuote > 0
        commaIdx = Instr(lastQuote + 1, line, ",")
    else
        commaIdx = Instr(1, line, ",")
    end if

    if commaIdx = 0 then return "Untitled"
    name = Mid(line, commaIdx + 1).Trim()
    if Len(name) = 0 then return "Untitled"
    return name
end function

function M3U_AddSeriesEntries(items as Object, titles as Object, sourceName as String, sourceId as String) as Void
    firstEpisode = {}
    for each item in items
        if item.kind = "Episode" and Len(item.seriesKey) > 0
            if firstEpisode.DoesExist(item.seriesKey) = false
                firstEpisode[item.seriesKey] = item
            end if
        end if
    end for

    for each key in firstEpisode
        episode = firstEpisode[key]
        items.Push({
            name: titles[key]
            url: "series://" + sourceId + "/" + key
            category: episode.category
            logo: episode.logo
            epgId: ""
            source: sourceName
            sourceId: sourceId
            kind: "Series"
            seriesKey: key
            season: 0
            episode: 0
            isFavorite: false
        })
    end for
end function

' "#EXTM3U url-tvg="http://...xml"" o x-tvg-url: guía XMLTV que anuncia la propia lista.
' Si trae varias separadas por comas, se usa la primera.
function M3U_HeaderEpgUrl(text as String) as String
    firstBreak = Instr(1, text, Chr(10))
    header = text
    if firstBreak > 0 then header = left(text, firstBreak - 1)
    header = M3U_StripCr(header)
    if left(header, 7) <> "#EXTM3U" then return ""
    url = M3U_ExtractAttr(header, "url-tvg")
    if Len(url) = 0 then url = M3U_ExtractAttr(header, "x-tvg-url")
    comma = Instr(1, url, ",")
    if comma > 0 then url = left(url, comma - 1)
    return url.Trim()
end function
