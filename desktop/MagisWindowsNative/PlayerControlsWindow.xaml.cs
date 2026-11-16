using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LibVLCSharp.Shared;

namespace MagisWindowsNative;

public partial class PlayerControlsWindow : Window
{
    private readonly MediaPlayer _player;
    private readonly Action _toggleFullscreen;
    private readonly Action _stopPlayback;
    private readonly Action<int> _changeChannel;
    private readonly Action _toggleGuide;
    private bool _updatingTracks;
    private bool _draggingProgress;
    private sealed record TrackOption(int Id, string Name);

    public PlayerControlsWindow(MediaPlayer player, Action toggleFullscreen, Action stopPlayback, Action<int> changeChannel, Action toggleGuide)
    {
        InitializeComponent();
        _player = player;
        _toggleFullscreen = toggleFullscreen;
        _stopPlayback = stopPlayback;
        _changeChannel = changeChannel;
        _toggleGuide = toggleGuide;
        VolumeSlider.Value = _player.Volume;
    }

    public void SetTitle(string title) => TitleText.Text = title;

    public void SetPlaybackStatus(string message) => PlaybackStatusText.Text = message;

    public void ActivateOverlay()
    {
        if (!IsVisible) Show();
        // Do not steal keyboard focus from the video window.
    }

    public void RefreshTrackOptions()
    {
        _updatingTracks = true;
        try
        {
            AudioTrackBox.Items.Clear();
            var audioTracks = _player.AudioTrackDescription;
            if (audioTracks is not null)
                foreach (var track in audioTracks)
                    AudioTrackBox.Items.Add(new TrackOption(track.Id, string.IsNullOrWhiteSpace(track.Name) ? $"Audio {track.Id}" : track.Name));
            AudioTrackBox.DisplayMemberPath = nameof(TrackOption.Name);
            AudioTrackBox.SelectedItem = AudioTrackBox.Items.Cast<TrackOption>().FirstOrDefault(x => x.Id == _player.AudioTrack);

            SubtitleTrackBox.Items.Clear();
            SubtitleTrackBox.Items.Add(new TrackOption(-1, "Desactivados"));
            var subtitleTracks = _player.SpuDescription;
            if (subtitleTracks is not null)
                foreach (var track in subtitleTracks)
                    SubtitleTrackBox.Items.Add(new TrackOption(track.Id, string.IsNullOrWhiteSpace(track.Name) ? $"Subtítulo {track.Id}" : track.Name));
            SubtitleTrackBox.DisplayMemberPath = nameof(TrackOption.Name);
            SubtitleTrackBox.SelectedItem = SubtitleTrackBox.Items.Cast<TrackOption>().FirstOrDefault(x => x.Id == _player.Spu)
                ?? SubtitleTrackBox.Items[0];
        }
        finally { _updatingTracks = false; }
    }

    public void UpdateProgress()
    {
        if (!IsVisible) return;
        if (!_draggingProgress)
            ProgressSlider.Value = Math.Clamp(_player.Position * 1000d, 0, 1000);
        CurrentTimeText.Text = FormatTime(_player.Time);
        DurationText.Text = FormatTime(_player.Length);
    }

    private static string FormatTime(long ms)
    {
        if (ms < 0) ms = 0;
        var t = TimeSpan.FromMilliseconds(ms);
        return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"mm\:ss");
    }

    private void AudioTrackBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_updatingTracks && AudioTrackBox.SelectedItem is TrackOption track) _player.SetAudioTrack(track.Id);
    }
    private void SubtitleTrackBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_updatingTracks && SubtitleTrackBox.SelectedItem is TrackOption track) _player.SetSpu(track.Id);
    }
    private void ProgressSlider_MouseDown(object sender, MouseButtonEventArgs e) => _draggingProgress = true;
    private void ProgressSlider_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _player.Position = (float)(ProgressSlider.Value / 1000d);
        _draggingProgress = false;
    }
    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_player is not null) _player.Volume = (int)e.NewValue;
    }
    private void SeekBySeconds(long seconds)
    {
        var current = Math.Max(0, _player.Time);
        var duration = _player.Length;
        var target = Math.Max(0, current + seconds * 1000L);
        if (duration > 0) target = Math.Min(duration, target);
        _player.Time = target;
    }

    private void PreviousChannel_Click(object sender, RoutedEventArgs e) => _changeChannel(-1);
    private void NextChannel_Click(object sender, RoutedEventArgs e) => _changeChannel(1);
    private void Back15_Click(object sender, RoutedEventArgs e) => SeekBySeconds(-15);
    private void Forward15_Click(object sender, RoutedEventArgs e) => SeekBySeconds(15);
    private void Pause_Click(object sender, RoutedEventArgs e) => _player.Pause();
    private void Stop_Click(object sender, RoutedEventArgs e) => _stopPlayback();
    private void Fullscreen_Click(object sender, RoutedEventArgs e) => _toggleFullscreen();
    private void Guide_Click(object sender, RoutedEventArgs e) => _toggleGuide();
}
