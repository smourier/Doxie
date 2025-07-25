namespace Doxie.Model;

public class IndexCreationRequest(string inputDirectoryPath)
{
    public string InputDirectoryPath { get; } = inputDirectoryPath ?? throw new ArgumentNullException(nameof(inputDirectoryPath));
    public CancellationTokenSource? CancellationTokenSource { get; set; }
    public virtual bool AsyncProcessing { get; set; } = true;
    public virtual string SearchPattern { get; set; } = "*.*";
    public virtual EnumerationOptions EnumerationOptions { get; set; } = new EnumerationOptions
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = true,
        MatchCasing = MatchCasing.CaseInsensitive,
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
    };
}
