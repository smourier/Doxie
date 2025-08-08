namespace Doxie.Utilities;

public class EnumDisplayConverter : IValueConverter
{
    private static IEnumerable<FieldInfo> GetEnumFields(Type enumType) => enumType.GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral && f.IsPublic && f.IsStatic);
    private static readonly ConcurrentDictionary<Type, EnumDef> _enumDefs = new();

    private readonly struct EnumDef(IReadOnlyDictionary<ulong, string> fields, string? zeroFieldName = null)
    {
        public readonly bool IsFlags => ZeroFieldName != null;
        public string? ZeroFieldName { get; } = zeroFieldName;
        public IReadOnlyDictionary<ulong, string> Fields { get; } = fields;

        public static EnumDef Get(Type enumType)
        {
            if (!_enumDefs.TryGetValue(enumType, out var def))
            {
                var fields = GetEnumFields(enumType)
                    .Where(f => f.IsLiteral && f.IsPublic && f.IsStatic)
                    .Select(f => new { v = Conversions.EnumToUInt64(f.GetValue(null)), n = f.GetCustomAttribute<DescriptionAttribute>()?.Description ?? Conversions.Decamelize(f.Name) })
                    .ToDictionary(f => f.v, f => f.n);
                string? zeroFieldName = null;
                if (Conversions.IsFlagsEnum(enumType))
                {
                    // zero is special case in flags enums since it can't be checked using binary operations
                    var zero = GetEnumFields(enumType).FirstOrDefault(f => Conversions.EnumToUInt64(f.GetValue(null)) == 0);
                    if (zero != null)
                    {
                        zeroFieldName = zero.GetCustomAttribute<DescriptionAttribute>()?.Description ?? Conversions.Decamelize(zero.Name);
                    }
                    else
                    {
                        zeroFieldName = string.Empty;
                    }
                }

                def = new EnumDef(fields, zeroFieldName);
                _enumDefs[enumType] = def;
            }
            return def;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
            return string.Empty;

        var separator = string.Format("{0}", parameter).Nullify() ?? ", ";
        var enumType = value.GetType();
        if (!enumType.IsEnum)
            return value.ToString() ?? string.Empty;

        var number = Conversions.EnumToUInt64(value);
        var def = EnumDef.Get(enumType);
        if (!def.IsFlags)
        {
            if (def.Fields.TryGetValue(number, out var name))
                return name;

            return value.ToString() ?? string.Empty;
        }

        if (number == 0)
            return def.ZeroFieldName ?? string.Empty;

        var sb = new StringBuilder();
        foreach (var kv in def.Fields)
        {
            if (kv.Key == 0)
                continue; // skip zero

            if ((number & kv.Key) == kv.Key)
            {
                if (sb.Length > 0)
                {
                    sb.Append(separator);
                }
                sb.Append(kv.Value);
            }
        }
        return sb.ToString();
    }
}

