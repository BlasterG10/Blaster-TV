using System.Windows;
namespace MagisWindowsNative;

public partial class UrlDialog : Window
{
    public string Address => UrlBox.Text.Trim();
    public UrlDialog() { InitializeComponent(); }
    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (!Uri.TryCreate(Address, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && uri.Scheme != "http"))
        { MessageBox.Show("Introduce una URL HTTP o HTTPS válida."); return; }
        DialogResult = true;
    }
}
