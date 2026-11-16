using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using MagisWindowsNative.Models;

namespace MagisWindowsNative.Services;

public sealed record CachedList(List<MediaItem> Items, DateTime SavedUtc);

/// <summary>
/// Local copy of each source's last downloaded catalog, so the app can show content
/// immediately at startup instead of downloading everything again. Stream URLs can
/// contain Xtream credentials, so every file is compressed and then protected with
/// DPAPI (current Windows user), the same protection used for the catalog state.
/// </summary>
public static class SourceCache
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MagisWindowsNative", "cache");
    private static readonly object WriteLock = new();

    public static CachedList? TryLoad(string sourceId, string kind)
    {
        try
        {
            var path = PathFor(sourceId, kind);
            if (!File.Exists(path)) return null;
            var savedUtc = File.GetLastWriteTimeUtc(path);
            var encrypted = File.ReadAllBytes(path);
            var compressed = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            try
            {
                using var input = new MemoryStream(compressed, writable: false);
                using var gzip = new GZipStream(input, CompressionMode.Decompress);
                var items = JsonSerializer.Deserialize<List<MediaItem>>(gzip);
                return items is null ? null : new CachedList(items, savedUtc);
            }
            finally { CryptographicOperations.ZeroMemory(compressed); }
        }
        catch
        {
            // Corrupt, from another Windows profile, or from an incompatible build: ignore it.
            return null;
        }
    }

    /// <summary>Writes the cache on a worker thread. The caller passes a private snapshot list.</summary>
    public static Task SaveAsync(string sourceId, string kind, List<MediaItem> snapshot)
    {
        return Task.Run(() =>
        {
            try
            {
                using var buffer = new MemoryStream();
                using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
                    JsonSerializer.Serialize(gzip, snapshot);
                var compressed = buffer.ToArray();
                try
                {
                    var encrypted = ProtectedData.Protect(compressed, null, DataProtectionScope.CurrentUser);
                    lock (WriteLock)
                    {
                        Directory.CreateDirectory(Folder);
                        var path = PathFor(sourceId, kind);
                        var temp = path + ".tmp";
                        File.WriteAllBytes(temp, encrypted);
                        File.Move(temp, path, true);
                    }
                }
                finally { CryptographicOperations.ZeroMemory(compressed); }
            }
            catch { /* The cache is only an accelerator; failing to write it is harmless. */ }
        });
    }

    public static void Delete(string sourceId)
    {
        try
        {
            if (!Directory.Exists(Folder)) return;
            foreach (var file in Directory.GetFiles(Folder, Safe(sourceId) + "_*.bin"))
                File.Delete(file);
        }
        catch { }
    }

    private static string PathFor(string sourceId, string kind) => Path.Combine(Folder, $"{Safe(sourceId)}_{Safe(kind)}.bin");

    private static string Safe(string value)
    {
        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
            if (!char.IsAsciiLetterOrDigit(chars[i])) chars[i] = '_';
        return new string(chars);
    }
}
