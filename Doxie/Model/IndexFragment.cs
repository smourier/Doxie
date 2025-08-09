namespace Doxie.Model;

public class IndexFragment(int startOffset, int endOffset)
{
    public int StartOffset { get; } = startOffset;
    public int EndOffset { get; } = endOffset;

    public bool IsEmpty => Length == 0;
    public int Length => EndOffset - StartOffset + 1;

    public override string ToString() => $"[{StartOffset}, {EndOffset}]";
}
