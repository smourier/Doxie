namespace Doxie;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();

        DataContext = this;
        _ = Task.Run(Settings.Current.CleanRecentFiles);

        //Extensions.SaveDefaultTemplate<GroupBox>();
    }

    public Model.Index? Index { get; private set; }
    public Task? IndexingTask { get; private set; }
    public IndexDirectory? CurrentDirectory => directories.SelectedItem as IndexDirectory;
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
        var index = Model.Index.OpenWrite(filePath);
        Settings.Current.AddRecentFile(filePath);
        OnPropertyChanged(nameof(RecentFiles));
        Index?.Dispose();
        Index = index;
        OnPropertyChanged(nameof(Index));
        grid.Visibility = Visibility.Visible;
        if (index.Directories.Count > 0)
        {
            directories.ItemsSource = index.Directories;
            directories.SelectedIndex = 0;
            batches.Visibility = Visibility.Visible;
        }
        else
        {
            directories.ItemsSource = null;
            directories.SelectedIndex = -1;
            batches.Visibility = Visibility.Collapsed;
        }
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

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        // for some reason, the WindowChrome does not update automatically
        WindowChrome.GetWindowChrome(this).CaptionHeight = (int)(30 * newDpi.DpiScaleY);
    }

    private void OnDirectoriesSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CurrentDirectory));
        batches.Visibility = CurrentDirectory?.Batches.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
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
            Filter = $"Doxie Index Files (*{Model.Index.FileExtension})|*{Model.Index.FileExtension}",
            DefaultExt = Model.Index.FileExtension,
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void ViewIncludedExts_Click(object sender, RoutedEventArgs e)
    {
        var batch = sender.GetDataContext<IndexDirectoryBatch>();
        if (batch == null)
            return;

        var list = new ListWindow(batch.IncludedFileExtensions.Select(ext => new ListItem(ext)))
        {
            Owner = this,
            Title = "View Included File Extensions",
        };
        list.ShowDialog();
    }

    private void ViewNonIndexedExts_Click(object sender, RoutedEventArgs e)
    {
        var batch = sender.GetDataContext<IndexDirectoryBatch>();
        if (batch == null)
            return;

        var extensions = batch.NonIndexedFileExtensions.ToHashSet();
        if (Index != null)
        {
            foreach (var extension in Index.IncludedFileExtensions)
            {
                extensions.Remove(extension);
            }
        }

        var list = new ListWindow(extensions.Select(e => new ListItem(e)
        {
            ShowButton = true,
            ButtonText = "Include",
            Description = getDesc(e),
            Action = () =>
            {
                Index?.EnsureIncludedFileExtension(e);
                return true;
            }
        }))
        {
            Owner = this,
            Title = "View Non-Indexed File Extensions",
            SortByDescriptionButtonText = "Sort by perceived type",
            SortByNameButtonText = "Sort by extension",
        }
        ;
        list.ShowDialog();

        static string? getDesc(string ext)
        {
            var perceived = Perceived.GetPerceivedType(ext);
            if (perceived.PerceivedType == PerceivedType.Unknown ||
                perceived.PerceivedType == PerceivedType.Unspecified)
                return null;

            return Conversions.Decamelize(perceived.PerceivedType.ToString());
        }
    }

    private void ViewExcludedDirs_Click(object sender, RoutedEventArgs e)
    {
        var batch = sender.GetDataContext<IndexDirectoryBatch>();
        if (batch == null)
            return;

        var list = new ListWindow(batch.ExcludedDirectoryNames.Select(name => new ListItem(name)))
        {
            Owner = this,
            Title = "View Excluded Directories",
        };
        list.ShowDialog();
    }

    private void AddDirectory_Click(object sender, RoutedEventArgs e)
    {
        var fld = new OpenFolderDialog
        {
            Title = "Select a directory to add to index",
            Multiselect = false
        };
        if (fld.ShowDialog(this) == true)
        {
            Index?.EnsureDirectory(fld.FolderName);
        }
    }

    private void AddIncludedExt_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new AddExtensionWindow()
        {
            Owner = this,
        };
        if (dlg.ShowDialog() == true)
        {
            var ext = dlg.Extension;
            if (!string.IsNullOrWhiteSpace(ext) && Index != null)
            {
                Index.EnsureIncludedFileExtension(ext);
            }
        }
    }

    private void AddExcludedDir_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new AddDirectoryWindow()
        {
            Owner = this,
        };
        if (dlg.ShowDialog() == true)
        {
            var dirName = dlg.DirectoryName;
            if (!string.IsNullOrWhiteSpace(dirName) && Index != null)
            {
                Index.EnsureExcludedDirectoryName(dirName);
            }
        }
    }

    private void DeleteDir_Click(object sender, RoutedEventArgs e)
    {
        var dir = sender.GetDataContext<IndexDirectory>();
        if (dir == null)
            return;

        if (MessageBox.Show(this, $"Are you sure you want to delete the '{dir.Path}' directory from the index?",
            AssemblyUtilities.GetProduct(),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) != MessageBoxResult.Yes)
            return;

        Index?.RemoveDirectory(dir.Path);
    }

    private void ScanDir_Click(object sender, RoutedEventArgs e)
    {
        var dir = sender.GetDataContext<IndexDirectory>();
        if (dir == null)
            return;

        if (MessageBox.Show(this, $"Are you sure you want to scan the '{dir.Path}' directory?",
            AssemblyUtilities.GetProduct(),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) != MessageBoxResult.Yes)
            return;

        var indexingWindow = new IndexingWindow(dir, false)
        {
            Owner = this
        };

        indexingWindow.ShowDialog();
        OnPropertyChanged(nameof(Index));
        OnDirectoriesSelectionChanged(null!, null!);
    }

    private void RemapDir_Click(object sender, RoutedEventArgs e)
    {
        var dir = sender.GetDataContext<IndexDirectory>();
        if (dir == null)
            return;

        var fld = new OpenFolderDialog
        {
            Title = "Select a directory",
            Multiselect = false
        };
        if (fld.ShowDialog(this) != true)
            return;

        if (MessageBox.Show(this, $"Are you sure you want to change '{dir.Path}' directory to {fld.FolderName}?",
            AssemblyUtilities.GetProduct(),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) != MessageBoxResult.Yes)
            return;
    }

    private void RemoveIncludedExt_Click(object sender, RoutedEventArgs e)
    {
        var ext = sender.GetDataContext<string>();
        if (ext == null)
            return;

        if (MessageBox.Show(this, $"Are you sure you want to remove the '{ext}' extension?",
            AssemblyUtilities.GetProduct(),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) != MessageBoxResult.Yes)
            return;

        Index?.RemoveIncludedFileExtension(ext);
    }

    private void RemoveDirectoryName_Click(object sender, RoutedEventArgs e)
    {
        var dirName = sender.GetDataContext<string>();
        if (dirName == null)
            return;

        if (MessageBox.Show(this, $"Are you sure you want to remove the '{dirName}' directory name?",
            AssemblyUtilities.GetProduct(),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) != MessageBoxResult.Yes)
            return;

        Index?.RemoveExcludedDirectoryName(dirName);
    }

    private void ScanAllDirs_Click(object sender, RoutedEventArgs e)
    {
        if (Index == null || Index.Directories.Count == 0)
            return;

        var sw = Stopwatch.StartNew();
        foreach (var dir in Index.Directories)
        {
            var indexingWindow = new IndexingWindow(dir, true)
            {
                Owner = this
            };

            indexingWindow.ShowDialog();
            OnPropertyChanged(nameof(Index));
            OnDirectoriesSelectionChanged(null!, null!);
        }

        MessageBox.Show(this, $"All directories have been processed successfully in {sw.Elapsed}",
            AssemblyUtilities.GetProduct(),
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void QueryIndex_Click(object sender, RoutedEventArgs e)
    {
        if (Index == null)
            return;

        var search = Model.Index.OpenRead(Index.FilePath);
        var queryWindow = new QueryWindow(search)
        {
            Owner = this,
            Width = Width - 40,
            Height = Height - 40,
        }
        ;
        queryWindow.Show();
    }
}