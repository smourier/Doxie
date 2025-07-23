namespace Doxie.Model;

public class IndexCreationResult
{
    public Exception? Exception { get; set; }
    public bool Success => Exception == null;
}
