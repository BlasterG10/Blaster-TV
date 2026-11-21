sub init()
    m.video = m.top.findNode("video")
    m.bar = m.top.findNode("infoBar")
    m.guide = m.top.findNode("guidePanel")
    m.guideList = m.top.findNode("guideList")
    m.guideTitle = m.top.findNode("guideTitle")
    m.barLogo = m.top.findNode("barLogo")
    m.barName = m.top.findNode("barName")
    m.barFav = m.top.findNode("barFav")
    m.barMeta = m.top.findNode("barMeta")
    m.barEpg = m.top.findNode("barEpg")
    m.barHint = m.top.findNode("barHint")
    m.errorLabel = m.top.findNode("errorLabel")
    m.hideTimer = m.top.findNode("hideTimer")

    m.items = []
    m.index = 0
    m.epgText = {}
    m.retried = false

    m.hideTimer.observeField("fire", "onHideTimer")
    m.video.observeField("state", "onVideoState")
    m.video.observeField("position", "onVideoPosition")
    m.guideList.observeField("itemSelected", "onGuideSelected")
    m.guideList.observeField("itemFocused", "onGuideFocused")
end sub

' ---------- Entradas desde MainScene ----------

sub onPlayRequest()
    req = m.top.playRequest
    if req = invalid or req.items = invalid or req.items.Count() = 0 then return
    m.items = req.items
    m.index = req.index
    if m.index < 0 or m.index >= m.items.Count() then m.index = 0
    m.epgText = {}
    if req.epgText <> invalid then m.epgText = req.epgText
    BuildGuide()
    m.top.visible = true
    m.top.setFocus(true)
    PlayCurrent()
end sub

sub onEpgUpdate()
    update = m.top.epgUpdate
    if update = invalid then return
    for each url in update
        m.epgText[url] = update[url]
    end for
    content = m.guideList.content
    if content <> invalid
        for i = 0 to content.getChildCount() - 1
            child = content.getChild(i)
            if update.DoesExist(child.url) then child.description = update[child.url]
        end for
    end if
    UpdateBar()
end sub

sub onFavoriteState()
    state = m.top.favoriteState
    if state = invalid then return
    for each item in m.items
        if item.url = state.url then item.isFavorite = state.isFavorite
    end for
    UpdateBar()
end sub

' ---------- Reproducción ----------

sub PlayCurrent()
    item = m.items[m.index]
    m.retried = false
    StartStream(item, VS_StreamFormat(item))
    m.errorLabel.visible = false
    m.guideList.jumpToItem = m.index
    MarkCurrentInGuide()
    UpdateBar()
    ShowOverlay(false)
    m.top.nowPlaying = item
end sub

sub StartStream(item as Object, format as String)
    content = CreateObject("roSGNode", "ContentNode")
    content.url = item.url
    content.title = item.name
    content.streamFormat = format
    content.live = (item.kind = "Live")
    m.currentFormat = format
    m.video.content = content
    m.video.control = "play"
end sub

function VS_StreamFormat(item as Object) as String
    path = MK_PathOf(item.url)
    if right(path, 5) = ".m3u8" then return "hls"
    if right(path, 4) = ".mpd" then return "dash"
    if right(path, 4) = ".mkv" then return "mkv"
    if right(path, 4) = ".mp4" or right(path, 4) = ".m4v" then return "mp4"
    if right(path, 4) = ".mov" then return "mov"
    if right(path, 3) = ".ts" then return "ts"
    if item.kind = "Live" then return "hls"
    return "mp4"
end function

sub onVideoState()
    state = m.video.state
    if state = "error"
        item = m.items[m.index]
        ' Muchos paneles IPTV sirven .ts y HLS indistintamente: se prueba el otro formato una vez.
        if not m.retried
            m.retried = true
            alternative = "hls"
            if m.currentFormat = "hls" then alternative = "ts"
            if item.kind <> "Live" and m.currentFormat <> "mp4" then alternative = "mp4"
            StartStream(item, alternative)
            return
        end if
        m.errorLabel.text = "No se pudo reproducir «" + item.name + "»." + Chr(10) + m.video.errorMsg
        m.errorLabel.visible = true
        ShowOverlay(true)
    else if state = "buffering"
        m.barEpg.text = "Cargando..."
        ShowOverlay(true)
    else if state = "playing"
        m.errorLabel.visible = false
        UpdateBar()
        RestartHideTimer()
    else if state = "finished"
        ' Al terminar un episodio pasa al siguiente de la lista.
        item = m.items[m.index]
        if item.kind = "Episode" and m.index + 1 < m.items.Count()
            m.index = m.index + 1
            PlayCurrent()
        else
            ShowOverlay(true)
        end if
    end if
end sub

sub onVideoPosition()
    if m.bar.visible and m.items.Count() > 0
        item = m.items[m.index]
        if item.kind <> "Live" then m.barEpg.text = VS_FormatPosition(m.video.position, m.video.duration)
    end if
end sub

function VS_FormatPosition(position as Dynamic, duration as Dynamic) as String
    if position = invalid then position = 0
    if duration = invalid or duration <= 0 then return VS_Clock(position)
    return VS_Clock(position) + " / " + VS_Clock(duration)
end function

function VS_Clock(seconds as Dynamic) as String
    total = Int(seconds)
    h = Int(total / 3600)
    mins = Int((total mod 3600) / 60)
    secs = total mod 60
    s = Str(secs).Trim()
    if secs < 10 then s = "0" + s
    if h > 0
        mm = Str(mins).Trim()
        if mins < 10 then mm = "0" + mm
        return Str(h).Trim() + ":" + mm + ":" + s
    end if
    return Str(mins).Trim() + ":" + s
