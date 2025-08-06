namespace Doxie;

public partial class About : Window
{
    public About()
    {
        InitializeComponent();
        Copyright.Text = "Doxie V" + AssemblyUtilities.GetInformationalVersion() + " " + AssemblyUtilities.GetConfiguration() + Environment.NewLine +
             "Copyright (C) 2024-" + DateTime.Now.Year + " Simon Mourier." + Environment.NewLine +
             "All rights reserved.";
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
        base.OnKeyDown(e);
    }

    private void OK_Click(object sender, RoutedEventArgs e) => Close();
    private void Details_Click(object sender, RoutedEventArgs e) { }
}
