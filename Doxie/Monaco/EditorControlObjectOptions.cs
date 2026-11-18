namespace Doxie.Monaco;

public class EditorControlObjectOptions
{
    // Monaco's own ResizeObserver loops on resize, the window lays the editor out itself instead.
    public bool AutomaticLayout { get; set; }
    public string? Language { get; set; }
    public string? FontSize { get; set; } = "13px";
    public bool DragAndDrop { get; set; }
    public bool MouseWheelZoom { get; set; } = true;
    public bool Contextmenu { get; set; }
    public string? Theme { get; set; } = "vs";
}
