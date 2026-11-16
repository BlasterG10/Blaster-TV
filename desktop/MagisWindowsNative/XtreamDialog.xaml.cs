using System.Windows;
using MagisWindowsNative.Services;

namespace MagisWindowsNative;

public partial class XtreamDialog : Window
{
    public string Server => ServerBox.Text.Trim();
    public string Username => UserBox.Text.Trim();
    public string Password => PassBox.Password;
    public bool Remember => RememberBox.IsChecked == true;

    public XtreamDialog()
    {
        InitializeComponent();
        var saved = CredentialStore.Load();
        if (saved is not null)
        {
            ServerBox.Text = saved.Server;
            UserBox.Text = saved.Username;
            PassBox.Password = saved.Password;
            RememberBox.IsChecked = true;
        }
    }

    private void Connect_Click(object sender, RoutedEventArgs e)
    {
        if (!Uri.TryCreate(Server, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && uri.Scheme != "http") ||
            string.IsNullOrWhiteSpace(Username) || string.IsNullOrEmpty(Password))
        { MessageBox.Show("Completa una URL válida, usuario y contraseña."); return; }

        if (Remember)
            CredentialStore.Save(new SavedCredentials { Server = Server, Username = Username, Password = Password });
        else
            CredentialStore.Delete();

        DialogResult = true;
    }
}
