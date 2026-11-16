using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MagisWindowsNative.Services;

public sealed class ExternalCatalogItem
{
    public string Title { get; init; } = "";
    public string Year { get; init; } = "";
    public string Summary { get; init; } = "";
    public string PosterUrl { get; init; } = "";
    public string ExternalId { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Genres { get; init; } = "";
    public string ExtraInfo { get; init; } = "";
    public override string ToString() => string.IsNullOrWhiteSpace(Year) ? Title : $"{Title} ({Year})";
}

public sealed class ExternalEpisode
{
    public string Name { get; init; } = "";
    public int Season { get; init; }
    public int Number { get; init; }
    public string AirDate { get; init; } = "";
    public string Summary { get; init; } = "";
    public override string ToString() => $"T{Season:00} · E{Number:00}  {Name}";
}

/// <summary>Metadata-only discovery. It never manufactures media or playback URLs.</summary>
public sealed class CatalogMetadataService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly string _omdbApiKey;

    public CatalogMetadataService(string? omdbApiKey = null) =>
        _omdbApiKey = string.IsNullOrWhiteSpace(omdbApiKey)
            ? Environment.GetEnvironmentVariable("OMDB_API_KEY") ?? ""
            : omdbApiKey;

    public async Task<List<ExternalCatalogItem>> SearchMoviesAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_omdbApiKey))
            throw new InvalidOperationException("OMDb necesita una clave API. Configura la variable de entorno OMDB_API_KEY y reinicia la aplicación.");

        var url = $"https://www.omdbapi.com/?apikey={Uri.EscapeDataString(_omdbApiKey)}&s={Uri.EscapeDataString(query.Trim())}&type=movie&page=1";
        using var doc = await GetJsonAsync(url, ct);
        if (ReadString(doc.RootElement, "Response") == "False")
            throw new InvalidOperationException(ReadString(doc.RootElement, "Error") ?? "OMDb no encontró resultados.");

        var results = new List<ExternalCatalogItem>();
        if (doc.RootElement.TryGetProperty("Search", out var search) && search.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in search.EnumerateArray())
            {
                results.Add(new ExternalCatalogItem
                {
                    Title = ReadString(item, "Title") ?? "Sin título",
                    Year = ReadString(item, "Year") ?? "",
                    PosterUrl = ValidImageUrl(ReadString(item, "Poster")),
                    ExternalId = ReadString(item, "imdbID") ?? "",
                    Kind = "Movie"
                });
            }
        }
        return results;
    }

    public async Task<ExternalCatalogItem> GetMovieDetailsAsync(string imdbId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_omdbApiKey))
            throw new InvalidOperationException("Configura OMDB_API_KEY para consultar detalles de películas.");

        var url = $"https://www.omdbapi.com/?apikey={Uri.EscapeDataString(_omdbApiKey)}&i={Uri.EscapeDataString(imdbId)}&plot=full";
        using var doc = await GetJsonAsync(url, ct);
        var root = doc.RootElement;
        if (ReadString(root, "Response") == "False")
            throw new InvalidOperationException(ReadString(root, "Error") ?? "No se pudieron consultar los detalles.");
        return new ExternalCatalogItem
        {
            Title = ReadString(root, "Title") ?? "Sin título",
            Year = ReadString(root, "Year") ?? "",
            Summary = ReadString(root, "Plot") ?? "Sin sinopsis disponible.",
            PosterUrl = ValidImageUrl(ReadString(root, "Poster")),
            ExternalId = imdbId,
            Kind = "Movie",
            Genres = ReadString(root, "Genre") ?? "",
            ExtraInfo = JoinInfo(ReadString(root, "Runtime"), ReadString(root, "Rated"), ReadString(root, "imdbRating"), "IMDb")
        };
    }

    public async Task<List<ExternalCatalogItem>> SearchSeriesAsync(string query, CancellationToken ct = default)
    {
        var url = $"https://api.tvmaze.com/search/shows?q={Uri.EscapeDataString(query.Trim())}";
        using var doc = await GetJsonAsync(url, ct);
        var results = new List<ExternalCatalogItem>();
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return results;

        foreach (var result in doc.RootElement.EnumerateArray())
        {
            if (!result.TryGetProperty("show", out var show)) continue;
            var image = show.TryGetProperty("image", out var imageNode) ?
                ReadString(imageNode, "medium") ?? ReadString(imageNode, "original") : null;
            var genres = show.TryGetProperty("genres", out var genreNode) && genreNode.ValueKind == JsonValueKind.Array
                ? string.Join(", ", genreNode.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)))
                : "";
            results.Add(new ExternalCatalogItem
            {
                Title = ReadString(show, "name") ?? "Sin título",
                Year = (ReadString(show, "premiered") ?? "").Split('-', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "",
                Summary = CleanHtml(ReadString(show, "summary") ?? ""),
                PosterUrl = ValidImageUrl(image),
                ExternalId = ReadString(show, "id") ?? "",
                Kind = "Series",
                Genres = genres,
                ExtraInfo = ReadString(show, "status") ?? ""
            });
        }
        return results;
    }

    public async Task<List<ExternalEpisode>> GetSeriesEpisodesAsync(string tvMazeId, CancellationToken ct = default)
    {
        if (!int.TryParse(tvMazeId, out var id) || id <= 0)
            throw new ArgumentException("El identificador de TVmaze no es válido.", nameof(tvMazeId));
        using var doc = await GetJsonAsync($"https://api.tvmaze.com/shows/{id}/episodes", ct);
        var episodes = new List<ExternalEpisode>();
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return episodes;
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            episodes.Add(new ExternalEpisode
            {
                Name = ReadString(item, "name") ?? "Episodio sin título",
                Season = ReadInt(item, "season"),
                Number = ReadInt(item, "number"),
                AirDate = ReadString(item, "airdate") ?? "",
                Summary = CleanHtml(ReadString(item, "summary") ?? "")
            });
        }
        return episodes.OrderBy(x => x.Season).ThenBy(x => x.Number).ToList();
    }

    private static async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("MagisWindowsNative/1.0");
        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
        value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined) ? value.ToString() : null;

    private static int ReadInt(JsonElement element, string name) =>
        int.TryParse(ReadString(element, name), out var value) ? value : 0;

    private static string ValidImageUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps ? uri.ToString() : "";

    private static string CleanHtml(string value) => Regex.Replace(value, "<[^>]+>", " ").Replace("&nbsp;", " ").Replace("&amp;", "&").Trim();

    private static string JoinInfo(params string?[] values) => string.Join("  ·  ", values.Where(x => !string.IsNullOrWhiteSpace(x)));
}
