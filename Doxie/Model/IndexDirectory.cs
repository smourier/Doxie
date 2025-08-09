namespace Doxie.Model;

public class IndexDirectory : INotifyPropertyChanged, IEquatable<IndexDirectory>
{
    public event PropertyChangedEventHandler? PropertyChanged;

    internal IndexDirectory(Index index, int id, string path)
    {
        Index = index;
        Id = id;
        Path = path;
    }

    public int Id { get; }
    public Index Index { get; }
    public string Path { get; }
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
