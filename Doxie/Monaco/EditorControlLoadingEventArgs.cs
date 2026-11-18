namespace Doxie.Monaco;

public class MonacoLoadEventArgs() : MonacoEventArgs(MonacoEventType.Load)
{
    public string? DocumentText { get; set; }
}
