using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using MagisWindowsNative.Models;

namespace MagisWindowsNative.Services;

public sealed class XtreamClient : IDisposable
{
    // Compressed transfers (gzip/deflate/brotli) shrink big catalog responses a lot.
    // The 25 s timeout applies until the response headers arrive; the body is
    // streamed into the JSON parser and capped separately in GetJsonDocAsync.
    private readonly HttpClient _http = new(new SocketsHttpHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        ConnectTimeout = TimeSpan.FromSeconds(15)
    }) { Timeout = TimeSpan.FromSeconds(25) };

    private static readonly TimeSpan BodyTimeout = TimeSpan.FromSeconds(120);
    private string _base = "", _user = "", _pass = "";
    public bool IsConnected { get; private set; }
    private string SourceHost => Uri.TryCreate(_base, UriKind.Absolute, out var parsed) ? parsed.Host : "Xtream";

    public async Task ConnectAsync(string host, string username, string password, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(host.Trim().TrimEnd('/'), UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
            throw new ArgumentException("El servidor debe ser una dirección http:// o https:// válida.");

        _base = uri.ToString().TrimEnd('/');
        _user = username.Trim();
        _pass = password;
        IsConnected = false;
        using var doc = await GetJsonDocAsync("player_api.php", null, ct).ConfigureAwait(false);
        var json = doc.RootElement;
        var userInfo = json.TryGetProperty("user_info", out var info) ? info : default;
        if (userInfo.ValueKind == JsonValueKind.Undefined ||
            (userInfo.TryGetProperty("auth", out var auth) && auth.ToString() == "0"))
            throw new InvalidOperationException("El servidor no aceptó las credenciales o no devolvió user_info.");

        IsConnected = true;
    }

    public Task<List<MediaItem>> GetLiveAsync(CancellationToken ct = default) =>
        GetStreamsAsync("get_live_streams", "Live", ct);

    public Task<List<MediaItem>> GetMoviesAsync(CancellationToken ct = default) =>
        GetStreamsAsync("get_vod_streams", "Movie", ct);

    public Task<List<MediaItem>> GetSeriesAsync(CancellationToken ct = default) =>
        GetStreamsAsync("get_series", "Series", ct);


    public async Task<List<SeriesEpisode>> GetSeriesEpisodesAsync(int seriesId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_base) || string.IsNullOrWhiteSpace(_user))
            throw new InvalidOperationException("Conecta primero un servidor Xtream.");

        using var doc = await GetJsonDocAsync("player_api.php", $"get_series_info&series_id={seriesId}", ct).ConfigureAwait(false);
        var data = doc.RootElement;
        var episodes = new List<SeriesEpisode>();
        if (!data.TryGetProperty("episodes", out var seasons) || seasons.ValueKind != JsonValueKind.Object)
            return episodes;

        var user = Esc(_user);
        var pass = Esc(_pass);
        foreach (var season in seasons.EnumerateObject())
        {
            if (season.Value.ValueKind != JsonValueKind.Array) continue;
            foreach (var ep in season.Value.EnumerateArray())
            {
                var id = Int(ep, "id") ?? Int(ep, "episode_id");
                if (id is null) continue;
                var ext = Str(ep, "container_extension") ?? "mp4";
                var title = Str(ep, "title") ?? Str(ep, "name") ?? $"Episodio {Str(ep, "episode_num") ?? ""}";
                var seasonNumber = Str(ep, "season") ?? season.Name;
                var episodeNumber = Str(ep, "episode_num") ?? "";
                episodes.Add(new SeriesEpisode {
                    Title = title,
                    Season = seasonNumber,
                    EpisodeNumber = episodeNumber,
                    Plot = Str(ep, "info") is { } info ? info : "",
                    Duration = Str(ep, "duration") ?? "",
                    Url = $"{_base}/series/{user}/{pass}/{id}.{Esc(ext)}"
                });
            }
        }
        return episodes.OrderBy(x => int.TryParse(x.Season, out var n) ? n : 0)
            .ThenBy(x => int.TryParse(x.EpisodeNumber, out var n) ? n : 0).ToList();
    }

    private async Task<List<MediaItem>> GetStreamsAsync(string action, string kind, CancellationToken ct)
    {
        var categoryAction = kind switch
        {
            "Live" => "get_live_categories",
            "Movie" => "get_vod_categories",
            _ => "get_series_categories"
        };

        // Category names and the stream list are independent requests: fetch both at once.
        var categoriesTask = GetCategoriesSafeAsync(categoryAction, ct);
        using var data = await GetJsonDocAsync("player_api.php", action, ct).ConfigureAwait(false);
        var categories = await categoriesTask.ConfigureAwait(false);

        var root = data.RootElement;
        if (root.ValueKind != JsonValueKind.Array) return new List<MediaItem>();

        // JSON parsing and item creation happen on this worker continuation
        // (ConfigureAwait(false)), never on the UI thread.
        var list = new List<MediaItem>(root.GetArrayLength());
        var user = Esc(_user);
        var pass = Esc(_pass);
        var host = SourceHost;
        var endpoint = kind == "Live" ? "live" : kind == "Movie" ? "movie" : "series";
        foreach (var row in root.EnumerateArray())
        {
            var id = Int(row, "stream_id") ?? Int(row, "series_id");
            var name = Str(row, "name") ?? Str(row, "title") ?? $"Item {id}";
            var ext = Str(row, "container_extension") ?? (kind == "Movie" ? "mp4" : "");
            var suffix = kind == "Live" ? ".ts" :
                (kind == "Movie" || kind == "Series") && !string.IsNullOrWhiteSpace(ext) ? "." + Esc(ext) : "";
            var url = id is null ? "" : $"{_base}/{endpoint}/{user}/{pass}/{id}{suffix}";
            var categoryIdValue = Str(row, "category_id");

            string category;
            var categoryNameValue = Str(row, "category_name");
            if (categoryNameValue is not null) category = categoryNameValue;
            else if (categoryIdValue is not null)
                category = categories.TryGetValue(categoryIdValue, out var categoryName)
                    ? categoryName : $"Categoría {categoryIdValue}";
            else
            {
                var inferredCountry = CountryResolver.Resolve(name);
                category = inferredCountry != "Sin país identificado" ? inferredCountry : "General";
            }

            list.Add(new MediaItem {
                Name = name, Url = url, Kind = kind,
                Category = category,
                Country = CountryResolver.Resolve(category, name),
                Source = host,
                Logo = Str(row, "stream_icon") ?? Str(row, "cover") ?? "",
                EpgId = Str(row, "epg_channel_id") ?? "",
                StreamId = id, ContainerExtension = ext
            });
        }
        return list;
    }

    private async Task<Dictionary<string, string>> GetCategoriesSafeAsync(string action, CancellationToken ct)
    {
        try { return await GetCategoriesAsync(action, ct).ConfigureAwait(false); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            // Providers that do not publish categories still load; items fall back to "Categoría N".
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task<Dictionary<string, string>> GetCategoriesAsync(string action, CancellationToken ct)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var doc = await GetJsonDocAsync("player_api.php", action, ct).ConfigureAwait(false);
        var data = doc.RootElement;
        if (data.ValueKind != JsonValueKind.Array) return result;
        foreach (var row in data.EnumerateArray())
        {
            var id = Str(row, "category_id");
            var name = Str(row, "category_name");
            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
                result[id] = name;
        }
        return result;
    }

    // The caller owns (and must dispose) the returned document. Parsing straight from
    // the response stream avoids buffering the whole body and cloning the tree.
    private async Task<JsonDocument> GetJsonDocAsync(string endpoint, string? action, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_base) || string.IsNullOrWhiteSpace(_user))
            throw new InvalidOperationException("No hay una conexión Xtream activa. Carga una lista M3U para navegar por sus canales o conecta un servidor Xtream para acceder a series y películas.");

        var query = $"username={Uri.EscapeDataString(_user)}&password={Uri.EscapeDataString(_pass)}";
        if (!string.IsNullOrWhiteSpace(action))
        {
            var parts = action.Split('&', 2);
            query += "&action=" + Uri.EscapeDataString(parts[0]);
            if (parts.Length == 2) query += "&" + parts[1];
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(BodyTimeout);
        using var response = await _http.GetAsync($"{_base}/{endpoint}?{query}", HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token).ConfigureAwait(false);
    }

    private static string Esc(string s) => Uri.EscapeDataString(s);
    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : null;
    private static int? Int(JsonElement e, string name) =>
        int.TryParse(Str(e, name), out var n) ? n : null;

    public void Dispose() => _http.Dispose();
}
