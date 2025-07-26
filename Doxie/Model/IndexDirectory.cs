namespace Doxie.Model;

public class IndexDirectory(string path) : INotifyPropertyChanged, IEquatable<IndexDirectory>
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));
    public ObservableCollection<IndexDirectoryBatch> Batches { get; } = [];

    public override string ToString() => Path;

    internal void Update(IndexDirectory? other)
    {
        if (other == null || other == this)
            return;

        Batches.UpdateWith(other.Batches, (a, b) => a.Update(b));
    }

    protected virtual void OnPropertyChanged(object sender, PropertyChangedEventArgs e) => PropertyChanged?.Invoke(sender, e);
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) => OnPropertyChanged(this, new PropertyChangedEventArgs(propertyName));

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Path);
    public override bool Equals(object? obj) => obj is IndexDirectory other && Equals(other);
    public bool Equals(IndexDirectory? other) => other != null && string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase);
}
