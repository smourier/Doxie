namespace Doxie.Monaco;

#pragma warning disable CA1822 // Mark members as static

[ComVisible(true)]
public partial class EditorControlObject
{
    public event EventHandler<EditorControlLoadingEventArgs>? Loading;
    public event EventHandler<EditorControlEventArgs>? Event;

    public string GetOptions()
    {
        var options = new EditorControlObjectOptions
        {
            FontSize = Settings.Current.MonacoFontSize.ToString(CultureInfo.InvariantCulture) + "px",
            Theme = Settings.Current.MonacoTheme,
        };
        return JsonSerializer.Serialize(options, MonacoExtensions.SerializerOptions);
    }

    public string? Load()
    {
        try
        {
            var e = new EditorControlLoadingEventArgs();
            Loading?.Invoke(this, e);
            return e.DocumentText;
        }
        catch (Exception ex)
        {
            EventProvider.Default.WriteMessage("Error: " + ex);
            throw;
        }
    }

    public void OnEvent(EditorControlEventType type, string? json = null)
    {
        var handler = Event;
        if (handler == null)
            return;

        try
        {
            EditorControlEventArgs e = type switch
            {
                EditorControlEventType.KeyDown or EditorControlEventType.KeyUp => new EditorControlKeyEventArgs(type, json),
                EditorControlEventType.ConfigurationChanged => new EditorControlConfigurationChangedEventArgs(json),
                _ => new EditorControlEventArgs(type, json),
            };
            handler.Invoke(this, e);
        }
        catch (Exception ex)
        {
            EventProvider.Default.WriteMessage("Error: " + ex);
            throw;
        }
    }
}
