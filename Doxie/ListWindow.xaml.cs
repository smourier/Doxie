namespace Doxie;

public partial class ListWindow : Window
{
    public ListWindow(IEnumerable enumerable)
    {
        InitializeComponent();
        list.ItemsSource = enumerable;
    }
}
