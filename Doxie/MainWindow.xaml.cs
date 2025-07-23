using Microsoft.Win32;

namespace Doxie;

public partial class MainWindow : Window
{
    private readonly List<Task> _indexingTasks = [];

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Close();
    private void OnAboutClick(object sender, RoutedEventArgs e) => new About { Owner = this }.ShowDialog();

    private void OnRefresh(object sender, RoutedEventArgs e)
    {

    }

    private void OnFileOpened(object sender, RoutedEventArgs e)
    {

    }

    private static void Prepare(FileDialog dialog)
    {
        dialog.RestoreDirectory = true;
        dialog.Filter = $"Doxie Index Files (*{DoxieIndex.FileExtension})|*{DoxieIndex.FileExtension}|All Files (*.*)|*.*";
        dialog.DefaultExt = DoxieIndex.FileExtension;
        dialog.CheckPathExists = true;
    }

    private void CreateNewIndex_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Choose an index file",
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

        var request = new IndexCreationRequest(fld.FolderName);
        _indexingTasks.Add(index.AddToIndex(request));
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
}