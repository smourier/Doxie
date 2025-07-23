namespace Doxie;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        var app = new App();
        Application.Current.DispatcherUnhandledException += DispatcherUnhandledException;
        app.InitializeComponent();
        app.Run();
    }

    private static void DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
    }
}
