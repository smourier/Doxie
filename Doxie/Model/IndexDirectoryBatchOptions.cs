namespace Doxie.Model;

[Flags]
public enum IndexDirectoryBatchOptions
{
    [Description("")]
    None = 0x0,
    IndexingWasCancelled = 0x1,
    DataWasDeleted = 0x2,
}
