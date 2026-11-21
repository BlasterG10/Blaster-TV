' MagisTV para Roku: escena principal.
' Estructura equivalente a MainWindow.xaml.cs de Windows: fuentes (M3U/Xtream) cargadas en
' segundo plano por LoaderTask, catálogo agrupado por categoría, favoritos, recientes, búsqueda,
' series con episodios, guía EPG y reproductor.

sub init()
    m.navList = m.top.findNode("navList")
    m.categoryList = m.top.findNode("categoryList")
    m.itemList = m.top.findNode("itemList")
    m.sectionTitle = m.top.findNode("sectionTitle")
    m.sectionSubtitle = m.top.findNode("sectionSubtitle")
    m.statusLabel = m.top.findNode("statusLabel")
    m.emptyHint = m.top.findNode("emptyHint")
    m.player = m.top.findNode("player")

    m.navItems = [
        { id: "live", label: "TV en vivo" }
        { id: "movie", label: "Películas" }
        { id: "series", label: "Series" }
        { id: "favorites", label: "Favoritos" }
        { id: "recent", label: "Continuar viendo" }
        { id: "search", label: "Buscar" }
        { id: "sources", label: "Fuentes" }
    ]
    navContent = CreateObject("roSGNode", "ContentNode")
    for each n in m.navItems
        child = navContent.createChild("ContentNode")
        child.title = n.label
    end for
    m.navList.content = navContent

    m.sources = SS_LoadSources()
    m.favs = SS_LoadFavorites()
    m.data = {}             ' sourceId -> { kinds: { "M3U"|"Live"|"Movie"|"Series": [items] }, epgUrl }
    m.loading = {}          ' "sourceId|kind" -> true mientras se carga
    m.tasks = {}            ' referencias vivas a los LoaderTask en marcha
    m.queue = []
    m.running = 0
    m.taskSeq = 0
    m.epgShort = {}         ' url -> { programs, fetchedAt }   (Xtream get_short_epg)
    m.epgPending = {}       ' url -> true mientras se pide
    m.epgXmltv = {}         ' tvg-id en minúsculas -> programs   (XMLTV de listas M3U)
    m.xmltvRequested = {}
    m.section = ""
    m.view = invalid
    m.shownCategories = []
    m.renderedCategory = -1
    m.currentItems = []
    m.seriesContext = invalid
    m.pendingSeries = invalid
    m.pendingDelete = ""
    m.searchQuery = ""
    m.xtDraft = invalid
    m.keyboardDialog = invalid
    m.keyboardPurpose = ""
    m.playerQueue = []
    m.queueToList = []
    m.viewDirty = false
    m.pendingDeepLink = ""

    m.categoryTimer = m.top.findNode("categoryTimer")
    m.epgTimer = m.top.findNode("epgTimer")
    m.rebuildTimer = m.top.findNode("rebuildTimer")
    m.deleteTimer = m.top.findNode("deleteTimer")
    m.categoryTimer.observeField("fire", "onCategoryTimer")
    m.epgTimer.observeField("fire", "onEpgTimer")
    m.rebuildTimer.observeField("fire", "onRebuildTimer")
    m.deleteTimer.observeField("fire", "onDeleteTimer")

    m.navList.observeField("itemSelected", "onNavSelected")
    m.categoryList.observeField("itemFocused", "onCategoryFocused")
    m.categoryList.observeField("itemSelected", "onCategorySelected")
    m.itemList.observeField("itemFocused", "onItemFocused")
    m.itemList.observeField("itemSelected", "onItemSelected")

    m.player.observeField("closed", "onPlayerClosed")
    m.player.observeField("favoriteToggle", "onPlayerFavorite")
    m.player.observeField("nowPlaying", "onPlayerNowPlaying")

    if m.sources.Count() = 0
        ShowSection("sources")
        SetStatus("Bienvenido. Añade una lista M3U o una cuenta Xtream para empezar.")
    else
        ShowSection("live")
        LoadAllSources(false)
    end if
    m.categoryList.setFocus(true)
end sub

' ======================================================================
'  Utilidades
' ======================================================================

sub SetStatus(text as String)
    m.statusLabel.text = text
end sub

sub SetHeader(title as String, subtitle as String)
    m.sectionTitle.text = title
    m.sectionSubtitle.text = subtitle
end sub

function FormatCount(n as Integer) as String
    s = Str(n).Trim()
    if n < 1000 then return s
    out = ""
    while Len(s) > 3
        out = "." + right(s, 3) + out
        s = left(s, Len(s) - 3)
    end while
    return s + out
end function

function KindForSection(section as String) as String
    if section = "movie" then return "Movie"
    if section = "series" then return "Series"
    return "Live"
end function

function KindLabel(kind as String) as String
    if kind = "Movie" then return "Películas"
    if kind = "Series" then return "Series"
    if kind = "Episode" then return "Episodios"
    return "TV en vivo"
end function

function SectionLabel(section as String) as String
    for each n in m.navItems
        if n.id = section then return n.label
    end for
    return ""
end function

function FindSource(id as String) as Dynamic
    for each s in m.sources
        if s.id = id then return s
    end for
    return invalid
end function

function IsFav(item as Object) as Boolean
    if item.isFavorite = invalid then return false
    return item.isFavorite
end function

function TextOf(v as Dynamic) as String
    if v = invalid then return ""
    if GetInterface(v, "ifString") <> invalid then return v
    return ""
