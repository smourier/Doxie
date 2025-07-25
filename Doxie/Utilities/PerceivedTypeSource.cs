namespace Doxie.Utilities;

[Flags]
public enum PerceivedTypeSource
{
    Undefined = 0x0000,
    SoftCoded = 0x0001,
    HardCoded = 0x0002,
    NativeSupport = 0x0004,
    GdiPlus = 0x0010,
    WmSdk = 0x0020,
    ZipFolder = 0x0040,
    Mime = 0x0080,
}
