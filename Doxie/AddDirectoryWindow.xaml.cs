namespace Doxie;

public partial class AddDirectoryWindow : Window
{
    public AddDirectoryWindow()
    {
        InitializeComponent();
        DataContext = this;
        UpdateControls();
    }

    public string? DirectoryName { get; set; }

    private void UpdateControls() => ok.IsEnabled = !string.IsNullOrWhiteSpace(DirectoryName);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
        base.OnKeyDown(e);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void DirectoryName_TextChanged(object sender, TextChangedEventArgs e) => UpdateControls();
}
