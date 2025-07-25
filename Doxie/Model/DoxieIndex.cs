using Lucene.Net.Analysis.Standard;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Util;
using SqlNado;

namespace Doxie.Model;

public class DoxieIndex
{
    public const string FileExtension = ".doxidx";
    public const string DefaultFieldName = "corpus";
    public const string FieldPath = "path";

    // database settings
    private const string _version = "version";
    private const string _creationDateUtc = "creationDateUtc";
    private const string _countOfDocuments = "countOfDocuments";
    private const string _totalDurationSeconds = "totalDurationSeconds";
    private const string _wasCancelled = "wasCancelled";
    private const string _nonTextExtensions = "nonTextExtensions";
    private const char _nonTextExtensionsSeparator = '|';

    public event EventHandler<FileIndexingEventArgs>? FileIndexing;

    private readonly SqliteDirectory _directory;
    private readonly StandardAnalyzer _analyzer;
    private readonly IndexWriter? _writer;
    private readonly DirectoryReader? _reader;
    private readonly IndexSearcher? _searcher;

    private DoxieIndex(SqliteDirectory directory)
    {
        _directory = directory;
        _analyzer = new StandardAnalyzer(LuceneVersion.LUCENE_48);
        if (!directory.Database.OpenOptions.HasFlag(SQLiteOpenOptions.SQLITE_OPEN_READONLY))
        {
            _directory.SetSetting(_version, AssemblyUtilities.GetInformationalVersion());
            _directory.SetSetting(_creationDateUtc, DateTime.UtcNow.ToString("O"));
            var config = new IndexWriterConfig(LuceneVersion.LUCENE_48, _analyzer);
            _writer = new IndexWriter(_directory, config);
        }
        else
        {
            _reader = DirectoryReader.Open(_directory);
            _searcher = new IndexSearcher(_reader);
        }
    }

    public string Name => _directory.Database.FilePath;
    public bool VacuumOnCommit { get; set; } = true;
    public bool IsReadOnly => _writer == null;
    public bool IsWriteOnly => !IsReadOnly;
    public bool IsIndexing { get; private set; }

    public int CountOfDocuments => _directory.GetSetting<int>(_countOfDocuments);
    public double TotalDurationSeconds => _directory.GetSetting<double>(_totalDurationSeconds);
    public DateTime CreationDateUtc => _directory.GetSetting<DateTime>(_creationDateUtc);
    public string? Version => _directory.GetNullifiedSetting(_version);
    public bool IndexingWasCancelled => _directory.GetSetting<bool>(_wasCancelled);
    public IReadOnlyList<string> NonTextExtensions => [.. Conversions.SplitToNullifiedList(_directory.GetNullifiedSetting(_nonTextExtensions), [_nonTextExtensionsSeparator])];

    public override string ToString() => Name;

    public Task<IndexCreationResult> AddToIndex(IndexCreationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.AsyncProcessing)
            return Task.FromResult(DoCreateIndex(request));

        return Task.Run(() => DoCreateIndex(request));
    }

    private IndexCreationResult DoCreateIndex(IndexCreationRequest request)
    {
        var result = new IndexCreationResult();
        try
        {
            DoCreateIndex(request, result);
        }
        catch (Exception ex)
        {
            if (Debugger.IsAttached)
                throw;

            result.Exception = ex;
        }
        return result;
    }

    protected virtual void OnFileIndexing(FileIndexingEventArgs e) => FileIndexing?.Invoke(this, e);

    protected virtual void DoCreateIndex(IndexCreationRequest request, IndexCreationResult result)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);

        var nonTextExtensions = new HashSet<string>();

        var startTimeUtc = DateTime.UtcNow;
        var writer = GetWriter();
        var count = 0;
        foreach (var entry in System.IO.Directory.EnumerateFileSystemEntries(request.InputDirectoryPath, request.SearchPattern, request.EnumerationOptions))
        {
            if (request.CancellationTokenSource?.IsCancellationRequested == true)
            {
                _directory.SetSetting(_wasCancelled, true);
                break;
            }

            if (System.IO.Directory.Exists(entry))
            {
                // Skip directories
                continue;
            }

            var ext = Path.GetExtension(entry).ToLowerInvariant();
            if (Perceived.GetPerceivedType(ext).PerceivedType != PerceivedType.Text)
            {
                nonTextExtensions.Add(ext);
                continue;
            }

            var e = new FileIndexingEventArgs(entry) { IndexedFilesCount = count, StartTimeUtc = startTimeUtc };
            OnFileIndexing(e);
            if (e.Cancel)
                continue;

            var file = File.ReadAllText(entry);

            var idx = new IndexDocument(DefaultFieldName);
            idx.AddField(DefaultFieldName, file.Trim());

            var relPath = Path.GetRelativePath(request.InputDirectoryPath, entry);
            idx.AddField(FieldPath, relPath, true);

            var doc = idx.FinishAndGetDocument();
            writer.AddDocument(doc);
            count++;
        }

        if (nonTextExtensions.Count > 0)
        {
            _directory.SetSetting(_nonTextExtensions, string.Join(_nonTextExtensionsSeparator, nonTextExtensions));
        }
        else
        {
            _directory.RemoveSetting(_nonTextExtensions);
        }

        _directory.SetSetting(_totalDurationSeconds, (DateTime.UtcNow - startTimeUtc).TotalSeconds);
        _directory.SetSetting(_countOfDocuments, count);

        Commit();
        if (VacuumOnCommit)
        {
            _directory.Database.Vacuum();
        }
    }

    public static DoxieIndex OpenRead(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        var directory = new SqliteDirectory(filePath, SQLiteOpenOptions.SQLITE_OPEN_READONLY);
        return new DoxieIndex(directory);
    }

    public static DoxieIndex OpenWrite(string filePath, SQLiteOpenOptions options = SQLiteOpenOptions.SQLITE_OPEN_READWRITE | SQLiteOpenOptions.SQLITE_OPEN_CREATE)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        var directory = new SqliteDirectory(filePath, options);
        return new DoxieIndex(directory);
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
}
