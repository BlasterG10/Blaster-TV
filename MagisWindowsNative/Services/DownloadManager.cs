using System.IO;
using System.Net.Http;
using MagisWindowsNative.Models;

namespace MagisWindowsNative.Services;

/// <summary>
/// Descarga una película o un episodio a disco para verlo sin conexión. La TV en vivo no se puede
/// descargar porque no tiene un final. <paramref name="onProgress"/> se llama desde un hilo de
/// segundo plano (igual que el resto de servicios de red de la app): quien llame debe pasar el
/// resultado al hilo de interfaz con Dispatcher si va a tocar un control.
/// </summary>
public static class DownloadManager
{
    public static readonly string Root = ResolveRoot();

    public static async Task<DownloadedItem> DownloadAsync(MediaItem item, Action<double> onProgress, CancellationToken ct = default)
    {
        if (item.Kind != "Movie" && item.Kind != "Episode")
            throw new InvalidOperationException("Solo se pueden descargar películas o episodios.");
        if (string.IsNullOrWhiteSpace(item.Url) || !Uri.TryCreate(item.Url, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("Este elemento no tiene una dirección descargable.");

        Directory.CreateDirectory(Root);
        var extension = Path.GetExtension(uri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(extension) || extension.Length > 5) extension = ".mp4";
        var destination = UniquePath(Path.Combine(Root, SanitizeFileName(item.Name) + extension));
        var tempPath = destination + ".part";

        using var http = new HttpClient(new System.Net.Http.SocketsHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All
        })
        { Timeout = Timeout.InfiniteTimeSpan };

        using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;

        try
        {
            await using (var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            await using (var target = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true))
            {
                var buffer = new byte[1 << 16];
                long readTotal = 0;
                var lastReport = DateTime.UtcNow;
                int read;
                while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    readTotal += read;
                    if (total is > 0 && (DateTime.UtcNow - lastReport).TotalMilliseconds > 250)
                    {
                        lastReport = DateTime.UtcNow;
                        onProgress(Math.Min(99.9, readTotal * 100.0 / total.Value));
                    }
                }
            }
            File.Move(tempPath, destination);
        }
        catch
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch (Exception) { }
            throw;
        }

        onProgress(100);
        return new DownloadedItem
        {
            Name = item.Name,
            OriginalUrl = item.Url,
            LocalPath = destination,
            Kind = item.Kind,
            Category = item.Category,
            Logo = item.Logo,
            Source = item.Source,
            DownloadedAt = DateTime.Now
        };
    }

    public static void Delete(DownloadedItem entry)
    {
        try { if (File.Exists(entry.LocalPath)) File.Delete(entry.LocalPath); } catch (Exception) { }
    }

    private static string ResolveRoot()
    {
        try
        {
            var videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            if (!string.IsNullOrWhiteSpace(videos)) return Path.Combine(videos, "MagisTV");
        }
        catch (Exception) { }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MagisWindowsNative", "Descargas");
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c).ToArray();
        var clean = new string(chars).Trim();
        if (clean.Length == 0) return "Descarga";
        return clean.Length > 120 ? clean[..120] : clean;
    }

    private static string UniquePath(string path)
    {
        if (!File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (var i = 2; ; i++)
        {
            var candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }
    }
}