end function

' ---------- Barra flotante y guía (siempre juntas) ----------

sub UpdateBar()
    if m.items.Count() = 0 then return
    item = m.items[m.index]
    m.barName.text = item.name
    m.barLogo.uri = item.logo
    m.barFav.text = ""
    if item.isFavorite <> invalid
        if item.isFavorite then m.barFav.text = "FAV"
    end if
    meta = item.category
    if item.kind = "Episode" then meta = "Temporada " + Str(item.season).Trim() + " · Episodio " + Str(item.episode).Trim()
    position = Str(m.index + 1).Trim() + "/" + Str(m.items.Count()).Trim()
    m.barMeta.text = meta + "   ·   " + position
    if item.kind = "Live"
        text = ""
        if m.epgText.DoesExist(item.url) then text = m.epgText[item.url]
        m.barEpg.text = text
        m.barHint.text = "OK guía   ·   Arriba/Abajo cambiar canal   ·   * favorito   ·   Atrás volver"
    else
        m.barEpg.text = VS_FormatPosition(m.video.position, m.video.duration)
        m.barHint.text = "OK guía   ·   << >> / Izq./Der. adelantar o retroceder   ·   Play pausa   ·   * favorito"
    end if
end sub

sub ShowOverlay(withGuide as Boolean)
    m.bar.visible = true
    if withGuide then ShowGuide()
    RestartHideTimer()
end sub

sub ShowGuide()
    m.bar.visible = true
    m.guide.visible = true
    m.guideList.jumpToItem = m.index
    m.guideList.setFocus(true)
    RestartHideTimer()
end sub

sub HideOverlay()
    m.hideTimer.control = "stop"
    m.bar.visible = false
    m.guide.visible = false
    m.top.setFocus(true)
end sub

sub RestartHideTimer()
    m.hideTimer.control = "stop"
    m.hideTimer.control = "start"
end sub

sub onHideTimer()
    ' Solo se ocultan si el vídeo se está reproduciendo; con error o cargando se quedan a la vista.
    if m.video.state = "playing" then HideOverlay() else RestartHideTimer()
end sub

sub onGuideFocused()
    RestartHideTimer()
end sub

sub onGuideSelected()
    m.index = m.guideList.itemSelected
    PlayCurrent()
    m.guideList.setFocus(true)
end sub

sub BuildGuide()
    content = CreateObject("roSGNode", "ContentNode")
    for each item in m.items
        child = content.createChild("ContentNode")
        child.title = item.name
        child.url = item.url
        child.HDPosterUrl = item.logo
        if m.epgText.DoesExist(item.url)
            child.description = m.epgText[item.url]
        else
            child.description = item.category
        end if
    end for
    m.guideList.content = content
    m.guideTitle.text = "GUÍA  ·  " + Str(m.items.Count()).Trim() + " elementos"
end sub

sub MarkCurrentInGuide()
    content = m.guideList.content
    if content = invalid then return
    for i = 0 to content.getChildCount() - 1
        child = content.getChild(i)
        if i = m.index
            child.shortDescriptionLine1 = "current"
            child.shortDescriptionLine2 = "AHORA"
        else if child.shortDescriptionLine1 = "current"
            child.shortDescriptionLine1 = ""
            child.shortDescriptionLine2 = ""
        end if
    end for
end sub

' ---------- Teclas ----------

sub Zap(delta as Integer)
    count = m.items.Count()
    if count < 2 then return
    m.index = (m.index + delta + count) mod count
    PlayCurrent()
end sub

sub Seek(delta as Integer)
    item = m.items[m.index]
    if item.kind = "Live" then return
    target = m.video.position + delta
    if target < 0 then target = 0
    m.video.seek = target
    ShowOverlay(false)
end sub

sub Close()
    HideOverlay()
    m.video.control = "stop"
    m.video.content = invalid
    m.top.visible = false
    m.top.closed = m.index
end sub

function onKeyEvent(key as String, press as Boolean) as Boolean
    if not press then return false
    item = invalid
    if m.items.Count() > 0 then item = m.items[m.index]

    if m.guide.visible
        RestartHideTimer()
        if key = "back" or key = "right"
            HideOverlay()
            return true
        end if
        if key = "options" and item <> invalid
            m.top.favoriteToggle = item
            return true
        end if
        ' Arriba/Abajo y OK los maneja la propia lista de la guía.
        return false
    end if

    if key = "back"
        if m.bar.visible
            HideOverlay()
        else
            Close()
        end if
        return true
    end if

    if key = "OK"
        ShowGuide()
        return true
    end if
    if key = "up" or key = "channelUp"
        Zap(-1)
        return true
    end if
    if key = "down" or key = "channelDown"
        Zap(1)
        return true
    end if
    if key = "options" and item <> invalid
        m.top.favoriteToggle = item
        ShowOverlay(false)
        return true
    end if
    if key = "play"
        if m.video.state = "paused"
            m.video.control = "resume"
        else
            m.video.control = "pause"
        end if
        ShowOverlay(false)
        return true
    end if
    if item <> invalid and item.kind = "Live"
        if key = "left"
            ShowGuide()
            return true
        end if
        ShowOverlay(false)
        return true
    end if
    if key = "left"
        Seek(-15)
        return true
    end if
    if key = "right"
        Seek(30)
        return true
    end if
    if key = "rewind"
        Seek(-60)
        return true
    end if
    if key = "fastforward"
        Seek(60)
        return true
    end if
    ShowOverlay(false)
    return true
end function
