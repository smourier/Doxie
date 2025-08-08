namespace Doxie.Monaco;

#pragma warning disable IDE1006 // Naming Styles
public class MonacoRange(int startLineNumber, int startColumn, int endLineNumber, int endColumn)
{
    public int startLineNumber { get; set; } = startLineNumber;
    public int startColumn { get; set; } = startColumn;
    public int endLineNumber { get; set; } = endLineNumber;
    public int endColumn { get; set; } = endColumn;
}
#pragma warning restore IDE1006 // Naming Styles
