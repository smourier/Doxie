using Lucene.Net.Analysis.Standard;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Util;
using SqlNado;

namespace Doxie.Model;

public class Index : INotifyPropertyChanged, IDisposable
{
    public const string FileExtension = ".doxidx";
    public const string DefaultFieldName = "corpus";
    public const string FieldPath = "path";
    public const string FieldExt = "ext";
    public const string FieldBatchId = "batchId";

    // database settings
    private const string _version = "version";
    private const string _includedFileExtensions = "includedFileExtensions";
    private const string _excludedDirectoryNames = "excludedDirectoryNames";
    private const char _dbSeparator = '|';

    public event EventHandler<FileIndexingEventArgs>? FileIndexing;
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly SqliteDirectory _sqlDirectory;
    private readonly StandardAnalyzer _analyzer;
    private readonly IndexWriter? _writer;
    private readonly DirectoryReader? _reader;
    private readonly IndexSearcher? _searcher;
    private bool _disposedValue;

    private Index(SqliteDirectory directory)
    {
        _sqlDirectory = directory;
        _analyzer = new StandardAnalyzer(LuceneVersion.LUCENE_48);
        if (!directory.Database.OpenOptions.HasFlag(SQLiteOpenOptions.SQLITE_OPEN_READONLY))
        {
            _sqlDirectory.SaveSetting(_version, AssemblyUtilities.GetInformationalVersion());
            var config = new IndexWriterConfig(LuceneVersion.LUCENE_48, _analyzer);
            _writer = new IndexWriter(_sqlDirectory, config);
        }
        else
        {
            _reader = DirectoryReader.Open(_sqlDirectory);
            _searcher = new IndexSearcher(_reader);
        }

        UpdateDirectories();
        UpdateSettings();
    }

    public string FilePath => _sqlDirectory.Database.FilePath;
    public string? Version => _sqlDirectory.LoadNullifiedSetting(_version);
    public string Name => Path.GetFileNameWithoutExtension(_sqlDirectory.Database.FilePath);
    public bool VacuumOnCommit { get; set; } = true;
    public bool IsReadOnly => _writer == null;
    public bool IsWriteOnly => !IsReadOnly;
    public bool IsIndexing { get; private set; }
    public int DocumentsCount
    {
        get
        {
            if (_writer != null)
                return _writer.NumDocs;

            return _reader?.NumDocs ?? 0;
        }
    }

    public ObservableCollection<IndexDirectory> Directories { get; } = [];
    public ObservableCollection<string> IncludedFileExtensions { get; } = [];
    public ObservableCollection<string> ExcludedDirectoryNames { get; } = [];

    public long FileSize
    {
        get
        {
            try
            {
                return new FileInfo(_sqlDirectory.Database.FilePath).Length;
            }
            catch (Exception)
            {
                return 0; // file might not exist or be inaccessible
            }
        }
    }

    public override string ToString() => Name;

    public Task<IndexScanResult> Scan(IndexScanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.AsyncProcessing)
            return Task.FromResult(DoScan(request));

