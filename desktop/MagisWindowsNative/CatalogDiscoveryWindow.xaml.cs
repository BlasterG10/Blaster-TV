using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using MagisWindowsNative.Services;

namespace MagisWindowsNative;

public partial class CatalogDiscoveryWindow : Window
{
    private readonly CatalogMetadataService _catalog = new();
    private bool _updatingSelection;

    public CatalogDiscoveryWindow()
    {
        InitializeComponent();
    }

    private async void MovieSearch_Click(object sender, RoutedEventArgs e) => await SearchMoviesAsync();
    private async void SeriesSearch_Click(object sender, RoutedEventArgs e) => await SearchSeriesAsync();
    private async void MovieSearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { e.Handled = true; await SearchMoviesAsync(); }
    }
    private async void SeriesSearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { e.Handled = true; await SearchSeriesAsync(); }
    }

    private async Task SearchMoviesAsync()
    {
        var query = MovieSearchBox.Text.Trim();
        if (query.Length < 2) { CatalogStatus.Text = "Escribe al menos dos caracteres para buscar películas."; return; }
        try
        {
            SetBusy("Buscando películas en OMDb…");
            MovieResults.ItemsSource = await _catalog.SearchMoviesAsync(query);
            CatalogStatus.Text = $"OMDb: {MovieResults.Items.Count} resultados.";
            if (MovieResults.Items.Count > 0) MovieResults.SelectedIndex = 0;
        }
        catch (Exception ex) { CatalogStatus.Text = ex.Message; }
    }

    private async Task SearchSeriesAsync()
    {
        var query = SeriesSearchBox.Text.Trim();
        if (query.Length < 2) { CatalogStatus.Text = "Escribe al menos dos caracteres para buscar series."; return; }
        try
        {
            SetBusy("Buscando series en TVmaze…");
            SeriesResults.ItemsSource = await _catalog.SearchSeriesAsync(query);
            CatalogStatus.Text = $"TVmaze: {SeriesResults.Items.Count} resultados.";
            if (SeriesResults.Items.Count > 0) SeriesResults.SelectedIndex = 0;
        }
        catch (Exception ex) { CatalogStatus.Text = ex.Message; }
    }

    private async void MovieResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingSelection || MovieResults.SelectedItem is not ExternalCatalogItem item) return;
        try
        {
            SetBusy("Consultando ficha completa en OMDb…");
            var details = await _catalog.GetMovieDetailsAsync(item.ExternalId);
            MovieTitle.Text = details.Title;
            MovieMeta.Text = string.Join("  ·  ", new[] { details.Year, details.Genres, details.ExtraInfo }.Where(x => !string.IsNullOrWhiteSpace(x)));
            MovieSummary.Text = details.Summary;
            SetImage(MoviePoster, details.PosterUrl);
            CatalogStatus.Text = "Ficha de película cargada desde OMDb. No se ha creado ninguna URL de reproducción.";
        }
        catch (Exception ex) { CatalogStatus.Text = ex.Message; }
    }

    private async void SeriesResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingSelection || SeriesResults.SelectedItem is not ExternalCatalogItem item) return;
        try
        {
            _updatingSelection = true;
            SeriesTitle.Text = item.Title;
            SeriesMeta.Text = string.Join("  ·  ", new[] { item.Year, item.Genres, item.ExtraInfo }.Where(x => !string.IsNullOrWhiteSpace(x)));
            SeriesSummary.Text = string.IsNullOrWhiteSpace(item.Summary) ? "Sin sinopsis disponible." : item.Summary;
            SetImage(SeriesPoster, item.PosterUrl);
            SeriesEpisodes.ItemsSource = null;
            _updatingSelection = false;

            SetBusy("Cargando episodios desde TVmaze…");
            var episodes = await _catalog.GetSeriesEpisodesAsync(item.ExternalId);
            SeriesEpisodes.ItemsSource = episodes;
            CatalogStatus.Text = $"TVmaze: {episodes.Count} episodios informativos cargados.";
        }
        catch (Exception ex) { _updatingSelection = false; CatalogStatus.Text = ex.Message; }
    }

    private static void SetImage(System.Windows.Controls.Image image, string url)
    {
        image.Source = null;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;
        try
        {
            image.Source = new BitmapImage(uri);
        }
        catch { image.Source = null; }
    }

    private void SetBusy(string message)
    {
        CatalogStatus.Text = message;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
