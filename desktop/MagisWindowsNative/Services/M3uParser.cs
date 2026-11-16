using System.IO;
using System.Text.RegularExpressions;
using MagisWindowsNative.Models;

namespace MagisWindowsNative.Services;

public static class M3uParser
{
    private static readonly Regex Attr = new(
        "(?<key>[A-Za-z0-9_-]+)=\"(?<value>[^\"]*)\"",
        RegexOptions.Compiled);

    public static List<MediaItem> Parse(string text, string sourceName = "Lista M3U", string sourceId = "")
    {
        using var reader = new StringReader(text);
        return Parse(reader, sourceName, sourceId);
    }

    // Reads line by line so a large list never has to exist as one huge string
    // plus a second huge array of lines.
    public static List<MediaItem> Parse(TextReader reader, string sourceName = "Lista M3U", string sourceId = "")
    {
        var result = new List<MediaItem>();
        string name = "Untitled", group = "General", logo = "", country = "", epgId = "";
        Dictionary<string, string>? episodeTitles = null;
        string? raw;
        while ((raw = reader.ReadLine()) is not null)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
            {
                var comma = line.IndexOf(',');
                name = comma >= 0 ? line[(comma + 1)..].Trim() : "Untitled";
                group = "General"; logo = ""; country = ""; epgId = "";
                foreach (Match m in Attr.Matches(line))
                {
                    var key = m.Groups["key"].Value.ToLowerInvariant();
                    var value = m.Groups["value"].Value;
                    if (key == "group-title") group = value;
                    if (key == "tvg-logo") logo = value;
                    if (key == "tvg-id") epgId = value;
                    if (key == "tvg-country" || key == "country") country = value;
                }
            }
            else if (!line.StartsWith('#') && Uri.TryCreate(line, UriKind.Absolute, out var uri)
                     && (uri.Scheme == "http" || uri.Scheme == "https" || uri.Scheme == "rtsp" || uri.Scheme == "rtmp"))
            {
                var url = uri.ToString();
                var kind = MediaKindClassifier.Classify(url, group, name);
                var item = new MediaItem { Name = name, Url = url, Category = group, Country = CountryResolver.Resolve(country, group, name), Source = sourceName, SourceId = sourceId, Logo = logo, EpgId = epgId, Kind = kind.Kind };
                if (kind.Kind == "Episode")
                {
                    item.SeriesKey = kind.SeriesTitle.ToLowerInvariant();
                    item.Season = kind.Season;
                    item.EpisodeNumber = kind.Episode;
                    (episodeTitles ??= new Dictionary<string, string>(StringComparer.Ordinal)).TryAdd(item.SeriesKey, kind.SeriesTitle);
                }
                result.Add(item);
                name = "Untitled";
            }
        }
        if (episodeTitles is not null) AddSeriesEntries(result, episodeTitles, sourceName, sourceId);
        return result;
    }

    // Episodes of the same show become one "Series" entry, which opens the episode list like an Xtream series.
    private static void AddSeriesEntries(List<MediaItem> items, Dictionary<string, string> titles, string sourceName, string sourceId)
    {
        var firstEpisode = new Dictionary<string, MediaItem>(StringComparer.Ordinal);
        foreach (var item in items)
            if (item.Kind == "Episode" && item.SeriesKey.Length > 0) firstEpisode.TryAdd(item.SeriesKey, item);
        foreach (var pair in firstEpisode)
        {
            var episode = pair.Value;
            items.Add(new MediaItem
            {
                Name = titles[pair.Key],
                Url = $"series://{Uri.EscapeDataString(sourceId)}/{Uri.EscapeDataString(pair.Key)}",
                Category = episode.Category,
                Country = episode.Country,
                Source = sourceName,
                SourceId = sourceId,
                Logo = episode.Logo,
                Kind = "Series",
                SeriesKey = pair.Key
            });
        }
    }
}