end function

' Listas de items de las fuentes activas (referencias, sin copiar).
function ActiveLists() as Object
    lists = []
    for each source in m.sources
        if source.enabled and m.data.DoesExist(source.id)
            kinds = m.data[source.id].kinds
            for each kind in kinds
                lists.Push(kinds[kind])
            end for
        end if
    end for
    return lists
end function

sub ApplyFavorites(items as Object)
    for each item in items
        item.isFavorite = m.favs.DoesExist(SS_FavoriteKey(item))
    end for
end sub

function IsLoadingAnything() as Boolean
    return m.loading.Count() > 0 or m.running > 0 or m.queue.Count() > 0
end function

' ======================================================================
'  Vistas (qué categorías y elementos se muestran en cada sección)
' ======================================================================

function NewView(title as String, subtitle as String, emptyText as String) as Object
    return { title: title, subtitle: subtitle, emptyText: emptyText, mode: "list", categories: [], byCategory: {} }
end function

sub AddToCategory(view as Object, name as String, item as Object)
    if Len(name) = 0 then name = "General"
    if not view.byCategory.DoesExist(name)
        view.byCategory[name] = []
        view.categories.Push(name)
    end if
    view.byCategory[name].Push(item)
end sub

function EmptyTextFor(section as String) as String
    if m.sources.Count() = 0 then return "No hay fuentes todavía. Ve a «Fuentes» y añade una lista M3U o una cuenta Xtream."
    if IsLoadingAnything() then return "Cargando tus fuentes..."
    if section = "movie" then return "No hay películas en tus fuentes activas. Conecta una cuenta Xtream o añade una lista M3U con películas (archivos .mp4/.mkv o direcciones /movie/)."
    if section = "series" then return "No hay series en tus fuentes activas. Conecta una cuenta Xtream o añade una lista M3U con episodios (títulos como S01E02)."
    return "No hay canales en vivo en tus fuentes activas."
end function

function BuildView(section as String) as Object
    if section = "sources" then return BuildSourcesView()

    if section = "live" or section = "movie" or section = "series"
        kind = KindForSection(section)
        view = NewView(SectionLabel(section), "", "")
        for each list in ActiveLists()
            for each item in list
                if item.kind = kind then AddToCategory(view, item.category, item)
            end for
        end for
        view.categories.Sort("i")
        total = 0
        for each name in view.categories
            total = total + view.byCategory[name].Count()
        end for
        view.subtitle = FormatCount(total) + " elementos en " + FormatCount(view.categories.Count()) + " categorías   ·   OK reproducir   ·   * favorito"
        if total = 0 then view.emptyText = EmptyTextFor(section)
        return view
    end if

    if section = "favorites"
        view = NewView("Favoritos", "Tus canales, películas y series guardados   ·   * quitar", "Todavía no tienes favoritos. Pulsa * sobre cualquier canal, película o serie para guardarlo aquí.")
        temp = NewView("", "", "")
        for each list in ActiveLists()
            for each item in list
                if IsFav(item) and item.kind <> "Episode" then AddToCategory(temp, KindLabel(item.kind), item)
            end for
        end for
        for each name in ["TV en vivo", "Películas", "Series"]
            if temp.byCategory.DoesExist(name) then AddCategoryList(view, name, temp.byCategory[name])
        end for
        return view
    end if

    if section = "recent"
        view = NewView("Continuar viendo", "Lo último que abriste en este Roku", "Aún no has reproducido nada.")
        history = SS_LoadHistory()
        if history.Count() = 0 then return view
        index = {}
        for each list in ActiveLists()
            for each item in list
                index[SS_FavoriteKey(item)] = item
            end for
        end for
        for each h in history
            key = TextOf(h.sourceId) + "|" + TextOf(h.url)
            if index.DoesExist(key)
                AddToCategory(view, "Recientes", index[key])
            else
                AddToCategory(view, "Recientes", NormalizeItem(h))
            end if
        end for
        return view
    end if

    if section = "search"
        q = LCase(m.searchQuery)
        view = NewView("Buscar: «" + m.searchQuery + "»", "OK en «Buscar» del menú para cambiar la búsqueda", "Sin resultados para «" + m.searchQuery + "».")
        if Len(q) = 0 then return view
        counts = { Live: 0, Movie: 0, Series: 0 }
        temp = NewView("", "", "")
        for each list in ActiveLists()
            for each item in list
                if counts.DoesExist(item.kind)
                    if counts[item.kind] < 300 and Instr(1, LCase(item.name), q) > 0
                        counts[item.kind] = counts[item.kind] + 1
                        AddToCategory(temp, KindLabel(item.kind), item)
                    end if
                end if
            end for
        end for
        for each name in ["TV en vivo", "Películas", "Series"]
            if temp.byCategory.DoesExist(name) then AddCategoryList(view, name, temp.byCategory[name])
        end for
        return view
    end if

    return NewView("", "", "")
end function

sub AddCategoryList(view as Object, name as String, items as Object)
    view.categories.Push(name)
    view.byCategory[name] = items
end sub

' Historial guardado de una fuente que ya no está cargada: se completa lo mínimo para mostrarlo.
function NormalizeItem(h as Object) as Object
    return {
        name: TextOf(h.name)
        url: TextOf(h.url)
        category: TextOf(h.category)
        logo: TextOf(h.logo)
        epgId: ""
        source: TextOf(h.source)
        sourceId: TextOf(h.sourceId)
        kind: TextOf(h.kind)
        seriesKey: TextOf(h.seriesKey)
        season: 0
        episode: 0
        isFavorite: m.favs.DoesExist(TextOf(h.sourceId) + "|" + TextOf(h.url))
    }
