namespace MagisWindowsNative.Models;

/// <summary>Una lista M3U o conexión Xtream configurada por el usuario.
/// El conjunto completo se persiste dentro de CatalogState, cifrado con DPAPI.</summary>
public sealed class PlaylistSource
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Nueva fuente";
    /// <summary>M3UUrl, M3UFile o Xtream.</summary>
    public string Type { get; set; } = "M3UUrl";
    public string Address { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string TypeLabel => Type switch
    {
        "Xtream" => "Xtream Codes",
        "M3UFile" => "Archivo M3U/M3U8",
        "ArchiveOrg" => "Archive.org (dominio público)",
        _ => "URL M3U/M3U8"
    };

    // Hide URL query strings, which can contain provider tokens, in the manager UI.
    public string DisplayAddress
    {
        get
        {
            if (Type == "M3UFile") return Address;
            if (Type == "ArchiveOrg") return $"archive.org — colección {Address}";
            if (!Uri.TryCreate(Address, UriKind.Absolute, out var uri)) return Address;
            var port = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";
            return $"{uri.Scheme}://{uri.Host}{port}{uri.AbsolutePath}";
        }
    }
}
