namespace Doxie.Model;

[Flags]
public enum InclusionDefinitionOptions
{
    None = 0x0,
    IsExtension = 0x1,
    IsEndOfFile = 0x2,
    ForceRegex = 0x4,
    ForceExclusion = 0x8,
}
