namespace Doxie;

public partial class AddMultipleDirectories : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public AddMultipleDirectories(string directoryPath, IEnumerable<string> alreadyAdded)
    {
        ArgumentNullException.ThrowIfNull(directoryPath);
        ArgumentNullException.ThrowIfNull(alreadyAdded);
        DirectoryPath = directoryPath;
        InitializeComponent();

        var added = alreadyAdded.ToHashSet(StringComparer.OrdinalIgnoreCase);
        list.ItemsSource = Directory.EnumerateDirectories(DirectoryPath, "*.*").Where(p => !added.Contains(p)).Order().Select(d => new DirectoryItem(this, d));
        DataContext = this;
    }

    public string DirectoryPath { get; }
    public int SelectedCount => list.Items.OfType<DirectoryItem>().Count(i => i.IsSelected);
    public IEnumerable<string> SelectedPaths => list.Items.OfType<DirectoryItem>().Where(i => i.IsSelected).Select(i => i.Path);

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

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in list.Items.OfType<DirectoryItem>())
        {
            item.IsSelected = false;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class DirectoryItem(AddMultipleDirectories window, string path) : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private bool _isSelected;

        public string Path { get; } = path;
        public string Name => System.IO.Path.GetFileName(Path);

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                    return;

                _isSelected = value;
                OnPropertyChanged();
                window.OnPropertyChanged(nameof(SelectedCount));
            }
        }

        public override string ToString() => Path;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
