using Lucene.Net.Documents;
using Lucene.Net.Search;

namespace Doxie.Model;

public class IndexSearchResultItem(IndexDirectory directory) : SearchResultItem
{
    public IndexDirectory Directory { get; } = directory ?? throw new ArgumentNullException(nameof(directory));

    public string? RelativePath => Fields.GetNullifiedString(Model.Index.FieldRelPath);
    public string? Name => RelativePath != null ? System.IO.Path.GetFileName(RelativePath) : null;
    public string? Extension => RelativePath != null ? System.IO.Path.GetExtension(RelativePath) : null;
    public int LinesCount => Fields.GetValue(Model.Index.FieldLinesCount, 0);
    public string? Path
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Directory.Path) || string.IsNullOrWhiteSpace(RelativePath))
                return null;

            return System.IO.Path.Combine(Directory.Path, RelativePath);
        }
    }

    public override string ToString() => RelativePath ?? string.Empty;

    public static IndexSearchResultItem? CreateItem(Index index, int docIndex, ScoreDoc scoreDoc, Document doc)
    {
        ArgumentNullException.ThrowIfNull(index);
        var did = doc.Fields.FirstOrDefault(f => f.Name.EqualsIgnoreCase(Model.Index.FieldDirectoryId))?.GetInt32Value() ?? -1;
        if (did < 0)
            return null;

        var directory = index.Directories.FirstOrDefault(d => d.Id == did);
        if (directory == null)
            return null;

        return new IndexSearchResultItem(directory)
        {
            Index = docIndex,
            Score = scoreDoc.Score,
            DocumentId = scoreDoc.Doc,
            Document = doc,
        };
    }
}
