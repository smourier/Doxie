namespace Doxie.Monaco;

public class EditorControlConfigurationChangedEventArgs(string? json)
    : EditorControlEventArgs(EditorControlEventType.ConfigurationChanged, json)
{
    // https://microsoft.github.io/monaco-editor/typedoc/enums/editor.EditorOption.html#fontSize
    public int Index => RootElement.GetValue("index", -1);
    public EditorOption Option => (EditorOption)Index;

    public override string ToString() => EventType + " " + Option;
}
