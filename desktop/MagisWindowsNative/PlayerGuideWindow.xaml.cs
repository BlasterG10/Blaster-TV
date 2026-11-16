using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using MagisWindowsNative.Models;

namespace MagisWindowsNative;

/// <summary>One row of the in-player guide. Wraps a catalog item and tells the row whether it is playing.</summary>
public sealed class GuideEntry : INotifyPropertyChanged
{
    private const string EpgMissing = "Guía EPG no cargada";
    private bool _isCurrent;

    public GuideEntry(MediaItem item)
    {
        Item = item;
        Group = item.Kind == "Live" ? $"{item.CountryGroup} · {item.CategoryGroup}" : item.CategoryGroup;
    }

    public MediaItem Item { get; }
    public string Group { get; }
    public string Name => Item.Name;
    public string Logo => Item.Logo;

    // Live channels show the current programme when an XMLTV guide is loaded; everything else shows its category.
    public string Subtitle
    {
        get
        {
            if (Item.IsLive && !string.IsNullOrEmpty(Item.CurrentProgramTitle) && Item.CurrentProgramTitle != EpgMissing)
                return Item.CurrentProgramTime.Contains('–') ? $"{Item.CurrentProgramTitle} · {Item.CurrentProgramTime}" : Item.CurrentProgramTitle;
            return Item.Category;
        }
    }

    public string NextText =>
        Item.IsLive && !string.IsNullOrEmpty(Item.NextProgramTitle)
            ? $"Después: {Item.NextProgramTitle} {Item.NextProgramTime}".TrimEnd()
            : "";