end function

function BuildSourcesView() as Object
    view = NewView("Fuentes", "OK sobre una fuente: activar o desactivar   ·   * dos veces: eliminar", "No hay fuentes. Elige «Añadir lista M3U» o «Añadir cuenta Xtream» a la izquierda.")
    view.mode = "sources"
    view.actions = ["Añadir lista M3U (URL)", "Añadir cuenta Xtream", "Actualizar todas las fuentes", "Borrar copias guardadas"]
    view.sourceItems = []
    for each s in m.sources
        view.sourceItems.Push(SourceDisplayItem(s))
    end for
    return view
end function

function SourceDisplayItem(s as Object) as Object
    typeLabel = "Lista M3U"
    if s.type = "Xtream" then typeLabel = "Xtream Codes"
    state = "Desactivada"
    if s.enabled then state = "Activa"
    detail = ""
    if m.data.DoesExist(s.id)
        total = 0
        for each kind in m.data[s.id].kinds
            for each item in m.data[s.id].kinds[kind]
                if item.kind <> "Episode" then total = total + 1
            end for
        end for
        detail = "   ·   " + FormatCount(total) + " elementos"
    end if
    for each key in m.loading
        if left(key, Len(s.id)) = s.id then detail = "   ·   cargando..."
    end for
    badge = "OFF"
    if s.enabled then badge = "ON"
    return { name: s.name, url: s.id, logo: "", kind: "Source", category: "", source: "", sourceId: s.id, description: typeLabel + "   ·   " + state + detail, badge: badge, isFavorite: false }
end function

' ======================================================================
'  Pintar la vista
' ======================================================================

sub ShowSection(id as String)
    m.section = id
    m.seriesContext = invalid
    if id = "movie" or id = "series" then EnsureXtreamKind(KindForSection(id))
    m.view = BuildView(id)
    ApplyView(false)
    for i = 0 to m.navItems.Count() - 1
        if m.navItems[i].id = id then m.navList.jumpToItem = i
    end for
end sub

sub ApplyView(keepPosition as Boolean)
    view = m.view
    previousName = ""
    previousItem = 0
    if keepPosition and m.renderedCategory >= 0 and m.renderedCategory < m.shownCategories.Count()
        previousName = m.shownCategories[m.renderedCategory]
        previousItem = m.itemList.itemFocused
    end if

    SetHeader(view.title, view.subtitle)
    content = CreateObject("roSGNode", "ContentNode")
    if view.mode = "sources"
        m.shownCategories = view.actions
        for each action in view.actions
            child = content.createChild("ContentNode")
            child.title = action
        end for
    else
        m.shownCategories = view.categories
        for each name in view.categories
            child = content.createChild("ContentNode")
            child.title = name + "  (" + FormatCount(view.byCategory[name].Count()) + ")"
        end for
    end if

    target = 0
    if Len(previousName) > 0
        for i = 0 to m.shownCategories.Count() - 1
            if m.shownCategories[i] = previousName then target = i
        end for
    end if
    m.renderedCategory = target
    m.categoryList.content = content
    if target > 0 then m.categoryList.jumpToItem = target

    if view.mode = "sources"
        RenderItems(view.sourceItems)
    else if view.categories.Count() > 0
        RenderItems(view.byCategory[view.categories[target]])
    else
        RenderItems([])
    end if
    if keepPosition and previousItem > 0 and previousItem < m.itemList.content.getChildCount()
        m.itemList.jumpToItem = previousItem
    end if
end sub

sub RenderItems(items as Object)
    maxShown = 1500
    m.currentItems = items
    content = CreateObject("roSGNode", "ContentNode")
    now = EPG_Now()
    count = items.Count()
    shown = count
    if shown > maxShown then shown = maxShown
    for i = 0 to shown - 1
        item = items[i]
        child = content.createChild("ContentNode")
        child.title = item.name
        child.HDPosterUrl = item.logo
        child.url = item.url
        child.description = DescribeItem(item, now)
        child.shortDescriptionLine2 = BadgeFor(item)
    end for
    m.itemList.content = content

    m.emptyHint.visible = (count = 0)
    if count = 0
        if m.view <> invalid then m.emptyHint.text = m.view.emptyText
    end if
    if count > maxShown then SetStatus("Se muestran los primeros " + FormatCount(maxShown) + " de " + FormatCount(count) + ". Usa «Buscar» para encontrar el resto.")
end sub

function DescribeItem(item as Object, now as Integer) as String
    if item.kind = "Source" then return item.description
    if item.kind = "Live"
        text = EpgTextFor(item, now)
        if Len(text) > 0 then return text
        return TextOf(item.category) + "   ·   " + TextOf(item.source)
    end if
    if item.kind = "Series" then return "Serie   ·   " + TextOf(item.category)
    if item.kind = "Episode"
        return "Temporada " + Str(item.season).Trim() + "   ·   Episodio " + Str(item.episode).Trim()
    end if
    return TextOf(item.category) + "   ·   " + TextOf(item.source)
end function

function BadgeFor(item as Object) as String
    if item.kind = "Source" then return item.badge
    if IsFav(item) then return "FAV"
    if item.kind = "Series" then return "»"
    return ""
