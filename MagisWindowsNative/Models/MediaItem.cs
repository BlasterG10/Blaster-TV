using System.Text.Json.Serialization;

namespace MagisWindowsNative.Models;

public sealed class MediaItem
{
    private string _name = "";
    private string _category = "General";
    private string _country = "Sin país identificado";
    // CountryGroup is read on every filter, sort and grouping pass over the whole
    // catalog, so it is computed once and refreshed only when its inputs change.
    private string? _countryGroup;

    public string Name { get => _name; set { _name = value; _countryGroup = null; } }
    public string Url { get; set; } = "";
    public string Category { get => _category; set { _category = value; _countryGroup = null; } }
    public string Country { get => _country; set { _country = value; _countryGroup = null; } }
    public string Source { get; set; } = "Lista local";
    public string SourceId { get; set; } = "";
    public string Logo { get; set; } = "";
    public string EpgId { get; set; } = "";
    [JsonIgnore] public string CurrentProgramTitle { get; set; } = "Guía EPG no cargada";
    [JsonIgnore] public string CurrentProgramTime { get; set; } = "Importa XMLTV para ver horarios";
    [JsonIgnore] public string NextProgramTitle { get; set; } = "";
    [JsonIgnore] public string NextProgramTime { get; set; } = "";
    [JsonIgnore] public bool IsLive => Kind.Equals("Live", StringComparison.OrdinalIgnoreCase);
    [JsonIgnore] public string KindGroup => Kind switch
    {
        "Live" => "TV en vivo",
        "Movie" => "Películas",
        "Series" => "Series",
        "Episode" => "Episodios",
        _ => "Otros"
    };
    [JsonIgnore] public string CountryGroup => _countryGroup ??= ComputeCountryGroup();

    private string ComputeCountryGroup()
    {
        if (!string.IsNullOrWhiteSpace(Country) && Country != "Sin país identificado") return Country;
        var text = $"{Category} {Name}".ToLowerInvariant();
        if (text.Contains("latino") || text.Contains("latam") || text.Contains("latin america")) return "Latinoamérica";
        if (text.Contains("caribe") || text.Contains("caribbean")) return "Caribe";
        if (text.Contains("centroamerica") || text.Contains("centroamérica") || text.Contains("central america")) return "Centroamérica";
        if (text.Contains("sudamerica") || text.Contains("sudamérica") || text.Contains("south america")) return "Sudamérica";
        if (text.Contains("norteamerica") || text.Contains("norteamérica") || text.Contains("north america")) return "Norteamérica";
        if (text.Contains("europa") || text.Contains("europe")) return "Europa";
        if (text.Contains("africa") || text.Contains("áfrica")) return "África";
        if (text.Contains("asia")) return "Asia";
        if (text.Contains("internacional") || text.Contains("international") || text.Contains("worldwide")) return "Internacional";
        return "Región sin identificar";
    }

    [JsonIgnore] public string CategoryGroup => string.IsNullOrWhiteSpace(Category) ? "General" : Category;
    public string Kind { get; set; } = "Live";
    public int? StreamId { get; set; }
    public string ContainerExtension { get; set; } = "";
    [JsonIgnore] public bool IsFavorite { get; set; }
    public override string ToString() => (IsFavorite ? "★  " : "") + Name;
}