    public bool IsCurrent
    {
        get => _isCurrent;
        set
        {
            if (_isCurrent == value) return;
            _isCurrent = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCurrent)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// Channel/content guide that sits on the left side of the player. The video surface is a native
/// window, so (like the controls bar) the guide is a separate window owned by the player.
/// </summary>
public partial class PlayerGuideWindow : Window
{
    private sealed class KindCache
    {
        public List<GuideEntry> Entries = new();
        public List<string> Groups = new();
        public Dictionary<MediaItem, GuideEntry> Map = new();
        public GuideEntry? Current;
    }

    private const string AllGroups = "Todas las categorías";
    private const double SideMargin = 20;
    private const double ControlsReserve = 190; // space left free for the controls bar at the bottom

    private readonly Func<IReadOnlyList<MediaItem>> _catalog;
    private readonly Action<MediaItem, List<MediaItem>> _activate;
    private readonly Dictionary<string, KindCache> _caches = new();
    private readonly System.Windows.Threading.DispatcherTimer _searchTimer;
    private List<GuideEntry> _shown = new();
    private MediaItem? _current;
    private int _suppress;
    private bool _hooked;
    private bool _wanted; // the user has the guide open; it may still be hidden together with the controls bar

    /// <summary>Raised while the user works in the guide, so the player keeps the controls bar (and the guide) on screen.</summary>
    public event Action? Interacted;

    /// <summary>True from the moment the guide is opened until the user closes it, even while it is hidden with the bar.</summary>
    public bool IsOpen => _wanted;

    public PlayerGuideWindow(Func<IReadOnlyList<MediaItem>> catalog, Action<MediaItem, List<MediaItem>> activate)
    {
        InitializeComponent();
        _catalog = catalog;
        _activate = activate;
        TypeBox.Items.Add("Todo");
        TypeBox.Items.Add("TV en vivo");
        TypeBox.Items.Add("Películas");
        TypeBox.Items.Add("Series");
        _searchTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            Refresh();
        };
        PreviewMouseMove += (_, _) => Interacted?.Invoke();
        PreviewMouseDown += (_, _) => Interacted?.Invoke();
        PreviewMouseWheel += (_, _) => Interacted?.Invoke();
        PreviewKeyDown += (_, _) => Interacted?.Invoke();
    }

    public void Open(MediaItem? current)
    {
        _wanted = true;
        HookOwner();
        _caches.Clear();
        _current = current;
        _suppress++;
        try
        {
            TypeBox.SelectedItem = current?.Kind switch
            {
                "Live" => "TV en vivo",
                "Movie" => "Películas",
                "Series" or "Episode" => "Series",
                _ => "Todo"
            };
            SearchBox.Text = string.Empty;
            FillGroups();
        }
        finally { _suppress--; }

        Refresh();
        Reposition();
        Show();
        Activate();
        ScrollToSelected(focus: true);
    }

    /// <summary>Closes the guide for good (Esc, ✕, G).</summary>
    public void HideGuide()
    {
        _wanted = false;
        _searchTimer.Stop();
        Hide();
        Owner?.Activate();
    }

    /// <summary>Shows the guide again after it was hidden together with the bar, keeping the keyboard on the list.</summary>
    public void Reveal()
    {
        if (!_wanted) return;
        if (!IsVisible)
        {
            Reposition();
            Show();
        }
        Activate();
        ScrollToSelected(focus: true);
    }

    /// <summary>
    /// Follows the floating controls bar: the guide is hidden at the same moment as the bar and comes
    /// back with it, without stealing keyboard focus from the player.
    /// </summary>
    public void SetControlsVisible(bool visible)
    {
        if (!_wanted) return;
        if (visible)
        {
            if (IsVisible) return;
            Reposition();
            Show(); // ShowActivated="False": the player keeps the keyboard
        }
        else if (IsVisible)
        {
            var hadFocus = IsActive;
            _searchTimer.Stop();
            Hide();
            if (hadFocus) Owner?.Activate();
        }
    }

    /// <summary>Marks the item that is now playing (called by the main window on every channel change).</summary>
    public void SetCurrent(MediaItem? item)
    {
        _current = item;
        foreach (var cache in _caches.Values)
        {
            if (cache.Current is not null) { cache.Current.IsCurrent = false; cache.Current = null; }
            if (item is not null && cache.Map.TryGetValue(item, out var entry))
            {
                entry.IsCurrent = true;
                cache.Current = entry;
            }
        }
    }

    // ----- data -----

    private static string? KindOf(string? label) => label switch
    {
        "TV en vivo" => "Live",
        "Películas" => "Movie",
        "Series" => "Series",
        _ => null
    };

    // Entries for one content type are built and sorted once per time the guide is opened.
    private KindCache GetCache(string? kind)
    {
        var key = kind ?? "All";
        if (_caches.TryGetValue(key, out var existing)) return existing;

        var names = StringComparer.CurrentCulture;
        var entries = _catalog()
            .Where(x => x.Kind != "Episode" && (kind is null || x.Kind == kind))
            .Select(x => new GuideEntry(x))
            .ToList();

        var groups = new HashSet<string>(entries.Select(e => e.Group), StringComparer.Ordinal).ToList();
        groups.Sort(names);
        var rank = new Dictionary<string, int>(groups.Count, StringComparer.Ordinal);
        for (var i = 0; i < groups.Count; i++) rank[groups[i]] = i;

        var cache = new KindCache
        {
            Groups = groups,
            Entries = entries.OrderBy(e => rank[e.Group]).ThenBy(e => e.Item.Name, names).ToList()
        };
        foreach (var entry in cache.Entries) cache.Map[entry.Item] = entry;
        if (_current is not null && cache.Map.TryGetValue(_current, out var currentEntry))
        {
            currentEntry.IsCurrent = true;
            cache.Current = currentEntry;
        }
        _caches[key] = cache;
        return cache;
    }

    private void FillGroups()
    {
        var cache = GetCache(KindOf(TypeBox.SelectedItem as string));
        _suppress++;
        try
        {
            CategoryCombo.Items.Clear();
            CategoryCombo.Items.Add(AllGroups);
            foreach (var group in cache.Groups) CategoryCombo.Items.Add(group);
            CategoryCombo.SelectedIndex = 0;
        }
        finally { _suppress--; }
    }

    private void Refresh()
    {
        var cache = GetCache(KindOf(TypeBox.SelectedItem as string));
        var selectedGroup = CategoryCombo.SelectedItem as string;
        var group = selectedGroup is null || selectedGroup == AllGroups ? null : selectedGroup;
        var query = SearchBox.Text.Trim();

        var shown = new List<GuideEntry>();
        foreach (var entry in cache.Entries)
        {
            if (group is not null && !entry.Group.Equals(group, StringComparison.Ordinal)) continue;
            if (query.Length > 0 &&
                !entry.Item.Name.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !entry.Group.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
            shown.Add(entry);
        }
        _shown = shown;

        var view = new ListCollectionView(shown);
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(GuideEntry.Group)));
        GuideList.ItemsSource = view;
        CountText.Text = $"{shown.Count:N0} elementos";

        // While searching the first match is selected; otherwise the playing item is.
        GuideEntry? target = null;
        if (query.Length > 0) target = shown.Count > 0 ? shown[0] : null;
        else if (cache.Current is not null && shown.Contains(cache.Current)) target = cache.Current;
        if (target is not null)
        {
            GuideList.SelectedItem = target;
            ScrollToSelected(focus: false);
        }
    }

