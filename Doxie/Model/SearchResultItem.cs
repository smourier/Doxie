namespace Doxie.Model;

public class SearchResultItem
{
    private readonly ConcurrentDictionary<string, object?> _fields = new();

    public int Index { get; set; }
    public float Score { get; set; }

    public IReadOnlyDictionary<string, object?> Fields => _fields;

    internal void AddField(string name, object? value)
    {
        if (value == null)
            return;

        _fields[name] = value;
    }

    public override string ToString() => Index + " (" + Score.ToString("F", CultureInfo.InvariantCulture) + ")";
}
