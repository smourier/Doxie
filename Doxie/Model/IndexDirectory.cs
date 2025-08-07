namespace Doxie.Model;

public class IndexDirectory(Index index, string path) : INotifyPropertyChanged, IEquatable<IndexDirectory>
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public Index Index { get; } = index ?? throw new ArgumentNullException(nameof(index));
    public string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));
    public ObservableCollection<IndexDirectoryBatch> Batches { get; } = [];
    public IEnumerable<IndexDirectoryBatch> OrderedBatches => Batches.OrderByDescending(b => b.StartTime);

    public override string ToString() => Path;

    internal void Update(IndexDirectory? other)
    {
        if (other == null || other == this)
            return;

        Batches.UpdateWith(other.Batches, (a, b) => a.Update(b));
        OnPropertyChanged(nameof(OrderedBatches));
    }

    protected virtual void OnPropertyChanged(object sender, PropertyChangedEventArgs e) => PropertyChanged?.Invoke(sender, e);
    public void OnPropertyChanged([CallerMemberName] string? propertyName = null) => OnPropertyChanged(this, new PropertyChangedEventArgs(propertyName));

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Path);
    public override bool Equals(object? obj) => obj is IndexDirectory other && Equals(other);
    public bool Equals(IndexDirectory? other) => other != null && string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase);
}
