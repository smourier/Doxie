using Doxie.Model.Highlighting;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Analysis.Util;
using Lucene.Net.Documents;
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
    public const string FieldRelPath = "path";
    public const string FieldDirectoryId = "did";
    public const string FieldExt = "ext";
    public const string FieldBatchId = "bid";
    public const string FieldLinesCount = "lines";

    // database settings
    private const string _version = "version";
    private const string _inclusions = "inclusions";
    private const string _excludedDirectoryNames = "excludedDirectoryNames";
    private const char _dbSeparator = '|';

    public event EventHandler<IndexingEventArgs>? FileIndexing;
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
    public ObservableCollection<InclusionDefinition> Inclusions { get; } = [];
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

    protected virtual void OnFileIndexing(object sender, IndexingEventArgs e) => FileIndexing?.Invoke(sender, e);
    protected virtual void OnPropertyChanged(object sender, PropertyChangedEventArgs e) => PropertyChanged?.Invoke(sender, e);
    public void OnPropertyChanged([CallerMemberName] string? propertyName = null) => OnPropertyChanged(this, new PropertyChangedEventArgs(propertyName));

    protected virtual void UpdateDirectories() => Extensions.WrapDispatcher(() =>
    {
        var directories = _sqlDirectory.Database.LoadAll<Directory>();
        Directories.UpdateWith(directories.Select(d => d.ToIndexDirectory(this, d)) ?? [], (existing, p) => existing.Update(p));
    });

    protected virtual void UpdateSettings() => Extensions.WrapDispatcher(() =>
    {
        Inclusions.AddRange(Conversions.SplitToNullifiedList(_sqlDirectory.LoadNullifiedSetting(_inclusions), [_dbSeparator]).Select(InclusionDefinition.Deserialize).WhereNotNull());
        ExcludedDirectoryNames.AddRange(Conversions.SplitToNullifiedList(_sqlDirectory.LoadNullifiedSetting(_excludedDirectoryNames), [_dbSeparator]));
    });

    protected virtual void SaveInclusions() => _sqlDirectory.SaveSetting(_inclusions, string.Join(_dbSeparator, Inclusions.Select(i => i.Serialize())));
    protected virtual void SaveExcludedDirectoryNames() => _sqlDirectory.SaveSetting(_excludedDirectoryNames, string.Join(_dbSeparator, ExcludedDirectoryNames));
    protected virtual void SaveDirectory(IndexDirectory dir)
    {
        ArgumentNullException.ThrowIfNull(dir);
        if (dir.Path == null || dir.Id <= 0)
            throw new InvalidOperationException();

        _sqlDirectory.Save(new Directory(_sqlDirectory.Database) { Path = dir.Path, Id = dir.Id });
    }

    public virtual IndexDirectory EnsureDirectory(string path) => Extensions.WrapDispatcher(() =>
    {
        ArgumentNullException.ThrowIfNull(path);
        var dir = Directories.FirstOrDefault(n => n.Path.EqualsIgnoreCase(path));
        dir ??= WithTransaction(() =>
        {
            var max = _sqlDirectory.Database.ExecuteScalar($"SELECT MAX({nameof(Directory.Id)}) FROM {nameof(Directory)}", 0);
            dir = new IndexDirectory(this, max + 1, path);
            SaveDirectory(dir);
            Directories.Add(dir);
            OnPropertyChanged(nameof(Directories));
            return dir;
        });
        return dir;
    });

    public virtual bool RemoveDirectory(string path) => Extensions.WrapDispatcher(() =>
    {
        ArgumentNullException.ThrowIfNull(path);
        var dir = Directories.FirstOrDefault(n => n.Path.EqualsIgnoreCase(path));
        if (dir == null)
            return false;

        Directories.Remove(dir);
        DeleteDirectory(dir);
        OnPropertyChanged(nameof(Directories));
        return true;
    });

    public virtual bool RemoveInclusion(InclusionDefinition definition) => Extensions.WrapDispatcher(() =>
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (!Inclusions.Any(n => n.Equals(definition)))
            return false;

        Inclusions.Remove(definition);
        SaveInclusions();
        OnPropertyChanged(nameof(Inclusions));
        return true;
    });

    public bool EnsureIncludedFileExtension(string ext)
    {
        ArgumentNullException.ThrowIfNull(ext);
        if (!ext.StartsWith('.'))
        {
            ext = '.' + ext;
        }

        var inclusion = InclusionDefinition.Parse(ext);
        if (inclusion == null)
            return false;

        return EnsureInclusion(inclusion);
    }

    public virtual bool EnsureInclusion(InclusionDefinition definition) => Extensions.WrapDispatcher(() =>
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (Inclusions.Any(n => n.Equals(definition)))
        {
            SaveInclusions();
            return false;
        }

        Inclusions.Add(definition);
        SaveInclusions();
        OnPropertyChanged(nameof(Inclusions));
        return true;
    });

    public virtual bool RemoveExcludedDirectoryName(string name) => Extensions.WrapDispatcher(() =>
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!ExcludedDirectoryNames.Any(n => n.EqualsIgnoreCase(name)))
            return false;

        ExcludedDirectoryNames.Remove(name);
        SaveExcludedDirectoryNames();
        OnPropertyChanged(nameof(ExcludedDirectoryNames));
        return true;
    });

    public virtual bool EnsureExcludedDirectoryName(string name) => Extensions.WrapDispatcher(() =>
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
    });

    public virtual bool Vacuum()
    {
        try
        {
            _sqlDirectory.Database.Vacuum();
            return true;
        }
        catch
        {
            return false;
        }
    }

    protected virtual void DoScan(IndexScanRequest request, IndexScanResult result)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);

        var dir = EnsureDirectory(request.InputDirectory.Path);
        DeleteDocuments(dir);
        var batch = new IndexDirectoryBatch(request.InputDirectory, Guid.NewGuid())
        {
            StartTimeUtc = DateTime.UtcNow
        };

        WithTransaction(() =>
        {
            SaveDirectoryBatch(batch);
            UpdateDirectories();

            var inclusions = (Inclusions ?? []).ToHashSet();
            var excludedDirs = (ExcludedDirectoryNames?.Select(e => e.ToLowerInvariant()) ?? []).ToHashSet();
            var excludedExts = new HashSet<string>();

            // we do it ourselves
            request.EnumerationOptions.RecurseSubdirectories = false;

            var writer = GetWriter();
            indexDirectory(request.InputDirectory.Path);

            batch.EndTimeUtc = DateTime.UtcNow;
            batch.NonIndexedFileExtensions.AddRange(excludedExts);
            batch.Inclusions.AddRange(inclusions);
            batch.ExcludedDirectoryNames.AddRange(excludedDirs);
            SaveDirectoryBatch(batch);
            UpdateDirectories();
            writer.Commit();

            bool indexDirectory(string directoryPath)
            {
                foreach (var file in System.IO.Directory.EnumerateFiles(directoryPath, request.FileSearchPattern, request.EnumerationOptions))
                {
                    if (request.CancellationTokenSource?.IsCancellationRequested == true)
                    {
                        batch.Options |= IndexDirectoryBatchOptions.IndexingWasCancelled;
                        return false;
                    }

                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext == FileExtension) // never index doxie files
                    {
                        batch.NumberOfSkippedFiles++;
                        continue;
                    }

                    batch.EndTimeUtc = DateTime.UtcNow;
                    var e = new IndexingEventArgs(batch, file);
                    OnFileIndexing(this, e);
                    if (e.CancelIndexing)
                        return false;

                    if (e.Cancel)
                    {
                        batch.NumberOfSkippedFiles++;
                        continue;
                    }

                    if (!indexFile(file, ext))
                    {
                        batch.NumberOfSkippedFiles++;
                    }
                    else
                    {
                        batch.NumberOfDocuments++;
                    }
                }

                foreach (var dirPath in System.IO.Directory.EnumerateDirectories(directoryPath, request.DirectorySearchPattern, request.EnumerationOptions))
                {
                    if (request.CancellationTokenSource?.IsCancellationRequested == true)
                    {
                        batch.Options |= IndexDirectoryBatchOptions.IndexingWasCancelled;
                        return false;
                    }

                    var dirName = Path.GetFileName(dirPath).ToLowerInvariant();

                    // note excluded dir is a file name pattern for which to search
                    // https://learn.microsoft.com/en-us/windows/win32/api/shlwapi/nf-shlwapi-pathmatchspecexw
                    if (excludedDirs.Any(d => IOUtilities.PathMatchSpec(dirName, d)))
                    {
                        batch.NumberOfSkippedDirectories++;
                        continue;
                    }

                    // recurse into subdirectories
                    if (!indexDirectory(dirPath))
                        return false; // cancelled
                }
                return true;
            }

            bool indexFile(string entry, string ext)
            {
                // run all exclusions first
                if (inclusions.Where(i => i.IsExclusion).Any(d => d.Matches(entry)))
                    return false;

                if (!inclusions.Where(i => !i.IsExclusion).Any(d => d.Matches(entry)))
                {
                    excludedExts.Add(ext);
                    return false;
                }

                var relPath = Path.GetRelativePath(request.InputDirectory.Path, entry);
                string[] lines;
                try
                {
                    var encoding = EncodingDetector.DetectEncoding(entry, Settings.Current.EncodingDetectorMode);
                    lines = File.ReadAllLines(entry, encoding);
                }
                catch
                {
                    return false;
                }

                var pathForIndex = relPath;

                var idx = new IndexDocument(DefaultFieldName);
                idx.AddField(DefaultFieldName, pathForIndex + " " + string.Join(Environment.NewLine, lines));

                var fext = ext;
                if (fext.StartsWith('.'))
                {
                    fext = fext[1..]; // remove leading dot
                }
                idx.AddField(FieldExt, fext, true);
                idx.AddField(FieldLinesCount, lines.Length, true);
                idx.AddField(FieldBatchId, batch.Id.ToString("N"), true);
                idx.AddField(FieldDirectoryId, dir.Id, true);
                idx.AddField(FieldRelPath, relPath, true);

                var doc = idx.FinishAndGetDocument();
                writer.AddDocument(doc);
                return true;
            }
        });
    }

    protected virtual bool SaveDirectoryBatch(IndexDirectoryBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Directory == null)
            throw new InvalidOperationException();

        var db = DirectoryBatch.From(new Directory(_sqlDirectory.Database) { Path = batch.Directory.Path, Id = batch.Directory.Id, }, batch);
        return _sqlDirectory.Save(db);
    }

    public virtual void DeleteDocuments(IndexDirectory directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        if (directory.Path == null || directory.Id <= 0)
            throw new InvalidOperationException();

        WithTransaction(() => DeleteDocuments(new Directory(_sqlDirectory.Database) { Path = directory.Path, Id = directory.Id, }));
        Vacuum();
        Extensions.WrapDispatcher(() => OnPropertyChanged(nameof(Directories)));
    }

    private void DeleteDocuments(Directory directory)
    {
        var qry = NumericRangeQuery.NewInt32Range(FieldDirectoryId, directory.Id, directory.Id, true, true);
        var writer = GetWriter();
        writer.DeleteDocuments(qry);
        writer.Commit();
    }

    public virtual bool DeleteDirectory(IndexDirectory dir)
    {
        ArgumentNullException.ThrowIfNull(dir);
        var ret = WithTransaction(() =>
        {
            var dd = new Directory(_sqlDirectory.Database) { Id = dir.Id };
            DeleteDocuments(dd);
            var ret = _sqlDirectory.Database.Delete(dd);
            if (_sqlDirectory.Database.ExecuteNonQuery($"DELETE FROM {nameof(DirectoryBatch)} WHERE {nameof(DirectoryBatch.Directory)} = ?", dir.Id) > 0)
            {
                ret = true;
            }
            return ret;
        });

        Vacuum();
        Extensions.WrapDispatcher(() => OnPropertyChanged(nameof(Directories)));
        return ret;
    }

    public void WithTransaction(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        _sqlDirectory.Database.BeginTransaction();
        try
        {
            action();
            _sqlDirectory.Database.Commit();
        }
        catch
        {
            _sqlDirectory.Database.Rollback();
            throw;
        }
    }

    public T WithTransaction<T>(Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        _sqlDirectory.Database.BeginTransaction();
        try
        {
            var result = func();
            _sqlDirectory.Database.Commit();
            return result;
        }
        catch
        {
            _sqlDirectory.Database.Rollback();
            throw;
        }
    }

    public virtual void BuildTemplate(string targetFilePath)
    {
        ArgumentNullException.ThrowIfNull(targetFilePath);

        IOUtilities.FileOverwrite(FilePath, targetFilePath);

        using var writer = OpenWrite(targetFilePath);
        writer.WithTransaction(() =>
        {
            writer._sqlDirectory.PrepareForTemplate();
            writer._sqlDirectory.Database.DeleteAll(nameof(Directory));
            writer._sqlDirectory.Database.DeleteAll(nameof(DirectoryBatch));
        });
        writer.Vacuum();
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
            EventProvider.Default.WriteMessage("Error: " + ex);
            throw new Exception($"File '{filePath}' doesn't appear to be a valid Doxie V{AssemblyUtilities.GetInformationalVersion()} index file.", ex);
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
            EventProvider.Default.WriteMessage("Error: " + ex);
            throw new Exception($"File '{filePath}' doesn't appear to be a valid Doxie V{AssemblyUtilities.GetInformationalVersion()} index file.", ex);
        }
    }

    private IndexWriter GetWriter()
    {
        if (_writer == null)
            throw new InvalidOperationException("Cannot perform write operations on a read-only index.");

        return _writer;
    }

    protected virtual void WriterCommit() => GetWriter().Commit();
    public virtual void DeleteAllItems(bool commit = true)
    {
        GetWriter().DeleteAll();
        if (commit)
        {
            WriterCommit();
        }
    }

    public virtual void DeleteItems(string query, string defaultFieldName, bool commit = true)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(defaultFieldName);

        var parser = new QueryParser(LuceneVersion.LUCENE_48, defaultFieldName, _analyzer) { AllowLeadingWildcard = true, };
        var qry = parser.Parse(query);
        GetWriter().DeleteDocuments(qry);
        if (commit)
        {
            WriterCommit();
        }
    }

    public virtual IReadOnlyList<TextFragment> GetFragmentsToHighlight(string query, string originalText)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(originalText);

        const string field = "whatever";

        // don't use stop word list
        var analyzer = new StandardAnalyzer(LuceneVersion.LUCENE_48, new CharArraySet(LuceneVersion.LUCENE_48, 0, true));
        var parser = new QueryParser(LuceneVersion.LUCENE_48, field, analyzer) { AllowLeadingWildcard = true, };
        var qry = parser.Parse(query);
        var scorer = new QueryScorer(qry, field);

        // note: the lucene highlighter has been modified to work simpler, better and faster at least in our context
        var highlighter = new Highlighter(scorer) { TextFragmenter = new SimpleFragmenter(0) };

        //var sw = Stopwatch.StartNew();
        var fragments = highlighter.GetBestTextFragments(analyzer, field, originalText);
        //var texts = fragments.Where(f => f.Score > 0).Select(frag => frag?.ToString() ?? string.Empty).ToArray();
        //EventProvider.Default.WriteMessage("sw: " + sw.Elapsed + " count:" + texts.Length);
        //sw.Restart();
        return fragments;
    }

    public IReadOnlyList<IndexSearchResultItem> GetIndexedDocuments(IndexDirectory directory, int maximumDocuments = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(directory);
        var qry = NumericRangeQuery.NewInt32Range(FieldDirectoryId, directory.Id, directory.Id, true, true);
        var result = Search(qry, IndexSearchResultItem.CreateItem, maximumDocuments);
        return result.Items;
    }

    private static SearchResultItem CreateItemFunc(Index index, int docIndex, ScoreDoc scoreDoc, Document doc) => new() { Index = docIndex, Score = scoreDoc.Score, DocumentId = scoreDoc.Doc, Document = doc };

    public SearchResult<SearchResultItem> Search(string query, int maximumDocuments = int.MaxValue)
        => Search(query, CreateItemFunc, maximumDocuments);

    public virtual SearchResult<T> Search<T>(string query, Func<Index, int, ScoreDoc, Document, T?> createItem, int maximumDocuments = int.MaxValue) where T : SearchResultItem
    {
        var parser = new QueryParser(LuceneVersion.LUCENE_48, DefaultFieldName, _analyzer) { AllowLeadingWildcard = true, };
        var qry = parser.Parse(query);
        return Search(qry, createItem, maximumDocuments);
    }

    public SearchResult<SearchResultItem> Search(Query query, int maximumDocuments = int.MaxValue)
        => Search(query, CreateItemFunc, maximumDocuments);

    public virtual SearchResult<T> Search<T>(Query query, Func<Index, int, ScoreDoc, Document, T?> createItem, int maximumDocuments = int.MaxValue) where T : SearchResultItem
    {
        ArgumentNullException.ThrowIfNull(query);
        if (_searcher == null)
            throw new InvalidOperationException("Cannot search in a write-only index.");

        var topDocs = _searcher.Search(query, maximumDocuments);
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

            var item = createItem(this, list.Count + 1, topDocs.ScoreDocs[i], doc);
            if (item == null)
                continue;

            list.Add(item);

            foreach (var fld in doc)
            {
                switch (fld.NumericType)
                {
                    case NumericFieldType.BYTE:
                        item.AddField(fld.Name, fld.GetByteValue());
                        break;

                    case NumericFieldType.INT16:
                        item.AddField(fld.Name, fld.GetInt16Value());
                        break;

                    case NumericFieldType.INT32:
                        item.AddField(fld.Name, fld.GetInt32Value());
                        break;

                    case NumericFieldType.INT64:
                        item.AddField(fld.Name, fld.GetInt64Value());
                        break;

                    case NumericFieldType.SINGLE:
                        item.AddField(fld.Name, fld.GetSingleValue());
                        break;

                    case NumericFieldType.DOUBLE:
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
        public int Id { get; set; }
        public string? Path { get; set; }
        public DateTime CreationTimUtc { get; set; } = DateTime.UtcNow;
        public IEnumerable<DirectoryBatch> Batches => ((ISQLiteObject)this).Database?.LoadByForeignKey<DirectoryBatch>(this).WhereNotNull() ?? [];

        SQLiteDatabase? ISQLiteObject.Database { get; set; } = db;
        public override string ToString() => Path ?? string.Empty;

        public IndexDirectory ToIndexDirectory(Index index, Directory directory)
        {
            if (directory.Path == null)
                throw new InvalidOperationException();

            var dir = new IndexDirectory(index, directory.Id, directory.Path);
            foreach (var batch in index._sqlDirectory.Database.Load<DirectoryBatch>($"SELECT * FROM {nameof(DirectoryBatch)} WHERE {nameof(DirectoryBatch.Directory)} = ?", directory.Id))
            {
                dir.Batches.Add(batch.ToIndexDirectoryBatch(dir));
            }
            return dir;
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
        public int NumberOfSkippedDirectories { get; set; }
        public string? Inclusions { get; set; }
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
                NumberOfSkippedDirectories = NumberOfSkippedDirectories,
            };

            batch.Inclusions.AddRange(Conversions.SplitToNullifiedList(Inclusions, [_dbSeparator]).Select(i => InclusionDefinition.Parse(i)).WhereNotNull());
            batch.ExcludedDirectoryNames.AddRange(Conversions.SplitToNullifiedList(ExcludedDirectoryNames, [_dbSeparator]));
            batch.NonIndexedFileExtensions.AddRange(Conversions.SplitToNullifiedList(NonIndexedFileExtensions, [_dbSeparator]));
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
            NumberOfSkippedDirectories = batch.NumberOfSkippedDirectories,
            Inclusions = string.Join(_dbSeparator, batch.Inclusions),
            ExcludedDirectoryNames = string.Join(_dbSeparator, batch.ExcludedDirectoryNames),
            NonIndexedFileExtensions = string.Join(_dbSeparator, batch.NonIndexedFileExtensions),
        };
    }
}
