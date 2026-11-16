using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using LibVLCSharp.Shared;

namespace MagisWindowsNative;

public partial class PlayerWindow : Window
{
    private readonly MediaPlayer _player;
    private readonly Action<int> _changeChannel;
    private readonly Action _toggleGuide;
    private readonly DispatcherTimer _timer;
    private PlayerControlsWindow? _controls;
    private bool _fullscreen;
    private bool _allowClose;
    private bool _controlsShown;
    private readonly DispatcherTimer _cursorTimer;
    private readonly DispatcherTimer _hideControlsTimer;
    private System.Drawing.Point _lastCursorPosition;
    private bool _hasCursorPosition;
    private WindowStyle _previousStyle;
    private ResizeMode _previousResizeMode;
    private WindowState _previousState;

    /// <summary>Raised whenever the floating controls bar appears (true) or hides (false). The channel guide follows it.</summary>
    public event Action<bool>? ControlsVisibilityChanged;

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out System.Drawing.Point point);

    public PlayerWindow(MediaPlayer player, Action<int> changeChannel, Action toggleGuide)
    {
        InitializeComponent();
        _player = player;
        _changeChannel = changeChannel;
        _toggleGuide = toggleGuide;
        VideoSurface.Loaded += VideoSurface_Loaded;
        _player.Playing += Player_Playing;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _timer.Tick += (_, _) => _controls?.UpdateProgress();
        _timer.Start();

        _hideControlsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(7) };
        _hideControlsTimer.Tick += (_, _) => HideControlsIfPlaying();
        _cursorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _cursorTimer.Tick += (_, _) => DetectCursorMovement();
        _cursorTimer.Start();

        Loaded += (_, _) => { EnsureControls(); ShowControls(); };
        PreviewMouseMove += (_, _) => ShowControls();
        PreviewMouseDown += (_, _) => ShowControls();
        LocationChanged += (_, _) => PositionControls();
        SizeChanged += (_, _) => PositionControls();
        StateChanged += (_, _) => PositionControls();
        IsVisibleChanged += (_, e) =>
        {
            if ((bool)e.NewValue) { EnsureControls(); PositionControls(); ShowControls(); }
            else { _hideControlsTimer.Stop(); _controls?.Hide(); SetControlsShown(false); }
        };
        Closing += PlayerWindow_Closing;
    }

    public void SetTitle(string title)
    {
        Title = title;
        // The player window may not have been shown yet. Defer creation of the
        // owned controls window until Loaded/IsVisibleChanged.
        _controls?.SetTitle(title);
    }

    public void SetPlaybackStatus(string message)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => SetPlaybackStatus(message)));
            return;
        }
        if (IsLoaded && IsVisible) EnsureControls();
        _controls?.SetPlaybackStatus(message);
    }

    private void VideoSurface_Loaded(object sender, RoutedEventArgs e)
    {
        VideoSurface.Loaded -= VideoSurface_Loaded;
        VideoSurface.MediaPlayer = _player;
        SetPlaybackStatus("Video listo");
    }

    private void Player_Playing(object? sender, EventArgs e)
        => Dispatcher.BeginInvoke(new Action(() =>
        {
            _controls?.RefreshTrackOptions();
            ShowControls();
        }));

    private void EnsureControls()
    {
        if (_controls is not null) return;
        if (!IsLoaded || !IsVisible) return;
        _controls = new PlayerControlsWindow(_player, ToggleFullscreen, StopPlayback, _changeChannel, _toggleGuide);
        _controls.Owner = this;
        _controls.SetTitle(Title);
        _controls.SetPlaybackStatus("Preparado");
        _controls.Show();
        PositionControls();
    }

    private void PositionControls()
    {
        if (_controls is null || !IsVisible) return;
        try
        {
            const double margin = 20;
            _controls.Width = Math.Max(650, ActualWidth - margin * 2);
            _controls.Height = 146;
            _controls.Left = Left + margin;
            _controls.Top = Top + Math.Max(0, ActualHeight - _controls.Height - margin - (WindowStyle == WindowStyle.None ? 0 : 8));
            if (!_controls.IsVisible) _controls.Show();
        }
        catch { /* Window can be between initialization and first layout pass. */ }
    }

    private void DetectCursorMovement()
    {
        if (!IsVisible || !GetCursorPos(out var point)) return;
        if (!_hasCursorPosition || point != _lastCursorPosition)
        {
            _lastCursorPosition = point;
            _hasCursorPosition = true;
            var screenBounds = new System.Drawing.Rectangle(
                (int)Left, (int)Top, (int)Math.Max(1, ActualWidth), (int)Math.Max(1, ActualHeight));
            if (screenBounds.Contains(point.X, point.Y))
                ShowControls();
        }
    }

    private void ShowControls()
    {
        if (!IsVisible) return;
        EnsureControls();
        if (_controls is null) return;
        PositionControls();
        if (!_controls.IsVisible) _controls.Show();
        _controls.ActivateOverlay();
        _hideControlsTimer.Stop();
        if (_player.IsPlaying) _hideControlsTimer.Start();
        SetControlsShown(true);
    }

    /// <summary>
    /// Called by the guide while the user is working in it (mouse, keys, scrolling) so the bar
    /// and the guide stay on screen together and are hidden together after the idle time.
    /// </summary>
    public void KeepControlsAlive()
    {
        if (_controls is { IsVisible: true })
        {
            _hideControlsTimer.Stop();
            if (_player.IsPlaying) _hideControlsTimer.Start();
        }
        else ShowControls();
    }

    private void HideControlsIfPlaying()
    {
        _hideControlsTimer.Stop();
        if (_player.IsPlaying && _controls is not null)
        {
            _controls.Hide();
            SetControlsShown(false);
        }
    }

    // The guide listens to this so that it appears and disappears at exactly the same moment as the bar.
    private void SetControlsShown(bool shown)
    {
        if (_controlsShown == shown) return;
        _controlsShown = shown;
        ControlsVisibilityChanged?.Invoke(shown);
    }

    private void StopPlayback()
    {
        _hideControlsTimer.Stop();
        _player.Stop();
        _controls?.SetPlaybackStatus("Detenido");
    }

    public void ToggleFullscreen()
    {
        if (!_fullscreen)
        {
            _previousStyle = WindowStyle;
            _previousResizeMode = ResizeMode;
            _previousState = WindowState;
            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Left = 0; Top = 0;
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
            WindowState = WindowState.Maximized;
            _fullscreen = true;
            _controls?.SetPlaybackStatus("Pantalla completa · Esc para salir");
        }
        else
        {
            WindowState = WindowState.Normal;
            WindowStyle = _previousStyle;
            ResizeMode = _previousResizeMode;
            WindowState = _previousState;
            _fullscreen = false;
        }
        PositionControls();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var guideKey = e.Key == Key.G || e.Key == Key.Enter || e.Key == Key.Tab;
        // For the guide key the toggle runs first: if the guide is only hidden together with the
        // bar, the key brings it back instead of closing it.
        if (!guideKey) ShowControls();
        if (e.Key == Key.F11) { ToggleFullscreen(); e.Handled = true; }
        else if (e.Key == Key.Escape && _fullscreen) { ToggleFullscreen(); e.Handled = true; }
        else if (e.Key == Key.Space || e.Key == Key.K)
        {
            _player.Pause();
            ShowControls();
            e.Handled = true;
        }
        else if (guideKey)
        {
            _toggleGuide();
            ShowControls();
            e.Handled = true;
        }
        else if (e.Key == Key.PageUp)
        {
            _changeChannel(-1);
            ShowControls();
            e.Handled = true;
        }
        else if (e.Key == Key.PageDown)
        {
            _changeChannel(1);
            ShowControls();
            e.Handled = true;
        }
    }

    private void PlayerWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            _player.Stop();
            Hide();
            return;
        }
        _timer.Stop();
        _cursorTimer.Stop();
        _hideControlsTimer.Stop();
        _player.Playing -= Player_Playing;
        if (_controls is not null)
        {
            _controls.Close();
            _controls = null;
        }
        VideoSurface.MediaPlayer = null;
        VideoSurface.Dispose();
    }

    public void Shutdown()
    {
        _allowClose = true;
        Close();
    }
}
