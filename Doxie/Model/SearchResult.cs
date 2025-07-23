namespace Doxie.Model;

public class SearchResult<T> where T : SearchResultItem
{
    public int TotalHits { get; internal set; }
    public IReadOnlyList<T> Items { get; internal set; } = [];
}
