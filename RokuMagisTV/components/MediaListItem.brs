' Compartido por MediaListItem y GuideListItem. La lista no dibuja su propio foco
' (drawFocusFeedback="false"): el resaltado lo pinta la fila, con los colores de la app.
sub init()
    m.bg = m.top.findNode("bg")
    m.accent = m.top.findNode("accent")
    m.logo = m.top.findNode("logo")
    m.title = m.top.findNode("title")
    m.subtitle = m.top.findNode("subtitle")
    m.badge = m.top.findNode("badge")
    m.normalColor = m.bg.color
end sub

sub onContentChanged()
    content = m.top.itemContent
    if content = invalid then return
    m.title.text = content.title
    m.subtitle.text = content.description
    m.badge.text = content.shortDescriptionLine2
    m.logo.uri = content.HDPosterUrl
    ' "AHORA" en la guía del reproductor: marca el canal que se está viendo.
    m.accent.visible = (content.shortDescriptionLine1 = "current")
end sub

sub onFocusChanged()
    focused = m.top.focusPercent > 0.5
    if focused and m.top.listHasFocus
        m.bg.color = "0x3D2057FF"
    else if focused
        m.bg.color = "0x2B163FFF"
    else
        m.bg.color = m.normalColor
    end if
end sub
