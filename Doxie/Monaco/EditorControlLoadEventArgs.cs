namespace Doxie.Monaco;

public class EditorControlLoadEventArgs() : EditorControlEventArgs(EditorControlEventType.Load)
{
    public string? DocumentText { get; set; }
}
