namespace Doxie;

internal static class Program
{
    internal static Task _monacoInstalledTask = null!;

    [STAThread]
    static void Main()
    {
        _monacoInstalledTask = MonacoResources.EnsureMonacoFilesAsync();
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        var app = new App();
        Application.Current.DispatcherUnhandledException += DispatcherUnhandledException;
        app.InitializeComponent();
        app.Run();
    }

    private static void DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) => e.Handled = true;

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
    }
}
