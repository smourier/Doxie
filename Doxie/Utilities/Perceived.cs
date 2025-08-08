namespace Doxie.Utilities;

public sealed class Perceived(string extension)
{
    public string Extension { get; } = extension ?? throw new ArgumentNullException(nameof(extension));
    public PerceivedType PerceivedType { get; private set; }
    public PerceivedTypeSource PerceivedTypeSource { get; private set; }

    public override string ToString() => Extension + ":" + PerceivedType + " (" + PerceivedTypeSource + ")";

    public static Perceived GetPerceivedType(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        var extension = Path.GetExtension(fileName) ?? throw new ArgumentException(null, nameof(fileName));
        extension = extension.ToLowerInvariant();
        if (!_perceivedTypes.TryGetValue(extension, out var perceived))
        {
            perceived = new Perceived(extension);
            using (var key = Registry.ClassesRoot.OpenSubKey(extension))
            {
                if (key != null)
                {
                    var ct = Conversions.ChangeType<string>(key.GetValue("PerceivedType"));
                    if (ct != null)
                    {
                        perceived.PerceivedType = Conversions.ChangeType(ct, PerceivedType.Custom);
                        perceived.PerceivedTypeSource = PerceivedTypeSource.SoftCoded;
                    }
                    else
                    {
                        ct = Conversions.ChangeType<string>(key.GetValue("Content Type"));
                        if (ct != null)
                        {
                            var pos = ct.IndexOf('/');
                            if (pos > 0)
                            {
                                perceived.PerceivedType = Conversions.ChangeType(ct[..pos], PerceivedType.Custom);
                                perceived.PerceivedTypeSource = PerceivedTypeSource.Mime;
                            }
                        }
                    }
                    key.Close();
                }
            }

            if (perceived.PerceivedType == PerceivedType.Unknown)
            {
                var text = IntPtr.Zero;
                var type = PerceivedType.Unknown;
                var source = PerceivedTypeSource.Undefined;
                var hr = AssocGetPerceivedType(extension, ref type, ref source, ref text);
                if (hr != 0)
                {
                    perceived.PerceivedType = PerceivedType.Unspecified;
                    perceived.PerceivedTypeSource = PerceivedTypeSource.Undefined;
                }
                else
                {
                    perceived.PerceivedType = type;
                    perceived.PerceivedTypeSource = source;
                }
            }
            _perceivedTypes[perceived.Extension] = perceived;
        }
        return perceived;
    }

