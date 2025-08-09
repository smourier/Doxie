namespace Doxie;

public class Settings : Serializable<Settings>
{
    public const string FileName = "settings.json";
    public const string PerceivedTypesFileName = "perceivedTypes.json";

    public static Settings Current { get; }
    public static string ConfigurationFilePath { get; }
    public static string PerceivedTypesFilePath { get; }
    public static string TempDirectoryPath { get; }

    static Settings()
    {
        // data is stored in user's Documents
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), typeof(Settings).Namespace!);

        ConfigurationFilePath = Path.Combine(path, FileName);
        PerceivedTypesFilePath = Path.Combine(path, PerceivedTypesFileName);
        TempDirectoryPath = Path.Combine(Path.GetTempPath(), typeof(Settings).Namespace!);

        // build settings
        Current = Deserialize(ConfigurationFilePath)!;

        // force settings from cmdline
        foreach (var arg in CommandLine.NamedArguments)
        {
            var pi = Current.GetType().GetProperty(arg.Key);
            if (pi == null || !pi.CanWrite)
                continue;

            if (pi.PropertyType == typeof(bool) && string.IsNullOrWhiteSpace(arg.Value))
            {
                try
                {
                    pi.SetValue(Current, true);
                }
                catch
                {
                    // continue;
                }
                continue;
            }

            if (Conversions.TryChangeObjectType(arg.Value, pi.PropertyType, out var value))
            {
                try
                {
                    pi.SetValue(Current, value);
                }
                catch
                {
                    // continue
                }
            }
        }
    }

    public virtual void SerializeToConfiguration() => Serialize(ConfigurationFilePath);
    public static void BackupConfiguration() => Backup(ConfigurationFilePath);

    private readonly List<RecentFile> _recentFiles = [];

    [Browsable(false)]
    public virtual IList<RecentFile> RecentFiles
    {
        get => _recentFiles;
        set
        {
            _recentFiles.Clear();
            if (value != null)
            {
                _recentFiles.AddRange(value);
            }
        }
    }

    // "vs", "vs-dark", "hc-light", "hc-black" 
    [DefaultValue("vs-dark")]
    public virtual string MonacoTheme { get => GetPropertyValue("vs-dark")!; set { SetPropertyValue(value); } }

    [DefaultValue(13d)]
    public virtual double MonacoFontSize { get => GetPropertyValue(13d); set { SetPropertyValue(value); } }

    [DefaultValue(true)]
    public virtual bool MonacoShowMinimap { get => GetPropertyValue(true); set { SetPropertyValue(value); } }

    [DefaultValue(EncodingDetectorMode.AutoDetect)]
    public virtual EncodingDetectorMode EncodingDetectorMode { get => GetPropertyValue(EncodingDetectorMode.AutoDetect); set { SetPropertyValue(value); } }

    private Dictionary<string, DateTime> GetRecentFiles()
    {
        var dic = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        foreach (var recent in RecentFiles)
        {
            if (recent?.FilePath == null)
                continue;

            if (!IOUtilities.PathIsFile(recent.FilePath))
                continue;

            dic[recent.FilePath] = recent.LastAccessTime;
        }
        return dic;
    }

    private void SaveRecentFiles(Dictionary<string, DateTime> dic)
    {
        var list = dic.Select(kv => new RecentFile { FilePath = kv.Key, LastAccessTime = kv.Value }).OrderByDescending(r => r.LastAccessTime).ToList();
        if (list.Count == 0)
        {
            RecentFiles = [];
        }
        else
        {
            RecentFiles = list;
        }
    }

    public void CleanRecentFiles() => SaveRecentFiles(GetRecentFiles());
    public void ClearRecentFiles()
    {
        RecentFiles = [];
        SerializeToConfiguration();
    }

    public void AddRecentFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        if (!IOUtilities.PathIsFile(filePath))
            return;

        var dic = GetRecentFiles();
        dic[filePath] = DateTime.Now;
        SaveRecentFiles(dic);
        SerializeToConfiguration();
    }
}