end function

function EpgTextFor(item as Object, now as Integer) as String
    programs = invalid
    if m.epgShort.DoesExist(item.url) then programs = m.epgShort[item.url].programs
    if programs = invalid
        epgId = TextOf(item.epgId)
        if Len(epgId) > 0
            key = LCase(epgId)
            if m.epgXmltv.DoesExist(key) then programs = m.epgXmltv[key]
        end if
    end if
    if programs = invalid then return ""
    return EPG_Describe(EPG_NowNext(programs, now))
end function

' Vuelve a calcular la línea secundaria de las filas visibles (llegó EPG o cambió un favorito).
sub RefreshVisibleDescriptions()
    content = m.itemList.content
    if content = invalid then return
    now = EPG_Now()
    count = content.getChildCount()
    if count > m.currentItems.Count() then count = m.currentItems.Count()
    for i = 0 to count - 1
        child = content.getChild(i)
        item = m.currentItems[i]
        child.description = DescribeItem(item, now)
        child.shortDescriptionLine2 = BadgeFor(item)
    end for
end sub

' ======================================================================
'  Eventos de las listas
' ======================================================================

sub onNavSelected()
    id = m.navItems[m.navList.itemSelected].id
    if id = "search"
        ShowKeyboard("Buscar canal, película o serie", m.searchQuery, "search")
        return
    end if
    ShowSection(id)
    m.categoryList.setFocus(true)
end sub

sub onCategoryFocused()
    if m.view = invalid or m.view.mode = "sources" then return
    m.categoryTimer.control = "stop"
    m.categoryTimer.control = "start"
end sub

sub onCategoryTimer()
    RenderFocusedCategory()
end sub

sub RenderFocusedCategory()
    if m.view = invalid or m.view.mode = "sources" then return
    idx = m.categoryList.itemFocused
    if idx = m.renderedCategory then return
    if idx < 0 or idx >= m.view.categories.Count() then return
    m.renderedCategory = idx
    RenderItems(m.view.byCategory[m.view.categories[idx]])
end sub

sub onCategorySelected()
    if m.view = invalid then return
    if m.view.mode = "sources"
        RunSourceAction(m.categoryList.itemSelected)
        return
    end if
    m.categoryTimer.control = "stop"
    RenderFocusedCategory()
    if m.currentItems.Count() > 0 then m.itemList.setFocus(true)
end sub

sub onItemFocused()
    if m.view = invalid or m.view.mode = "sources" then return
    m.epgTimer.control = "stop"
    m.epgTimer.control = "start"
end sub

sub onEpgTimer()
    ' Pide la guía de los canales Xtream que se ven en pantalla (el enfocado y los siguientes).
    start = m.itemList.itemFocused
    if start < 0 then return
    visible = []
    last = start + 10
    if last > m.currentItems.Count() - 1 then last = m.currentItems.Count() - 1
    for i = start to last
        visible.Push(m.currentItems[i])
    end for
    RequestShortEpg(visible)
end sub

sub onItemSelected()
    index = m.itemList.itemSelected
    if index < 0 or index >= m.currentItems.Count() then return
    if m.view.mode = "sources"
        ToggleSource(index)
        return
    end if
    item = m.currentItems[index]
    if item.kind = "Series"
        OpenSeries(item)
    else
        PlayFrom(m.currentItems, index)
    end if
end sub

function onKeyEvent(key as String, press as Boolean) as Boolean
    if not press then return false
    if m.player.visible then return false

    if key = "right"
        if m.navList.hasFocus()
            m.categoryList.setFocus(true)
            return true
        end if
        if m.categoryList.hasFocus()
            RenderFocusedCategory()
            if m.currentItems.Count() > 0 then m.itemList.setFocus(true)
            return true
        end if
    else if key = "left"
        if m.itemList.hasFocus()
            m.categoryList.setFocus(true)
            return true
        end if
        if m.categoryList.hasFocus()
            m.navList.setFocus(true)
            return true
        end if
    else if key = "back"
        if m.seriesContext <> invalid
            ExitSeries()
            return true
        end if
        if m.itemList.hasFocus()
            m.categoryList.setFocus(true)
            return true
        end if
        if m.categoryList.hasFocus()
            m.navList.setFocus(true)
            return true
        end if
        return false ' en el menú: Roku cierra el canal
    else if key = "options"
        if m.itemList.hasFocus()
            OnOptionsOnItem()
            return true
        end if
    else if key = "play"
        if m.itemList.hasFocus()
            index = m.itemList.itemFocused
            if index >= 0 and index < m.currentItems.Count() and m.view.mode <> "sources"
                item = m.currentItems[index]
                if item.kind = "Series" then OpenSeries(item) else PlayFrom(m.currentItems, index)
            end if
            return true
        end if
    end if
    return false
end function

sub OnOptionsOnItem()
    index = m.itemList.itemFocused
    if index < 0 or index >= m.currentItems.Count() then return
    item = m.currentItems[index]
    if m.view.mode = "sources"
        RequestDeleteSource(index)
        return
    end if
    ToggleFavorite(item)
end sub

' ======================================================================
'  Favoritos
' ======================================================================

