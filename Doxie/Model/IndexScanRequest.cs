namespace Doxie.Model;

public class IndexScanRequest(IndexDirectory inputDirectory)
{
    public IndexDirectory InputDirectory { get; } = inputDirectory ?? throw new ArgumentNullException(nameof(inputDirectory));
    public CancellationTokenSource? CancellationTokenSource { get; set; }
    public IndexScanRequestOptions Options { get; set; }
    public virtual bool AsyncProcessing { get; set; } = true;
    public virtual string DirectorySearchPattern { get; set; } = "*";
    public virtual string FileSearchPattern { get; set; } = "*";
    public virtual EnumerationOptions EnumerationOptions { get; set; } = new EnumerationOptions
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = true,
        MatchCasing = MatchCasing.CaseInsensitive,
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
    };
}
