namespace Doxie.Model;

public class IndexDirectory(string path) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));
    public ObservableCollection<IndexDirectoryBatch> Batches { get; } = [];

    public override string ToString() => Path;

    internal void Update(IndexDirectory? other)
    {
        if (other == null || other == this)
            return;

        Batches.UpdateWith(other.Batches, (a, b) => a.Path.Equals(b.Path, StringComparison.OrdinalIgnoreCase), (a, b) => a.Update(b));
    }

    protected virtual void OnPropertyChanged(object sender, PropertyChangedEventArgs e) => PropertyChanged?.Invoke(sender, e);
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) => OnPropertyChanged(this, new PropertyChangedEventArgs(propertyName));
}
