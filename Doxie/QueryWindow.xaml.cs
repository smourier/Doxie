namespace Doxie;

public partial class QueryWindow : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _query = string.Empty;
    private IndexSearchResultItem? _item;
    private int _totalHits;
    private readonly Task _webView2Initialized;
    private readonly EditorControlObject _eco = new();
    private char[]? _buffer;
    private int? _bufferSize;
    private StreamReader? _reader;
    private string? _modelLanguageName;

    public QueryWindow(Model.Index index)
    {
        ArgumentNullException.ThrowIfNull(index);
        Index = index;
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
                var result = Index.Search<IndexSearchResultItem>(_query);
                TotalHits = result.TotalHits;
                var files = result.Items.OrderBy(i => i.RelativePath);
                Results.AddRange(files);
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _reader?.Dispose();
        webView?.Dispose();
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

            _reader?.Dispose();
            _reader = new StreamReader(item.Path, encoding);
            var max = Settings._defaultMaxLoadBufferSize;

            _bufferSize = (int)Math.Min(_reader.BaseStream.Length, max);
            await webView.ExecuteScriptAsync($"loadFromHost()");

            await SetEditorPosition();
            await MoveEditorTo(1, 1);
            await SetLanguage(item);

            await HighlightRanges([new MonacoRange(2, 4, 3, 6)]);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            _reader?.Dispose();
            _reader = null;
            _buffer = null;
            _bufferSize = null;
            return false;
        }
    }

    private void EditorControlOnLoad(object? sender, EditorControlLoadEventArgs e)
    {
        if (_reader == null || !_bufferSize.HasValue)
        {
            e.DocumentText = null;
            _reader?.Dispose();
            _reader = null;
            _buffer = null;
            _bufferSize = null;
            return;
        }

        _buffer = new char[_bufferSize.Value];

        var read = (_reader?.ReadBlock(_buffer, 0, _buffer.Length)).GetValueOrDefault();
        if (read == 0)
        {
            e.DocumentText = null;
            _reader?.Dispose();
            _reader = null;
            _buffer = null;
            _bufferSize = null;
            return;
        }

        e.DocumentText = new string(_buffer, 0, read);
    }

    private Task<string> EnableMinimap(bool enabled) => webView.ExecuteScriptAsync("editor.updateOptions({minimap:{enabled:" + enabled.ToString().ToLowerInvariant() + "}})");
    private Task<string> SetEditorTheme(string? theme = null) { theme = theme.Nullify() ?? "vs-dark"; return webView.ExecuteScriptAsync($"monaco.editor.setTheme('{theme}')"); }
    private async Task<string> FocusEditor() { var result = await webView.ExecuteScriptAsync("editor.focus()"); webView.Focus(); return result; }
    private Task<string> SetEditorLanguage(string? lang) => webView.ExecuteScriptAsync($"monaco.editor.setModelLanguage(editor.getModel(), '{lang.Nullify() ?? LanguageExtensionPoint.DefaultLanguageId}');");
    private Task<string> SetEditorPosition(int lineNumber = 0, int column = 0) => webView.ExecuteScriptAsync("editor.setPosition({lineNumber:" + lineNumber + ",column:" + column + "})");
    private Task<string> MoveEditorTo(int? line = null, int? column = null) => webView.ExecuteScriptAsync($"moveEditorTo({column}, {line})");
    private Task<string> HighlightRanges(IEnumerable<MonacoRange> ranges)
    {
        var json = JsonSerializer.Serialize(ranges);
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

    private async void EditorControlEvent(object? sender, EditorControlEventArgs e)
    {
        switch (e.EventType)
        {
            case EditorControlEventType.EditorCreated:
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
}
