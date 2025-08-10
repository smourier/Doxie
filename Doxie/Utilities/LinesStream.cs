namespace Doxie.Utilities;

public class LinesStream : IDisposable
{
    public const int DefaultBufferSize = 4096;
    public const int MinBufferSize = 128;
    public const int DefaultDeferredParsingLineCountThreshold = 10000;
    public const int DefaultWrapWidth = 80;

    // occurs on any thread
    public event EventHandler<LoadEventArgs>? Loading;
    public event EventHandler<LoadEventArgs>? Loaded;

    private bool _disposedValue;
    private readonly Lock _lock = new();
    private Line[]? _lines;

    public LinesStream(string filePath, Encoding? encodingIfNoBom = null, int bufferSize = DefaultBufferSize)
        : this(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read), encodingIfNoBom, bufferSize)
    {
    }

    public LinesStream(Stream stream, Encoding? encodingIfNoBom = null, int bufferSize = DefaultBufferSize, bool owned = true)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Stream = stream;
        Owned = owned;
        BufferSize = bufferSize < MinBufferSize ? MinBufferSize : bufferSize;
        EncodingIfNoBom = encodingIfNoBom ?? Encoding.UTF8;
        LoadBom();
    }

    public Stream Stream { get; }
    public bool Owned { get; }
    public int BufferSize { get; }
    public Encoding EncodingIfNoBom { get; }
    public virtual Task? LoadingTask { get; protected set; }
    public virtual int MaxBytesPerLine { get; protected set; } = 0;
    public virtual Encoding Encoding { get; protected set; }
    public int BomSize { get; protected set; }
    public virtual Bom Bom { get; protected set; }
    public virtual bool LoadingWasCancelled { get; protected set; }
    public virtual int DeferredParsingLineCountThreshold { get; set; } = DefaultDeferredParsingLineCountThreshold;
    public virtual Wrap Wrap { get; set; }
    public virtual int WrapWidth { get; set; } = DefaultWrapWidth; // in bytes, not characters, so it's a bit approximative depending on the encoding
    public string Text => string.Join(Environment.NewLine, Texts);
    public IReadOnlyList<Line> Lines => _lines ??= [];
    public IEnumerable<string> Texts => GetTexts(Lines);

    public Line? GetContainingLine(long position) => GetContainingLine(Lines, position);
    private Line? GetContainingLine(IReadOnlyList<Line> lines, long position)
    {
        if (position < 0 || position >= Stream.Length)
            return null;

        if (lines == null || lines.Count == 0)
            return null;

        var low = 0;
        var high = lines.Count - 1;
        Line? candidate = null;

        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            var line = lines[mid];
            var start = line.Position;
            var end = line.Position + line.Length;

            if (position < start)
            {
                high = mid - 1;
            }
            else
            {
                candidate = line;
                if (position < end)
                    return line;

                low = mid + 1;
            }
        }

        return candidate;
    }

    public Range? GetRange(long position, long length)
    {
        var lines = Lines;
        if (lines == null || lines.Count == 0)
            return null;

        var startLine = GetContainingLine(lines, position);
        if (startLine == null)
            return null;

        var startColumn = (int)(position - startLine.Position);
        if (lines.Count == 1)
            return new Range(startLine.Index, startColumn, startLine.Index, startColumn + (int)length - 1);

        var endPosition = position + length - 1;
        var endLine = GetContainingLine(lines, endPosition);
        int endColumn;
        if (endLine == null)
        {
            // last line
            endLine = lines[lines.Count - 1];
            endColumn = endLine.Length - 1;
        }
        else
        {
            endColumn = (int)(endPosition - endLine.Position);
        }
        return new Range(startLine.Index, startColumn, endLine.Index, endColumn);
    }

    public string? GetText(Line line) => GetTexts([line]).FirstOrDefault();
    public string? GetText(int lineIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(lineIndex);
        var lines = Lines;
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(lineIndex, lines.Count);
        return GetText(lines[lineIndex]);
    }

    public IEnumerable<Line> GetLines(int linesIndexStart, int count)
    {
        var lines = Lines;
        while (count > 0 && linesIndexStart < lines.Count)
        {
            yield return lines[linesIndexStart++];
            count--;
        }
    }

    public IEnumerable<string> GetTexts(int linesIndexStart, int count) => GetTexts(GetLines(linesIndexStart, count));
    public IEnumerable<string> GetTexts(IEnumerable<Line> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        CheckDisposed();
        var pool = ArrayPool<byte>.Shared;
        foreach (var line in lines)
        {
            int read;
            var buffer = pool.Rent(line.Length);
            if (LoadingTask != null)
            {
                lock (_lock)
                {
                    var pos = Stream.Position;
                    Stream.Seek(line.Position, SeekOrigin.Begin);
                    read = Stream.Read(buffer, 0, line.Length);
                    Stream.Seek(pos, SeekOrigin.Begin);
                }
            }
            else
            {
                Stream.Seek(line.Position, SeekOrigin.Begin);
                read = Stream.Read(buffer, 0, line.Length);
            }

            if (read > 0)
            {
                var txt = Encoding.GetString(buffer, 0, read);
                yield return txt;
            }
            else
            {
                yield return string.Empty;
            }
            pool.Return(buffer);
        }
    }

    protected virtual void OnLoading(object sender, LoadEventArgs e) => Loading?.Invoke(this, e);
    protected virtual void OnLoaded(object sender, LoadEventArgs e) => Loaded?.Invoke(this, e);

    [MemberNotNull(nameof(Encoding))]
    protected virtual void LoadBom()
    {
        CheckDisposed();
        Stream.Position = 0;
        ReadBom();
        Encoding = Bom switch
        {
            Bom.None => EncodingIfNoBom,
            Bom.Utf16Le => Encoding.Unicode,
            Bom.Utf16Be => Encoding.BigEndianUnicode,
            Bom.Utf32Le => new UTF32Encoding(false, true),
            Bom.Utf32Be => new UTF32Encoding(true, true),
            _ => Encoding.UTF8,
        };
    }

    public virtual void Load()
    {
        CheckDisposed();

        // reset state
        Stream.Position = BomSize;
        LoadingWasCancelled = false;
        MaxBytesPerLine = 0;

        var buffer = new byte[BufferSize];
        var bufferIndex = -1;
        var bytesInBuffer = 0;
        var position = Stream.Position;
        var lastPosition = position;
        var eof = false;
        var eob = false;
        var lines = new List<Line>();
        var taskLines = new List<Line>();
        var width = 0;
        var batchSize = 0;
        var sendLoaded = true;
        var bufferedBytes = new byte[4];
        var bufferedBytesIndex = 0;
        var e = new LoadEventArgs();
        var wrap = Wrap;
        var wrapWidth = WrapWidth;
        if (wrap != Wrap.None)
        {
            if (wrapWidth <= 2)
                throw new ArgumentOutOfRangeException(nameof(WrapWidth));

            if (wrapWidth == int.MaxValue)
            {
                wrap = Wrap.None;
            }
            else
            {
                wrapWidth--; // for the result to match more approximation due to encodings
            }
        }

        var singleByteEof = Encoding.GetByteCount(['\n']) == 1;
        var decoder = wrap == Wrap.None ? null : Encoding.GetDecoder();
        var countFallback = CountFallback.Instance;
        if (decoder != null)
        {
            decoder.Fallback = countFallback;
        }

        var buffersSize = wrapWidth;
        if (wrap == Wrap.Word || wrap == Wrap.WordBreak)
        {
            buffersSize += 2; // unicode separator chars are all maximum 2 bytes long
        }

        var wrapBuffer = wrap == Wrap.None ? null : new byte[buffersSize];
        var wrapChars = wrap == Wrap.None ? null : new char[buffersSize];

        do
        {
            if (!loadCharacter())
                break;
        }
        while (!eob);

        if (LoadingTask == null && width > 0)
        {
            var line = new Line(lastPosition, width);
            lock (_lock)
            {
                line.Index = lines.Count;
                lines.Add(line);
            }
        }

        lock (_lock) _lines = [.. lines];

        if (sendLoaded)
        {
            e.LoadedLines = _lines.Length;
            OnLoaded(this, e);
        }

        void bufferByte(byte b)
        {
            if (bufferedBytesIndex >= bufferedBytes.Length)
            {
                var newBuffer = new byte[bufferedBytes.Length * 2];
                Array.Copy(bufferedBytes, newBuffer, bufferedBytes.Length);
                bufferedBytes = newBuffer;
            }
            bufferedBytes[bufferedBytesIndex++] = b;
            position--;
        }

        void ensureNextByte()
        {
            if (eof)
                return;

            if (bufferIndex + 1 >= bytesInBuffer)
            {
                bufferIndex = -1;
                bytesInBuffer = Stream.Read(buffer, 0, buffer.Length);
                if (bytesInBuffer < buffer.Length)
                {
                    eof = true;
                }
            }
        }

        int readNext()
        {
            if (bufferedBytesIndex > 0)
            {
                position++;
                return bufferedBytes[--bufferedBytesIndex];
            }

            if (eob)
                return -1;

            ensureNextByte();
            bufferIndex++;
            if (bufferIndex >= bytesInBuffer)
            {
                eob = true;
                return -1;
            }
            position++;
            return buffer[bufferIndex];
        }

        int peekNext()
        {
            if (bufferedBytesIndex > 0)
                return bufferedBytes[bufferedBytesIndex - 1];

            if (eob)
                return -1;

            ensureNextByte();
            var nextIndex = bufferIndex + 1;
            if (nextIndex >= bytesInBuffer)
            {
                eob = true;
                return -1;
            }

            return buffer[nextIndex];
        }

        void addLine(LineOptions options = LineOptions.None)
        {
            var line = new Line(lastPosition, width, options);
            if (line.Length > MaxBytesPerLine)
            {
                MaxBytesPerLine = line.Length;
            }

            lock (_lock)
            {
                if (LoadingTask != null)
                {
                    line.Index = taskLines.Count;
                    taskLines.Add(line);
                }
                else
                {
                    line.Index = lines.Count;
                    lines.Add(line);
                }
            }
            lastPosition = position;
            width = 0;
        }

        bool loadCharacter()
        {
            var c = readNext();
            var n = peekNext();

            bool isCr(int c, int n)
            {
                if (singleByteEof)
                {
                    if (c == '\r')
                    {
                        readNext();
                        return true;
                    }
                    return false;
                }

                if (c == '\r' && n == 0)
                {
                    readNext(); // read 0
                    return true;
                }
                return false;
            }

            bool isEol(int c, int n)
            {
                if (singleByteEof)
                {
                    if (c == '\n' || (c == '\r' && n == '\n'))
                        return true;

                    return false;
                }

                if (c == '\n' && n == 0)
                {
                    readNext(); // read 0
                    return true;
                }

                if (c == '\r' && n == 0)
                {
                    readNext(); // read 0
                    var c2 = readNext();
                    var n2 = peekNext();
                    if (c2 == '\n' && n2 == 0)
                        return true;

                    if (c2 >= 0)
                    {
                        bufferByte((byte)c2);
                    }
                    bufferByte(0);
                }
                return false;
            }

            if (isEol(c, n))
            {
                addLine();
                if (isCr(c, n))
                {
                    lastPosition = position;
                }

                if (LoadingTask != null)
                {
                    batchSize++;
                    if (e.NextEventBatchSize == batchSize)
                    {
                        batchSize = 0;
                        e.LoadedLines = taskLines.Count;
                        OnLoading(this, e);
                        if (e.Cancel)
                        {
                            LoadingWasCancelled = true;
                            return false;
                        }
                    }
                }

                if (LoadingTask == null && lines.Count == DeferredParsingLineCountThreshold)
                {
                    sendLoaded = false;
                    e.LoadedLines = lines.Count;
                    OnLoading(this, e);
                    LoadingWasCancelled = e.Cancel;
                    if (!LoadingWasCancelled)
                    {
                        LoadingTask = Task.Run(() =>
                        {
                            lock (_lock)
                            {
                                taskLines.AddRange(lines);
                            }

                            do
                            {
                                loadCharacter();
                                if (LoadingWasCancelled)
                                    break;
                            }
                            while (!eob);

                            if (width > 0 && !LoadingWasCancelled)
                            {
                                var line = new Line(lastPosition, width);
                                lastPosition = position;
                                line.Index = taskLines.Count;
                                taskLines.Add(line);
                            }

                            lock (_lock) _lines = [.. taskLines];

                            e.LoadedLines = taskLines.Count;
                            taskLines.Clear();
                            LoadingTask = null;
                            OnLoaded(this, e);
                        });
                    }
                    return false;
                }
            }
            else
            {
                switch (wrap)
                {
                    case Wrap.None:
                        width++;
                        return true;

                    case Wrap.Character:
                        wrapBuffer![width] = (byte)c;
                        width++;

                        if (width == buffersSize)
                        {
                            decoder!.FallbackBuffer.Reset();
                            decoder.GetChars(wrapBuffer!, 0, buffersSize, wrapChars!, 0, true);
                            unbreakChars();
                            addLine();
                        }
                        return true;

                    case Wrap.Word:
                    case Wrap.WordBreak:
                        wrapBuffer![width] = (byte)c;
                        width++;

                        if (width > wrapWidth)
                        {
                            decoder!.FallbackBuffer.Reset();
                            var charCount = decoder.GetChars(wrapBuffer!, 0, width, wrapChars!, 0, true);
                            Debug.Assert(charCount > 0);

                            // get last unicode separator (before any other character)
                            var last = -1;
                            for (var i = charCount - 1; i >= 0; i--)
                            {
                                if (!isUnicodeSeparator(wrapChars![i]))
                                    break;

                                last = i;
                            }

                            if (last < 0)
                            {
                                if (wrap == Wrap.Word)
                                {
                                    // continue on the current word
                                    if (wrapBuffer.Length <= width)
                                    {
                                        var newBuffer = new byte[wrapBuffer.Length * 2];
                                        Array.Copy(wrapBuffer, newBuffer, wrapBuffer.Length);
                                        wrapBuffer = newBuffer;

                                        wrapChars = new char[wrapChars!.Length * 2];
                                    }
                                    return true; // continue;
                                }

                                // break here
                                unbreakChars();
                                addLine(LineOptions.Broken);
                            }
                            else
                            {
                                if (last > 0)
                                {
                                    // search for its bytes
                                    Debug.Assert(charCount > last);
                                    var removeCount = Encoding.GetByteCount(wrapChars!, last, charCount - last);
                                    Debug.Assert(removeCount > 0 && removeCount < width);

                                    // put these back in buffer
                                    for (var i = 0; i < removeCount; i++)
                                    {
                                        width--;
                                        bufferByte(wrapBuffer![width]);
                                    }
                                }

                                addLine();
                            }
                        }
                        return true;

                    default:
                        throw new NotSupportedException();
                }

                void unbreakChars()
                {
                    // we want to make sure we don't cut a character in half when wrapping between two lines.
                    // maximum size of a character is 4 bytes (UTF-32) so we check if we need to go a bit backwards
                    var cb = (CountFallbackBuffer)decoder.FallbackBuffer;
                    if (cb.Index >= 0 && cb.Index > width - 4)
                    {
                        var w = width;
                        // put back last read bytes into the buffer
                        for (var i = cb.Index; i < cb.Index + cb.BytesCount; i++)
                        {
                            width--;
                            bufferByte(wrapBuffer![width]);
                        }
                    }
                }

                static bool isUnicodeSeparator(char ch)
                {
                    var uc = char.GetUnicodeCategory(ch);
                    return uc == UnicodeCategory.SpaceSeparator || uc == UnicodeCategory.LineSeparator || uc == UnicodeCategory.ParagraphSeparator;
                }
            }
            return true;
        }
    }

    private sealed class CountFallback : DecoderFallback
    {
        public static CountFallback Instance { get; } = new();
        public override int MaxCharCount => 0;

        public override DecoderFallbackBuffer CreateFallbackBuffer() => new CountFallbackBuffer();
    }

    private sealed class CountFallbackBuffer : DecoderFallbackBuffer
    {
        private int _count;

        public int BytesCount { get; private set; }
        public int Index { get; private set; } = -1;
        public override int Remaining => _count;
        public override bool MovePrevious() => throw new NotImplementedException();
        public override bool Fallback(byte[] bytesUnknown, int index)
        {
            BytesCount = bytesUnknown.Length;
            Index = index;
            _count = bytesUnknown.Length + 1;
            return true;
        }

        public override char GetNextChar()
        {
            _count--;
            return _count == 0 ? '\0' : '\uFFFD';
        }

        public override void Reset()
        {
            _count = 0;
            BytesCount = 0;
            Index = -1;
        }
    }

    protected void CheckDisposed() => ObjectDisposedException.ThrowIf(_disposedValue, nameof(LinesStream));

    public void Dispose() { Dispose(disposing: true); GC.SuppressFinalize(this); }
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing && Owned)
            {
                Stream.Dispose();
            }

            _disposedValue = true;
        }
    }

    public readonly struct Range(int startLineNumber, int startColumn, int endLineNumber, int endColumn)
    {
        public int StartLineIndex { get; } = startLineNumber;
        public int StartColumn { get; } = startColumn;
        public int EndLineIndex { get; } = endLineNumber;
        public int EndColumn { get; } = endColumn;

        public override string ToString() => $"{StartLineIndex}:{StartColumn} - {EndLineIndex}:{EndColumn}";
    }

    public sealed class Line(long position, int length, LineOptions options = LineOptions.None) : IComparable<Line>, IEquatable<Line>
    {
        public long Position { get; } = position;
        public int Length { get; } = length;
        public LineOptions Options { get; } = options;
        public int Index { get; internal set; }
        public bool IsBroken => Options.HasFlag(LineOptions.Broken);

        public override string ToString() => $"{Index}: {Position} => {Position + Length - 1} ({Length})/{Options}";
        public override int GetHashCode() => Index.GetHashCode();
        public override bool Equals(object? obj) => obj is Line other && Equals(other);
        public bool Equals(Line? other) => other is not null && Index == other.Index;
        public int CompareTo(Line? other) { if (other is null) return 1; return Index.CompareTo(other.Index); }
    }

    public enum LineOptions
    {
        None,
        Broken,
    }

    protected virtual void ReadBom()
    {
        Span<byte> bytes = stackalloc byte[4];
        var read = Stream.Read(bytes);
        if (read >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            BomSize = 3;
            Bom = Bom.Utf8;
        }
        else if (read >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            BomSize = 2;
            Bom = Bom.Utf16Le;
        }
        else if (read >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            BomSize = 2;
            Bom = Bom.Utf16Be;
        }
        else if (read == 4 && bytes[0] == 0xFE && bytes[1] == 0xFF && bytes[2] == 0 && bytes[3] == 0)
        {
            BomSize = 4;
            Bom = Bom.Utf32Le;
        }
        else if (read >= 4 && bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0xFE && bytes[3] == 0xFF)
        {
            BomSize = 4;
            Bom = Bom.Utf32Be;
        }
        else
        {
            BomSize = 0;
            Bom = Bom.None;
        }
        Stream.Seek(BomSize, SeekOrigin.Begin);
    }
}
