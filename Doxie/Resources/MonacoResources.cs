namespace Doxie.Resources;

public static class MonacoResources
{
    [NotNull]
    [MaybeNull]
    private static string MonacoFilesDirectoryPath { get; set; }

    [NotNull]
    [MaybeNull]
    public static string IndexFilePath { get; set; }

    private const string _cobolLanguageName = @"Languages\cobol.js";
    private static Task? _ensureMonacoFilesTask;
    private static readonly Lock _ensureMonacoFilesLock = new();

    public static Task EnsureMonacoFilesAsync()
    {
        if (_ensureMonacoFilesTask != null)
            return _ensureMonacoFilesTask;

        lock (_ensureMonacoFilesLock)
        {
            _ensureMonacoFilesTask ??= Task.Run(EnsureMonacoFiles);
            return _ensureMonacoFilesTask;
        }
    }

    public static void EnsureMonacoFiles()
    {
        var asm = Assembly.GetExecutingAssembly();
        var startTok = typeof(MonacoResources).Namespace + ".vs.";
        const string ext = ".zip";
        var zip = asm.GetManifestResourceNames().FirstOrDefault(n => n.StartsWith(startTok) && n.EndsWith(ext)) ?? throw new InvalidOperationException();
        var version = zip.Substring(startTok.Length, zip.Length - startTok.Length - ext.Length);
        MonacoFilesDirectoryPath = Path.Combine(Settings.TempDirectoryPath, "Monaco", version);

        const string indexName = "index.html";
        IndexFilePath = Path.Combine(MonacoFilesDirectoryPath, indexName);
        WritePageFile(asm, indexName);
        WritePageFile(asm, _cobolLanguageName);

        // we check the last known file is there
        var someFile = Path.Combine(MonacoFilesDirectoryPath, @"vs\language\typescript\monaco.contribution.js");
        var fi = new FileInfo(someFile);
        if (fi.Exists && fi.Length > 0)
        {
            _ensureMonacoFilesTask = null;
            return;
        }

        DeleteOtherVersions();
        using var stream = asm.GetManifestResourceStream(zip) ?? throw new InvalidOperationException();
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        archive.ExtractToDirectory(MonacoFilesDirectoryPath, true);
        _ensureMonacoFilesTask = null;
    }

    // a page file is embedded under its path with dots for separators, Languages\cobol.js as Languages.cobol.js.
    private static void WritePageFile(Assembly asm, string relativePath)
    {
        var resourceName = typeof(MonacoResources).Namespace + "." + relativePath.Replace(Path.DirectorySeparatorChar, '.');
        using var resourceStream = asm.GetManifestResourceStream(resourceName) ?? throw new InvalidOperationException();

        var filePath = Path.Combine(MonacoFilesDirectoryPath, relativePath);
        IOUtilities.FileEnsureDirectory(filePath);
        using var file = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        resourceStream.CopyTo(file);
    }

    private static void DeleteOtherVersions()
    {
        var root = Path.GetDirectoryName(MonacoFilesDirectoryPath)!;
        foreach (var folder in Directory.EnumerateDirectories(root))
        {
            if (folder.EqualsIgnoreCase(MonacoFilesDirectoryPath))
                continue;

            try
            {
                Directory.Delete(folder, true);
            }
            catch (Exception ex)
            {
                EventProvider.Default.WriteMessage("Error deleting '" + folder + "': " + ex.Message);
            }
        }
    }
}
