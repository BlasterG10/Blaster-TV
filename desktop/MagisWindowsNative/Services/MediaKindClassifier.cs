using System.Text.RegularExpressions;

namespace MagisWindowsNative.Services;

/// <summary>
/// M3U lists have no field for "this is a movie / a series episode": everything used to be treated as
/// live TV. This classifier decides from what the entry itself shows, the way Xtream exports and
/// VOD playlists are normally written:
///   • Xtream-style addresses  …/movie/user/pass/123.mp4  and  …/series/user/pass/456.mkv
///   • video files (.mp4, .mkv, .avi …) instead of a live stream (.m3u8, .ts, no extension)
///   • episode markers in the title: "Show S01E02", "Show S1 E2", "Show T1E2", "Show 1x02"
/// Live 24/7 channels (for example the iptv-org "Movies" category, which are .m3u8 streams) stay live TV.
/// </summary>
public static class MediaKindClassifier
{
    public readonly record struct Result(string Kind, string SeriesTitle, int Season, int Episode);

    private static readonly Regex SxxEyy = new(
        @"^(?<t>.*?)[\s._\-–:]*[\[(]?\b[ST]\s*(?<s>\d{1,3})\s*[._\-]?\s*E[Pp]?\.?\s*(?<e>\d{1,4})\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex NxNN = new(
        @"^(?<t>.*?)[\s._\-–:]+(?<s>\d{1,2})x(?<e>\d{2,3})\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly string[] VideoFiles = { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".mpg", ".mpeg" };

    public static Result Classify(string url, string group, string name)
    {
        var path = PathOf(url);
        var xtreamMovie = path.Contains("/movie/", StringComparison.Ordinal);
        var xtreamSeries = path.Contains("/series/", StringComparison.Ordinal);
        var videoFile = HasVideoExtension(path);

        // Cheap checks first: almost every entry is a live channel and must not pay for regex work.
        if (!xtreamMovie && !xtreamSeries && !videoFile && !HasEpisodeMarker(name))
            return new Result("Live", "", 0, 0);

        if (TryEpisode(name, out var title, out var season, out var episode))
            return new Result("Episode", title, season, episode);
        if (xtreamSeries)
            return new Result("Episode", name.Trim(), 1, 0);
        if (xtreamMovie || videoFile)
            return new Result("Movie", "", 0, 0);
        return new Result("Live", "", 0, 0);
    }

    private static bool TryEpisode(string name, out string title, out int season, out int episode)
    {
        title = ""; season = 0; episode = 0;
        if (!HasEpisodeMarker(name)) return false;
        var m = SxxEyy.Match(name);
        if (!m.Success) m = NxNN.Match(name);
        if (!m.Success) return false;
        title = m.Groups["t"].Value.Trim(' ', '-', '–', '.', '_', ':', '[', '(');
        if (title.Length == 0) return false;
        _ = int.TryParse(m.Groups["s"].Value, out season);
        _ = int.TryParse(m.Groups["e"].Value, out episode);
        return true;
    }

    // Every supported marker has a digit right before and after an "x" (1x02) or an "E" (S01E02, S1 E2).
    private static bool HasEpisodeMarker(string name)
    {
        for (var i = 1; i < name.Length - 1; i++)
        {
            var c = name[i];
            if (!char.IsAsciiDigit(name[i + 1])) continue;
            if ((c is 'x' or 'X') && char.IsAsciiDigit(name[i - 1])) return true;
            if ((c is 'e' or 'E') && (char.IsAsciiDigit(name[i - 1]) || name[i - 1] == ' ')) return true;
        }
        return false;
    }

    private static string PathOf(string url)
    {
        var start = url.IndexOf("://", StringComparison.Ordinal);
        start = start < 0 ? 0 : url.IndexOf('/', start + 3);
        if (start < 0) return "";
        var end = url.IndexOfAny(new[] { '?', '#' }, start);
        return (end < 0 ? url[start..] : url[start..end]).ToLowerInvariant();
    }

    private static bool HasVideoExtension(string path)
    {
        foreach (var ext in VideoFiles)
            if (path.EndsWith(ext, StringComparison.Ordinal)) return true;
        return false;
    }
}
