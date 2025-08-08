namespace Doxie;

public partial class QueryWindow : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _query = string.Empty;
    private string? _relativeFilePath;
    private int _totalHits;
    private readonly Task _webView2Initialized;
    private readonly EditorControlObject _eco = new();
    private char[]? _buffer;
    private int? _bufferSize;
    private StreamReader? _reader;

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
    public ObservableCollection<string> Files { get; } = [];
    public string? ModelLanguageId { get; private set; }
    public string? ModelLanguageName { get; private set; }
    public string? RelativeFilePath
    {
        get => _relativeFilePath;
        set
        {
            if (_relativeFilePath == value)
                return;

            _relativeFilePath = value;
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
            Files.Clear();
            TotalHits = 0;
            RelativeFilePath = null;

            if (!string.IsNullOrWhiteSpace(_query))
            {
                var result = Index.Search<IndexSearchResultItem>(_query);
                TotalHits = result.TotalHits;
                var files = result.Items.Select(i => i.RelativePath).WhereNotNull().OrderBy(i => i);
                Files.AddRange(files);
            }
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private async void OnFilesListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await _webView2Initialized;

        if (filesList.SelectedItem is not string path)
        {
            webView.Visibility = Visibility.Hidden;
            RelativeFilePath = null;
            return;
        }

        webView.Visibility = Visibility.Visible;
        RelativeFilePath = path;
    }

    private async void CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        await Program._monacoInstalledTask;
        webView.CoreWebView2.ContextMenuRequested += (s, args) => args.Handled = true;
        webView.CoreWebView2.AddHostObjectToScript("doxie", _eco);
        webView.Source = new Uri(MonacoResources.IndexFilePath);
    }

    private async Task<bool> LoadFile()
    {
        if (RelativeFilePath == null)
            return false;

        var filePath = Path.Combine(Index.FilePath, RelativeFilePath);
        var encoding = EncodingDetector.DetectEncoding(filePath, Settings.Current.EncodingDetectorMode);

        _reader?.Dispose();
        _reader = new StreamReader(filePath, encoding);
        var max = Settings._defaultMaxLoadBufferSize;

        _bufferSize = (int)Math.Min(_reader.BaseStream.Length, max);
        await webView.ExecuteScriptAsync($"loadFromHost()");

        await SetEditorPosition();
        return true;
    }

    private void EditorControlOnLoad(object? sender, EditorControlLoadEventArgs e)
    {
        if (_buffer == null || _reader == null || !_bufferSize.HasValue)
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
    private async Task<string> SetEditorPosition(int lineNumber = 0, int column = 0) => await webView.ExecuteScriptAsync("editor.setPosition({lineNumber:" + lineNumber + ",column:" + column + "})");

    private async Task SetLanguage()
    {
        if (ModelLanguageId != null)
        {
            await SetEditorLanguage(ModelLanguageId);
        }
        else
        {
            if (RelativeFilePath == null)
            {
                await SetEditorLanguage(null);
            }
            else
            {
                await SetEditorLanguage(MonacoExtensions.GetLanguageByExtension(Path.GetExtension(RelativeFilePath)));
            }
        }

        var id = await webView.ExecuteScriptAsync("editor.getModel().getLanguageId()");
        id = MonacoExtensions.UnescapeEditorText(id);
        SetLanguageId(id);
    }

    private void SetLanguageId(string? id)
    {
        if (id != null)
        {
            var text = MonacoExtensions.GetLanguageName(id);
            ModelLanguageName = text ?? id;
            ModelLanguageId = id;
        }
        else
        {
            ModelLanguageName = string.Empty;
            ModelLanguageId = string.Empty;
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
                if (!await LoadFile())
                    return;

                await SetLanguage();
                break;
        }
    }
}