    // ----- keyboard focus and scrolling -----

    private void ScrollToSelected(bool focus)
    {
        var target = GuideList.SelectedItem;
        if (target is null)
        {
            if (focus) GuideList.Focus();
            return;
        }
        Dispatcher.BeginInvoke(new Action(() =>
        {
            GuideList.ScrollIntoView(target);
            GuideList.UpdateLayout();
            if (!focus) return;
            if (GuideList.ItemContainerGenerator.ContainerFromItem(target) is ListBoxItem container) container.Focus();
            else GuideList.Focus();
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void ActivateSelected()
    {
        var entry = GuideList.SelectedItem as GuideEntry ?? (_shown.Count > 0 ? _shown[0] : null);
        if (entry is null) return;
        // Channel +/- in the player walks this list (series cannot be played directly, so they are skipped).
        var queue = _shown.Where(x => x.Item.Kind != "Series").Select(x => x.Item).ToList();
        _activate(entry.Item, queue);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var comboActive = TypeBox.IsDropDownOpen || CategoryCombo.IsDropDownOpen;
        switch (e.Key)
        {
            case Key.Escape:
                if (comboActive) return;
                HideGuide();
                e.Handled = true;
                return;
            case Key.Enter:
                if (comboActive || TypeBox.IsKeyboardFocusWithin || CategoryCombo.IsKeyboardFocusWithin) return;
                ActivateSelected();
                e.Handled = true;
                return;
            case Key.Down when SearchBox.IsKeyboardFocusWithin:
                if (GuideList.SelectedItem is null && _shown.Count > 0) GuideList.SelectedItem = _shown[0];
                ScrollToSelected(focus: true);
                e.Handled = true;
                return;
            case Key.F when Keyboard.Modifiers == ModifierKeys.Control:
                SearchBox.Focus();
                SearchBox.SelectAll();
                e.Handled = true;
                return;
            case Key.G when Keyboard.Modifiers == ModifierKeys.Control:
                HideGuide();
                e.Handled = true;
                return;
        }
    }

    // Typing anywhere in the guide starts a search, like a TV channel list.
    private void Window_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (SearchBox.IsKeyboardFocusWithin || TypeBox.IsKeyboardFocusWithin || CategoryCombo.IsKeyboardFocusWithin) return;
        if (string.IsNullOrEmpty(e.Text) || char.IsControl(e.Text[0])) return;
        SearchBox.Focus();
        SearchBox.Text += e.Text;
        SearchBox.CaretIndex = SearchBox.Text.Length;
        e.Handled = true;
    }

    // ----- control events -----

    private void TypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress > 0) return;
        FillGroups();
        Refresh();
    }

    private void CategoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress > 0) return;
        Refresh();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppress > 0) return;
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void GuideList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source &&
            ItemsControl.ContainerFromElement(GuideList, source) is ListBoxItem)
            ActivateSelected();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => HideGuide();

    // ----- placement over the player -----

    private void HookOwner()
    {
        if (_hooked || Owner is not Window owner) return;
        _hooked = true;
        owner.LocationChanged += (_, _) => Reposition();
        owner.SizeChanged += (_, _) => Reposition();
        owner.StateChanged += (_, _) => Reposition();
        owner.IsVisibleChanged += (_, args) =>
        {
            if (args.NewValue is false) { _wanted = false; Hide(); }
        };
    }

    public void Reposition()
    {
        if (!IsVisible && !IsLoaded && Owner is null) return;
        if (Owner is not Window { IsVisible: true } owner) return;
        try
        {
            var source = PresentationSource.FromVisual(owner);
            if (source?.CompositionTarget is null) return;
            var origin = source.CompositionTarget.TransformFromDevice.Transform(owner.PointToScreen(new Point(0, 0)));
            Width = 430;
            Left = origin.X + SideMargin;
            Top = origin.Y + SideMargin;
            Height = Math.Max(320, owner.ActualHeight - SideMargin - ControlsReserve);
        }
        catch { /* The owner can be between initialization and its first layout pass. */ }
    }
}