' Devuelve el estado nuevo. "item" puede ser una copia (la que manda el reproductor):
' se actualiza también el original guardado en m.data.
function ToggleFavorite(item as Object) as Boolean
    if item.kind = "Source" or item.kind = "Episode" then return IsFav(item)
    key = SS_FavoriteKey(item)
    isFav = not m.favs.DoesExist(key)
    if isFav
        m.favs[key] = true
    else
        m.favs.Delete(key)
    end if
    SS_SaveFavorites(m.favs)
    item.isFavorite = isFav
    for each list in ActiveLists()
        for each other in list
            if other.url = item.url and other.sourceId = item.sourceId then other.isFavorite = isFav
        end for
    end for
    if isFav
        SetStatus("«" + item.name + "» añadido a favoritos.")
    else
        SetStatus("«" + item.name + "» quitado de favoritos.")
    end if
    if m.section = "favorites" and not m.player.visible
        m.view = BuildView("favorites")
        ApplyView(true)
    else
        RefreshVisibleDescriptions()
    end if
    return isFav
end function

' ======================================================================
'  Series
' ======================================================================

sub OpenSeries(series as Object)
    source = FindSource(series.sourceId)
    if source = invalid
        SetStatus("La fuente de esta serie ya no está disponible.")
        return
    end if
    if source.type = "Xtream"
        SetStatus("Cargando episodios de «" + series.name + "»...")
        m.pendingSeries = series
        StartTask({ mode: "episodes", source: source, seriesId: series.seriesKey })
        return
    end if
    episodes = []
    if m.data.DoesExist(source.id)
        for each kind in m.data[source.id].kinds
            for each item in m.data[source.id].kinds[kind]
                if item.kind = "Episode" and item.seriesKey = series.seriesKey then episodes.Push(item)
            end for
        end for
    end if
    EnterSeries(series, episodes)
end sub

sub EnterSeries(series as Object, episodes as Object)
    if episodes.Count() = 0
        SetStatus("«" + series.name + "» no tiene episodios disponibles.")
        return
    end if
    for each e in episodes
        e.sortKey = e.season * 10000 + e.episode
        if e.logo = invalid or Len(e.logo) = 0 then e.logo = series.logo
    end for
    episodes.SortBy("sortKey")

    view = NewView(series.name, "Episodios   ·   OK reproducir   ·   Atrás para volver", "Esta serie no tiene episodios.")
    view.mode = "episodes"
    for each e in episodes
        AddToCategory(view, "Temporada " + Str(e.season).Trim(), e)
    end for

    m.seriesContext = {
        previousView: m.view
        previousCategory: m.renderedCategory
        previousItem: m.itemList.itemFocused
    }
    m.view = view
    ApplyView(false)
    m.itemList.setFocus(true)
    SetStatus(FormatCount(episodes.Count()) + " episodios en " + FormatCount(view.categories.Count()) + " temporadas.")
end sub

sub ExitSeries()
    ctx = m.seriesContext
    m.seriesContext = invalid
    m.view = ctx.previousView
    ApplyView(false)
    cat = ctx.previousCategory
    if cat > 0 and cat < m.view.categories.Count()
        m.categoryList.jumpToItem = cat
        m.renderedCategory = cat
        RenderItems(m.view.byCategory[m.view.categories[cat]])
    end if
    if ctx.previousItem > 0 and ctx.previousItem < m.currentItems.Count() then m.itemList.jumpToItem = ctx.previousItem
    m.itemList.setFocus(true)
    if m.viewDirty then ScheduleRebuild()
end sub

' ======================================================================
'  Reproductor
' ======================================================================

sub PlayFrom(items as Object, index as Integer)
    maxQueue = 1500
    queue = []
    m.queueToList = []
    target = 0
    now = EPG_Now()
    epgText = {}
    last = items.Count() - 1
    if last > maxQueue - 1 then last = maxQueue - 1
    for i = 0 to last
        it = items[i]
        if it.kind <> "Series" and it.kind <> "Source"
            if i = index then target = queue.Count()
            queue.Push(it)
            m.queueToList.Push(i)
            if it.kind = "Live"
                text = EpgTextFor(it, now)
                if Len(text) > 0 then epgText[it.url] = text
            end if
        end if
    end for
    if queue.Count() = 0 then return
    m.playerQueue = queue
    m.player.playRequest = { items: queue, index: target, epgText: epgText }
end sub

sub onPlayerNowPlaying()
    item = m.player.nowPlaying
    if item = invalid then return
    SS_AddHistory(item)
    ' Guía del canal actual y de los vecinos, para la barra y la guía del reproductor.
    neighbours = []
    position = -1
    for i = 0 to m.playerQueue.Count() - 1
        if m.playerQueue[i].url = item.url then position = i
    end for
    if position >= 0
        first = position - 3
        if first < 0 then first = 0
        last = position + 8
        if last > m.playerQueue.Count() - 1 then last = m.playerQueue.Count() - 1
        for i = first to last
            neighbours.Push(m.playerQueue[i])
        end for
    end if
    RequestShortEpg(neighbours)
end sub

sub onPlayerFavorite()
    item = m.player.favoriteToggle
    if item = invalid then return
    isFav = ToggleFavorite(item)
    m.player.favoriteState = { url: item.url, isFavorite: isFav }
end sub

sub onPlayerClosed()
    index = m.player.closed
    if index >= 0 and index < m.queueToList.Count()
        listIndex = m.queueToList[index]
        if listIndex < m.itemList.content.getChildCount() then m.itemList.jumpToItem = listIndex
    end if
    m.itemList.setFocus(true)
    if m.viewDirty then ScheduleRebuild()
    RefreshVisibleDescriptions()
