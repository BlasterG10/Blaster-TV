using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using MagisWindowsNative.Models;

namespace MagisWindowsNative.Services;

/// <summary>
/// Public M3U lists bundled with the app (Assets/public-playlists.json). Default feeds are
/// kept lean; overlapping presets remain manageable and can be enabled when needed.
/// </summary>
public static class PublicPlaylists
{
    public const string IdPrefix = "pub";

    private static readonly Dictionary<string, string> SectionLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en-vivo"] = "",
        ["peliculas"] = "Películas · ",
        ["series"] = "Series · ",
        ["cine-abierto"] = "Contenido abierto \u00b7 "
    };

    private static readonly HashSet<string> FastDefaultUrls = new(StringComparer.OrdinalIgnoreCase)
    {
        "https://iptv-org.github.io/iptv/index.m3u",
        "https://raw.githubusercontent.com/Free-TV/IPTV/master/playlist.m3u8"
    };

    private static readonly HashSet<string> OpenVideoUrls = new(StringComparer.OrdinalIgnoreCase)
    {
        "https://archivewatch.org/feeds/archivewatch.m3u",
        "https://raw.githubusercontent.com/onlinem3u/publicdomainm3u/main/movies.m3u",
        "https://raw.githubusercontent.com/onlinem3u/publicdomainm3u/main/series.m3u"
    };

    /// <summary>True for sources created from the bundled list (their ids start with "pub").</summary>
    public static bool IsPublic(PlaylistSource source) => source.Id.StartsWith(IdPrefix, StringComparison.Ordinal);

    /// <summary>Adds the presets once and enables only the two general live feeds by default.</summary>
    public static bool SeedOnce(CatalogState state)
    {
        if (state.PublicPlaylistsSeeded) return false;
        state.PublicPlaylistsSeeded = true; // even if the file is unreadable, do not retry on every launch
        try
        {
            var known = new HashSet<string>(state.Sources.Select(x => x.Address), StringComparer.OrdinalIgnoreCase);
            foreach (var entry in Read().Where(x => !x.SectionId.Equals("cine-abierto", StringComparison.OrdinalIgnoreCase)))
            {
                if (!known.Add(entry.Url)) continue;
                state.Sources.Add(new PlaylistSource
                {
                    Id = IdPrefix + StableId(entry.Url),
                    Name = "Público · " + entry.Label,
                    Type = "M3UUrl",
                    Address = entry.Url,
                    Enabled = FastDefaultUrls.Contains(entry.Url)
                });
            }
            return true; // the "already seeded" flag changed even if every list was already present
        }
        catch (Exception)
        {
            return true;
        }
    }

    /// <summary>Adds open movie and series feeds once, including on upgrades from older builds.</summary>
    public static bool SeedOpenVideoSourcesOnce(CatalogState state)
    {
        if (state.OpenVideoSourcesSeeded) return false;
        state.OpenVideoSourcesSeeded = true;
        try
        {
            var known = new HashSet<string>(state.Sources.Select(x => x.Address), StringComparer.OrdinalIgnoreCase);
            foreach (var entry in Read().Where(x => x.SectionId.Equals("cine-abierto", StringComparison.OrdinalIgnoreCase)))
            {
                if (!known.Add(entry.Url)) continue;
                state.Sources.Add(new PlaylistSource
                {
                    Id = IdPrefix + StableId(entry.Url),
                    Name = entry.Label,
                    Type = "M3UUrl",
                    Address = entry.Url,
                    Enabled = true
                });
            }
            return true;
        }
        catch (Exception)
        {
            return true;
        }
    }

    /// <summary>Disables redundant bundled presets once while keeping them available to re-enable.</summary>
    public static bool OptimizeBundledSourcesOnce(CatalogState state)
    {
        if (state.BundledSourcesOptimized) return false;
        state.BundledSourcesOptimized = true;
        var keep = new HashSet<string>(FastDefaultUrls, StringComparer.OrdinalIgnoreCase);
        keep.UnionWith(OpenVideoUrls);
        foreach (var source in state.Sources)
            if (IsPublic(source) && source.Enabled && !keep.Contains(source.Address))
                source.Enabled = false;
        return true;
    }

    private sealed record Entry(string SectionId, string Label, string Url);

    private static List<Entry> Read()
    {
        var result = new List<Entry>();
        var info = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/public-playlists.json", UriKind.Absolute));
        if (info is null) return result;
        using var stream = info.Stream;
        using var doc = JsonDocument.Parse(stream);
        if (!doc.RootElement.TryGetProperty("sections", out var sections)) return result;
        foreach (var section in sections.EnumerateArray())
        {
            var id = section.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? "" : "";
            SectionLabels.TryGetValue(id, out var prefix);
            if (!section.TryGetProperty("playlists", out var lists)) continue;
            foreach (var list in lists.EnumerateArray())
            {
                var url = list.TryGetProperty("url", out var u) ? u.GetString() : null;
                var name = list.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(name)) continue;
                result.Add(new Entry(id, (prefix ?? "") + name, url));
            }
        }
        return result;
    }

    // A fixed id per address keeps the saved copy (SourceCache) valid between launches.
    private static string StableId(string url)
    {
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(url.ToLowerInvariant()));
        return Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
    }
}
