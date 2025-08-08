namespace Doxie.Model;

public class IndexSearchResultItem : SearchResultItem
{
    public string? DirectoryPath => Fields.GetNullifiedValueByPath(Model.Index.FieldDir);
    public string? RelativePath => Fields.GetNullifiedValueByPath(Model.Index.FieldRelPath);
    public string? Path
    {
        get
        {
            if (string.IsNullOrWhiteSpace(DirectoryPath) || string.IsNullOrWhiteSpace(RelativePath))
                return null;

            return System.IO.Path.Combine(DirectoryPath, RelativePath);
        }
    }

    public string? Name => RelativePath != null ? System.IO.Path.GetFileName(RelativePath) : null;
    public string? Extension => RelativePath != null ? System.IO.Path.GetExtension(RelativePath) : null;

    public override string ToString() => RelativePath ?? string.Empty;
}
