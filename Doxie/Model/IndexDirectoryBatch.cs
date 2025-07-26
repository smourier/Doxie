namespace Doxie.Model;

public class IndexDirectoryBatch(Guid id, string path) : INotifyPropertyChanged, IEquatable<IndexDirectoryBatch>
{
    private IndexDirectoryBatchOptions _options;
    private DateTime _startTimeUtc;
    private DateTime _endTimeUtc;
    private int _numberOfDocuments;
    private int _numberOfSkippedFiles;
    private int _numberOfSkippedDirectories;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid Id { get; } = id;
    public string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));
    public ObservableCollection<string> IncludedFileExtensions { get; } = [];
    public ObservableCollection<string> ExcludedDirectoryNames { get; } = [];
    public ObservableCollection<string> NonIndexedFileExtensions { get; } = [];
    public TimeSpan Duration => EndTimeUtc - StartTimeUtc;

    public IndexDirectoryBatchOptions Options
    {
        get => _options;
        internal set
        {
            if (_options == value)
                return;

            _options = value;
            OnPropertyChanged();
        }
    }

    public DateTime StartTimeUtc
    {
        get => _startTimeUtc;
        internal set
        {
            if (_startTimeUtc == value)
                return;

            _startTimeUtc = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProcessedFilesPerSecond));
        }
    }

    public DateTime EndTimeUtc
    {
        get => _endTimeUtc;
        internal set
        {
            _endTimeUtc = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProcessedFilesPerSecond));
        }
    }

    public int NumberOfDocuments
    {
        get => _numberOfDocuments;
        internal set
        {
            _numberOfDocuments = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProcessedFilesPerSecond));
        }
    }

    public int NumberOfSkippedFiles
    {
        get => _numberOfSkippedFiles; internal set
        {
            _numberOfSkippedFiles = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProcessedFilesPerSecond));
        }
    }

    public int NumberOfSkippedDirectories
    {
        get => _numberOfSkippedDirectories; internal set
        {
            _numberOfSkippedDirectories = value;
            OnPropertyChanged();
        }
    }

    public int ProcessedFilesPerSecond
    {
        get
        {
            var duration = EndTimeUtc - StartTimeUtc;
            if (duration.TotalSeconds <= 0)
                return 0;

            var indexedFilesCount = NumberOfDocuments + NumberOfSkippedFiles;
            return (int)(indexedFilesCount / duration.TotalSeconds);
        }
    }

    public override string ToString() => $"{Id} '{Path}' at {StartTimeUtc.ToLocalTime()}, docs: {NumberOfDocuments}";

    internal void Update(IndexDirectoryBatch? other)
    {
        if (other == null || other == this)
            return;

        Options = other.Options;
        StartTimeUtc = other.StartTimeUtc;
        EndTimeUtc = other.EndTimeUtc;
        NumberOfDocuments = other.NumberOfDocuments;
        NumberOfSkippedFiles = other.NumberOfSkippedFiles;
        NumberOfSkippedDirectories = other.NumberOfSkippedDirectories;
        IncludedFileExtensions.UpdateWith(other.IncludedFileExtensions, null, StringComparer.OrdinalIgnoreCase);
        ExcludedDirectoryNames.UpdateWith(other.ExcludedDirectoryNames, null, StringComparer.OrdinalIgnoreCase);
        NonIndexedFileExtensions.UpdateWith(other.NonIndexedFileExtensions, null, StringComparer.OrdinalIgnoreCase);
    }

    protected virtual void OnPropertyChanged(object sender, PropertyChangedEventArgs e) => PropertyChanged?.Invoke(sender, e);
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) => OnPropertyChanged(this, new PropertyChangedEventArgs(propertyName));

    public override int GetHashCode() => Id.GetHashCode();
    public override bool Equals(object? obj) => obj is IndexDirectoryBatch other && Equals(other);
    public bool Equals(IndexDirectoryBatch? other) => other != null && Id.Equals(other.Id);
}
