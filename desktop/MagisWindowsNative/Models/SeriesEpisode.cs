namespace MagisWindowsNative.Models;

public sealed class SeriesEpisode
{
    public string Title { get; set; } = "";
    public string Season { get; set; } = "";
    public string EpisodeNumber { get; set; } = "";
    public string Url { get; set; } = "";
    public string Plot { get; set; } = "";
    public string Duration { get; set; } = "";
    public override string ToString() => $"T{Season} · E{EpisodeNumber}  {Title}";
}
