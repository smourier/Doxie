namespace Doxie.Model;

public class IndexScanResult
{
    public Exception? Exception { get; set; }
    public bool Success => Exception == null;
}
