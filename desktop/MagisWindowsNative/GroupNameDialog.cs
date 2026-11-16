using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MagisWindowsNative;

public sealed class GroupNameDialog : Window
{
    private readonly TextBox _nameBox;
    public string GroupName => _nameBox.Text;

    public GroupNameDialog()
    {
        Title = "Crear grupo personalizado";
        Width = 390;
        Height = 205;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        if (Application.Current?.TryFindResource("Glass.Window") is Style glass) Style = glass;
        Foreground = Brushes.White;
        WindowStyle = WindowStyle.SingleBorderWindow;

        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock { Text = "Nombre del nuevo grupo", FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) });
        _nameBox = new TextBox { Padding = new Thickness(10), FontSize = 14 }; // colours come from the app-wide TextBox style
        panel.Children.Add(_nameBox);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var cancel = new Button { Content = "Cancelar", Padding = new Thickness(14, 7, 14, 7), Margin = new Thickness(0, 0, 8, 0), IsCancel = true };
        var create = new Button { Content = "Crear grupo", Padding = new Thickness(14, 7, 14, 7), IsDefault = true, Background = new SolidColorBrush(Color.FromRgb(0x92, 0x29, 0xD4)), Foreground = Brushes.White };
        create.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_nameBox.Text))
            {
                MessageBox.Show(this, "Escribe un nombre para el grupo.", "Crear grupo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            DialogResult = true;
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(create);
        panel.Children.Add(buttons);
        Content = panel;
        Loaded += (_, _) => { _nameBox.Focus(); _nameBox.SelectAll(); };
    }
}
