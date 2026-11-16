using System.Net.Http;
using System.Text.Json;
using MagisWindowsNative.Models;

namespace MagisWindowsNative.Services;

/// <summary>
/// Catálogo de películas de dominio público de Internet Archive (archive.org), consultado en vivo
/// con su API pública de búsqueda y metadatos — sin scraping ni credenciales.
/// Solo se piden ítems de la colección "feature_films" marcados explícitamente como dominio público
/// (licenseurl que contiene "publicdomain"), nunca de colecciones genéricas de TV (que mezclan
/// grabaciones subidas sin licencia clara). Referencia: https://archive.org/advancedsearch.php
/// y https://archive.org/metadata/{identifier}.
/// </summary>
public static class ArchiveOrgCatalog
{
    public const string SourceType = "ArchiveOrg";
    private const int MaxTitles = 60;        // mantiene acotadas las peticiones extra del primer arranque
    private const int MetadataConcurrency = 5;
    private static readonly string[] PlayableExtensions = { ".mp4", ".m4v", ".ogv", ".ogg", ".webm" };

    /// <summary>Añade la fuente una sola vez. Devuelve true si el estado cambió (hay que guardar).</summary>
    public static bool SeedOnce(CatalogState state)
    {
        if (state.ArchiveMoviesSeeded) return false;
        state.ArchiveMoviesSeeded = true;
        if (state.Sources.Any(x => x.Type == SourceType)) return true;
        state.Sources.Add(new PlaylistSource
        {
            Id = "archivepd1",
            Name = "Dominio público · Archive.org (películas)",
            Type = SourceType,
            Address = "feature_films",
            Enabled = true
        });
        return true;
    }

    public static async Task<List<MediaItem>> LoadAsync(HttpClient http, string sourceName, string sourceId)
    {
        var q = "collection:(feature_films) AND mediatype:(movies) AND licenseurl:(*publicdomain*)";
        var url = $"https://archive.org/advancedsearch.php?q={Uri.EscapeDataString(q)}&fl[]=identifier,title,year&rows={MaxTitles}&output=json";
        using var response = await http.GetAsync(url).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
        if (!doc.RootElement.TryGetProperty("response", out var resp) || !resp.TryGetProperty("docs", out var docs))
            return new List<MediaItem>();

        var entries = new List<(string Id, string Title, string Year)>();
        foreach (var d in docs.EnumerateArray())
        {
            var id = d.TryGetProperty("identifier", out var idEl) ? idEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(id)) continue;
            var title = d.TryGetProperty("title", out var tEl) ? tEl.GetString() ?? id : id;
            var year = d.TryGetProperty("year", out var yEl) ? yEl.ToString() : "";
            entries.Add((id!, title!, year));
        }

        var items = new List<MediaItem>();
        var gate = new SemaphoreSlim(MetadataConcurrency);
        await Task.WhenAll(entries.Select(async entry =>
        {
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var file = await FindPlayableFileAsync(http, entry.Id).ConfigureAwait(false);
                if (file is null) return;
                var item = new MediaItem
                {
                    Name = string.IsNullOrWhiteSpace(entry.Year) ? entry.Title : $"{entry.Title} ({entry.Year})",
                    Url = $"https://archive.org/download/{entry.Id}/{Uri.EscapeDataString(file)}",
                    Category = "Dominio público",
                    Country = "Internacional",
                    Source = sourceName,
                    SourceId = sourceId,
                    Logo = $"https://archive.org/services/img/{entry.Id}",
                    Kind = "Movie"
                };
                lock (items) items.Add(item);
            }
            catch (Exception)
            {
                // Un ítem con metadatos rotos no debe tumbar todo el catálogo.
            }
            finally { gate.Release(); }
        })).ConfigureAwait(false);
        return items;
    }

    // Cada ítem de archive.org suele traer, además de la película, miniaturas, vistas previas y
    // subtítulos con la misma extensión; el archivo de mayor tamaño es el que de verdad reproduce.
    private static async Task<string?> FindPlayableFileAsync(HttpClient http, string identifier)
    {
        using var response = await http.GetAsync($"https://archive.org/metadata/{identifier}").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;
        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
        if (!doc.RootElement.TryGetProperty("files", out var files)) return null;

        string? best = null;
        long bestSize = 0;
        foreach (var f in files.EnumerateArray())
        {
            var name = f.TryGetProperty("name", out var n) ? n.GetString() : null;
            if (name is null) continue;
            var matches = false;
            foreach (var ext in PlayableExtensions)
                if (name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) { matches = true; break; }
            if (!matches) continue;

            long size = 0;
            if (f.TryGetProperty("size", out var s))
            {
                if (s.ValueKind == JsonValueKind.String) long.TryParse(s.GetString(), out size);
                else if (s.ValueKind == JsonValueKind.Number) s.TryGetInt64(out size);
            }
            if (size > bestSize) { bestSize = size; best = name; }
        }
        return best;
    }
}