        return Task.Run(() => DoScan(request));
    }

    private IndexScanResult DoScan(IndexScanRequest request)
    {
        var result = new IndexScanResult();
        try
        {
            DoScan(request, result);
        }
        catch (Exception ex)
        {
            if (Debugger.IsAttached)
                throw;

            result.Exception = ex;
        }
        return result;
    }

    protected virtual void OnFileIndexing(object sender, FileIndexingEventArgs e) => FileIndexing?.Invoke(sender, e);
    protected virtual void OnPropertyChanged(object sender, PropertyChangedEventArgs e) => PropertyChanged?.Invoke(sender, e);
    public void OnPropertyChanged([CallerMemberName] string? propertyName = null) => OnPropertyChanged(this, new PropertyChangedEventArgs(propertyName));

    protected virtual void UpdateDirectories()
    {
        var directories = _sqlDirectory.Database.LoadAll<Directory>();
        Directories.UpdateWith(directories.Select(d => d.ToIndexDirectory(this, d)) ?? [], (existing, p) => existing.Update(p));
    }

    protected virtual void UpdateSettings()
    {
        IncludedFileExtensions.AddRange(Conversions.SplitToNullifiedList(_sqlDirectory.LoadNullifiedSetting(_includedFileExtensions), [_dbSeparator]));
        ExcludedDirectoryNames.AddRange(Conversions.SplitToNullifiedList(_sqlDirectory.LoadNullifiedSetting(_excludedDirectoryNames), [_dbSeparator]));
    }

    protected virtual void SaveIncludedFileExtensions() => _sqlDirectory.SaveSetting(_includedFileExtensions, string.Join(_dbSeparator, IncludedFileExtensions));
    protected virtual void SaveExcludedDirectoryNames() => _sqlDirectory.SaveSetting(_excludedDirectoryNames, string.Join(_dbSeparator, ExcludedDirectoryNames));
    protected virtual void SaveDirectory(IndexDirectory dir)
    {
        ArgumentNullException.ThrowIfNull(dir);
        if (dir.Path == null)
            throw new InvalidOperationException();

        _sqlDirectory.Save(new Directory(_sqlDirectory.Database) { Path = dir.Path });
    }

    public virtual bool EnsureDirectory(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var dir = new IndexDirectory(this, path);
        if (Directories.Any(n => n.Path.EqualsIgnoreCase(path)))
        {
            SaveDirectory(dir);
            return false;
        }

        Directories.Add(dir);
        SaveDirectory(dir);
        OnPropertyChanged(nameof(Directories));
        return true;
    }

    public virtual bool RemoveDirectory(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var dir = new IndexDirectory(this, path);
        if (!Directories.Any(n => n.Path.EqualsIgnoreCase(path)))
            return false;

        Directories.Remove(dir);
        DeleteDirectory(path);
        OnPropertyChanged(nameof(Directories));
        return true;
    }

    public virtual bool RemoveIncludedFileExtension(string ext)
    {
        ArgumentNullException.ThrowIfNull(ext);
        if (!ext.StartsWith('.'))
        {
            ext = '.' + ext;
        }

        if (!IncludedFileExtensions.Any(n => n.EqualsIgnoreCase(ext)))
            return false;

        IncludedFileExtensions.Remove(ext);
        SaveIncludedFileExtensions();
        OnPropertyChanged(nameof(IncludedFileExtensions));
        return true;
    }

    public virtual bool EnsureIncludedFileExtension(string ext)
    {
        ArgumentNullException.ThrowIfNull(ext);
        if (!ext.StartsWith('.'))
        {
            ext = '.' + ext;
        }

        if (IncludedFileExtensions.Any(n => n.EqualsIgnoreCase(ext)))
        {
            SaveIncludedFileExtensions();
            return false;
        }

        IncludedFileExtensions.Add(ext);
        SaveIncludedFileExtensions();
        OnPropertyChanged(nameof(IncludedFileExtensions));
        return true;
    }

    public virtual bool RemoveExcludedDirectoryName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!ExcludedDirectoryNames.Any(n => n.EqualsIgnoreCase(name)))
            return false;

        ExcludedDirectoryNames.Remove(name);
        SaveExcludedDirectoryNames();
        OnPropertyChanged(nameof(ExcludedDirectoryNames));
        return true;
    }

    public virtual bool EnsureExcludedDirectoryName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (ExcludedDirectoryNames.Any(n => n.EqualsIgnoreCase(name)))
        {
            SaveExcludedDirectoryNames();
            return false;
        }

        ExcludedDirectoryNames.Add(name);
        SaveExcludedDirectoryNames();
        OnPropertyChanged(nameof(ExcludedDirectoryNames));
        return true;
    }

    protected virtual void DoScan(IndexScanRequest request, IndexScanResult result)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);

        EnsureDirectory(request.InputDirectory.Path);
        var batch = new IndexDirectoryBatch(request.InputDirectory, Guid.NewGuid())
        {
            StartTimeUtc = DateTime.UtcNow
        };
        SaveDirectoryBatch(batch);
        UpdateDirectories();

        var includedExts = (IncludedFileExtensions?.Select(e => e.ToLowerInvariant()) ?? []).ToHashSet();
        var excludedDirs = (ExcludedDirectoryNames?.Select(e => e.ToLowerInvariant()) ?? []).ToHashSet();
        var excludedExts = new HashSet<string>();

        var writer = GetWriter();
        foreach (var entry in System.IO.Directory.EnumerateFileSystemEntries(request.InputDirectory.Path, request.SearchPattern, request.EnumerationOptions))
        {
            if (request.CancellationTokenSource?.IsCancellationRequested == true)
            {
                batch.Options |= IndexDirectoryBatchOptions.IndexingWasCancelled;
                break;
            }

            var attributes = IOUtilities.PathGetAttributes(entry);
            if (attributes == null)
                continue;

            if (attributes.Value.HasFlag(FileAttributes.Directory))
            {
                batch.NumberOfSkippedDirectories++;
                continue;
            }

            var ext = Path.GetExtension(entry).ToLowerInvariant();
            //if (Perceived.GetPerceivedType(ext).PerceivedType != PerceivedType.Text)
            //{
            //    if (!batch.NonIndexedFileExtensions.Contains(ext))
            //    {
            //        batch.NonIndexedFileExtensions.Add(ext);
            //    }

            //    batch.NumberOfSkippedFiles++;
            //    continue;
            //}

            batch.EndTimeUtc = DateTime.UtcNow;
            var e = new FileIndexingEventArgs(batch, entry);
            OnFileIndexing(this, e);
            if (e.Cancel)
                continue;

            if (!includedExts.Contains(ext))
            {
                excludedExts.Add(ext);
                batch.NumberOfSkippedFiles++;
                continue;
            }

            var relPath = Path.GetRelativePath(request.InputDirectory.Path, entry);
            var file = File.ReadAllText(entry);

            var pathForIndex = relPath;

            var idx = new IndexDocument(DefaultFieldName);
            idx.AddField(DefaultFieldName, pathForIndex + " " + file.Trim());
            idx.AddField(FieldExt, ext, true);
            idx.AddField(FieldBatchId, batch.Id.ToString("N"), true);

            idx.AddField(FieldPath, relPath, true);

            var doc = idx.FinishAndGetDocument();
            writer.AddDocument(doc);
            batch.NumberOfDocuments++;
        }

        batch.EndTimeUtc = DateTime.UtcNow;
        batch.NonIndexedFileExtensions.AddRange(excludedExts);
        batch.IncludedFileExtensions.AddRange(includedExts);
        batch.ExcludedDirectoryNames.AddRange(excludedDirs);
        SaveDirectoryBatch(batch);
        UpdateDirectories();

        Commit();
        if (VacuumOnCommit)
        {
            try
            {
                _sqlDirectory.Database.Vacuum();
            }
            catch
            {
                // continue regardless of vacuum failure
            }
        }
    }

    protected virtual bool SaveDirectoryBatch(IndexDirectoryBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        // delete other batches' lucene documents
        var batches = _sqlDirectory.Database.Load<DirectoryBatch>($"SELECT * FROM {nameof(DirectoryBatch)} WHERE {nameof(DirectoryBatch.Directory)} = ?", batch.Directory.Path);
        foreach (var deleteDataBatch in batches.Where(b => b.Id != batch.Id))
        {
            DeleteDocuments(deleteDataBatch);
        }

        var db = DirectoryBatch.From(new Directory(_sqlDirectory.Database) { Path = batch.Directory.Path }, batch);
        return _sqlDirectory.Save(db);
    }

    private void DeleteDocuments(DirectoryBatch batchData)
    {
        // delete all lucene documents in the batch
        GetWriter().DeleteDocuments(new Term(FieldBatchId, batchData.Id.ToString("N")));

        // remember it was deleted
        batchData.Options |= IndexDirectoryBatchOptions.DataWasDeleted;
        _sqlDirectory.Save(batchData);
    }

    public virtual bool DeleteDirectory(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        _sqlDirectory.Database.BeginTransaction();
        try
        {
            var ret = _sqlDirectory.Database.Delete(new Directory(_sqlDirectory.Database) { Path = path });
            if (_sqlDirectory.Database.ExecuteNonQuery($"DELETE FROM {nameof(DirectoryBatch)} WHERE {nameof(DirectoryBatch.Directory)} = ?", path) > 0)
            {
                ret = true;
            }
            _sqlDirectory.Database.Commit();
            OnPropertyChanged(nameof(Directories));
            return ret;
        }
        catch
        {
            _sqlDirectory.Database.Rollback();
            throw;
        }
    }

    public static Index OpenRead(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        try
        {
            var directory = new SqliteDirectory(filePath, SQLiteOpenOptions.SQLITE_OPEN_READONLY);
            return new Index(directory);
        }
        catch (Exception ex)
        {
            throw new Exception($"File '{filePath}' doesn't appear to be a valid Doxie index file.", ex);
        }
    }

    public static Index OpenWrite(string filePath, SQLiteOpenOptions options = SQLiteOpenOptions.SQLITE_OPEN_READWRITE | SQLiteOpenOptions.SQLITE_OPEN_CREATE)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        try
        {
            var directory = new SqliteDirectory(filePath, options);
            return new Index(directory);
        }
        catch (Exception ex)
        {
            throw new Exception($"File '{filePath}' doesn't appear to be a valid Doxie index file.", ex);
        }
    }

    private IndexWriter GetWriter()
    {
        if (_writer == null)
            throw new InvalidOperationException("Cannot perform write operations on a read-only index.");

        return _writer;
    }

    public virtual void Commit() => GetWriter().Commit();
    public virtual void DeleteAllItems(bool commit = true)
    {
        GetWriter().DeleteAll();
        if (commit)
        {
            Commit();
        }
    }

    public virtual void DeleteItems(string text, string defaultFieldName, bool commit = true)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(defaultFieldName);

        var parser = new QueryParser(LuceneVersion.LUCENE_48, defaultFieldName, _analyzer) { AllowLeadingWildcard = true, };
        var qry = parser.Parse(text);
        GetWriter().DeleteDocuments(qry);
        if (commit)
        {
            Commit();
        }
    }

    public SearchResult<SearchResultItem> Search(string text, int maximumDocuments = int.MaxValue)
        => Search<SearchResultItem>(text, maximumDocuments);

    public virtual SearchResult<T> Search<T>(string text, int maximumDocuments = int.MaxValue) where T : SearchResultItem, new()
    {
        ArgumentNullException.ThrowIfNull(text);
        if (_searcher == null)
            throw new InvalidOperationException("Cannot search in a write-only index.");

        var parser = new QueryParser(LuceneVersion.LUCENE_48, DefaultFieldName, _analyzer)
        {
            AllowLeadingWildcard = true,
        };

        var qry = parser.Parse(text);
        var topDocs = _searcher.Search(qry, maximumDocuments);

        var result = new SearchResult<T>
        {
            TotalHits = topDocs.TotalHits
        };

        var list = new List<T>();

        for (var i = 0; i < topDocs.ScoreDocs.Length; i++)
        {
            var doc = _searcher.Doc(topDocs.ScoreDocs[i].Doc);
            if (doc.Fields.Count == 0)
                continue;

            var item = new T { Index = list.Count + 1, Score = topDocs.ScoreDocs[i].Score };
            list.Add(item);

            foreach (var fld in doc)
            {
                switch (fld.NumericType)
                {
                    case Lucene.Net.Documents.NumericFieldType.BYTE:
                        item.AddField(fld.Name, fld.GetByteValue());
                        break;

                    case Lucene.Net.Documents.NumericFieldType.INT16:
                        item.AddField(fld.Name, fld.GetInt16Value());
                        break;

                    case Lucene.Net.Documents.NumericFieldType.INT32:
                        item.AddField(fld.Name, fld.GetInt32Value());
                        break;

                    case Lucene.Net.Documents.NumericFieldType.INT64:
                        item.AddField(fld.Name, fld.GetInt64Value());
                        break;

                    case Lucene.Net.Documents.NumericFieldType.SINGLE:
                        item.AddField(fld.Name, fld.GetSingleValue());
                        break;

                    case Lucene.Net.Documents.NumericFieldType.DOUBLE:
                        item.AddField(fld.Name, fld.GetDoubleValue());
                        break;

                    default:
                        item.AddField(fld.Name, fld.GetStringValue().Trim());
                        break;
                }
            }
        }

        result.Items = list;
        return result;
    }

    public void Dispose() { Dispose(disposing: true); GC.SuppressFinalize(this); }
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _analyzer?.Dispose();
                _writer?.Dispose();
                _reader?.Dispose();
                _sqlDirectory?.Dispose(); // must be last
            }

            _disposedValue = true;
        }
    }

    internal sealed class Directory(SQLiteDatabase db) : ISQLiteObject
    {
        [SQLiteColumn(IsPrimaryKey = true)]
        public string? Path { get; set; }
        public DateTime CreationTimUtc { get; set; } = DateTime.UtcNow;
        public IEnumerable<DirectoryBatch> Batches => ((ISQLiteObject)this).Database?.LoadByForeignKey<DirectoryBatch>(this).WhereNotNull() ?? [];

        SQLiteDatabase? ISQLiteObject.Database { get; set; } = db;
        public override string ToString() => Path ?? string.Empty;

        public IndexDirectory ToIndexDirectory(Index index, Directory directory)
        {
            if (directory.Path == null)
                throw new InvalidOperationException();

            var id = new IndexDirectory(index, directory.Path);
            foreach (var batch in index._sqlDirectory.Database.Load<DirectoryBatch>($"SELECT * FROM {nameof(DirectoryBatch)} WHERE {nameof(DirectoryBatch.Directory)} = ?", directory.Path))
            {
                id.Batches.Add(batch.ToIndexDirectoryBatch(id));
            }
            return id;
        }
    }

    internal sealed class DirectoryBatch
    {
        [SQLiteColumn(IsPrimaryKey = true)]
        public Guid Id { get; set; } = Guid.NewGuid();
        public Directory? Directory { get; set; }
        public IndexDirectoryBatchOptions Options { get; set; }
        public DateTime StartTimeUtc { get; set; }
        public DateTime EndTimeUtc { get; set; }
        public int NumberOfDocuments { get; set; }
        public int NumberOfSkippedFiles { get; set; }
        public string? IncludedFileExtensions { get; set; }
        public string? ExcludedDirectoryNames { get; set; }
        public string? NonIndexedFileExtensions { get; set; }

        [SQLiteColumn(Ignore = true)]
        public TimeSpan Duration => EndTimeUtc - StartTimeUtc;

        public override string ToString() => Id.ToString();

        public IndexDirectoryBatch ToIndexDirectoryBatch(IndexDirectory directory)
        {
            var batch = new IndexDirectoryBatch(directory, Id)
            {
                Options = Options,
                StartTimeUtc = StartTimeUtc,
                EndTimeUtc = EndTimeUtc,
                NumberOfDocuments = NumberOfDocuments,
                NumberOfSkippedFiles = NumberOfSkippedFiles,
            };

            batch.IncludedFileExtensions.AddRange(IncludedFileExtensions?.Split(_dbSeparator) ?? []);
            batch.ExcludedDirectoryNames.AddRange(ExcludedDirectoryNames?.Split(_dbSeparator) ?? []);
            batch.NonIndexedFileExtensions.AddRange(NonIndexedFileExtensions?.Split(_dbSeparator) ?? []);
            return batch;
        }

        public static DirectoryBatch From(Directory directory, IndexDirectoryBatch batch) => new()
        {
            Id = batch.Id,
            Directory = directory,
            Options = batch.Options,
            StartTimeUtc = batch.StartTimeUtc,
            EndTimeUtc = batch.EndTimeUtc,
            NumberOfDocuments = batch.NumberOfDocuments,
            NumberOfSkippedFiles = batch.NumberOfSkippedFiles,
            IncludedFileExtensions = string.Join(_dbSeparator, batch.IncludedFileExtensions),
            ExcludedDirectoryNames = string.Join(_dbSeparator, batch.ExcludedDirectoryNames),
            NonIndexedFileExtensions = string.Join(_dbSeparator, batch.NonIndexedFileExtensions),
        };
    }
}
