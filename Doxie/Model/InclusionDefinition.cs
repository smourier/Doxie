namespace Doxie.Model;

public class InclusionDefinition : IEquatable<InclusionDefinition>
{
    public InclusionDefinitionOptions Options { get; private set; }
    public string? Extension { get; private set; }

    public override string ToString()
    {
        if (Options.HasFlag(InclusionDefinitionOptions.IsExtension))
            return Extension!;

        return "???";
    }

    public virtual bool Matches(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        if (Options.HasFlag(InclusionDefinitionOptions.IsExtension))
        {
            var extension = Path.GetExtension(filePath);
            return string.Equals(extension, Extension, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    public static InclusionDefinition? Parse(string? text)
    {
        text = text.Nullify();
        if (text == null)
            return null;

        return new InclusionDefinition
        {
            Options = InclusionDefinitionOptions.IsExtension,
            Extension = text.ToLowerInvariant()
        };
    }

    public override int GetHashCode() => (Options, Extension).GetHashCode();
    public override bool Equals(object? obj) => obj is InclusionDefinition other && Equals(other);
    public bool Equals(InclusionDefinition? other) => other is not null && Options == other.Options && Extension.EqualsIgnoreCase(other.Extension);
}
