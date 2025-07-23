namespace Doxie.Model;

public class SearchResultItem(int index, float score)
{
    private readonly ConcurrentDictionary<string, object> _fields = new();

    public int Index { get; } = index;
    public float Score { get; } = score;

    public IReadOnlyDictionary<string, object> Fields => _fields;

    internal void AddField(string name, object? value)
    {
        if (value == null)
            return;

        _fields[name] = value;
    }

    public override string ToString() => Index + " (" + Score.ToString("F", CultureInfo.InvariantCulture) + ")";
}
