namespace Doxie.Utilities;

public class StringJoinConverter : IValueConverter
{
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is IEnumerable enumerable)
            return string.Join(", ", enumerable.Cast<object?>().WhereNotNull().Order());

        return value;
    }
}
