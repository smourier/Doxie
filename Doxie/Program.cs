namespace Doxie;

internal static class Program
{
    internal static Task _monacoInstalledTask = null!;

    [STAThread]
    static void Main()
    {
        _monacoInstalledTask = MonacoResources.EnsureMonacoFilesAsync();

        var app = new App();
        Application.Current.DispatcherUnhandledException += DispatcherUnhandledException;
        app.InitializeComponent();
        app.Run();
    }

    private static void DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show($"A fatal error has occurred: " + e.Exception.GetInterestingExceptionMessage(),
                    AssemblyUtilities.GetProduct(),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
    }
}
