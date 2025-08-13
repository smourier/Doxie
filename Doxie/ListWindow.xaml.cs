namespace Doxie;

public partial class ListWindow : Window
{
    private readonly ObservableCollection<ListItem> _items = [];
    private bool _sortedByNameAsc;
    private bool _sortedByDescriptionAsc;

    public ListWindow(IEnumerable<ListItem> enumerable)
    {
        ArgumentNullException.ThrowIfNull(enumerable);
        InitializeComponent();
        var sortedItems = enumerable.OrderBy(i => i.Name);
        _sortedByNameAsc = true;
        _items.AddRange(sortedItems);
        list.ItemsSource = _items;
        DataContext = this;
    }

    public string? SortByDescriptionButtonText { get; set; }
    public string? SortByNameButtonText { get; set; }
    public string? CopyButtonText { get; set; }
    public bool IsSortByDescriptionButtonVisible => !string.IsNullOrEmpty(SortByDescriptionButtonText);
    public bool IsSortByNameButtonVisible => !string.IsNullOrEmpty(SortByNameButtonText);
    public bool IsCopyButtonVisible => !string.IsNullOrEmpty(CopyButtonText);
    public bool IsSortPanelEnabled => IsSortByDescriptionButtonVisible || IsSortByNameButtonVisible || IsCopyButtonVisible;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    private void Action_Click(object sender, RoutedEventArgs e)
    {
        var item = sender.GetDataContext<ListItem>();
        if (item != null && item.Action != null)
        {
            if (item.Action())
            {
                _items.Remove(item);
            }
        }
    }

    private void SortByName_Click(object sender, RoutedEventArgs e)
    {
        var sortedItems = (_sortedByNameAsc ? _items.OrderByDescending(i => i.Name) : _items.OrderBy(i => i.Name)).ToArray();
        _items.Clear();
        _items.AddRange(sortedItems);
        _sortedByNameAsc = !_sortedByNameAsc;
    }

    private void SortByDescription_Click(object sender, RoutedEventArgs e)
    {
        var sortedItems = (_sortedByDescriptionAsc ? _items.OrderByDescending(i => i.Description) : _items.OrderBy(i => i.Description)).ToArray();
        _items.Clear();
        _items.AddRange(sortedItems);
        _sortedByDescriptionAsc = !_sortedByDescriptionAsc;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        var sb = string.Join(Environment.NewLine, _items.Select(i => i.Name));
        Clipboard.SetText(sb);
        MessageBox.Show($"{_items.Count} line(s) copied to clipboard", AssemblyUtilities.GetProduct(), MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
