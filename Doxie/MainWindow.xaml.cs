namespace Doxie;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();

        DataContext = this;
        _ = Task.Run(Settings.Current.CleanRecentFiles);

        //Extensions.SaveDefaultTemplate<GridSplitter>();
        var lastRecent = Settings.Current.RecentFiles.FirstOrDefault()?.FilePath;
        if (lastRecent != null)
        {
            OpenIndex(lastRecent);
        }
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

        Model.Index index;
        try
        {
            index = Model.Index.OpenWrite(filePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to open index file: {ex.GetInterestingExceptionMessage()}", AssemblyUtilities.GetProduct(), MessageBoxButton.OK, MessageBoxImage.Error);
            return;

        }
        Settings.Current.AddRecentFile(filePath);
        OnPropertyChanged(nameof(RecentFiles));
        Index?.Dispose();
        Index = index;
        OnPropertyChanged(nameof(Index));
        UpdateGridVisibility();
    }

    private void UpdateGridVisibility()
    {
        grid.Visibility = Visibility.Visible;
        if (Index?.Directories.Count > 0)
        {
            directories.ItemsSource = Index.Directories;
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
        if (e.Key == Key.F4)
        {
            var lastRecent = Settings.Current.RecentFiles.FirstOrDefault()?.FilePath;
            if (lastRecent != null)
            {
                OpenIndex(lastRecent);
            }
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            switch (e.Key)
            {
                case Key.Q:
                    QueryIndex_Click(null!, null!);
                    break;

                case Key.A:
                    AddDirectory_Click(null!, null!);
                    break;

                case Key.M:
                    AddDirectory_Click(null!, null!);
                    break;
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
        if (Index == null)
            return;

        var fld = new OpenFolderDialog
        {
            Title = "Select a directory to add to index",
            Multiselect = false
        };
        if (fld.ShowDialog(this) == true)
        {
            Index.EnsureDirectory(fld.FolderName);
            OnPropertyChanged(nameof(Index));
            UpdateGridVisibility();
        }
    }

    private void AddMultipleDirectories_Click(object sender, RoutedEventArgs e)
    {
        if (Index == null)
            return;

        var fld = new OpenFolderDialog
        {
            Title = "Select a parent directory to pick sub directories from",
            Multiselect = false,
        };
        if (fld.ShowDialog(this) == true)
        {
            var dlg = new AddMultipleDirectories(fld.FolderName, Index.Directories.Select(p => p.Path))
            {
                Owner = this,
            };
            if (dlg.ShowDialog() == true)
            {
                var paths = dlg.SelectedPaths.ToArray();
                if (paths.Length > 0)
                {
                    foreach (var path in paths)
                    {
                        Index.EnsureDirectory(path);
                    }
                    OnPropertyChanged(nameof(Index));
                    UpdateGridVisibility();
                }
            }
        }
    }

    private void AddIncludedExt_Click(object sender, RoutedEventArgs e)
    {
        if (Index == null)
            return;

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
        try
        {
            var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
        }
        catch
        {
            MessageBox.Show(this, "Failed to check for WebView2 runtime. Please ensure you have the latest version installed.", AssemblyUtilities.GetProduct(), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (Index == null || Index.Directories.Count == 0)
        {
            MessageBox.Show(this, "There's nothing to query yet, you could add a directory and scan it.", AssemblyUtilities.GetProduct(), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var queryWindow = new QueryWindow(Index.FilePath)
        {
            Owner = this,
            Width = Width - 40,
            Height = Height - 40,
        }
        ;
        queryWindow.Show();
    }

    private void OpenInExplorer_Click(object sender, RoutedEventArgs e)
    {
        var dir = sender.GetDataContext<IndexDirectory>();
        if (dir == null)
            return;

        Extensions.OpenInExplorer(dir.Path);
    }
}