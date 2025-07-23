namespace Doxie
{
    public partial class MainWindow : Window
    {
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
    }
}