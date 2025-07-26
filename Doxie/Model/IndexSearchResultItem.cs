namespace Doxie.Model;

public class IndexSearchResultItem : SearchResultItem
{
    public string? RelativePath => Fields.GetNullifiedValueByPath(Model.Index.FieldPath);

    public override string ToString() => RelativePath ?? string.Empty;
}
