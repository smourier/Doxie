namespace Doxie.Model;

public class FileIndexingEventArgs(string filePath) : CancelEventArgs
{
    public string FilePath { get; } = filePath ?? throw new ArgumentNullException(nameof(filePath));
    public int IndexedFilesCount { get; internal set; }
    public DateTime StartTimeUtc { get; internal set; }
    public TimeSpan ElapsedTime => DateTime.UtcNow - StartTimeUtc;
    public int ProcessedFilesPerSecond
    {
        get
        {
            if (ElapsedTime.TotalSeconds <= 0)
                return 0;

            return (int)(IndexedFilesCount / ElapsedTime.TotalSeconds);
        }
    }
}
