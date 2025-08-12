namespace Doxie.Utilities;

public class BooleanToParameterConverter : IValueConverter
{
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (Conversions.TryChangeType(value, out bool result) && result)
            return parameter;

        return null;
    }
}
