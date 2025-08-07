namespace Doxie;

public partial class ListWindow : Window
{
    private readonly ObservableCollection<string> _strings = new();

    public ListWindow(IEnumerable<string> enumerable)
    {
        InitializeComponent();
        _strings.AddRange(enumerable);
        list.ItemsSource = _strings;
    }

    public bool ShowButton { get; set; }
    public string? ButtonText { get; set; }
    public Func<string, bool>? IncludeAction { get; set; }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var ext = sender.GetDataContext<string>();
        if (ext != null && IncludeAction != null)
        {
            if (IncludeAction(ext))
            {
                _strings.Remove(ext);
            }
        }
    }
}
