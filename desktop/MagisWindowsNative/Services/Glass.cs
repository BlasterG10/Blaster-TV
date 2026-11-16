using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace MagisWindowsNative.Services;

public enum GlassBackdrop
{
    /// <summary>No system backdrop; the window keeps its own background.</summary>
    None,
    /// <summary>Soft, tinted wallpaper material (Windows 11). Good for large windows.</summary>
    Mica,
    /// <summary>Blur of whatever is behind the window (Windows 10 1803+ / Windows 11). Good for floating panels.</summary>
    Acrylic
}

/// <summary>
/// Gives a window the Windows "glass" look (Mica / acrylic blur) with rounded corners and a dark
/// title bar. Use it from XAML:  <c>svc:Glass.Backdrop="Acrylic"</c>  (the window's own Background
/// must be Transparent and its content should paint a semi-transparent tint of the app colours).
/// Everything is best-effort: on systems without the effect the window simply shows its tint.
/// </summary>
public static class Glass
{
    public static readonly DependencyProperty BackdropProperty = DependencyProperty.RegisterAttached(
        "Backdrop", typeof(GlassBackdrop), typeof(Glass),
        new PropertyMetadata(GlassBackdrop.None, OnBackdropChanged));

    public static GlassBackdrop GetBackdrop(DependencyObject element) => (GlassBackdrop)element.GetValue(BackdropProperty);
    public static void SetBackdrop(DependencyObject element, GlassBackdrop value) => element.SetValue(BackdropProperty, value);

    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmWindowCornerPreference = 33;
    private const int DwmSystemBackdropType = 38;
    private const int CornerRound = 2;
    private const int BackdropMica = 2;
    private const int BackdropAcrylic = 3;
    private const int Windows11Build22H2 = 22621;

    private static void OnBackdropChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Window window || (GlassBackdrop)e.NewValue == GlassBackdrop.None) return;
        if (new WindowInteropHelper(window).Handle != IntPtr.Zero)
        {
            Apply(window);
            return;
        }
        window.SourceInitialized -= OnSourceInitialized;
        window.SourceInitialized += OnSourceInitialized;
    }

    private static void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (sender is not Window window) return;
        window.SourceInitialized -= OnSourceInitialized;
        Apply(window);
    }

    // Brushes that are translucent in Themes/Glass.xaml. When the system cannot blur (older Windows,
    // "Transparency effects" turned off) each one is replaced, for that window only, by its opaque
    // "<key>.Solid" twin, so the window looks exactly like it did before and never ends up see-through.
    private static readonly string[] TranslucentBrushes =
    {
        "Glass.Main", "Glass.Dialog", "Glass.Deep", "Glass.Rail", "Glass.Well", "Glass.Panel", "Glass.Bar",
        "Glass.Field", "Glass.Card", "Glass.Header"
    };

    private static void Apply(Window window)
    {
        var kind = GetBackdrop(window);
        if (kind == GlassBackdrop.None) return;

        var applied = false;
        try
        {
            applied = TryApply(window, kind);
        }
        catch (Exception)
        {
            // Purely decorative: never let a missing OS feature break a window.
        }
        if (!applied) UseSolidBrushes(window);
    }

    private static bool TryApply(Window window, GlassBackdrop kind)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763)) return false;
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return false;

        // Dark caption and rounded corners are harmless extras that do not need the blur itself.
        SetAttribute(hwnd, DwmUseImmersiveDarkMode, 1);
        SetAttribute(hwnd, DwmWindowCornerPreference, CornerRound);
        if (!TransparencyEnabled()) return false;

        var target = HwndSource.FromHwnd(hwnd)?.CompositionTarget;
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, Windows11Build22H2))
        {
            var type = kind == GlassBackdrop.Acrylic ? BackdropAcrylic : BackdropMica;
            if (SetAttribute(hwnd, DwmSystemBackdropType, type) != 0) return false;
            // WPF must render with an alpha channel and the frame must be glass so the backdrop shows through.
            if (target is not null) target.BackgroundColor = Colors.Transparent;
            var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            DwmExtendFrameIntoClientArea(hwnd, ref margins);
            return true;
        }

        // Windows 10 / early Windows 11: only floating panels get the (undocumented) acrylic accent.
        if (kind != GlassBackdrop.Acrylic) return false;
        if (target is not null) target.BackgroundColor = Colors.Transparent;
        if (EnableAcrylicAccent(hwnd)) return true;
        if (target is not null) target.BackgroundColor = Colors.Black;
        return false;
    }

    private static void UseSolidBrushes(Window window)
    {
        var app = Application.Current;
        if (app is null) return;
        foreach (var key in TranslucentBrushes)
            if (app.TryFindResource(key + ".Solid") is Brush solid)
                window.Resources[key] = solid;
    }

    // Settings > Personalization > Colors > Transparency effects.
    private static bool TransparencyEnabled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("EnableTransparency") is not int value || value != 0;
        }
        catch (Exception)
        {
            return true;
        }
    }

    private static int SetAttribute(IntPtr hwnd, int attribute, int value) =>
        DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int));

    private static bool EnableAcrylicAccent(IntPtr hwnd)
    {
        var accent = new AccentPolicy
        {
            AccentState = 4,                                   // ACCENT_ENABLE_ACRYLICBLURBEHIND
            AccentFlags = 2,
            GradientColor = unchecked((int)0x40220D17)         // ABGR tint, same purple as the app (#170D22)
        };
        var size = Marshal.SizeOf(accent);
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(accent, buffer, false);
            var data = new WindowCompositionAttributeData { Attribute = 19, Data = buffer, SizeOfData = size }; // WCA_ACCENT_POLICY
            return SetWindowCompositionAttribute(hwnd, ref data) != 0;
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins { public int Left, Right, Top, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy { public int AccentState, AccentFlags, GradientColor, AnimationId; }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData { public int Attribute; public IntPtr Data; public int SizeOfData; }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);
}
