namespace Doxie;

public partial class AddDirectoryWindow : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string? _directoryName;

    public AddDirectoryWindow()
    {
        InitializeComponent();
        DataContext = this;
        UpdateControls();
    }

    public string? DirectoryName
    {
        get => _directoryName;
        set
        {
            if (_directoryName == value)
                return;

            _directoryName = value;
            OnPropertyChanged();
            UpdateControls();
        }
    }

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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
