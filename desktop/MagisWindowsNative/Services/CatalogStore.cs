using System.IO;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using MagisWindowsNative.Models;

namespace MagisWindowsNative.Services;

public sealed class CatalogState
{
    public HashSet<string> Favorites { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<HistoryEntry> History { get; set; } = new();
    public Dictionary<string, string> CustomGroups { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<PlaylistSource> Sources { get; set; } = new();
    /// <summary>The bundled public lists were added to Sources once (see PublicPlaylists).</summary>
    public bool PublicPlaylistsSeeded { get; set; }
    /// <summary>Open movie/series sources were added during the catalog-feed upgrade.</summary>
    public bool OpenVideoSourcesSeeded { get; set; }
    /// <summary>Overlapping bundled presets were disabled once to reduce startup downloads.</summary>
    public bool BundledSourcesOptimized { get; set; }
}
public sealed class HistoryEntry
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public string Kind { get; set; } = "";
    public DateTime PlayedAt { get; set; }
}

public static class CatalogStore
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MagisWindowsNative");
    private static readonly string FilePath = Path.Combine(Folder, "catalog.json");

    private static readonly object WriteLock = new();
    private static byte[]? _pending;
    private static bool _writing;

    public static CatalogState Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new CatalogState();
            var encrypted = File.ReadAllBytes(FilePath);
            var plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            try
            {
                return JsonSerializer.Deserialize<CatalogState>(Encoding.UTF8.GetString(plain)) ?? new CatalogState();
            }
            finally { CryptographicOperations.ZeroMemory(plain); }
        }
        catch
        {
            // One-time migration: older builds stored catalog.json as plain JSON.
            // Convert it to DPAPI immediately so Xtream URLs are no longer exposed.
            try
            {
                var legacy = JsonSerializer.Deserialize<CatalogState>(File.ReadAllText(FilePath));
                if (legacy is not null) { Save(legacy); return legacy; }
            }
            catch { }
            return new CatalogState();
        }
    }

    // The state is serialized on the caller's thread (so it is a consistent snapshot),
    // while encryption and disk I/O happen on a worker. Rapid successive saves are
    // coalesced: only the newest snapshot is written.
    public static void Save(CatalogState state)
    {
        var plain = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(state));
        lock (WriteLock)
        {
            if (_pending is not null) CryptographicOperations.ZeroMemory(_pending);
            _pending = plain;
            if (_writing) return;
            _writing = true;
        }
        Task.Run(WriteLoop);
    }

    private static void WriteLoop()
    {
        while (true)
        {
            byte[]? plain;
            lock (WriteLock)
            {
                plain = _pending;
                _pending = null;
                if (plain is null) { _writing = false; return; }
            }

            try
            {
                Directory.CreateDirectory(Folder);
                var encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
                var temp = FilePath + ".tmp";
                File.WriteAllBytes(temp, encrypted);
                File.Move(temp, FilePath, true);
            }
            catch { /* A failed save must never crash playback; the next save retries. */ }
            finally { CryptographicOperations.ZeroMemory(plain); }
        }
    }

    /// <summary>Waits (briefly) for pending writes; call when the app is closing.</summary>
    public static void Flush()
    {
        for (var i = 0; i < 150; i++)
        {
            lock (WriteLock)
            {
                if (!_writing && _pending is null) return;
            }
            Thread.Sleep(20);
        }
    }

    public static string Key(MediaItem item) => $"{item.Kind}|{item.Url}";
}
