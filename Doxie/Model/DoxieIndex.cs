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

    private readonly Lucene.Net.Store.Directory _directory;
    private readonly StandardAnalyzer _analyzer;
    private readonly IndexWriter? _writer;
    private readonly DirectoryReader? _reader;
    private readonly IndexSearcher? _searcher;

    private DoxieIndex(SqliteDirectory directory)
    {
        _directory = directory;
        _analyzer = new StandardAnalyzer(LuceneVersion.LUCENE_48);
        Directory = directory;
        if (!directory.Database.OpenOptions.HasFlag(SQLiteOpenOptions.SQLITE_OPEN_READONLY))
        {
            var config = new IndexWriterConfig(LuceneVersion.LUCENE_48, _analyzer);
            _writer = new IndexWriter(_directory, config);
        }
        else
        {
            _reader = DirectoryReader.Open(_directory);
            _searcher = new IndexSearcher(_reader);
        }
    }

    public bool IsReadOnly => _writer == null;
    public bool IsWriteOnly => !IsReadOnly;
    public bool IsIndexing { get; private set; }
    public SqliteDirectory Directory { get; }

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

    protected virtual void DoCreateIndex(IndexCreationRequest request, IndexCreationResult result)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);

        var writer = GetWriter();
        request.IncludeFilter ??= IndexCreationRequest.Include;
        foreach (var entry in System.IO.Directory.EnumerateFileSystemEntries(request.InputDirectoryPath, request.SearchPattern, request.EnumerationOptions))
        {
            if (!request.IncludeFilter(entry))
                continue;

            var ext = Path.GetExtension(entry).ToLowerInvariant();
            if (ext != ".cs")
                continue;

            var file = File.ReadAllText(entry);

            var idx = new IndexDocument(DefaultFieldName);
            idx.AddField(DefaultFieldName, file.Trim());

            var doc = idx.FinishAndGetDocument();
            writer.AddDocument(doc);

            //break;
            Console.WriteLine($"Indexing {entry}...");
        }
        Commit();
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

    public virtual SearchResult<T> Search<T>(string text, string defaultFieldName, int maximumDocuments, Func<int, float, T>? createItem = null) where T : SearchResultItem
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(defaultFieldName);
        if (_searcher == null)
            throw new InvalidOperationException("Cannot search in a write-only index.");

        var parser = new QueryParser(LuceneVersion.LUCENE_48, defaultFieldName, _analyzer)
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

            T item;
            if (createItem == null)
            {
                if (!typeof(T).IsAssignableFrom(typeof(SearchResultItem)))
                    throw new ArgumentNullException(nameof(createItem));

                item = (T)new SearchResultItem(list.Count + 1, topDocs.ScoreDocs[i].Score);
            }
            else
            {
                item = createItem(list.Count + 1, topDocs.ScoreDocs[i].Score);
                if (item == null)
                    continue;
            }
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
