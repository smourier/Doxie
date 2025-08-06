namespace Doxie.Utilities;

public class CountToBooleanConverter : IValueConverter
{
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is IEnumerable enumerable)
            return enumerable.Cast<object?>().Any();

        return false;
    }
}
