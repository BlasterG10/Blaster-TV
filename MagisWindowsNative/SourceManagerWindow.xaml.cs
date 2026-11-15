using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using MagisWindowsNative.Models;

namespace MagisWindowsNative;

public partial class SourceManagerWindow : Window
{
    public ObservableCollection<PlaylistSource> Sources { get; }
    public bool HasChanges { get; private set; }

    public SourceManagerWindow(IEnumerable<PlaylistSource> sources)
    {
        InitializeComponent();
        Sources = new ObservableCollection<PlaylistSource>(sources.Select(Clone));
        SourceList.ItemsSource = Sources;
        UpdateCount();
    }

    private static PlaylistSource Clone(PlaylistSource source) => new()
    {
        Id = source.Id, Name = source.Name, Type = source.Type,
        Address = source.Address, Username = source.Username,
        Password = source.Password, Enabled = source.Enabled
    };

    private void AddM3uUrl_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new UrlDialog { Owner = this };
        if (dialog.ShowDialog() != true) return;
        if (Sources.Any(x => x.Type == "M3UUrl" && x.Address.Equals(dialog.Address, StringComparison.OrdinalIgnoreCase)))
        { MessageBox.Show("Esa URL ya está registrada.", "Fuentes", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var host = Uri.TryCreate(dialog.Address, UriKind.Absolute, out var uri) ? uri.Host : "Lista remota";
        var listName = Uri.TryCreate(dialog.Address, UriKind.Absolute, out var listUri)
            ? Path.GetFileName(listUri.AbsolutePath.TrimEnd('/')) : "";
        Add(new PlaylistSource { Name = string.IsNullOrWhiteSpace(listName) ? host : $"{host} · {listName}", Type = "M3UUrl", Address = dialog.Address });
    }

    private void AddM3uFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Listas M3U (*.m3u;*.m3u8)|*.m3u;*.m3u8|Todos (*.*)|*.*" };
        if (dialog.ShowDialog() != true) return;
        if (Sources.Any(x => x.Type == "M3UFile" && x.Address.Equals(dialog.FileName, StringComparison.OrdinalIgnoreCase)))
        { MessageBox.Show("Ese archivo ya está registrado.", "Fuentes", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        Add(new PlaylistSource { Name = Path.GetFileNameWithoutExtension(dialog.FileName), Type = "M3UFile", Address = dialog.FileName });
    }

    private void AddXtream_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new XtreamDialog { Owner = this };
        if (dialog.ShowDialog() != true) return;
        var host = Uri.TryCreate(dialog.Server, UriKind.Absolute, out var uri) ? uri.Host : "Servidor Xtream";
        if (Sources.Any(x => x.Type == "Xtream" && x.Address.Equals(dialog.Server.TrimEnd('/'), StringComparison.OrdinalIgnoreCase) && x.Username.Equals(dialog.Username, StringComparison.OrdinalIgnoreCase)))
        { MessageBox.Show("Esa conexión Xtream ya está registrada.", "Fuentes", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        Add(new PlaylistSource
        {
            Name = $"Xtream · {host} · {dialog.Username}", Type = "Xtream",
            Address = dialog.Server.TrimEnd('/'), Username = dialog.Username,
            Password = dialog.Password
        });
    }

    private void Add(PlaylistSource source)
    {
        Sources.Add(source);
        SourceList.SelectedItem = source;
        HasChanges = true;
        UpdateCount();
    }

    private void Toggle_Click(object sender, RoutedEventArgs e)
    {
        if (SourceList.SelectedItem is not PlaylistSource source) return;
        source.Enabled = !source.Enabled;
        SourceList.Items.Refresh();
        HasChanges = true;
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (SourceList.SelectedItem is not PlaylistSource source) return;
        if (MessageBox.Show($"¿Eliminar la fuente '{source.Name}'?", "Eliminar fuente", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        Sources.Remove(source);
        HasChanges = true;
        UpdateCount();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = HasChanges;
    }

    private void UpdateCount() => CountText.Text = $"{Sources.Count} fuentes registradas · {Sources.Count(x => x.Enabled)} activas";
}
