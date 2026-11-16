using System.Net.Http;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Windows.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using LibVLCSharp.Shared;
using MagisWindowsNative.Models;
using MagisWindowsNative.Services;
using Microsoft.Win32;

namespace MagisWindowsNative;

public partial class MainWindow : Window
{
    // _items is the filtered, already-sorted list currently bound to the ListBox. It is
    // replaced (never mutated) on every filter pass, so it is safe to keep as the playback queue.
    private List<MediaItem> _items = new();
    private readonly List<MediaItem> _all = new();
    private readonly Dictionary<string, List<MediaItem>> _sortedCache = new();
    private readonly Dictionary<string, HashSet<string>> _loadedXtreamKinds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, XtreamClient> _xtreamClients = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task<XtreamClient>> _xtreamConnecting = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task> _inflight = new(StringComparer.OrdinalIgnoreCase);
    private readonly HttpClient _sourceHttp = new(new SocketsHttpHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        ConnectTimeout = TimeSpan.FromSeconds(15)
    }) { Timeout = TimeSpan.FromSeconds(30) };
    private readonly System.Windows.Threading.DispatcherTimer _publicRefreshTimer;
    private readonly List<EpgProgram> _epgPrograms = new();
    private Dictionary<string, List<EpgProgram>>? _epgById;
    private Dictionary<string, List<EpgProgram>>? _epgByName;
    private bool _epgApplied;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);
    private readonly object _vlcLock = new();
    private int _suppress;
    private bool _viewDirty;
    private bool _refreshQueued;
    private int _lastBufferPercent = -1;
    private readonly System.Windows.Threading.DispatcherTimer _searchTimer;
    private string _currentSection = "Home";
    private LibVLC? _libVlc;
    private MediaPlayer? _player;
    private string _activeKind = "All";
    private readonly CatalogState _catalogState = CatalogStore.Load();
    private bool _showFavorites;
    private bool _showHistory;
    private PlayerWindow? _playerWindow;
    private PlayerGuideWindow? _guideWindow;
    private MediaItem? _nowPlaying;
    // Keep the Media alive for the entire playback session. Disposing it immediately
    // after Play() can terminate playback on some LibVLCSharp versions/streams.
    private Media? _currentMedia;
    private List<MediaItem> _playbackQueue = new();
    private int _playbackIndex = -1;

    public MainWindow()
    {
        InitializeComponent();
        // LibVLC is created lazily (see EnsureVlc) so the window appears immediately.
        _searchTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            ApplyFilter();
        };
        _publicRefreshTimer = new System.Windows.Threading.DispatcherTimer { Interval = CacheTtl };
        _publicRefreshTimer.Tick += PublicRefreshTimer_Tick;
        GroupModeBox.Items.Add("País");
        GroupModeBox.Items.Add("Categoría");
        GroupModeBox.Items.Add("Servidor");
        GroupModeBox.Items.Add("Mis grupos");
        GroupModeBox.SelectedIndex = 0;
        CategoryBox.Items.Add("Todos");
        CategoryBox.SelectedIndex = 0;
        RefreshTargetGroups();
        ConfigureSectionUi();

        // Upgrade the old single-login setting to a first managed source without
        // overwriting existing source entries.
        _catalogState.Sources ??= new List<PlaylistSource>();
        if (_catalogState.Sources.Count == 0 && CredentialStore.Load() is { } legacyLogin)
        {
            var host = Uri.TryCreate(legacyLogin.Server, UriKind.Absolute, out var legacyUri) ? legacyUri.Host : "Xtream";
            _catalogState.Sources.Add(new PlaylistSource
            {
                Name = $"Xtream · {host} · {legacyLogin.Username}", Type = "Xtream",
                Address = legacyLogin.Server.TrimEnd('/'), Username = legacyLogin.Username,
                Password = legacyLogin.Password
            });
            CatalogStore.Save(_catalogState);
        }

        Loaded += async (_, _) => { await StartupAsync(); _publicRefreshTimer.Start(); };
        Closed += (_, _) =>
        {
            _searchTimer.Stop();
            _publicRefreshTimer.Stop();
            _playerWindow?.Shutdown();
            lock (_vlcLock)
            {
                _player?.Stop();
                _currentMedia?.Dispose();
                _currentMedia = null;
                _player?.Dispose();
                _libVlc?.Dispose();
            }
            foreach (var client in _xtreamClients.Values) client.Dispose();
            _xtreamClients.Clear();
            _sourceHttp.Dispose();
            CatalogStore.Flush();
        };
    }

    // Creates LibVLC and the shared MediaPlayer the first time they are needed.
    // Safe to call from any thread; also used to warm LibVLC up in the background.
    private void EnsureVlc()
    {
        lock (_vlcLock)
        {
            if (_player is not null) return;
            Core.Initialize();
            _libVlc = new LibVLC("--no-video-title-show");
            var player = new MediaPlayer(_libVlc);
            player.Opening += (_, _) => { _lastBufferPercent = -1; SetPlayerStatus("Conectando con el stream…"); };
            player.Buffering += (_, e) =>
            {
                // Buffering fires many times per second; only update the UI when the percentage changes.
                var percent = (int)Math.Round(e.Cache);
                if (percent == _lastBufferPercent) return;
                _lastBufferPercent = percent;
                SetPlayerStatus($"Cargando… {percent}%");
            };
            player.Playing += (_, _) => SetPlayerStatus("Reproduciendo");
            player.Paused += (_, _) => SetPlayerStatus("En pausa");
            player.Stopped += (_, _) => SetPlayerStatus("Reproducción detenida");
            player.EndReached += (_, _) => SetPlayerStatus("La transmisión terminó");
            player.EncounteredError += (_, _) => SetPlayerStatus("VLC no pudo decodificar o abrir el stream. Comprueba la URL y la conexión.");
            _player = player;
        }
    }

    private void WarmUpVlc() => _ = Task.Run(() =>
    {
        try { EnsureVlc(); }
        catch { /* The real error, if any, is reported when the user presses play. */ }
    });

    private async Task StartupAsync()
    {
        try
        {
            // Keep startup sources lean, add open video catalogs, and migrate older installs once.
            var sourcesChanged = PublicPlaylists.SeedOnce(_catalogState);
            sourcesChanged |= PublicPlaylists.SeedOpenVideoSourcesOnce(_catalogState);
            sourcesChanged |= PublicPlaylists.OptimizeBundledSourcesOnce(_catalogState);
            if (sourcesChanged) CatalogStore.Save(_catalogState);
            var enabled = _catalogState.Sources.Where(x => x.Enabled).ToList();
            if (enabled.Count == 0)
            {
                StatusText.Text = "Añade una o varias fuentes M3U o Xtream para empezar.";
                WarmUpVlc();
                return;
            }

            // 1) Show the catalog saved by the previous session right away.
            StatusText.Text = "Cargando catálogo guardado…";
            var cachedBySource = await Task.WhenAll(enabled.Select(source => Task.Run(() => ReadCachedKinds(source))));
            var stale = new List<PlaylistSource>();
            for (var i = 0; i < enabled.Count; i++)
            {
                var source = enabled[i];
                var cached = cachedBySource[i];
                foreach (var entry in cached)
                {
                    MergeSourceKind(source, entry.Key, entry.Value.Items, refresh: false);
                    GetLoadedSet(source.Id).Add(entry.Key);
                }
                var liveIsFresh = cached.TryGetValue("Live", out var live) && DateTime.UtcNow - live.SavedUtc < CacheTtl;
                if (!liveIsFresh) stale.Add(source);
            }
            if (_all.Count > 0)
            {
                RefreshNow();
                StatusText.Text = $"Contenido guardado: {_all.Count:N0} elementos.";
            }
            WarmUpVlc();

            // 2) Only sources without a recent copy are downloaded again.
            if (stale.Count == 0)
            {
                StatusText.Text = $"Fuentes activas: {enabled.Count} · Contenido cargado: {_all.Count:N0} elementos.";
                return;
            }
            if (_all.Count > 0) StatusText.Text = $"Actualizando {stale.Count} fuente(s) en segundo plano…";
            var failures = await LoadSourcesAsync(stale, force: true, refreshAllKinds: true);
            FinishLoad(enabled, failures);
        }
        catch (Exception ex)
        {
            StatusText.Text = "No se pudo cargar el catálogo inicial: " + ex.Message;
        }
    }

    private async void PublicRefreshTimer_Tick(object? sender, EventArgs e)
    {
        _publicRefreshTimer.Stop();
        try
        {
            var sources = _catalogState.Sources
                .Where(x => x.Enabled && PublicPlaylists.IsPublic(x))
                .ToList();
            if (sources.Count == 0) return;
            var failures = await LoadSourcesAsync(sources, force: true, refreshAllKinds: true);
            if (failures.Count > 0)
                StatusText.Text = $"Cat?logo actualizado con avisos: {string.Join(" | ", failures)}";
        }
        catch (Exception ex)
        {
            StatusText.Text = "No se pudo actualizar el cat?logo p?blico: " + ex.Message;
        }
        finally
        {
            _publicRefreshTimer.Start();
        }
    }

    private const string M3uCacheKind = "M3uAll";

    private static Dictionary<string, CachedList> ReadCachedKinds(PlaylistSource source)
    {
        var result = new Dictionary<string, CachedList>(StringComparer.OrdinalIgnoreCase);
        if (source.Type != "Xtream")
        {
            // An M3U list holds live channels, movies and series together under one saved copy.
            // M3uCacheKind is new, so copies saved before movies/series were detected are ignored.
            if (SourceCache.TryLoad(source.Id, M3uCacheKind) is { } m3u) result["Live"] = m3u;
            return result;
        }
        foreach (var kind in new[] { "Live", "Movie", "Series" })
            if (SourceCache.TryLoad(source.Id, kind) is { } cached)
                result[kind] = cached;
        return result;
    }

    private HashSet<string> GetLoadedSet(string sourceId)
    {
        if (!_loadedXtreamKinds.TryGetValue(sourceId, out var loaded))
            _loadedXtreamKinds[sourceId] = loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return loaded;
    }

    // Lets concurrent callers (for example startup refresh and a click on "TV en vivo")
    // share one download instead of starting the same request twice.
    private async Task RunOnceAsync(string key, Func<Task> work)
    {
        if (_inflight.TryGetValue(key, out var running))
        {
            await running;
            return;
        }
        var task = work();
        _inflight[key] = task;
        try { await task; }
        finally { _inflight.Remove(key); }
    }

    private async void OpenM3u_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Listas M3U (*.m3u;*.m3u8)|*.m3u;*.m3u8|Todos (*.*)|*.*" };
        if (dlg.ShowDialog() != true) return;
        if (_catalogState.Sources.Any(x => x.Type == "M3UFile" && x.Address.Equals(dlg.FileName, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("Ese archivo ya está registrado en tus fuentes.", "Fuentes", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var source = new PlaylistSource
        {
            Name = Path.GetFileNameWithoutExtension(dlg.FileName),
            Type = "M3UFile", Address = dlg.FileName, Enabled = true
        };
        _catalogState.Sources.Add(source);
        CatalogStore.Save(_catalogState);
        try { await LoadSourceAsync(source, force: true); }
        catch (Exception ex) { Error($"No se pudo cargar {source.Name}", ex); }
    }

    private async void OpenM3uUrl_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new UrlDialog { Owner = this };
        if (dialog.ShowDialog() != true) return;
        var host = Uri.TryCreate(dialog.Address, UriKind.Absolute, out var sourceUri) ? sourceUri.Host : "Lista remota";
        var listName = Uri.TryCreate(dialog.Address, UriKind.Absolute, out var listUri)
            ? Path.GetFileName(listUri.AbsolutePath.TrimEnd('/')) : "";
        if (_catalogState.Sources.Any(x => x.Type == "M3UUrl" && x.Address.Equals(dialog.Address, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("Esa URL ya está registrada. Puedes gestionarla desde Administrar fuentes.", "Fuentes", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var source = new PlaylistSource
        {
            Name = string.IsNullOrWhiteSpace(listName) ? host : $"{host} · {listName}",
            Type = "M3UUrl", Address = dialog.Address
        };
        _catalogState.Sources.Add(source);
        CatalogStore.Save(_catalogState);
        try { await LoadSourceAsync(source, force: true); }
        catch (Exception ex) { Error($"No se pudo cargar {source.Name}", ex); }
    }

    private async void ConnectXtream_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new XtreamDialog { Owner = this };
        if (dialog.ShowDialog() != true) return;
        var host = Uri.TryCreate(dialog.Server, UriKind.Absolute, out var uri) ? uri.Host : "Xtream";
        if (_catalogState.Sources.Any(x => x.Type == "Xtream" &&
            x.Address.Equals(dialog.Server.TrimEnd('/'), StringComparison.OrdinalIgnoreCase) &&
            x.Username.Equals(dialog.Username, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("Esta cuenta ya está registrada. Abre Administrar fuentes para activarla o actualizarla.", "Fuentes", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var source = new PlaylistSource
        {
            Name = $"Xtream · {host} · {dialog.Username}", Type = "Xtream",
            Address = dialog.Server.TrimEnd('/'), Username = dialog.Username,
            Password = dialog.Password, Enabled = true
        };
        _catalogState.Sources.Add(source);
        CatalogStore.Save(_catalogState);
        try
        {
            await LoadSourceAsync(source, force: true);
            SetSection("Home");
        }
        catch (Exception ex) { Error($"No se pudo conectar con {source.Name}", ex); }
    }

    private async void ManageSources_Click(object sender, RoutedEventArgs e)
    {
        var previousIds = _catalogState.Sources.Select(x => x.Id).ToList();
        var manager = new SourceManagerWindow(_catalogState.Sources) { Owner = this };
        manager.ShowDialog();
        if (!manager.HasChanges) return;

        _catalogState.Sources = manager.Sources.ToList();
        CatalogStore.Save(_catalogState);
        var currentIds = _catalogState.Sources.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var removedId in previousIds.Where(id => !currentIds.Contains(id)))
            SourceCache.Delete(removedId);
        var activeIds = _catalogState.Sources.Where(x => x.Enabled).Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var id in _xtreamClients.Keys.Where(id => !activeIds.Contains(id)).ToList())
        {
            _xtreamClients[id].Dispose();
            _xtreamClients.Remove(id);
        }
        foreach (var id in _loadedXtreamKinds.Keys.Where(id => !activeIds.Contains(id)).ToList())
            _loadedXtreamKinds.Remove(id);
        _all.RemoveAll(x => !activeIds.Contains(x.SourceId));
        InvalidateSorted();
        ApplyFilter();
        await LoadEnabledSourcesAsync(force: true);
    }

    private async void ReloadSources_Click(object sender, RoutedEventArgs e)
    {
        await LoadEnabledSourcesAsync(force: true, refreshAllKinds: true);
    }

    private async Task LoadEnabledSourcesAsync(bool force = false, bool refreshAllKinds = false)
    {
        var enabled = _catalogState.Sources.Where(x => x.Enabled).ToList();
        if (enabled.Count == 0)
        {
            if (_all.Count == 0) StatusText.Text = "Añade una o varias fuentes M3U o Xtream para empezar.";
            return;
        }

        var failures = await LoadSourcesAsync(enabled, force, refreshAllKinds);
        FinishLoad(enabled, failures);
    }

    // Sources are downloaded and parsed in parallel, but only a few at a time: with the ~90 bundled
    // public lists, starting every download at once would saturate the connection and the CPU.
    private readonly SemaphoreSlim _loadGate = new(6);

    private async Task<List<string>> LoadSourcesAsync(IEnumerable<PlaylistSource> sources, bool force, bool refreshAllKinds)
    {
        var failures = new List<string>();
        await Task.WhenAll(sources.ToList().Select(async source =>
        {
            await _loadGate.WaitAsync();
            try { await LoadSourceAsync(source, force, refreshAllKinds); }
            catch (Exception ex) { lock (failures) failures.Add($"{source.Name}: {ex.Message}"); }
            finally { _loadGate.Release(); }
        }));
        return failures;
    }

    private void FinishLoad(List<PlaylistSource> enabled, List<string> failures)
    {
        var enabledIds = enabled.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (_all.RemoveAll(x => !enabledIds.Contains(x.SourceId)) > 0)
        {
            InvalidateSorted();
            _viewDirty = true;
        }
        if (_viewDirty) RefreshNow();
        StatusText.Text = failures.Count == 0
            ? $"Fuentes activas: {enabled.Count} · Contenido cargado: {_all.Count:N0} elementos."
            : $"Contenido: {_all.Count:N0} elementos. Fuentes con error: {string.Join(" | ", failures)}";
    }

    private async Task LoadSourceAsync(PlaylistSource source, bool force = false, bool refreshAllKinds = false)
    {
        if (!source.Enabled) return;
        if (source.Type == "Xtream")
        {
            await LoadXtreamKindAsync(source, "Live", force);
            if (force && refreshAllKinds)
            {
                // Movies/series that were already loaded (or restored from the saved copy) are refreshed too.
                foreach (var kind in new[] { "Movie", "Series" })
                {
                    if (!GetLoadedSet(source.Id).Contains(kind)) continue;
                    try { await LoadXtreamKindAsync(source, kind, true); }
                    catch (Exception ex) { StatusText.Text = $"No se pudo actualizar {source.Name}: {ex.Message}"; }
                }
            }
            return;
        }

        if (!force && GetLoadedSet(source.Id).Contains("Live")) return;
        await RunOnceAsync($"{source.Id}|Live", async () =>
        {
            var parsed = DropStreamsAlreadyLoaded(source, await ReadM3uAsync(source));
            MergeSourceKind(source, "Live", parsed);
            GetLoadedSet(source.Id).Add("Live");
            _ = SourceCache.SaveAsync(source.Id, M3uCacheKind, parsed.ToList());
            var movies = parsed.Count(x => x.Kind == "Movie");
            var series = parsed.Count(x => x.Kind == "Series");
            StatusText.Text = $"{source.Name}: {parsed.Count - movies - series - parsed.Count(x => x.Kind == "Episode"):N0} canales" +
                (movies + series > 0 ? $", {movies:N0} películas y {series:N0} series" : "") + " cargados.";
        });
    }

    // Download and parse happen on a worker thread, streaming line by line,
    // so even a very large list never blocks the window.
    private Task<List<MediaItem>> ReadM3uAsync(PlaylistSource source)
    {
        return Task.Run(async () =>
        {
            if (source.Type == "M3UFile")
            {
                using var fileReader = new StreamReader(source.Address, Encoding.UTF8, true, 65536);
                return M3uParser.Parse(fileReader, source.Name, source.Id);
            }

            using var response = await _sourceHttp.GetAsync(source.Address, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            var encoding = Encoding.UTF8;
            var charset = response.Content.Headers.ContentType?.CharSet;
            if (!string.IsNullOrWhiteSpace(charset))
            {
                try { encoding = Encoding.GetEncoding(charset.Trim('"')); }
                catch (ArgumentException) { }
            }

            // The 30 s HttpClient timeout only covers the headers; cap the body separately.
            using var bodyTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(180));
            using var abort = bodyTimeout.Token.Register(() => response.Dispose());
            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream, encoding, true, 65536);
                return M3uParser.Parse(reader, source.Name, source.Id);
            }
            catch (Exception) when (bodyTimeout.IsCancellationRequested)
            {
                throw new TimeoutException("La lista tardó demasiado en descargarse.");
            }
        });
    }

    private async Task<XtreamClient> GetXtreamClientAsync(PlaylistSource source)
    {
        if (_xtreamClients.TryGetValue(source.Id, out var existing) && existing.IsConnected)
            return existing;
        // Several kinds (live, movies, series) may ask for the same connection at once: connect only once.
        if (_xtreamConnecting.TryGetValue(source.Id, out var pending))
            return await pending;

        var connecting = ConnectXtreamAsync(source);
        _xtreamConnecting[source.Id] = connecting;
        try { return await connecting; }
        finally { _xtreamConnecting.Remove(source.Id); }
    }

    private async Task<XtreamClient> ConnectXtreamAsync(PlaylistSource source)
    {
        if (_xtreamClients.Remove(source.Id, out var stale)) stale.Dispose();
        var client = new XtreamClient();
        try
        {
            await client.ConnectAsync(source.Address, source.Username, source.Password);
            _xtreamClients[source.Id] = client;
            return client;
        }
        catch { client.Dispose(); throw; }
    }

    private async Task LoadXtreamKindAsync(PlaylistSource source, string kind, bool force = false)
    {
        if (!source.Enabled || source.Type != "Xtream") return;
        if (!force && GetLoadedSet(source.Id).Contains(kind)) return;

        await RunOnceAsync($"{source.Id}|{kind}", async () =>
        {
            StatusText.Text = $"Cargando {kind switch { "Live" => "canales", "Movie" => "películas", _ => "series" }} · {source.Name}…";
            var client = await GetXtreamClientAsync(source);
            var result = kind switch
            {
                "Movie" => await client.GetMoviesAsync(),
                "Series" => await client.GetSeriesAsync(),
                _ => await client.GetLiveAsync()
            };
            MergeSourceKind(source, kind, result);
            GetLoadedSet(source.Id).Add(kind);
            _ = SourceCache.SaveAsync(source.Id, kind, result.ToList());
        });
    }

    // The bundled public lists overlap heavily (the full index already contains every category, country
    // and language list). Each stream is kept once, in the first public source that provides it, so the
    // catalog, the guide and the saved copies do not hold the same channel dozens of times.
    private List<MediaItem> DropStreamsAlreadyLoaded(PlaylistSource source, List<MediaItem> items)
    {
        if (!PublicPlaylists.IsPublic(source) || items.Count == 0) return items;
        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var existing in _all)
            if (!existing.SourceId.Equals(source.Id, StringComparison.OrdinalIgnoreCase) &&
                existing.SourceId.StartsWith(PublicPlaylists.IdPrefix, StringComparison.Ordinal))
                present.Add(existing.Url);
        return items.Where(x => present.Add(x.Url)).ToList();
    }

    private void MergeSourceKind(PlaylistSource source, string kind, List<MediaItem> items, bool refresh = true)
    {
        // An M3U source delivers every kind in one list, so a refresh replaces all of its items.
        var wholeSource = source.Type != "Xtream";
        _all.RemoveAll(x => x.SourceId.Equals(source.Id, StringComparison.OrdinalIgnoreCase) &&
                            (wholeSource || x.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase)));
        var favorites = _catalogState.Favorites;
        foreach (var item in items)
        {
            item.SourceId = source.Id;
            item.Source = source.Name;
            if (favorites.Count > 0) item.IsFavorite = favorites.Contains(CatalogStore.Key(item));
        }
        _all.AddRange(items);
        InvalidateSorted();
        if (refresh) ScheduleRefresh();
    }

    private void InvalidateSorted() => _sortedCache.Clear();

    // Several sources finishing close together trigger a single (low-priority) view rebuild.
    private void ScheduleRefresh()
    {
        _viewDirty = true;
        if (_refreshQueued) return;
        _refreshQueued = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _refreshQueued = false;
            if (_viewDirty) RefreshNow();
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void RefreshNow()
    {
        _viewDirty = false;
        UpdateEpgForChannels();
        RefreshCategories();
        ApplyFilter();
    }

    private async void Movies_Click(object sender, RoutedEventArgs e)
    {
        await LoadXtreamSection("Movie");
    }

    private async void Series_Click(object sender, RoutedEventArgs e)
    {
        await LoadXtreamSection("Series");
    }

    private void OpenMetadataCatalog_Click(object sender, RoutedEventArgs e)
    {
        new CatalogDiscoveryWindow { Owner = this }.ShowDialog();
        StatusText.Text = "Catálogo externo cerrado. Tus listas y reproducción permanecen sin cambios.";
    }

    private async void Live_Click(object sender, RoutedEventArgs e)
    {
        await LoadXtreamSection("Live");
    }

    private async void Catalog_Click(object sender, RoutedEventArgs e)
    {
        await LoadFullCatalogAsync();
    }

    private void All_Click(object sender, RoutedEventArgs e)
    {
        _activeKind = "All";
        SetSection("Home");
    }

    private async Task LoadFullCatalogAsync()
    {
        await LoadEnabledSourcesAsync();
        // Movies and series of every account are requested at the same time.
        var xtreamSources = _catalogState.Sources.Where(x => x.Enabled && x.Type == "Xtream").ToList();
        await Task.WhenAll(xtreamSources.SelectMany(source =>
            new[] { "Movie", "Series" }.Select(kind => LoadXtreamKindSafeAsync(source, kind, "Catálogo parcial."))));

        _activeKind = "All";
        SetSection("Catalog");
        StatusText.Text = $"Catálogo: {_all.Count:N0} elementos de {_catalogState.Sources.Count(x => x.Enabled)} fuentes activas, organizados por tipo y país o región.";
    }

    private async Task LoadXtreamKindSafeAsync(PlaylistSource source, string kind, string errorPrefix)
    {
        try { await LoadXtreamKindAsync(source, kind); }
        catch (Exception ex) { StatusText.Text = $"{errorPrefix} {source.Name}: {ex.Message}"; }
    }

    private async Task LoadXtreamSection(string kind)
    {
        if (kind == "Live")
        {
            await LoadEnabledSourcesAsync();
            _activeKind = "Live";
            SetSection("Live");
            StatusText.Text = $"Guía de TV en vivo: {_items.Count:N0} canales de varias fuentes.";
            if (_items.Count == 0)
                MessageBox.Show("Añade una lista M3U o una conexión Xtream desde Administrar fuentes.", "TV en vivo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Movies and series come from Xtream accounts and from M3U lists that contain them.
        await LoadEnabledSourcesAsync();
        var sources = _catalogState.Sources.Where(x => x.Enabled && x.Type == "Xtream").ToList();
        await Task.WhenAll(sources.Select(source => LoadXtreamKindSafeAsync(source, kind, "No se pudo cargar")));

        var label = kind == "Movie" ? "películas" : "series";
        if (!_all.Any(x => x.Kind == kind))
        {
            MessageBox.Show($"No hay {label} en tus fuentes activas.\n\nConecta una cuenta Xtream o añade una lista M3U que incluya {label} " +
                "(direcciones con /movie/ o /series/, archivos .mp4/.mkv, o títulos con S01E02).",
                "Contenido no disponible", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _activeKind = kind;
        SetSection("Catalog");
        StatusText.Text = $"{char.ToUpper(label[0])}{label[1..]}: {_items.Count:N0} elementos de {_catalogState.Sources.Count(x => x.Enabled)} fuentes activas (M3U y Xtream).";
    }

    private void SetSection(string section)
    {
        _currentSection = section;
        _showFavorites = false;
        _showHistory = false;
        HomeHero.Visibility = section == "Home" ? Visibility.Visible : Visibility.Collapsed;
        var showGroupingTools = section == "Live" || section == "Catalog";
        GroupModeBox.Visibility = showGroupingTools ? Visibility.Visible : Visibility.Collapsed;
        CategoryBox.Visibility = showGroupingTools ? Visibility.Visible : Visibility.Collapsed;
        TargetGroupBox.Visibility = showGroupingTools ? Visibility.Visible : Visibility.Collapsed;
        CreateGroupButton.Visibility = showGroupingTools ? Visibility.Visible : Visibility.Collapsed;
        AssignGroupButton.Visibility = showGroupingTools ? Visibility.Visible : Visibility.Collapsed;

        switch (section)
        {
            case "Live":
                PageTitleText.Text = "TV en vivo";
                PageSubtitleText.Text = "Guía de canales organizada por país, región y categoría.";
                CatalogTitleText.Text = "Guía de programación";
                CatalogSubtitleText.Text = _epgPrograms.Count > 0
                    ? "Consulta el programa actual y el siguiente; selecciona un canal para reproducirlo."
                    : "Canales agrupados por país y categoría. Importa XMLTV para ver la programación actual y siguiente.";
                _activeKind = "Live";
                break;
            case "Catalog":
                PageTitleText.Text = "Catálogo";
                PageSubtitleText.Text = "Todo tu contenido, agrupado primero por tipo y después por país o región.";
                CatalogTitleText.Text = "Explorar por tipo y región";
                CatalogSubtitleText.Text = "TV en vivo, películas y series organizadas en secciones fáciles de recorrer.";
                break;
            default:
                PageTitleText.Text = "Descubre tu entretenimiento";
                PageSubtitleText.Text = "Todo tu contenido en un solo lugar.";
                CatalogTitleText.Text = "Tu biblioteca";
                CatalogSubtitleText.Text = "Continúa viendo o selecciona contenido para reproducirlo.";
                _activeKind = "All";
                break;
        }

        ResetFilterControls();
        RefreshCategories();
        ApplyFilter();
    }

    // Changing these controls would otherwise fire a full filter pass for each one.
    private void ResetFilterControls()
    {
        _suppress++;
        try
        {
            GroupModeBox.SelectedItem = "País";
            CategoryBox.SelectedItem = "Todos";
            SearchBox.Text = string.Empty;
        }
        finally { _suppress--; }
        _searchTimer.Stop();
    }

    private void ConfigureSectionUi()
    {
        HomeHero.Visibility = Visibility.Visible;
        GroupModeBox.Visibility = Visibility.Collapsed;
        CategoryBox.Visibility = Visibility.Collapsed;
        TargetGroupBox.Visibility = Visibility.Collapsed;
        CreateGroupButton.Visibility = Visibility.Collapsed;
        AssignGroupButton.Visibility = Visibility.Collapsed;
        PageTitleText.Text = "Descubre tu entretenimiento";
        PageSubtitleText.Text = "Todo tu contenido en un solo lugar.";
        CatalogTitleText.Text = "Tu biblioteca";
        CatalogSubtitleText.Text = "Continúa viendo o selecciona contenido para reproducirlo.";
    }

    private Func<MediaItem, string> GetGroupSelector(string mode)
    {
        switch (mode)
        {
            case "Categoría":
                return x => x.CategoryGroup;
            case "Servidor":
                return x => x.Source;
            case "Mis grupos":
                return x => _catalogState.CustomGroups.TryGetValue(CatalogStore.Key(x), out var custom) ? custom : "Sin asignar";
            default:
                return x => x.CountryGroup;
        }
    }

    private void RefreshCategories()
    {
        if (CategoryBox is null) return;
        var selected = CategoryBox.SelectedItem?.ToString() ?? "Todos";
        var mode = GroupModeBox.SelectedItem?.ToString() ?? "País";
        CategoryBox.SelectionChanged -= CategoryBox_SelectionChanged;
        CategoryBox.Items.Clear();
        CategoryBox.Items.Add("Todos");

        IEnumerable<string> values;
        if (_currentSection == "Catalog" || _currentSection == "Live")
        {
            IEnumerable<MediaItem> scope = _currentSection == "Live" ? _all.Where(x => x.Kind == "Live") : _all;
            values = scope.Select(GetGroupSelector(mode));
            if (mode == "Mis grupos")
                values = values.Concat(_catalogState.CustomGroups
                    .Where(x => x.Key.StartsWith("__group__:", StringComparison.Ordinal)).Select(x => x.Value));
        }
        else
            values = Array.Empty<string>();

        // Distinct already guarantees uniqueness, so no per-item Items.Contains scan is needed.
        foreach (var value in values.Where(x => !string.IsNullOrWhiteSpace(x))
                     .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
            if (value != "Todos") CategoryBox.Items.Add(value);

        CategoryBox.SelectedItem = CategoryBox.Items.Contains(selected) ? selected : "Todos";
        CategoryBox.SelectionChanged += CategoryBox_SelectionChanged;
        RefreshTargetGroups();
    }

    private void RefreshTargetGroups()
    {
        if (TargetGroupBox is null) return;
        var selected = TargetGroupBox.SelectedItem?.ToString();
        TargetGroupBox.Items.Clear();
        TargetGroupBox.Items.Add("Selecciona grupo");
        TargetGroupBox.Items.Add("Sin asignar");
        foreach (var group in _catalogState.CustomGroups.Values
                     .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
            TargetGroupBox.Items.Add(group);
        TargetGroupBox.SelectedItem = selected is not null && TargetGroupBox.Items.Contains(selected)
            ? selected : "Selecciona grupo";
    }

    // The catalog is sorted once per section (and only again when the data changes),
    // using the same order the grouped view used before: group keys first, then name.
    private List<MediaItem> GetSortedItems()
    {
        var key = _currentSection == "Catalog" ? "Catalog" : _currentSection == "Live" ? "Live" : "Home";
        if (_sortedCache.TryGetValue(key, out var cached)) return cached;
        var sorted = BuildSorted(key);
        _sortedCache[key] = sorted;
        return sorted;
    }

    private List<MediaItem> BuildSorted(string key)
    {
        var names = StringComparer.CurrentCulture;
        switch (key)
        {
            case "Catalog":
            {
                var kindRank = BuildRanks(_all.Select(x => x.KindGroup));
                var countryRank = BuildRanks(_all.Select(x => x.CountryGroup));
                return _all.OrderBy(x => kindRank[x.KindGroup])
                    .ThenBy(x => countryRank[x.CountryGroup])
                    .ThenBy(x => x.Name, names).ToList();
            }
            case "Live":
            {
                var live = _all.Where(x => x.Kind == "Live").ToList();
                var countryRank = BuildRanks(live.Select(x => x.CountryGroup));
                var categoryRank = BuildRanks(live.Select(x => x.CategoryGroup));
                return live.OrderBy(x => countryRank[x.CountryGroup])
                    .ThenBy(x => categoryRank[x.CategoryGroup])
                    .ThenBy(x => x.Name, names).ToList();
            }
            default:
                return _all.OrderBy(x => x.Name, names).ToList();
        }
    }

    // Group labels are few and repeat across thousands of items: sort the distinct
    // labels once and compare integers afterwards.
    private static Dictionary<string, int> BuildRanks(IEnumerable<string> values)
    {
        var distinct = new HashSet<string>(values, StringComparer.Ordinal).ToList();
        distinct.Sort(StringComparer.CurrentCulture);
        var ranks = new Dictionary<string, int>(distinct.Count, StringComparer.Ordinal);
        for (var i = 0; i < distinct.Count; i++) ranks[distinct[i]] = i;
        return ranks;
    }

    private void ApplyFilter()
    {
        if (SearchBox is null || CategoryBox is null) return;
        var query = SearchBox.Text.Trim();
        var selectedGroup = CategoryBox.SelectedItem?.ToString() ?? "Todos";
        var mode = GroupModeBox.SelectedItem?.ToString() ?? "País";
        var grouped = _currentSection == "Catalog" || _currentSection == "Live";
        Func<MediaItem, string> groupOf = GetGroupSelector(mode);
        if (!grouped) groupOf = _ => "Todos";
        var historyUrls = _showHistory
            ? new HashSet<string>(_catalogState.History.Select(h => h.Url), StringComparer.Ordinal)
            : null;
        var activeKind = _activeKind;
        var liveOnly = _currentSection == "Live";
        var anyGroup = selectedGroup == "Todos";

        var filtered = new List<MediaItem>();
        foreach (var x in GetSortedItems())
        {
            if (x.Kind == "Episode") continue; // M3U episodes are opened from their series
            if (activeKind != "All" && !x.Kind.Equals(activeKind, StringComparison.OrdinalIgnoreCase)) continue;
            if (liveOnly && x.Kind != "Live") continue;
            if (_showFavorites && !x.IsFavorite) continue;
            if (historyUrls is not null && !historyUrls.Contains(x.Url)) continue;
            if (!anyGroup && !groupOf(x).Equals(selectedGroup, StringComparison.OrdinalIgnoreCase)) continue;
            if (query.Length > 0 && !x.Name.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !x.Category.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !x.Country.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !x.Source.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
            filtered.Add(x);
        }

        // One new view per pass instead of Clear()+Add() item by item (which regrouped
        // and re-sorted the view on every single insertion).
        _items = filtered;
        var view = new ListCollectionView(filtered);
        if (_currentSection == "Catalog")
        {
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(MediaItem.KindGroup)));
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(MediaItem.CountryGroup)));
        }
        else if (_currentSection == "Live")
        {
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(MediaItem.CountryGroup)));
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(MediaItem.CategoryGroup)));
        }
        MediaList.ItemsSource = view;

        if (CatalogCountText is not null)
        {
            CatalogCountText.Text = _currentSection == "Live"
                ? $"GUÍA DE TV · {_items.Count:N0} CANALES"
                : _showHistory ? $"{_items.Count:N0} ELEMENTOS RECIENTES"
                : _showFavorites ? $"{_items.Count:N0} FAVORITOS"
                : $"{_items.Count:N0} ELEMENTOS DISPONIBLES";
        }
    }

    private void GroupModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _suppress > 0) return;
        RefreshCategories();
        ApplyFilter();
    }

    private void CreateGroup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new GroupNameDialog { Owner = this };
        if (dialog.ShowDialog() != true) return;
        var name = dialog.GroupName.Trim();
        if (name.Length == 0) return;
        if (name.Equals("Sin asignar", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Todos", StringComparison.OrdinalIgnoreCase) ||
            _catalogState.CustomGroups.Values.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("Ya existe un grupo con ese nombre.", "Mis grupos", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        // A placeholder mapping keeps the group visible until channels are assigned.
        _catalogState.CustomGroups[$"__group__:{Guid.NewGuid():N}"] = name;
        CatalogStore.Save(_catalogState);
        RefreshCategories();
        RefreshTargetGroups();
        TargetGroupBox.SelectedItem = name;
        GroupModeBox.SelectedItem = "Mis grupos";
        StatusText.Text = $"Grupo creado: {name}. Selecciona canales y pulsa Asignar selección.";
    }

    private void AssignGroup_Click(object sender, RoutedEventArgs e)
    {
        var target = TargetGroupBox.SelectedItem?.ToString();
        if (string.IsNullOrWhiteSpace(target) || target == "Selecciona grupo")
        {
            MessageBox.Show("Primero crea o selecciona un grupo de destino.", "Mis grupos", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var selected = MediaList.SelectedItems.Cast<MediaItem>().ToList();
        if (selected.Count == 0 && MediaList.SelectedItem is MediaItem single) selected.Add(single);
        if (selected.Count == 0)
        {
            MessageBox.Show("Selecciona uno o varios canales de la lista. Puedes usar Ctrl o Shift para seleccionar varios.", "Mis grupos", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        foreach (var item in selected)
        {
            var key = CatalogStore.Key(item);
            if (target == "Sin asignar") _catalogState.CustomGroups.Remove(key);
            else _catalogState.CustomGroups[key] = target;
        }
        CatalogStore.Save(_catalogState);
        _suppress++;
        try { GroupModeBox.SelectedItem = "Mis grupos"; }
        finally { _suppress--; }
        RefreshCategories();
        _suppress++;
        try { CategoryBox.SelectedItem = target; }
        finally { _suppress--; }
        ApplyFilter();
        StatusText.Text = target == "Sin asignar"
            ? $"Se quitaron {selected.Count} canales de sus grupos personalizados."
            : $"Se asignaron {selected.Count} canales al grupo {target}.";
    }

    // Typing is debounced: the list is filtered once the user pauses, not on every keystroke.
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppress > 0 || _searchTimer is null) return;
        _searchTimer.Stop();
        _searchTimer.Start();
    }
    private void CategoryBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress > 0) return;
        ApplyFilter();
    }
    private void ShowFavorites_Click(object sender, RoutedEventArgs e)
    {
        HomeHero.Visibility = Visibility.Collapsed;
        _showFavorites = true; _showHistory = false;
        GroupModeBox.Visibility = Visibility.Collapsed;
        CategoryBox.Visibility = Visibility.Collapsed;
        TargetGroupBox.Visibility = Visibility.Collapsed;
        CreateGroupButton.Visibility = Visibility.Collapsed;
        AssignGroupButton.Visibility = Visibility.Collapsed;
        PageTitleText.Text = "Mis favoritos";
        CatalogTitleText.Text = "Tu selección";
        CatalogSubtitleText.Text = "Tus canales, películas y series guardados.";
        _currentSection = "Catalog"; _activeKind = "All";
        ResetFilterControls(); RefreshCategories(); ApplyFilter();
    }
    private void ShowHistory_Click(object sender, RoutedEventArgs e)
    {
        HomeHero.Visibility = Visibility.Collapsed;
        _showFavorites = false; _showHistory = true;
        GroupModeBox.Visibility = Visibility.Collapsed;
        CategoryBox.Visibility = Visibility.Collapsed;
        TargetGroupBox.Visibility = Visibility.Collapsed;
        CreateGroupButton.Visibility = Visibility.Collapsed;
        AssignGroupButton.Visibility = Visibility.Collapsed;
        PageTitleText.Text = "Continuar viendo";
        CatalogTitleText.Text = "Reproducidos recientemente";
        CatalogSubtitleText.Text = "Vuelve al contenido que abriste recientemente.";
        _currentSection = "Catalog"; _activeKind = "All";
        ResetFilterControls(); RefreshCategories(); ApplyFilter();
    }
    private void ShowAll_Click(object sender, RoutedEventArgs e)
    {
        _showFavorites = false; _showHistory = false; _activeKind = "All";
        SetSection("Home");
    }

    private async void OpenEpg_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog {
            Filter = "Guía XMLTV (*.xml;*.xmltv;*.gz)|*.xml;*.xmltv;*.gz|Archivos XML (*.xml)|*.xml|Todos (*.*)|*.*"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            StatusText.Text = "Importando guía XMLTV…";
            var programs = await XmlTvService.LoadAsync(dlg.FileName);
            _epgPrograms.Clear();
            _epgPrograms.AddRange(programs);
            RebuildEpgIndex();
            UpdateEpgForChannels();
            if (_currentSection == "Live")
            {
                CatalogSubtitleText.Text = "Consulta el programa actual y el siguiente; selecciona un canal para reproducirlo.";
                ApplyFilter();
            }
            new EpgWindow(programs) { Owner = this }.ShowDialog();
            StatusText.Text = $"Guía EPG cargada: {programs.Count:N0} programas.";
        }
        catch (Exception ex) { Error("No se pudo importar la guía XMLTV", ex); }
    }

    // Programmes are indexed once per imported guide (they arrive sorted by start time),
    // not rebuilt every time the channel list is refreshed.
    private void RebuildEpgIndex()
    {
        if (_epgPrograms.Count == 0)
        {
            _epgById = null;
            _epgByName = null;
            return;
        }
        _epgById = BuildEpgIndex(x => x.ChannelId);
        _epgByName = BuildEpgIndex(x => x.ChannelName);
    }

    private Dictionary<string, List<EpgProgram>> BuildEpgIndex(Func<EpgProgram, string> keyOf)
    {
        var map = new Dictionary<string, List<EpgProgram>>(StringComparer.OrdinalIgnoreCase);
        foreach (var program in _epgPrograms)
        {
            var raw = keyOf(program);
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var key = NormalizeChannelKey(raw);
            if (!map.TryGetValue(key, out var list)) map[key] = list = new List<EpgProgram>();
            list.Add(program);
        }
        return map;
    }

    private void UpdateEpgForChannels()
    {
        if (_epgPrograms.Count == 0)
        {
            // Channels already carry the "guide not loaded" defaults; only undo a previously applied guide.
            if (!_epgApplied) return;
            _epgApplied = false;
            foreach (var channel in _all)
            {
                if (channel.Kind != "Live") continue;
                channel.CurrentProgramTitle = "Guía EPG no cargada";
                channel.CurrentProgramTime = "Importa XMLTV para ver horarios";
                channel.NextProgramTitle = "";
                channel.NextProgramTime = "";
            }
            return;
        }

        if (_epgById is null || _epgByName is null) RebuildEpgIndex();
        var byId = _epgById!;
        var byName = _epgByName!;
        var now = DateTime.Now;
        _epgApplied = true;

        foreach (var channel in _all)
        {
            if (channel.Kind != "Live") continue;
            List<EpgProgram>? programs = null;
            if (!string.IsNullOrWhiteSpace(channel.EpgId))
                byId.TryGetValue(NormalizeChannelKey(channel.EpgId), out programs);
            if (programs is null)
                byName.TryGetValue(NormalizeChannelKey(channel.Name), out programs);

            EpgProgram? current = null;
            EpgProgram? next = null;
            if (programs is not null)
            {
                foreach (var program in programs)
                {
                    if (current is null && program.Start <= now && program.Stop > now) current = program;
                    if (next is null && program.Start >= now) next = program;
                    if (current is not null && next is not null) break;
                }
            }
            channel.CurrentProgramTitle = current?.Title ?? "Guía sin programa actual";
            channel.CurrentProgramTime = current is null ? "Sin emisión identificada en este momento" : $"{current.Start:HH:mm} – {current.Stop:HH:mm}";
            channel.NextProgramTitle = next is null || ReferenceEquals(next, current) ? "" : next.Title;
            channel.NextProgramTime = next is null ? "" : next.Start.ToString("HH:mm");
        }
    }

    private static string NormalizeChannelKey(string value)
    {
        var text = value.Trim();
        var ascii = true;
        foreach (var ch in text)
            if (ch > 127) { ascii = false; break; }
        if (!ascii) text = text.Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (!ascii && CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            var lower = char.ToLowerInvariant(ch);
            if ((lower >= 'a' && lower <= 'z') || (lower >= '0' && lower <= '9')) builder.Append(lower);
        }
        return builder.ToString();
    }

    private void Favorite_Click(object sender, RoutedEventArgs e)
    {
        if (MediaList.SelectedItem is not MediaItem item) return;
        item.IsFavorite = !item.IsFavorite;
        var key = CatalogStore.Key(item);
        if (item.IsFavorite) _catalogState.Favorites.Add(key);
        else _catalogState.Favorites.Remove(key);
        CatalogStore.Save(_catalogState);
        // The cards do not display the star, so the list only needs rebuilding in the favorites view.
        if (_showFavorites) ApplyFilter();
        StatusText.Text = item.IsFavorite ? "Añadido a favoritos." : "Eliminado de favoritos.";
    }

    private void MediaList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MediaList.SelectedItem is MediaItem item)
            StatusText.Text = $"Seleccionado: {item.Name}. Pulsa Reproducir o haz doble clic.";
    }

    private async void MediaList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (MediaList.SelectedItem is not MediaItem item) return;
        if (item.Kind == "Series") await OpenSeriesEpisodesAsync(item);
        else PlayMediaItem(item);
    }

    private async Task OpenSeriesEpisodesAsync(MediaItem series)
    {
        if (series.StreamId is null && series.SeriesKey.Length == 0)
        {
            StatusText.Text = "Esta serie no tiene un identificador válido.";
            return;
        }
        try
        {
            StatusText.Text = $"Cargando episodios de {series.Name}…";
            List<SeriesEpisode> episodes;
            if (series.StreamId is null)
            {
                // Series that come from an M3U list: the episodes are entries of the same list.
                episodes = _all
                    .Where(x => x.Kind == "Episode" && x.SeriesKey == series.SeriesKey &&
                                x.SourceId.Equals(series.SourceId, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(x => x.Season).ThenBy(x => x.EpisodeNumber).ThenBy(x => x.Name, StringComparer.CurrentCulture)
                    .Select(x => new SeriesEpisode
                    {
                        Title = x.Name,
                        Season = x.Season.ToString(),
                        EpisodeNumber = x.EpisodeNumber.ToString(),
                        Url = x.Url
                    }).ToList();
            }
            else
            {
                if (!_xtreamClients.TryGetValue(series.SourceId, out var seriesClient) || !seriesClient.IsConnected)
                {
                    var seriesSource = _catalogState.Sources.FirstOrDefault(x => x.Id == series.SourceId && x.Enabled);
                    if (seriesSource is null) { StatusText.Text = "No se encontró la fuente de esta serie."; return; }
                    seriesClient = await GetXtreamClientAsync(seriesSource);
                }
                episodes = await seriesClient.GetSeriesEpisodesAsync(series.StreamId.Value);
            }
            if (episodes.Count == 0)
            {
                StatusText.Text = "El proveedor no devolvió episodios para esta serie.";
                return;
            }
            // When launched from the in-player guide the dialog must appear above the player window.
            Window dialogOwner = _playerWindow is { IsVisible: true } ? _playerWindow : this;
            var dialog = new SeriesEpisodesWindow(series.Name, episodes) { Owner = dialogOwner };
            if (dialog.ShowDialog() == true && dialog.SelectedEpisode is { } episode)
            {
                PlayMediaItem(new MediaItem
                {
                    Name = $"{series.Name} · T{episode.Season} E{episode.EpisodeNumber} · {episode.Title}",
                    Url = episode.Url,
                    Kind = "Episode",
                    Category = series.Category,
                    Logo = series.Logo,
                    Source = series.Source,
                    SourceId = series.SourceId
                });
            }
            else StatusText.Text = $"Episodios disponibles: {episodes.Count}.";
        }
        catch (Exception ex) { Error("No se pudieron cargar los episodios", ex); }
    }

    private async void MediaList_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter && MediaList.SelectedItem is MediaItem item)
        {
            if (item.Kind == "Series") await OpenSeriesEpisodesAsync(item);
            else PlayMediaItem(item);
            e.Handled = true;
        }
    }

    private async void Play_Click(object sender, RoutedEventArgs e)
    {
        if (MediaList.SelectedItem is not MediaItem item)
        { StatusText.Text = "Selecciona un elemento reproducible."; return; }
        if (item.Kind == "Series") { await OpenSeriesEpisodesAsync(item); return; }
        if (string.IsNullOrWhiteSpace(item.Url))
        { StatusText.Text = "Selecciona un elemento reproducible."; return; }
        PlayMediaItem(item);
    }

    // Shared playback entry point for catalog items and Xtream episodes.
    private void PlayMediaItem(MediaItem item, List<MediaItem>? queue = null)
    {
        if (string.IsNullOrWhiteSpace(item.Url)) { StatusText.Text = "El elemento no tiene una dirección reproducible."; return; }
        if (!Uri.TryCreate(item.Url, UriKind.Absolute, out var uri)) { StatusText.Text = "La dirección del contenido no es válida."; return; }
        try
        {
            EnsureVlc();

            // The currently visible catalog is the playback queue. _items is replaced (never
            // modified) by every filter pass, so it can be referenced without copying it.
            var visibleQueue = queue ?? _items;
            var queueIndex = -1;
            for (var i = 0; i < visibleQueue.Count; i++)
                if (ReferenceEquals(visibleQueue[i], item)) { queueIndex = i; break; }
            if (queueIndex < 0)
                queueIndex = visibleQueue.FindIndex(x =>
                    x.Url.Equals(item.Url, StringComparison.OrdinalIgnoreCase));
            if (queueIndex >= 0)
            {
                _playbackQueue = visibleQueue;
                _playbackIndex = queueIndex;
            }

            _playerWindow ??= new PlayerWindow(_player!, ChangePlaybackItem, ToggleGuide);
            _playerWindow.SetTitle(item.Name);
            _nowPlaying = item;
            _guideWindow?.SetCurrent(item);
            if (!_playerWindow.IsVisible)
            {
                _playerWindow.Show();
                _playerWindow.WindowState = WindowState.Maximized;
            }
            // When the guide is open the user is channel surfing: keep keyboard focus in the guide.
            if (_guideWindow is not { IsVisible: true }) _playerWindow.Activate();

            // Let WPF finish loading VideoView and create its native HWND host first.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    _player!.Stop();
                    _currentMedia?.Dispose();
                    _currentMedia = new Media(_libVlc!, uri);
                    _currentMedia.AddOption(":network-caching=3000");
                    _playerWindow?.SetPlaybackStatus("Conectando con el stream…");
                    if (!_player.Play(_currentMedia))
                    {
                        SetPlayerStatus("VLC no pudo iniciar el contenido. Revisa el registro de diagnóstico.");
                        return;
                    }
                    _catalogState.History.RemoveAll(x => x.Url.Equals(item.Url, StringComparison.OrdinalIgnoreCase));
                    _catalogState.History.Insert(0, new HistoryEntry { Name = item.Name, Url = item.Url, Kind = item.Kind, PlayedAt = DateTime.Now });
                    if (_catalogState.History.Count > 100) _catalogState.History.RemoveRange(100, _catalogState.History.Count - 100);
                    CatalogStore.Save(_catalogState);
                    StatusText.Text = "Reproduciendo en ventana independiente.";
                }
                catch (Exception ex)
                {
                    Error("No se pudo iniciar la reproducción", ex);
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
        catch (Exception ex) { Error("No se pudo iniciar la reproducción", ex); }
    }

    private void ChangePlaybackItem(int direction)
    {
        if (_playbackQueue.Count == 0 || _playbackIndex < 0)
        {
            SetPlayerStatus("No hay una lista de reproducción para cambiar de canal.");
            return;
        }

        var nextIndex = _playbackIndex + Math.Sign(direction);
        if (nextIndex < 0 || nextIndex >= _playbackQueue.Count)
        {
            SetPlayerStatus(nextIndex < 0 ? "Estás en el primer canal." : "Estás en el último canal.");
            return;
        }

        _playbackIndex = nextIndex;
        PlayMediaItem(_playbackQueue[_playbackIndex], _playbackQueue);
    }

    // Opens/closes the channel guide that sits on top of the player.
    private void ToggleGuide()
    {
        if (_playerWindow is not { IsVisible: true }) return;
        if (_guideWindow is { IsOpen: true })
        {
            // Open and visible: close it. Open but hidden together with the bar: bring it back.
            if (_guideWindow.IsVisible) _guideWindow.HideGuide();
            else _guideWindow.Reveal();
            return;
        }
        if (_guideWindow is null)
        {
            var player = _playerWindow;
            var guide = new PlayerGuideWindow(() => _all, ActivateFromGuide) { Owner = player };
            // The guide shows and hides at exactly the same time as the floating controls bar.
            Action<bool> followBar = visible => guide.SetControlsVisible(visible);
            player.ControlsVisibilityChanged += followBar;
            guide.Interacted += player.KeepControlsAlive;
            guide.Closed += (_, _) =>
            {
                player.ControlsVisibilityChanged -= followBar;
                if (ReferenceEquals(_guideWindow, guide)) _guideWindow = null;
            };
            _guideWindow = guide;
        }
        _guideWindow.Open(_nowPlaying);
    }

    private async void ActivateFromGuide(MediaItem item, List<MediaItem> queue)
    {
        if (item.Kind == "Series") await OpenSeriesEpisodesAsync(item);
        else PlayMediaItem(item, queue);
    }

    private void SetPlayerStatus(string message)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => SetPlayerStatus(message)));
            return;
        }

        StatusText.Text = message;
        _playerWindow?.SetPlaybackStatus(message);
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.F11 && _playerWindow is { IsVisible: true })
        {
            _playerWindow.ToggleFullscreen();
            e.Handled = true;
        }
    }

    private void Error(string title, Exception ex)
    {
        StatusText.Text = title + ": " + ex.Message;
        MessageBox.Show(ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
