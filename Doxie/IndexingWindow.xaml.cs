namespace Doxie;

public partial class IndexingWindow : Window
{
    private readonly CancellationTokenSource _cts = new();
    private readonly IndexDirectory _directory;
    private readonly bool _autoClose;
    private bool _completed;
    private bool _cancelled;

    public IndexingWindow(IndexDirectory directory, bool autoClose)
    {
        ArgumentNullException.ThrowIfNull(directory);
        _directory = directory;
        _autoClose = autoClose;
        InitializeComponent();
        Title = $"Indexing '{_directory.Path}'";
        _ = Scan();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => ConfirmClose(true, true);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ConfirmClose(true, true);
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    private async Task Scan()
    {
        _directory.Index.FileIndexing += OnFileIndexing;
        try
        {
            var request = new IndexScanRequest(_directory)
            {
                CancellationTokenSource = _cts
            };

            var result = await _directory.Index.Scan(request).ConfigureAwait(false);
            _completed = true;
            _ = Dispatcher.BeginInvoke(() =>
            {
                if (_autoClose && !_cancelled)
                {
                    DialogResult = true;
                    Close();
                    return;
                }

                directory.Text = string.Empty;
                if (_cancelled)
                {
                    statusText.Text = "Indexing was cancelled.";
                }
                else
                {
                    if (result.Exception != null)
                    {
                        var error = result.Exception.GetInterestingException() ?? result.Exception;
                        statusText.Text = $"Indexing failed ({error.GetType().FullName}): " + error.GetInterestingExceptionMessage();
                    }
                    else
                    {
                        statusText.Text = "Indexing completed successfully.";
                    }
                }
                cancel.Content = "Close";
            });
        }
        finally
        {
            _directory.Index.FileIndexing -= OnFileIndexing;
        }
    }

    private void OnFileIndexing(object? sender, IndexingEventArgs e) => Dispatcher.BeginInvoke(() =>
    {
        directory.Text = Path.GetRelativePath(_directory.Path, Path.GetDirectoryName(e.FilePath)!);
        statusText.Text = Path.GetFileName(e.FilePath);
        numberOfDocuments.Text = e.Batch.NumberOfDocuments + " documents";
    });

    private bool ConfirmClose(bool canClose, bool cancel)
    {
        if (_completed)
        {
            if (canClose)
            {
                if (cancel)
                {
                    DialogResult = false;
                }
                Close();
            }

            return true;
        }

        if (MessageBox.Show(this, $"Are you sure you want to cancel the '{_directory.Path}' directory indexing process?",
            AssemblyUtilities.GetProduct(),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) != MessageBoxResult.Yes)
            return false;

        _cts.Cancel();
        _cancelled = true;
        return true;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (!ConfirmClose(false, false))
        {
            e.Cancel = true;
            return;
        }
    }
}
