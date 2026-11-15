using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace MagisWindowsNative.Services;

/// <summary>
/// Turns a provider logo/poster URL into a small bitmap. The cards only show a 66×88
/// thumbnail, so the image is decoded at that size instead of full resolution
/// (posters are often 1000+ px), and empty or invalid addresses return null instead
/// of throwing a conversion exception for every row that is scrolled into view.
/// Visually identical to the previous direct binding.
/// </summary>
public sealed class ThumbnailConverter : IValueConverter
{
    public static readonly ThumbnailConverter Instance = new();

    private const int DecodeWidth = 132;
    private const int MaxCached = 400;
    private static readonly Dictionary<string, BitmapImage> Cache = new(StringComparer.Ordinal);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string address || address.Length == 0) return null;
        if (Cache.TryGetValue(address, out var cached)) return cached;
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeFile))
            return null;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = uri;
            bitmap.DecodePixelWidth = DecodeWidth;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.EndInit();
            if (Cache.Count >= MaxCached) Cache.Clear();
            Cache[address] = bitmap;
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
