namespace Doxie;

public class RecentFile
{
    public string? FilePath { get; set; }
    public DateTime LastAccessTime { get; set; } = DateTime.Now;

    public override string ToString() => FilePath ?? string.Empty;
}

// these classes are just there for UI data templating purposes
public class ClearRecentFiles : RecentFile
{
}

public class RecentFileSeparator : RecentFile
{
}
