namespace Doxie;

public partial class ListWindow : Window
{
    public ListWindow(IEnumerable enumerable)
    {
        InitializeComponent();
        list.ItemsSource = enumerable;
    }

    public bool ShowButton { get; set; }
    public string? ButtonText { get; set; }
    public Action<object>? IncludeAction { get; set; }

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
            IncludeAction(ext);
        }
    }
}