end sub

sub onDeepLink()
    m.pendingDeepLink = m.top.deepLinkUrl
    TryDeepLink()
end sub

sub TryDeepLink()
    if Len(m.pendingDeepLink) = 0 then return
    for each list in ActiveLists()
        for each item in list
            if item.url = m.pendingDeepLink and item.kind <> "Series"
                m.pendingDeepLink = ""
                PlayFrom([item], 0)
                return
            end if
        end for
    end for
end sub

' ======================================================================
'  Guía EPG
' ======================================================================

sub RequestShortEpg(items as Object)
    now = EPG_Now()
    bySource = {}
    for each item in items
        if item.kind = "Live" and item.streamId <> invalid and not m.epgPending.DoesExist(item.url)
            fresh = false
            if m.epgShort.DoesExist(item.url)
                fresh = (now - m.epgShort[item.url].fetchedAt) < 900
            end if
            if not fresh
                if not bySource.DoesExist(item.sourceId) then bySource[item.sourceId] = []
                bySource[item.sourceId].Push({ url: item.url, streamId: item.streamId })
                m.epgPending[item.url] = true
            end if
        end if
    end for
    for each sourceId in bySource
        source = FindSource(sourceId)
        if source <> invalid then StartTask({ mode: "epgBatch", source: source, channels: bySource[sourceId] })
    end for
end sub

sub HandleEpgBatch(result as Object)
    now = EPG_Now()
    updates = {}
    for each url in result.extra
        m.epgPending.Delete(url)
        m.epgShort[url] = { programs: result.extra[url], fetchedAt: now }
        text = EPG_Describe(EPG_NowNext(result.extra[url], now))
        if Len(text) > 0 then updates[url] = text
    end for
    RefreshVisibleDescriptions()
    if m.player.visible and updates.Count() > 0 then m.player.epgUpdate = updates
end sub

sub MaybeLoadXmltv()
    for each source in m.sources
        if source.enabled and source.type <> "Xtream" and m.data.DoesExist(source.id)
            url = m.data[source.id].epgUrl
            if Len(url) > 0 and not m.xmltvRequested.DoesExist(url) and m.xmltvRequested.Count() < 2
                ids = {}
                for each kind in m.data[source.id].kinds
                    for each item in m.data[source.id].kinds[kind]
                        epgId = TextOf(item.epgId)
                        if item.kind = "Live" and Len(epgId) > 0 then ids[LCase(epgId)] = true
                    end for
                end for
                idList = []
                for each id in ids
                    idList.Push(id)
                end for
                if idList.Count() > 0
                    m.xmltvRequested[url] = true
                    SetStatus("Descargando la guía de programación...")
                    StartTask({ mode: "epgXmltv", url: url, ids: idList })
                end if
            end if
        end if
    end for
end sub

sub HandleXmltv(result as Object)
    if result.error <> ""
        SetStatus(result.error)
        return
    end if
    channels = 0
    for each id in result.extra
        m.epgXmltv[id] = result.extra[id]
        channels = channels + 1
    end for
    SetStatus("Guía de programación cargada para " + FormatCount(channels) + " canales.")
    RefreshVisibleDescriptions()
    if m.player.visible
        now = EPG_Now()
        updates = {}
        for each item in m.playerQueue
            text = EpgTextFor(item, now)
            if Len(text) > 0 then updates[item.url] = text
        end for
        if updates.Count() > 0 then m.player.epgUpdate = updates
    end if
end sub

' ======================================================================
'  Carga de fuentes (LoaderTask, máximo 3 a la vez para no saturar el Roku)
' ======================================================================

sub StartTask(request as Object)
    m.queue.Push(request)
    PumpQueue()
end sub

sub PumpQueue()
    while m.running < 3 and m.queue.Count() > 0
        request = m.queue.Shift()
        m.taskSeq = m.taskSeq + 1
        task = CreateObject("roSGNode", "LoaderTask")
        task.id = "task" + Str(m.taskSeq).Trim()
        task.request = request
        task.observeField("result", "onTaskResult")
        m.tasks[task.id] = task
        m.running = m.running + 1
        task.control = "RUN"
    end while
end sub

sub onTaskResult(event as Object)
    task = event.getRoSGNode()
    result = event.getData()
    m.tasks.Delete(task.id)
    m.running = m.running - 1
    if result <> invalid
        if result.mode = "source"
            HandleSourceResult(result)
        else if result.mode = "episodes"
            HandleEpisodes(result)
        else if result.mode = "epgBatch"
            HandleEpgBatch(result)
        else if result.mode = "epgXmltv"
            HandleXmltv(result)
        else if result.mode = "login"
            HandleLogin(result)
        end if
    end if
    PumpQueue()
end sub

sub LoadAllSources(force as Boolean)
    for each source in m.sources
        if source.enabled
            if source.type = "Xtream"
                LoadSourceKind(source, "Live", force)
                if m.data.DoesExist(source.id)
                    for each kind in ["Movie", "Series"]
                        if m.data[source.id].kinds.DoesExist(kind) then LoadSourceKind(source, kind, force)
                    end for
                end if
            else
                LoadSourceKind(source, "M3U", force)
            end if
        end if
    end for
end sub

