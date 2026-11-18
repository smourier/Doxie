namespace Doxie.Monaco;

public class EditorControlLoadingEventArgs() : EditorControlEventArgs(EditorControlEventType.Load)
{
    public string? DocumentText { get; set; }
}
