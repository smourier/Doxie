namespace Doxie;

public partial class MainWindow : Window
{
    private readonly List<WorkToDo> _indexingTasks = [];

    public MainWindow()
    {
        InitializeComponent();
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

    private void OnExitClick(object sender, RoutedEventArgs e) => Close();
    private void OnAboutClick(object sender, RoutedEventArgs e) => new About { Owner = this }.ShowDialog();

    private void OnRefresh(object sender, RoutedEventArgs e)
    {
    }

    private void OnFileOpened(object sender, RoutedEventArgs e)
    {
        var item = (MenuItem)sender;

        var working = _indexingTasks.Count > 0;
        ((MenuItem)item.FindName("createNewIndex")).IsEnabled = !working;
        ((MenuItem)item.FindName("addExistingIndex")).IsEnabled = !working;
    }

    private static void Prepare(FileDialog dialog)
    {
        dialog.Filter = $"Doxie Index Files (*{DoxieIndex.FileExtension})|*{DoxieIndex.FileExtension}|All Files (*.*)|*.*";
        dialog.DefaultExt = DoxieIndex.FileExtension;
        dialog.CheckPathExists = true;
    }

    private void CreateNewIndex_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Choose an index file",
            RestoreDirectory = true
        };
        Prepare(dlg);
        if (dlg.ShowDialog(this) != true)
            return;

        var index = DoxieIndex.OpenWrite(dlg.FileName);

        var fld = new OpenFolderDialog
        {
            Title = "Select a folder to index",
            Multiselect = false
        };
        if (fld.ShowDialog(this) != true)
            return;

        var request = new IndexCreationRequest(fld.FolderName)
        {
            CancellationTokenSource = new CancellationTokenSource()
        };
        _indexingTasks.Add(new WorkToDo(index, request, DoIndex(index, request)));
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

    private void AddExistingIndex_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Open an index file",
            CheckFileExists = true
        };
        Prepare(dlg);
        if (dlg.ShowDialog(this) != true)
            return;

        var index = DoxieIndex.OpenRead(dlg.FileName);
    }

    private sealed class WorkToDo(DoxieIndex index, IndexCreationRequest request, Task task)
    {
        public IndexCreationRequest Request { get; } = request ?? throw new ArgumentNullException(nameof(request));
        public DoxieIndex Index { get; } = index ?? throw new ArgumentNullException(nameof(index));
        public Task Task { get; } = task ?? throw new ArgumentNullException(nameof(task));
    }
}