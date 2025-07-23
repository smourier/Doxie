using SqlNado;
using SqlNado.Utilities;

namespace Doxie.Model;

public class SqliteDirectory : BaseDirectory
{
    private readonly SQLiteSaveOptions? _saveOptions;

    public SqliteDirectory(string filePath, SQLiteOpenOptions options)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        Database = new SQLiteDatabase(filePath, options)
        {
            MemoryMapSize = -1,
            //Logger = new ConsoleLogger(true)
        };
        SetLockFactory(new SingleInstanceLockFactory());

        if (!options.Equals(SQLiteOpenOptions.SQLITE_OPEN_READONLY))
        {
            EnsureSchemaAndIndices();
        }

        _saveOptions = Database.CreateSaveOptions();
        _saveOptions.SynchronizeSchema = false;
        _saveOptions.SynchronizeIndices = false;

    }

    public SQLiteDatabase Database { get; }

    public override IndexOutput CreateOutput(string name, IOContext context)
    {
        ArgumentNullException.ThrowIfNull(name);
        DeleteFile(name);
        var file = new LuceneFile(Database) { Name = name };
        if (!Database.Save(file, _saveOptions))
            throw new InvalidOperationException();

        file.Data.Save([]);
        return new LuceneOutput(file);
    }

    public override void DeleteFile(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var result = Database.ExecuteNonQuery($"DELETE FROM {nameof(LuceneFile)} WHERE {nameof(LuceneFile.Name)} = ?", name);
    }

    [Obsolete]
    public override bool FileExists(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var rowId = Database.ExecuteScalar<long>($"SELECT rowid FROM {nameof(LuceneFile)} WHERE {nameof(LuceneFile.Name)} = ?", name);
        return rowId > 0;
    }

    public override long FileLength(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var rowId = Database.ExecuteScalar<long>($"SELECT rowid FROM {nameof(LuceneFile)} WHERE {nameof(LuceneFile.Name)} = ?", name);
        if (rowId <= 0)
            throw new InvalidOperationException();

        var len = Database.GetBlobSize(nameof(LuceneFile), nameof(LuceneFile.Data), rowId);
        return len;
    }

    public override string[] ListAll() => [.. Database.Load<LuceneFile>($"SELECT {nameof(LuceneFile.Name)} FROM {nameof(LuceneFile)}").Select(f => f.Name)];

    [DebuggerNonUserCode]
    public override IndexInput OpenInput(string name, IOContext context)
    {
        ArgumentNullException.ThrowIfNull(name);
        var file = Database.LoadByPrimaryKey<LuceneFile>(name) ?? throw new FileNotFoundException(name);
        return new LuceneInput(file);
    }

    public override void Sync(ICollection<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        // do nothing
    }

    protected override void Dispose(bool disposing)
    {
        Database.Dispose();
    }

    private void EnsureSchemaAndIndices()
    {
        foreach (var type in KnownTableTypes)
        {
            Database.SynchronizeSchema(type);
            Database.SynchronizeIndices(type);
        }
    }

    private static IEnumerable<Type> KnownTableTypes
    {
        get
        {
            yield return typeof(LuceneFile);
        }
    }

    private sealed class LuceneOutput(LuceneFile file) : IndexOutput
    {
        private readonly XxHash64 _checksum = new();
        private readonly MemoryStream _memoryStream = new();

        public LuceneFile File { get; } = file ?? throw new ArgumentNullException(nameof(file));

        public override long Position => _memoryStream.Position;
        public override long Checksum => (long)_checksum.GetCurrentHashAsUInt64();

        public override void Flush()
        {
            // do nothing
        }

        [Obsolete]
        public override void Seek(long pos)
        {
            _memoryStream.Seek(pos, SeekOrigin.Begin);
        }

        public override void WriteByte(byte b)
        {
            _memoryStream.WriteByte(b);
        }

        public override void WriteBytes(byte[] b, int offset, int length)
        {
            var span = b.AsSpan(offset, length);
            _checksum.Append(span);
            _memoryStream.Write(span);
        }

        protected override void Dispose(bool disposing)
        {
            // do nothing
            _memoryStream.Position = 0;
            File.Data.Save(_memoryStream);
            _memoryStream.Dispose();
        }
    }

    private sealed class LuceneInput : IndexInput
    {
        public LuceneInput(LuceneFile file)
            : base(file.Name)
        {
            File = file;
        }

        public LuceneFile File { get; }

        public override long Position => File.Stream.Position;
        public override long Length => File.Stream.Length;

        public override byte ReadByte()
        {
            var b = File.Stream.ReadByte();
            return (byte)b;
        }

        public override void ReadBytes(byte[] b, int offset, int len)
        {
            File.Stream.Read(b, offset, len);
        }

        public override void Seek(long pos)
        {
            File.Stream.Seek(pos, SeekOrigin.Begin);
        }

        protected override void Dispose(bool disposing)
        {
            // do nothing
        }
    }

    private sealed class LuceneFile : SQLiteBaseObject
    {
        private readonly Lazy<MemoryStream> _stream;

        public LuceneFile(SQLiteDatabase db)
            : base(db)
        {
            Data = new SQLiteBlobObject(this, nameof(Data));
            _stream = new Lazy<MemoryStream>(LoadStream);
        }

        [SQLiteColumn(IsPrimaryKey = true)]
        [AllowNull]
        public string Name { get; set; }

        [AllowNull]
        public SQLiteBlobObject Data { get; }

        [SQLiteColumn(Ignore = true)]
        public MemoryStream Stream => _stream.Value;

        private MemoryStream LoadStream()
        {
            var ms = new MemoryStream();
            Data.Load(ms);
            return ms;
        }

        public override string ToString() => Name ?? string.Empty;
    }
}
