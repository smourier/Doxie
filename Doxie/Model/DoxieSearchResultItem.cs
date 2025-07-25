namespace Doxie.Model;

public class DoxieSearchResultItem : SearchResultItem
{
    public string? RelativePath => Fields.GetNullifiedValueByPath(DoxieIndex.FieldPath);

    public override string ToString() => RelativePath ?? string.Empty;
}
