namespace Doxie.Monaco;

#pragma warning disable IDE1006 // Naming Styles

[ComVisible(true)]
public partial class EditorControlObject
{
    public event EventHandler<EditorControlLoadEventArgs>? Load;
    public event EventHandler<EditorControlEventArgs>? Event;

    public object getOptions() => JsonSerializer.Serialize(new
    {
        automaticLayout = true,
        //language = "plaintext",
        fontSize = Settings.Current.MonacoFontSize.ToString(CultureInfo.InvariantCulture) + "px",
        dragAndDrop = false,
        mouseWheelZoom = true,
        contextmenu = false,
        theme = Settings.Current.MonacoTheme,
    });

    public string? load()
    {
        try
        {
            var e = new EditorControlLoadEventArgs();
            Load?.Invoke(this, e);
            return e.DocumentText;
        }
        catch (Exception ex)
        {
            EventProvider.Default.WriteMessage("Error: " + ex);
            throw;
        }
    }

    public void onEvent(EditorControlEventType type, string? json = null)
    {
        var handler = Event;
        if (handler == null)
            return;

        try
        {
            var e = type switch
            {
                EditorControlEventType.KeyDown or EditorControlEventType.KeyUp => new EditorControlKeyEventArgs(type, json),
                EditorControlEventType.ConfigurationChanged => new EditorControlConfigurationChangedEventArgs(json),
                _ => new EditorControlEventArgs(type, json),
            };
            handler?.Invoke(this, e);
        }
        catch (Exception ex)
        {
            EventProvider.Default.WriteMessage("Error: " + ex);
            throw;
        }
    }
}
#pragma warning restore IDE1006 // Naming Styles
