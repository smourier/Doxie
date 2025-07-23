namespace Doxie.Model;

public class IndexCreationRequest(string inputDirectoryPath)
{
    public string InputDirectoryPath { get; } = inputDirectoryPath ?? throw new ArgumentNullException(nameof(inputDirectoryPath));
    public virtual bool AsyncProcessing { get; set; } = true;
    public virtual string SearchPattern { get; set; } = "*.*";
    public virtual EnumerationOptions EnumerationOptions { get; set; } = new EnumerationOptions
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = true,
        MatchCasing = MatchCasing.CaseInsensitive,
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
    };

    public virtual CancellationToken CancellationToken { get; set; }
    public virtual Func<string, bool>? IncludeFilter { get; set; }
#pragma warning disable IDE0060 // Remove unused parameter
    internal static bool Include(string path) => true;
#pragma warning restore IDE0060 // Remove unused parameter
}
