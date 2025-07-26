namespace Doxie;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly List<WorkToDo> _indexingTasks = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();

        DataContext = this;
        _ = Task.Run(Settings.Current.CleanRecentFiles);
    }

    public DoxieIndex? Index { get; private set; }
    public IReadOnlyList<RecentFile> RecentFiles
    {
        get
        {
            var recentFiles = new List<RecentFile>(Settings.Current.RecentFiles.OrderByDescending(f => f.LastAccessTime))
            {
                new RecentFileSeparator(),
                new ClearRecentFiles()
            };
            return [.. recentFiles];
        }
    }

    public void OpenIndex(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        var index = DoxieIndex.OpenWrite(filePath);
        Settings.Current.AddRecentFile(filePath);
        OnPropertyChanged(nameof(RecentFiles));
        Index?.Dispose();
        Index = index;
        OnPropertyChanged(nameof(Index));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.T &&
            Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) &&
            Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            var lastRecent = Settings.Current.RecentFiles.FirstOrDefault()?.FilePath;
            if (lastRecent != null)
            {
                OpenIndex(lastRecent);
            }
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        var first = _indexingTasks.FirstOrDefault();
        if (first != null && MessageBox.Show(
                this,
                $"Index '{first.Index.Name}' creation is in progress, are you sure you want to exit?",
                AssemblyUtilities.GetProduct(),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            e.Cancel = true;
            return;
        }

        foreach (var task in _indexingTasks)
        {
            task.Request.CancellationTokenSource?.Cancel();
        }

        Task.WaitAll([.. _indexingTasks.Select(t => t.Task)], 5000);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        // for some reason, the WindowChrome does not update automatically
        WindowChrome.GetWindowChrome(this).CaptionHeight = (int)(30 * newDpi.DpiScaleY);
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Close();
    private void OnAboutClick(object sender, RoutedEventArgs e) => new About { Owner = this }.ShowDialog();
    private void OnFileOpened(object sender, RoutedEventArgs e)
    {
        var item = (MenuItem)sender;
        ((MenuItem)item.FindName("openRecent")).IsEnabled = Settings.Current.RecentFiles.Count > 0;
    }

    private void OpenRecent_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is MenuItem item && item.DataContext is RecentFile recentFile)
        {
            if (recentFile is ClearRecentFiles)
            {
                Settings.Current.ClearRecentFiles();
                OnPropertyChanged(nameof(RecentFiles));
                return;
            }

            if (IOUtilities.PathIsFile(recentFile.FilePath))
            {
                OpenIndex(recentFile.FilePath!);
            }
        }
    }

    private void OpenIndex_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Choose an index file path",
            Filter = $"Doxie Index Files (*{DoxieIndex.FileExtension})|*{DoxieIndex.FileExtension}",
            DefaultExt = DoxieIndex.FileExtension,
            CheckFileExists = false,
            CheckPathExists = true,
            RestoreDirectory = true,
        };
        if (dlg.ShowDialog(this) != true)
            return;

        try
        {
            OpenIndex(dlg.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to open index file '{dlg.FileName}': {ex.GetInterestingExceptionMessage()}", AssemblyUtilities.GetProduct(), MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
    }

    private void CreateNewIndex_Click(object sender, RoutedEventArgs e)
    {
        //var index = DoxieIndex.OpenWrite(dlg.FileName);

        //var fld = new OpenFolderDialog
        //{
        //    Title = "Select a folder to index",
        //    Multiselect = false
        //};
        //if (fld.ShowDialog(this) != true)
        //    return;

        //var request = new IndexCreationRequest(fld.FolderName)
        //{
        //    CancellationTokenSource = new CancellationTokenSource()
        //};
        //_indexingTasks.Add(new WorkToDo(index, request, DoIndex(index, request)));
    }

    private async Task DoIndex(DoxieIndex index, IndexCreationRequest request)
    {
        _ = Dispatcher.BeginInvoke(() =>
        {
            statusProgress.Visibility = Visibility.Visible;
        });
        index.FileIndexing += OnFileIndexing;
        try
        {
            await index.AddToIndex(request).ConfigureAwait(false);
        }
        finally
        {
            index.FileIndexing -= OnFileIndexing;
            _ = Dispatcher.BeginInvoke(() =>
            {
                statusProgress.Visibility = Visibility.Hidden;
            });
        }
    }

    private void OnFileIndexing(object? sender, FileIndexingEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            statusProgress.Visibility = Visibility.Visible;
            statusProgressText.Text = Path.GetFileName(e.FilePath);
        });
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class WorkToDo(DoxieIndex index, IndexCreationRequest request, Task task)
    {
        public IndexCreationRequest Request { get; } = request ?? throw new ArgumentNullException(nameof(request));
        public DoxieIndex Index { get; } = index ?? throw new ArgumentNullException(nameof(index));
        public Task Task { get; } = task ?? throw new ArgumentNullException(nameof(task));
    }
}