using System.Windows;
using MagisWindowsNative.Models;

namespace MagisWindowsNative;

public partial class EpgWindow : Window
{
    public EpgWindow(IEnumerable<EpgProgram> programs)
    {
        InitializeComponent();
        var list = programs.OrderBy(x => x.Start).ToList();
        ProgramsGrid.ItemsSource = list;
        Subtitle.Text = $"{list.Count:N0} programas · horarios convertidos a la hora local de Windows";
    }
}