' Películas y series de Xtream se piden solo al entrar en esa sección (como en Windows).
sub EnsureXtreamKind(kind as String)
    for each source in m.sources
        if source.enabled and source.type = "Xtream"
            loaded = false
            if m.data.DoesExist(source.id) then loaded = m.data[source.id].kinds.DoesExist(kind)
            if not loaded then LoadSourceKind(source, kind, false)
        end if
    end for
end sub

sub LoadSourceKind(source as Object, kind as String, force as Boolean)
    key = source.id + "|" + kind
    if m.loading.DoesExist(key) then return
    m.loading[key] = true
    StartTask({ mode: "source", source: source, kind: kind, force: force })
    SetStatus("Cargando " + source.name + "...")
end sub

sub HandleSourceResult(result as Object)
    m.loading.Delete(result.sourceId + "|" + result.kind)
    source = FindSource(result.sourceId)
    if source = invalid or not source.enabled
        ScheduleRebuild()
        return
    end if

    if result.items = invalid or result.items.Count() = 0
        if result.error <> "" then SetStatus(result.error)
    else
        if not m.data.DoesExist(source.id) then m.data[source.id] = { kinds: {}, epgUrl: "" }
        ApplyFavorites(result.items)
        m.data[source.id].kinds[result.kind] = result.items
        if Len(TextOf(result.epgUrl)) > 0 then m.data[source.id].epgUrl = result.epgUrl
        if result.warning <> ""
            SetStatus(result.warning)
        else
            SetStatus(SourceLoadedMessage(source, result))
        end if
    end if

    ScheduleRebuild()
    if not IsLoadingAnything()
        MaybeLoadXmltv()
        TryDeepLink()
    end if
end sub

function SourceLoadedMessage(source as Object, result as Object) as String
    counts = { Live: 0, Movie: 0, Series: 0 }
    for each item in result.items
        if counts.DoesExist(item.kind) then counts[item.kind] = counts[item.kind] + 1
    end for
    parts = []
    if counts.Live > 0 then parts.Push(FormatCount(counts.Live) + " canales")
    if counts.Movie > 0 then parts.Push(FormatCount(counts.Movie) + " películas")
    if counts.Series > 0 then parts.Push(FormatCount(counts.Series) + " series")
    text = source.name + ": " + parts.Join(", ")
    if result.fromCache then text = text + " (copia guardada)"
    return text + "."
end function

sub HandleEpisodes(result as Object)
    series = m.pendingSeries
    m.pendingSeries = invalid
    if series = invalid then return
    if result.error <> "" and result.items.Count() = 0
        SetStatus(result.error)
        return
    end if
    for each e in result.items
        e.isFavorite = false
    end for
    EnterSeries(series, result.items)
end sub

sub ScheduleRebuild()
    m.rebuildTimer.control = "stop"
    m.rebuildTimer.control = "start"
end sub

sub onRebuildTimer()
    ' No se toca la vista mientras se ve un vídeo o se recorre una serie: se rehace después.
    if m.player.visible or m.seriesContext <> invalid
        m.viewDirty = true
        return
    end if
    m.viewDirty = false
    if m.section = "" then return
    m.view = BuildView(m.section)
    ApplyView(true)
end sub

' ======================================================================
'  Fuentes: añadir, activar, eliminar
' ======================================================================

sub RunSourceAction(index as Integer)
    if index = 0
        ShowKeyboard("Dirección de la lista M3U", "http://", "m3u")
    else if index = 1
        ShowKeyboard("Cuenta Xtream · Servidor (ej. http://servidor:8080) o enlace get.php completo", "http://", "xtServer")
    else if index = 2
        SetStatus("Actualizando todas las fuentes...")
        LoadAllSources(true)
        m.view = BuildSourcesView()
        ApplyView(true)
    else if index = 3
        for each s in m.sources
            CS_DeleteSource(s.id)
        end for
        SetStatus("Copias guardadas borradas. La próxima carga descargará las listas de nuevo.")
    end if
end sub

sub ToggleSource(index as Integer)
    if index >= m.sources.Count() then return
    source = m.sources[index]
    source.enabled = not source.enabled
    SS_SaveSources(m.sources)
    if source.enabled
        if source.type = "Xtream" then LoadSourceKind(source, "Live", false) else LoadSourceKind(source, "M3U", false)
        SetStatus("«" + source.name + "» activada.")
    else
        m.data.Delete(source.id)
        SetStatus("«" + source.name + "» desactivada.")
    end if
    m.view = BuildSourcesView()
    ApplyView(true)
end sub

sub RequestDeleteSource(index as Integer)
    if index >= m.sources.Count() then return
    source = m.sources[index]
    if m.pendingDelete = source.id
        m.pendingDelete = ""
        m.deleteTimer.control = "stop"
        kept = []
        for each s in m.sources
            if s.id <> source.id then kept.Push(s)
        end for
        m.sources = kept
        SS_SaveSources(m.sources)
        m.data.Delete(source.id)
        CS_DeleteSource(source.id)
        SetStatus("«" + source.name + "» eliminada.")
        m.view = BuildSourcesView()
        ApplyView(true)
        return
    end if
    m.pendingDelete = source.id
    m.deleteTimer.control = "stop"
    m.deleteTimer.control = "start"
    SetStatus("Pulsa * otra vez para eliminar «" + source.name + "».")
end sub

sub onDeleteTimer()
    if m.pendingDelete <> ""
        m.pendingDelete = ""
        SetStatus("")
    end if
end sub

' ======================================================================
'  Teclado en pantalla
' ======================================================================

