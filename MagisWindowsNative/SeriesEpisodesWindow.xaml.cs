using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MagisWindowsNative.Models;

namespace MagisWindowsNative;

public partial class SeriesEpisodesWindow : Window
{
    public SeriesEpisode? SelectedEpisode { get; private set; }

    public SeriesEpisodesWindow(string seriesName, IEnumerable<SeriesEpisode> episodes)
    {
        InitializeComponent();
        SeriesTitle.Text = seriesName;
        var list = episodes.ToList();
        EpisodeList.ItemsSource = list;
        EpisodeCount.Text = $"{list.Count} episodios disponibles";
    }

    private void EpisodeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EpisodeList.SelectedItem is SeriesEpisode episode)
            EpisodeInfo.Text = $"Temporada {episode.Season} · Episodio {episode.EpisodeNumber} · {episode.Duration}";
    }

    private void EpisodeList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => SelectAndClose();

    private void Play_Click(object sender, RoutedEventArgs e) => SelectAndClose();

    private void SelectAndClose()
    {
        if (EpisodeList.SelectedItem is not SeriesEpisode episode)
        {
            MessageBox.Show("Selecciona un episodio.");
            return;
        }
        SelectedEpisode = episode;
        DialogResult = true;
    }
}
