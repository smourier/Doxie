using Lucene.Net.Store;
using SqlNado;
using SqlNado.Utilities;

namespace Doxie.Model;

public class SqliteDirectory : Lucene.Net.Store.Directory
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
        Database.BindOptions.DateTimeFormat = SQLiteDateTimeFormat.RoundTrip;

        if (!options.Equals(SQLiteOpenOptions.SQLITE_OPEN_READONLY))
        {
            EnsureSchemaAndIndices();
        }

        _saveOptions = Database.CreateSaveOptions();
        _saveOptions.SynchronizeSchema = false;
        _saveOptions.SynchronizeIndices = false;
    }

    public SQLiteDatabase Database { get; }
    public override LockFactory LockFactory { get; } = new LuceneLockFactory();
    public override Lucene.Net.Store.Lock MakeLock(string name) => LockFactory.MakeLock(name);
    public override void ClearLock(string name) => LockFactory.ClearLock(name);
    public override void SetLockFactory(LockFactory lockFactory) { } // do nothing

    public bool Save(object instance) => Database.Save(instance, _saveOptions);
    public virtual void SaveSetting(string name, object? value)
    {
        ArgumentNullException.ThrowIfNull(name);
        var svalue = string.Format(CultureInfo.InvariantCulture, "{0}", value).Nullify();
        Save(new Setting { Name = name, Value = svalue });
    }

    public T? LoadSetting<T>(string name, T? defaultValue = default) { if (TryLoadSetting<T>(name, out var value)) return value; return defaultValue; }
    public virtual bool TryLoadSetting<T>(string name, out T? value)
    {
        ArgumentNullException.ThrowIfNull(name);
        var setting = Database.LoadByPrimaryKey<Setting>(name);
        if (setting == null)
        {
            value = default;
            return false;
        }

        return Conversions.TryChangeType(setting.Value, CultureInfo.InvariantCulture, out value);
    }

    internal void PrepareForTemplate()
    {
        Database.DeleteAll(nameof(LuceneFile));
    }

    public virtual string? LoadNullifiedSetting(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var setting = Database.LoadByPrimaryKey<Setting>(name);
        if (setting == null)
            return null;

        return setting.Value.Nullify();
    }

    public virtual bool DeleteSetting(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Database.Delete(new Setting { Name = name });
    }

    public override IndexOutput CreateOutput(string name, IOContext context)
    {
        ArgumentNullException.ThrowIfNull(name);
        DeleteFile(name);
        var file = new LuceneFile(Database) { Name = name };
        if (!Save(file))
            throw new InvalidOperationException();

        file.Data.Save([]);
        return new LuceneOutput(this, file);
    }

    public override void DeleteFile(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var result = Database.ExecuteNonQuery($"DELETE FROM {nameof(LuceneFile)} WHERE {nameof(LuceneFile.Name)} = ?", name);
    }

    [Obsolete("FileExists is obsolete. Use other methods such as ListAll or FileLength to check for file existence.")]
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
        Database.CacheFlush();
    }

    protected override void Dispose(bool disposing) => Database.Dispose();

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
            yield return typeof(Setting);
            yield return typeof(Index.Directory);
            yield return typeof(Index.DirectoryBatch);
        }
    }

    private sealed class LuceneOutput(SqliteDirectory directory, LuceneFile file) : IndexOutput
    {
        // experience shows Lucene.Net does not support 64-bit checksum
        private readonly Crc32 _checksum = new();
        private readonly MemoryStream _memoryStream = new();
        private bool _disposed;

        public LuceneFile File { get; } = file ?? throw new ArgumentNullException(nameof(file));

        public override long Position => _memoryStream.Position;
        public override long Checksum => _checksum.GetCurrentHashAsUInt32();

        public override void Flush() => throw new NotSupportedException();

        [Obsolete]
        public override void Seek(long pos) => _memoryStream.Seek(pos, SeekOrigin.Begin);

        public override void WriteByte(byte b)
        {
            _checksum.Append([b]);
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
            if (disposing)
            {
                if (_disposed)
                    return;

                _memoryStream.Position = 0;
                File.Data.Save(_memoryStream);
                _memoryStream.Dispose();
                directory.Database.CacheFlush();
                _disposed = true;
            }
        }
    }

    private sealed class LuceneInput(LuceneFile file) : IndexInput(file.Name)
    {
        public LuceneFile File { get; } = file;

        public override long Position => File.Stream.Position;
        public override long Length => File.Stream.Length;

        public override byte ReadByte()
        {
            var b = File.Stream.ReadByte();
            if (b < 0)
                throw new EndOfStreamException();

            return (byte)b;
        }

        public override void ReadBytes(byte[] b, int offset, int len) => File.Stream.Read(b, offset, len);
        public override void Seek(long pos) => File.Stream.Seek(pos, SeekOrigin.Begin);
        protected override void Dispose(bool disposing) { }             // do nothing
    }

    private sealed class Setting
    {
        [SQLiteColumn(IsPrimaryKey = true)]
        public string? Name { get; set; }
        public string? Value { get; set; }

        public override string ToString() => Name + ": " + Value;
    }

    private sealed class LuceneFile : SQLiteBaseObject, IDisposable
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
            ms.Position = 0;
            return ms;
        }

        public override string ToString() => Name ?? string.Empty;

        public void Dispose()
        {
            if (_stream.IsValueCreated)
            {
                _stream.Value?.Dispose();
            }
        }
    }

    private sealed class LuceneLockFactory : LockFactory
    {
        public override Lucene.Net.Store.Lock MakeLock(string lockName) => new LuceneLock(lockName);
        public override void ClearLock(string lockName) { } // do nothing
    }

    private sealed class LuceneLock(string name) : Lucene.Net.Store.Lock
    {
        public string Name { get; } = name;

        public override string ToString() => Name;
        public override bool IsLocked() => false;
        public override bool Obtain() => true;
        protected override void Dispose(bool disposing) { } // do nothing
    }
}
