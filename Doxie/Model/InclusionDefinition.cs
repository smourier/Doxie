namespace Doxie.Model;

public class InclusionDefinition : IEquatable<InclusionDefinition>
{
    private readonly Lazy<Regex?> _regex;

    private InclusionDefinition(string text, InclusionDefinitionOptions options)
    {
        Text = text;
        Options = options;
        _regex = new Lazy<Regex?>(() =>
        {
            if (Options.HasFlag(InclusionDefinitionOptions.ForceRegex))
                return new Regex(Regex.Escape(text), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            return null;
        });
    }

    public bool IsExclusion => Options.HasFlag(InclusionDefinitionOptions.ForceExclusion);
    public InclusionDefinitionOptions Options { get; private set; }
    public string Text { get; }
    public string Description
    {
        get
        {
            var exclusionText = IsExclusion ? "Excludes" : "Includes";
            if (Options.HasFlag(InclusionDefinitionOptions.IsExtension))
                return $"{exclusionText} file names with extension '.{Text}'";

            if (Options.HasFlag(InclusionDefinitionOptions.IsEndOfFile))
                return $"{exclusionText} file names ending with '{Text}'";

            if (Options.HasFlag(InclusionDefinitionOptions.ForceRegex))
                return $"{exclusionText} file names using regex '{Text}'";

            return string.Empty;
        }
    }

    public override string ToString()
    {
        var str = toString();
        if (IsExclusion)
            return "!" + str;

        return str;

        string toString()
        {
            if (Options.HasFlag(InclusionDefinitionOptions.IsExtension))
                return $"ext:.{Text}";

            if (Options.HasFlag(InclusionDefinitionOptions.IsEndOfFile))
                return $"end:{Text}";

            if (Options.HasFlag(InclusionDefinitionOptions.ForceRegex))
                return $"regex:{Text}";

            return string.Empty;
        }
    }

    public virtual bool Matches(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        if (Options.HasFlag(InclusionDefinitionOptions.IsExtension))
        {
            var extension = Path.GetExtension(filePath);
            return string.Equals(extension, "." + Text, StringComparison.OrdinalIgnoreCase);
        }

        if (Options.HasFlag(InclusionDefinitionOptions.IsEndOfFile))
        {
            var fileName = Path.GetFileName(filePath);
            return fileName.EndsWith(Text!, StringComparison.OrdinalIgnoreCase);
        }

        if (Options.HasFlag(InclusionDefinitionOptions.ForceRegex))
            return _regex.Value!.IsMatch(filePath);

        return false;
    }

    internal string Serialize() => $"{Options}:{Text}";
    internal static InclusionDefinition? Deserialize(string? text)
    {
        if (text == null)
            return null;

        var parts = text.Split(':', 2);
        if (parts.Length != 2)
            return null;

        if (!Enum.TryParse<InclusionDefinitionOptions>(parts[0], out var options))
            return null;

        if (string.IsNullOrWhiteSpace(parts[1]))
            return null;

        return new InclusionDefinition(parts[1], options);
    }

    public static InclusionDefinition? Parse(string? text, InclusionDefinitionOptions options = InclusionDefinitionOptions.None)
    {
        text = text.Nullify();
        if (text == null)
            return null;

        var def = parse();
        if (def != null && def.IsExclusion)
        {
            def.Options |= InclusionDefinitionOptions.ForceExclusion;
        }
        return def;

        InclusionDefinition? parse()
        {
            if (options.HasFlag(InclusionDefinitionOptions.ForceRegex))
                return new InclusionDefinition(text, InclusionDefinitionOptions.ForceRegex);

            if (text.StartsWith('.'))
            {
                if (text[0] == '.')
                {
                    if (text.Length == 1)
                        return null;

                    return new InclusionDefinition(text[1..].ToLowerInvariant(), InclusionDefinitionOptions.IsExtension);
                }

                return new InclusionDefinition(text.ToLowerInvariant(), InclusionDefinitionOptions.IsExtension);
            }

            if (text.StartsWith("*."))
            {
                if (text == "*.")
                    return null;

                return new InclusionDefinition(text[1..].ToLowerInvariant(), InclusionDefinitionOptions.IsEndOfFile);
            }

            return null;
        }
    }

    public override int GetHashCode() => (Options, Text).GetHashCode();
    public override bool Equals(object? obj) => obj is InclusionDefinition other && Equals(other);
    public bool Equals(InclusionDefinition? other) => other is not null && Options == other.Options && Text.EqualsIgnoreCase(other.Text);
}
