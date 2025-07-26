namespace Doxie.Model;

[Flags]
public enum IndexDirectoryBatchOptions
{
    None = 0x0,
    WasCancelled = 0x1,
    DataWasDeleted = 0x2,
}
