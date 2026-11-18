namespace Doxie.Monaco;

public class MonacoKeyEventArgs(MonacoEventType type, string? json) : MonacoEventArgs(type, json)
{
    public int KeyCode => RootElement.GetValue("keyCode", 0);
    public string? Code => RootElement.GetNullifiedValue("code");
    public bool Alt => RootElement.GetValue("altKey", false);
    public bool AltGraph => RootElement.GetValue("altGraphKey", false);
    public bool Ctrl => RootElement.GetValue("ctrlKey", false);
    public bool Meta => RootElement.GetValue("metaKey", false);
    public bool Shift => RootElement.GetValue("shiftKey", false);
}
