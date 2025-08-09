namespace Doxie.Model;

public class IndexFragment(int startOffset, int endOffset)
{
    public int StartOffset { get; } = startOffset;
    public int EndOffset { get; } = endOffset;

    public override string ToString() => $"[{StartOffset}, {EndOffset}]";
}