sub ShowKeyboard(title as String, text as String, purpose as String)
    dlg = CreateObject("roSGNode", "StandardKeyboardDialog")
    if dlg = invalid then dlg = CreateObject("roSGNode", "KeyboardDialog")
    dlg.title = title
    dlg.text = text
    dlg.buttons = ["Aceptar", "Cancelar"]
    dlg.observeField("buttonSelected", "onKeyboardButton")
    m.keyboardPurpose = purpose
    m.keyboardDialog = dlg
    m.top.dialog = dlg
end sub

sub onKeyboardButton()
    dlg = m.keyboardDialog
    if dlg = invalid then return
    accepted = (dlg.buttonSelected = 0)
    text = TextOf(dlg.text).Trim()
    m.keyboardDialog = invalid
    dlg.close = true
    purpose = m.keyboardPurpose
    if accepted
        HandleKeyboardText(purpose, text)
    else
        if purpose = "search" then m.navList.setFocus(true) else m.categoryList.setFocus(true)
    end if
end sub

sub HandleKeyboardText(purpose as String, text as String)
    if purpose = "search"
        m.searchQuery = text
        if Len(text) = 0
            m.navList.setFocus(true)
            return
        end if
        ShowSection("search")
        if m.currentItems.Count() > 0 then m.itemList.setFocus(true) else m.categoryList.setFocus(true)
        return
    end if

    if purpose = "m3u"
        lower = LCase(text)
        if left(lower, 7) <> "http://" and left(lower, 8) <> "https://"
            SetStatus("La dirección debe empezar por http:// o https://")
            m.categoryList.setFocus(true)
            return
        end if
        for each s in m.sources
            if s.type = "M3UUrl" and LCase(s.address) = lower
                SetStatus("Esa lista ya está en tus fuentes.")
                m.categoryList.setFocus(true)
                return
            end if
        end for
        source = SS_NewM3uSource(text)
        AddSource(source, "M3U")
        return
    end if

    if purpose = "xtServer"
        parsed = ParseXtreamLink(text)
        if parsed <> invalid
            ' Enlace get.php completo: ya trae usuario y contraseña.
            m.xtDraft = parsed
            StartXtreamLogin()
            return
        end if
        m.xtDraft = { server: NormalizeServer(text), user: "", pass: "" }
        ShowKeyboard("Cuenta Xtream · Usuario", "", "xtUser")
        return
    end if

    if purpose = "xtUser"
        m.xtDraft.user = text
        ShowKeyboard("Cuenta Xtream · Contraseña", "", "xtPass")
        return
    end if

    if purpose = "xtPass"
        m.xtDraft.pass = text
        StartXtreamLogin()
        return
    end if
end sub

function NormalizeServer(text as String) as String
    s = text.Trim()
    lower = LCase(s)
    if left(lower, 7) <> "http://" and left(lower, 8) <> "https://" then s = "http://" + s
    return XC_BaseUrl(s)
end function

' "http://servidor:8080/get.php?username=U&password=P&type=m3u_plus" -> { server, user, pass }
function ParseXtreamLink(text as String) as Dynamic
    lower = LCase(text)
    if Instr(1, lower, "username=") = 0 or Instr(1, lower, "password=") = 0 then return invalid
    q = Instr(1, text, "?")
    if q = 0 then return invalid
    base = left(text, q - 1)
    slash = 0
    schemeEnd = Instr(1, base, "://")
    if schemeEnd > 0 then slash = Instr(schemeEnd + 3, base, "/")
    if slash > 0 then base = left(base, slash - 1)
    user = ""
    pass = ""
    xfer = CreateObject("roUrlTransfer")
    for each pair in Mid(text, q + 1).Split("&")
        eq = Instr(1, pair, "=")
        if eq > 0
            name = LCase(left(pair, eq - 1))
            value = xfer.Unescape(Mid(pair, eq + 1))
            if name = "username" then user = value
            if name = "password" then pass = value
        end if
    end for
    if Len(user) = 0 or Len(pass) = 0 then return invalid
    return { server: NormalizeServer(base), user: user, pass: pass }
end function

sub StartXtreamLogin()
    d = m.xtDraft
    for each s in m.sources
        if s.type = "Xtream" and LCase(s.address) = LCase(d.server) and LCase(s.username) = LCase(d.user)
            SetStatus("Esa cuenta Xtream ya está en tus fuentes.")
            m.categoryList.setFocus(true)
            return
        end if
    end for
    SetStatus("Comprobando la cuenta Xtream en " + d.server + "...")
    m.categoryList.setFocus(true)
    StartTask({ mode: "login", server: d.server, username: d.user, password: d.pass })
end sub

sub HandleLogin(result as Object)
    d = m.xtDraft
    m.xtDraft = invalid
    if d = invalid then return
    if result.error <> ""
        SetStatus("No se pudo añadir la cuenta: " + result.error + " Vuelve a intentarlo con «Añadir cuenta Xtream».")
        return
    end if
    AddSource(SS_NewXtreamSource(d.server, d.user, d.pass), "Live")
end sub

sub AddSource(source as Object, kind as String)
    m.sources.Push(source)
    SS_SaveSources(m.sources)
    LoadSourceKind(source, kind, true)
    SetStatus("«" + source.name + "» añadida. Cargando...")
    if m.section = "sources"
        m.view = BuildSourcesView()
        ApplyView(true)
    end if
    m.categoryList.setFocus(true)
end sub
