' Caché de catálogos en disco (equivalente a SourceCache.cs). Roku ofrece "cachefs:", que
' sobrevive entre aperturas del canal (el sistema puede vaciarlo si necesita espacio), y "tmp:",
' que se borra al salir. Se usa cachefs: si existe; si no, tmp:.
' TTL de 6 horas, igual que en la versión de escritorio.

function CS_Root() as String
    fs = CreateObject("roFileSystem")
    if fs.Exists("cachefs:/")
        if not fs.Exists("cachefs:/magistv") then fs.CreateDirectory("cachefs:/magistv")
        return "cachefs:/magistv/"
    end if
    if not fs.Exists("tmp:/magistv") then fs.CreateDirectory("tmp:/magistv")
    return "tmp:/magistv/"
end function

function CS_Path(sourceId as String, kind as String) as String
    return CS_Root() + sourceId + "_" + kind + ".json"
end function

function CS_TtlSeconds() as Integer
    return 6 * 60 * 60
end function

' Devuelve { items, savedAt } o invalid si no hay copia.
function CS_Load(sourceId as String, kind as String) as Dynamic
    path = CS_Path(sourceId, kind)
    fs = CreateObject("roFileSystem")
    if not fs.Exists(path) then return invalid
    data = ParseJson(ReadAsciiFile(path))
    if data = invalid or data.items = invalid then return invalid
    return data
end function

function CS_IsFresh(entry as Dynamic) as Boolean
    if entry = invalid or entry.savedAt = invalid then return false
    now = CreateObject("roDateTime").AsSeconds()
    return (now - entry.savedAt) < CS_TtlSeconds()
end function

sub CS_Save(sourceId as String, kind as String, items as Object, epgUrl as String)
    now = CreateObject("roDateTime").AsSeconds()
    WriteAsciiFile(CS_Path(sourceId, kind), FormatJson({ savedAt: now, epgUrl: epgUrl, items: items }))
end sub

sub CS_DeleteSource(sourceId as String)
    fs = CreateObject("roFileSystem")
    for each kind in ["M3U", "Live", "Movie", "Series"]
        path = CS_Path(sourceId, kind)
        if fs.Exists(path) then fs.Delete(path)
    end for
end sub
