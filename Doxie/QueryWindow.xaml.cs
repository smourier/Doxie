namespace Doxie;

public partial class QueryWindow : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _query = string.Empty;
    private IndexSearchResultItem? _item;
    private int _totalHits;
    private readonly Task _webView2Initialized;
    private readonly MonacolObject _eco = new();
    private LinesStream? _stream;
    private int _currentStreamLineIndex = 0;
    private int _linesCount;
    private bool _isLinesCountVisible;
    private bool _isDetectedLanguageVisible;
    private bool _isHitsVisible = true;
    private int _hitsCount;
    private string? _modelLanguageName;
    private Encoding? _detectedEncoding;
    private string? _errorMessage;

    public QueryWindow(string indexFilePath)
    {
        ArgumentNullException.ThrowIfNull(indexFilePath);
        Index = Model.Index.OpenRead(indexFilePath);
        _eco.Load += EditorControlOnLoad;
        _eco.Event += EditorControlEvent;

        InitializeComponent();
        webView.Visibility = Visibility.Hidden;
        webView.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted;
        _webView2Initialized = webView.EnsureCoreWebView2Async();
        DataContext = this;
    }

    public Model.Index Index { get; }
    public ObservableCollection<IndexSearchResultItem> Results { get; } = [];
    public string? ModelLanguageName
    {
        get => _modelLanguageName;
        private set
        {
            if (_modelLanguageName == value)
                return;

            _modelLanguageName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDetectedLanguageVisible));
        }
    }

    public bool QueryHasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (_errorMessage == value)
                return;

            _errorMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(QueryHasError));
        }
    }

    public Encoding? DetectedEncoding
    {
        get => _detectedEncoding;
        set
        {
            if (value == null)
            {
                if (_detectedEncoding == null)
                    return;
            }
            else if (_detectedEncoding != null && _detectedEncoding.WebName == value.WebName)
                return;

            _detectedEncoding = value;
            OnPropertyChanged();
        }
    }

    public int HitsCount
    {
        get => _hitsCount;
        set
        {
            if (_hitsCount == value)
                return;

            _hitsCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsHitsVisible));
        }
    }

    public bool IsHitsVisible
    {
        get => _isHitsVisible;
        set
        {
            if (_isHitsVisible == value)
                return;

            _isHitsVisible = value;
            OnPropertyChanged();
        }
    }

    public int LinesCount
    {
        get => _linesCount;
        set
        {
            if (_linesCount == value)
                return;

            _linesCount = value;
            OnPropertyChanged();
        }
    }

    public bool IsLinesCountVisible
    {
        get => _isLinesCountVisible;
        set
        {
            if (_isLinesCountVisible == value)
                return;

            _isLinesCountVisible = value;
            OnPropertyChanged();
        }
    }

    public bool IsDetectedLanguageVisible
    {
        get => _isDetectedLanguageVisible;
        set
        {
            if (_isDetectedLanguageVisible == value)
                return;

            _isDetectedLanguageVisible = value;
            OnPropertyChanged();
        }
    }

    public IndexSearchResultItem? Item
    {
        get => _item;
        set
        {
            if (value == null)
            {
                if (_item == null)
                    return;
            }
            else if (_item != null && _item.Path.EqualsIgnoreCase(value.Path))
                return;

            _item = value;
            OnPropertyChanged();
        }
    }

    public int TotalHits
    {
        get => _totalHits;
        private set
        {
            if (_totalHits == value)
                return;

            _totalHits = value;
            OnPropertyChanged();
        }
    }

    public string Query
    {
        get => _query;
        set
        {
            if (_query == value)
                return;

            _query = value;
            Results.Clear();
            TotalHits = 0;
            Item = null;

            if (!string.IsNullOrWhiteSpace(_query))
            {
                try
                {
                    var result = Index.Search(_query, IndexSearchResultItem.CreateItem);
                    TotalHits = result.TotalHits;
                    var files = result.Items.OrderBy(i => i.RelativePath);
                    Results.AddRange(files);
                    ErrorMessage = null;
                }
                catch (Exception ex)
                {
                    EventProvider.Default.WriteMessage("Error searching index: " + ex.Message);
                    ErrorMessage = (ex.GetInterestingExceptionMessage() ?? ex.Message).Replace(Environment.NewLine, " ");
                    TotalHits = 0;
                    Results.Clear();
                }
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _stream?.Dispose();
        webView?.Dispose();
        Index?.Dispose();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private async void OnFilesListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await _webView2Initialized;

        if (filesList.SelectedItem is not IndexSearchResultItem item || item.Path == null || !IOUtilities.PathIsFile(item.Path))
        {
            webView.Visibility = Visibility.Hidden;
            Item = null;
            return;
        }

        webView.Visibility = Visibility.Visible;
        Item = item;
        _ = LoadFile(item);
    }

    private async void CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        await Program._monacoInstalledTask;
        webView.CoreWebView2.ContextMenuRequested += (s, args) => args.Handled = true;
        webView.CoreWebView2.AddHostObjectToScript("doxie", _eco);
        webView.Source = new Uri(MonacoResources.IndexFilePath);
    }

    private async Task<bool> LoadFile(IndexSearchResultItem item)
    {
        if (item.Path == null || !IOUtilities.PathIsFile(item.Path))
            return false;

        try
        {
            var encoding = EncodingDetector.DetectEncoding(item.Path, Settings.Current.EncodingDetectorMode);
            DetectedEncoding = encoding;

            _stream?.Dispose();
            IsLinesCountVisible = false;
            IsHitsVisible = false;
            _currentStreamLineIndex = 0;
            _stream = new LinesStream(item.Path, encoding);
            _stream.Loaded += (s, e) =>
            {
                LinesCount = _stream.Lines.Count;
                IsLinesCountVisible = true;
            };
            _stream.Load();
            await webView.ExecuteScriptAsync($"loadFromHost()");

            await SetEditorPosition();
            await MoveEditorTo(1, 1);
            await SetLanguage(item);

            if (Settings.Current.MonacoHighlightHits)
            {
                // re-reading the whole for highlighting is (probably) faster than using the stream
                var text = File.ReadAllText(item.Path, encoding);
                var fragments = Index.GetFragmentsToHighlight(Query, text);

                var ranges = new List<MonacoRange>();
                foreach (var fragment in fragments)
                {
                    if (fragment.TextStartPos == fragment.TextEndPos)
                        continue;

                    var rng = _stream.GetRange(fragment.TextStartPos + 1, fragment.TextEndPos - fragment.TextStartPos);
                    if (rng == null)
                        continue;

                    var range = new MonacoRange(
                        rng.Value.StartLineIndex + 1,
                        rng.Value.StartColumn + 1,
                        rng.Value.EndLineIndex + 1,
                        rng.Value.EndColumn + 1);
                    ranges.Add(range);
                }

                if (ranges.Count > 0)
                {
                    HitsCount = ranges.Count;
                    await HighlightRanges(ranges);
                }
                IsHitsVisible = true;
            }
            return true;
        }
        catch (Exception ex)
        {
            EventProvider.Default.WriteMessage("Error:" + ex);
            _stream?.Dispose();
            _stream = null;
            return false;
        }
    }

    private void EditorControlOnLoad(object? sender, MonacoLoadEventArgs e)
    {
        if (_stream == null || _stream.Lines.Count == 0)
        {
            e.DocumentText = null;
            return;
        }

        // send lines in chunks to monaco editor
        const int chunkSize = 1000;

        var linesCount = Math.Min(chunkSize, _stream.Lines.Count - _currentStreamLineIndex);
        if (linesCount <= 0)
        {
            e.DocumentText = null;
            return;
        }

        var texts = string.Join(Environment.NewLine, _stream.GetTexts(_currentStreamLineIndex, linesCount));
        _currentStreamLineIndex += linesCount;
        e.DocumentText = texts;
    }

    private Task<string> EnableMinimap(bool enabled) => webView.ExecuteScriptAsync("editor.updateOptions({minimap:{enabled:" + enabled.ToString().ToLowerInvariant() + "}})");
    private Task<string> SetEditorTheme(string? theme = null) { theme = theme.Nullify() ?? "vs-dark"; return webView.ExecuteScriptAsync($"monaco.editor.setTheme('{theme}')"); }
    private async Task<string> FocusEditor() { var result = await webView.ExecuteScriptAsync("editor.focus()"); webView.Focus(); return result; }
    private Task<string> SetEditorLanguage(string? lang) => webView.ExecuteScriptAsync($"monaco.editor.setModelLanguage(editor.getModel(), '{lang.Nullify() ?? MonacoLanguageExtensionPoint.DefaultLanguageId}');");
    private Task<string> SetEditorPosition(int lineNumber = 0, int column = 0) => webView.ExecuteScriptAsync("editor.setPosition({lineNumber:" + lineNumber + ",column:" + column + "})");
    private Task<string> MoveEditorTo(int? line = null, int? column = null) => webView.ExecuteScriptAsync($"moveEditorTo({column}, {line})");
    private Task<string> HighlightRanges(IEnumerable<MonacoRange> ranges)
    {
        var json = JsonSerializer.Serialize(ranges, MonacoExtensions.SerializerOptions);
        return webView.ExecuteScriptAsync($"highlightRanges('{json}')");
    }

    private async Task SetLanguage(IndexSearchResultItem item)
    {
        if (item.Extension == null)
        {
            await SetEditorLanguage(null);
        }
        else
        {
            await SetEditorLanguage(MonacoExtensions.GetLanguageByExtension(item.Extension));
        }

        var id = await webView.ExecuteScriptAsync("editor.getModel().getLanguageId()");
        id = MonacoExtensions.UnescapeEditorText(id);
        if (id != null)
        {
            var text = MonacoExtensions.GetLanguageName(id);
            ModelLanguageName = text ?? id;
        }
        else
        {
            ModelLanguageName = string.Empty;
        }
    }

    private async void EditorControlEvent(object? sender, MonacoEventArgs e)
    {
        switch (e.EventType)
        {
            case MonacoEventType.EditorCreated:
                if (!MonacoExtensions.LanguagesLoaded)
                {
                    await MonacoExtensions.LoadLanguages(webView);
                }

                await EnableMinimap(Settings.Current.MonacoShowMinimap);
                await SetEditorTheme(Settings.Current.MonacoTheme);
                await FocusEditor();

                var item = Item;
                if (item != null)
                {
                    if (!await LoadFile(item))
                        return;
                }
                break;
        }
    }

    private void OpenInExplorer_Click(object sender, RoutedEventArgs e)
    {
        var item = sender.GetDataContext<IndexSearchResultItem>();
        if (item == null || !IOUtilities.PathIsFile(item.Path))
            return;

        Extensions.OpenInExplorer(item.Path);
    }
}