    private static readonly ConcurrentDictionary<string, Perceived> _perceivedTypes = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions _options = new() { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private sealed class PerceivedTypesFile
    {
        public bool ReadHardCodedValuesBefore { get; set; } = true;
        public bool ReadHardCodedValuesAfter { get; set; }
        public IList<PerceivedTypeItem> PerceivedTypes { get; set; } = [];
    }

    private sealed class PerceivedTypeItem
    {
        public string Extension { get; set; } = null!;
        public PerceivedType PerceivedType { get; set; }
    }

    static void ReadHardcodedPerceivedTypes()
    {
        AddPerceived(".appxmanifest", PerceivedType.Text);
        AddPerceived(".asax", PerceivedType.Text);
        AddPerceived(".ascx", PerceivedType.Text);
        AddPerceived(".ashx", PerceivedType.Text);
        AddPerceived(".asmx", PerceivedType.Text);
        AddPerceived(".asp", PerceivedType.Text);
        AddPerceived(".axml", PerceivedType.Text);
        AddPerceived(".bas", PerceivedType.Text);
        AddPerceived(".bash", PerceivedType.Text);
        AddPerceived(".bat", PerceivedType.Text);
        AddPerceived(".btproj", PerceivedType.Text);
        AddPerceived(".c", PerceivedType.Text);
        AddPerceived(".cake", PerceivedType.Text);
        AddPerceived(".cbl", PerceivedType.Text);
        AddPerceived(".class", PerceivedType.Text);
        AddPerceived(".cmake", PerceivedType.Text);
        AddPerceived(".cmd", PerceivedType.Text);
        AddPerceived(".cob", PerceivedType.Text);
        AddPerceived(".cpp", PerceivedType.Text);
        AddPerceived(".cs", PerceivedType.Text);
        AddPerceived(".cshtml", PerceivedType.Text);
        AddPerceived(".css", PerceivedType.Text);
        AddPerceived(".cxx", PerceivedType.Text);
        AddPerceived(".cfg", PerceivedType.Text);
        AddPerceived(".config", PerceivedType.Text);
        AddPerceived(".cbproj", PerceivedType.Text);
        AddPerceived(".crproj", PerceivedType.Text);
        AddPerceived(".csproj", PerceivedType.Text);
        AddPerceived(".dproj", PerceivedType.Text);
        AddPerceived(".dbproj", PerceivedType.Text);
        AddPerceived(".dbschema", PerceivedType.Text);
        AddPerceived(".disco", PerceivedType.Text);
        AddPerceived(".deploymanifest", PerceivedType.Text);
        AddPerceived(".diagram", PerceivedType.Text);
        AddPerceived(".dotsettings", PerceivedType.Text);
        AddPerceived(".editorconfig", PerceivedType.Text);
        AddPerceived(".edmx", PerceivedType.Text);
        AddPerceived(".eml", PerceivedType.Text);
        AddPerceived(".frm", PerceivedType.Text);
        AddPerceived(".fs", PerceivedType.Text);
        AddPerceived(".fsproj", PerceivedType.Text);
        AddPerceived(".go", PerceivedType.Text);
        AddPerceived(".h", PerceivedType.Text);
        AddPerceived(".htm", PerceivedType.Text);
        AddPerceived(".html", PerceivedType.Text);
        AddPerceived(".hpp", PerceivedType.Text);
        AddPerceived(".hxx", PerceivedType.Text);
        AddPerceived(".iqy", PerceivedType.Text);
        AddPerceived(".inf", PerceivedType.Text);
        AddPerceived(".ini", PerceivedType.Text);
        AddPerceived(".isl", PerceivedType.Text);
        AddPerceived(".isproj", PerceivedType.Text);
        AddPerceived(".java", PerceivedType.Text);
        AddPerceived(".js", PerceivedType.Text);
        AddPerceived(".json", PerceivedType.Text);
        AddPerceived(".l4g", PerceivedType.Text);
        AddPerceived(".log", PerceivedType.Text);
        AddPerceived(".licx", PerceivedType.Text);
        AddPerceived(".master", PerceivedType.Text);
        AddPerceived(".manifest", PerceivedType.Text);
        AddPerceived(".md", PerceivedType.Text);
        AddPerceived(".modelproj", PerceivedType.Text);
        AddPerceived(".nuspec", PerceivedType.Text);
        AddPerceived(".pas", PerceivedType.Text);
        AddPerceived(".package", PerceivedType.Text);
        AddPerceived(".pbxproj", PerceivedType.Text);
        AddPerceived(".plist", PerceivedType.Text);
        AddPerceived(".props", PerceivedType.Text);
        AddPerceived(".proj", PerceivedType.Text);
        AddPerceived(".ps1", PerceivedType.Text);
        AddPerceived(".psd1", PerceivedType.Text);
        AddPerceived(".psm1", PerceivedType.Text);
        AddPerceived(".py", PerceivedType.Text);
        AddPerceived(".rdl", PerceivedType.Text);
        AddPerceived(".readme", PerceivedType.Text);
        AddPerceived(".reg", PerceivedType.Text);
        AddPerceived(".resx", PerceivedType.Text);
        AddPerceived(".rs", PerceivedType.Text);
        AddPerceived(".rsp", PerceivedType.Text);
        AddPerceived(".rtf", PerceivedType.Text);
        AddPerceived(".rzt", PerceivedType.Text);
        AddPerceived(".schemaview", PerceivedType.Text);
        AddPerceived(".sh", PerceivedType.Text);
        AddPerceived(".shproj", PerceivedType.Text);
        AddPerceived(".sitemap", PerceivedType.Text);
        AddPerceived(".sln", PerceivedType.Text);
        AddPerceived(".spdata", PerceivedType.Text);
        AddPerceived(".sql", PerceivedType.Text);
        AddPerceived(".sqlproj", PerceivedType.Text);
        AddPerceived(".sqlcmdvars", PerceivedType.Text);
        AddPerceived(".sqldeployment", PerceivedType.Text);
        AddPerceived(".sqlsettings", PerceivedType.Text);
        AddPerceived(".snippet", PerceivedType.Text);
        AddPerceived(".storyboard", PerceivedType.Text);
        AddPerceived(".svc", PerceivedType.Text);
        AddPerceived(".svcinfo", PerceivedType.Text);
        AddPerceived(".svcmap", PerceivedType.Text);
        AddPerceived(".targets", PerceivedType.Text);
        AddPerceived(".tcl", PerceivedType.Text);
        AddPerceived(".tpl", PerceivedType.Text);
        AddPerceived(".tplxaml", PerceivedType.Text);
        AddPerceived(".vb", PerceivedType.Text);
        AddPerceived(".vbhtml", PerceivedType.Text);
        AddPerceived(".vbp", PerceivedType.Text);
        AddPerceived(".vbproj", PerceivedType.Text);
        AddPerceived(".vbs", PerceivedType.Text);
        AddPerceived(".vcproj", PerceivedType.Text);
        AddPerceived(".vcxproj", PerceivedType.Text);
        AddPerceived(".vdproj", PerceivedType.Text);
        AddPerceived(".vsconfig", PerceivedType.Text);
        AddPerceived(".wapproj", PerceivedType.Text);
        AddPerceived(".webpart", PerceivedType.Text);
        AddPerceived(".wsdl", PerceivedType.Text);
        AddPerceived(".wxi", PerceivedType.Text);
        AddPerceived(".wxl", PerceivedType.Text);
        AddPerceived(".wxs", PerceivedType.Text);
        AddPerceived(".wixlib", PerceivedType.Text);
        AddPerceived(".vixproj", PerceivedType.Text);
        AddPerceived(".xaml", PerceivedType.Text);
        AddPerceived(".xsd", PerceivedType.Text);
        AddPerceived(".xsl", PerceivedType.Text);
        AddPerceived(".xslt", PerceivedType.Text);
        AddPerceived(".yml", PerceivedType.Text);
        AddPerceived(".yaml", PerceivedType.Text);

        AddPerceived(".dll", PerceivedType.Application);
        AddPerceived(".exe", PerceivedType.Application);

        AddPerceived(".pdb", PerceivedType.Custom);

        AddPerceived(".dacpac", PerceivedType.Compressed);
        AddPerceived(".nupkg", PerceivedType.Compressed);
        AddPerceived(".rar", PerceivedType.Compressed);
        AddPerceived(".7z", PerceivedType.Compressed);
    }

    static Perceived()
    {
        if (IOUtilities.PathIsFile(Settings.PerceivedTypesFilePath))
        {
            try
            {
                using var stream = new FileStream(Settings.PerceivedTypesFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var file = JsonSerializer.Deserialize<PerceivedTypesFile>(stream, _options);
                if (file != null)
                {
                    if (file.ReadHardCodedValuesBefore)
                    {
                        ReadHardcodedPerceivedTypes();
                    }

                    foreach (var item in file.PerceivedTypes)
                    {
                        if (item.Extension == null)
                            continue;

                        AddPerceived(item.Extension, item.PerceivedType, PerceivedTypeSource.DoxieSettings);
                    }

                    if (file.ReadHardCodedValuesAfter)
                    {
                        ReadHardcodedPerceivedTypes();
                    }
                    return;
                }
            }
            catch
            {
                // continue;
            }
        }

        ReadHardcodedPerceivedTypes();

        //var file = new PerceivedTypesFile { PerceivedTypes = [.. _perceivedTypes.Values.Select(p => new PerceivedTypeItem { PerceivedType = p.PerceivedType, Extension = p.Extension })] };
        //using var stream = new FileStream(Settings.PerceivedTypesFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
        //JsonSerializer.Serialize(stream, file, _options);
    }

    public static Perceived AddPerceived(string extension, PerceivedType type, PerceivedTypeSource source = PerceivedTypeSource.HardCoded)
    {
        ArgumentNullException.ThrowIfNull(extension);
        var perceived = new Perceived(extension)
        {
            PerceivedType = type,
            PerceivedTypeSource = source
        };
        _perceivedTypes[perceived.Extension] = perceived;
        return perceived;
    }

    [DllImport("shlwapi")]
    private static extern int AssocGetPerceivedType([MarshalAs(UnmanagedType.LPWStr)] string pszExt, ref PerceivedType ptype, ref PerceivedTypeSource pflag, ref IntPtr ppszType);
}
