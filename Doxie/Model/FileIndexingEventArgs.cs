namespace Doxie.Model;

public class FileIndexingEventArgs(IndexDirectoryBatch batch, string filePath) : CancelEventArgs
{
    public IndexDirectoryBatch Batch { get; } = batch ?? throw new ArgumentNullException(nameof(batch));
    public string FilePath { get; } = filePath ?? throw new ArgumentNullException(nameof(filePath));

    public override string ToString() => FilePath;
}
