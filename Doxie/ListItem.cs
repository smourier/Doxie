namespace Doxie;

public class ListItem(string name)
{
    public string Name { get; set; } = name;
    public virtual string? Description { get; set; }
    public virtual bool ShowButton { get; set; }
    public virtual string? ButtonText { get; set; }
    public virtual Func<bool>? Action { get; set; }

    public bool IsDescriptionVisible => !string.IsNullOrEmpty(Description);

    public override string ToString() => Name;
}
