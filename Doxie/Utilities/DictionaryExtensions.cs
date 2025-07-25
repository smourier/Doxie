namespace Doxie.Utilities;

public static class DictionaryExtensions
{
    public const char DefaultSeparator = ';';
    public const char DefaultAssignment = '=';
    public static StringComparer DefaultStringComparer { get; } = StringComparer.OrdinalIgnoreCase;

    public static string? Serialize<T>(this IDictionary<string, T?>? dictionary, StringComparer? comparer = null, char separator = DefaultSeparator, char assignment = DefaultAssignment)
        => DictionarySerializer<T>.Serialize(dictionary, comparer, separator, assignment);

    public static IDictionary<string, T?> Deserialize<T>(this string? text, StringComparer? comparer = null, char separator = DefaultSeparator, char assignment = DefaultAssignment)
        => DictionarySerializer<T>.Deserialize(text, comparer, separator, assignment);

    public static string? GetNullifiedString(this IReadOnlyDictionary<string, string?>? dictionary, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (dictionary == null)
            return null;

        if (!dictionary.TryGetValue(name, out var str))
            return null;

        return str.Nullify();
    }

    public static string? GetNullifiedString(this IReadOnlyDictionary<string, object?>? dictionary, string name, IFormatProvider? provider = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (dictionary == null)
            return null;

        if (!dictionary.TryGetValue(name, out var obj) || obj == null)
            return null;

        return string.Format(provider, "{0}", obj).Nullify();
    }

    public static T? GetValue<T>(this IReadOnlyDictionary<string, string?>? dictionary, string name, IFormatProvider? provider = null, T? defaultValue = default)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (dictionary == null || !dictionary.TryGetValue(name, out var str))
            return defaultValue;

        return Conversions.ChangeType(str, defaultValue, provider);
    }

    public static bool TryGetValue<T>(this IReadOnlyDictionary<string, string?>? dictionary, string name, out T? value) => TryGetValue(dictionary, name, null, out value);
    public static bool TryGetValue<T>(this IReadOnlyDictionary<string, string?>? dictionary, string name, IFormatProvider? provider, out T? value)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (dictionary == null || !dictionary.TryGetValue(name, out var str))
        {
            value = default;
            return false;
        }

        return Conversions.TryChangeType(str, provider, out value);
    }

    public static T? GetValue<T>(this IReadOnlyDictionary<string, object?>? dictionary, string name, IFormatProvider? provider = null, T? defaultValue = default)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (dictionary == null || !dictionary.TryGetValue(name, out var obj))
            return defaultValue;

        return Conversions.ChangeType(obj, defaultValue, provider);
    }

    public static bool TryGetValue<T>(this IReadOnlyDictionary<string, object?>? dictionary, string name, out T? value) => TryGetValue(dictionary, name, null, out value);
    public static bool TryGetValue<T>(this IReadOnlyDictionary<string, object?>? dictionary, string name, IFormatProvider? provider, out T? value)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (dictionary == null || !dictionary.TryGetValue(name, out var obj))
        {
            value = default;
            return false;
        }

        return Conversions.TryChangeType(obj, provider, out value);
    }

    public static bool TryGetValueByPath<T>(this IReadOnlyDictionary<string, object?>? dictionary, string path, out T? value) => TryGetValueByPath(dictionary, path, null, out value);
    public static bool TryGetValueByPath<T>(this IReadOnlyDictionary<string, object?>? dictionary, string path, IFormatProvider? provider, out T? value)
    {
        if (!TryGetValueByPath(dictionary, path, out var obj))
        {
            value = default;
            return false;
        }

        return Conversions.TryChangeType(obj, provider, out value);
    }

    public static string? GetNullifiedValueByPath(this IReadOnlyDictionary<string, object?>? dictionary, string path, IFormatProvider? provider = null)
    {
        if (!TryGetValueByPath(dictionary, path, out object? value))
            return null;

        return Conversions.ChangeType<string>(value, null, provider).Nullify();
    }

    public static bool TryGetValueByPath(this IReadOnlyDictionary<string, object?>? dictionary, string path, out object? value)
    {
        ArgumentNullException.ThrowIfNull(path);
        value = null;
        if (dictionary == null)
            return false;

        var segments = path.Split('.');
        var current = dictionary;
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i].Nullify();
            if (segment == null)
                return false;

            if (!current.TryGetValue(segment, out var newElement))
                return false;

            // last?
            if (i == segments.Length - 1)
            {
                value = newElement;
                return true;
            }
            current = newElement as IReadOnlyDictionary<string, object?>;
            if (current == null)
                break;
        }
        return false;
    }

    public static ConcurrentDictionary<TKey, TValue> ToConcurrentDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> source) where TKey : notnull => source.ToConcurrentDictionary(null);
    public static ConcurrentDictionary<TKey, TValue> ToConcurrentDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> source, IEqualityComparer<TKey>? comparer) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(source);
        return new(source, comparer);
    }

    public static ConcurrentDictionary<TKey, TValue> ToConcurrentDictionary<TKey, TValue>(this IEnumerable<(TKey Key, TValue Value)> source) where TKey : notnull => source.ToConcurrentDictionary(null);
    public static ConcurrentDictionary<TKey, TValue> ToConcurrentDictionary<TKey, TValue>(this IEnumerable<(TKey Key, TValue Value)> source, IEqualityComparer<TKey>? comparer) where TKey : notnull => source.ToConcurrentDictionary(vt => vt.Key, vt => vt.Value, comparer);
    public static ConcurrentDictionary<TKey, TSource> ToConcurrentDictionary<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector) where TKey : notnull => ToConcurrentDictionary(source, keySelector, null);
    public static ConcurrentDictionary<TKey, TSource> ToConcurrentDictionary<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, IEqualityComparer<TKey>? comparer) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var count = 0;
        if (source is ICollection<TSource> collection)
        {
            count = collection.Count;
            if (count == 0)
                return new ConcurrentDictionary<TKey, TSource>(comparer);

            if (collection is TSource[] array)
                return ToConcurrentDictionary(array, keySelector, comparer);

            if (collection is List<TSource> list)
                return ToConcurrentDictionary(list, keySelector, comparer);
        }

        var d = new ConcurrentDictionary<TKey, TSource>(-1, count, comparer);
        foreach (var element in source)
        {
            d[keySelector(element)] = element;
        }
        return d;
    }

    private static ConcurrentDictionary<TKey, TSource> ToConcurrentDictionary<TSource, TKey>(TSource[] source, Func<TSource, TKey> keySelector, IEqualityComparer<TKey>? comparer) where TKey : notnull
    {
        var d = new ConcurrentDictionary<TKey, TSource>(-1, source.Length, comparer);
        for (var i = 0; i < source.Length; i++)
        {
            d[keySelector(source[i])] = source[i];
        }
        return d;
    }

    private static ConcurrentDictionary<TKey, TSource> ToConcurrentDictionary<TSource, TKey>(List<TSource> source, Func<TSource, TKey> keySelector, IEqualityComparer<TKey>? comparer) where TKey : notnull
    {
        var d = new ConcurrentDictionary<TKey, TSource>(-1, source.Count, comparer);
        foreach (var element in source)
        {
            d[keySelector(element)] = element;
        }
        return d;
    }

    public static ConcurrentDictionary<TKey, TElement> ToConcurrentDictionary<TSource, TKey, TElement>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector) where TKey : notnull => ToConcurrentDictionary(source, keySelector, elementSelector, null);
    public static ConcurrentDictionary<TKey, TElement> ToConcurrentDictionary<TSource, TKey, TElement>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey>? comparer) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(elementSelector);

        var count = 0;
        if (source is ICollection<TSource> collection)
        {
            count = collection.Count;
            if (count == 0)
                return new ConcurrentDictionary<TKey, TElement>(comparer);

            if (collection is TSource[] array)
                return ToConcurrentDictionary(array, keySelector, elementSelector, comparer);

            if (collection is List<TSource> list)
                return ToConcurrentDictionary(list, keySelector, elementSelector, comparer);
        }

        var d = new ConcurrentDictionary<TKey, TElement>(-1, count, comparer);
        foreach (var element in source)
        {
            d[keySelector(element)] = elementSelector(element);
        }
        return d;
    }

    private static ConcurrentDictionary<TKey, TElement> ToConcurrentDictionary<TSource, TKey, TElement>(TSource[] source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey>? comparer) where TKey : notnull
    {
        var d = new ConcurrentDictionary<TKey, TElement>(-1, source.Length, comparer);
        for (var i = 0; i < source.Length; i++)
        {
            d[keySelector(source[i])] = elementSelector(source[i]);
        }
        return d;
    }

    private static ConcurrentDictionary<TKey, TElement> ToConcurrentDictionary<TSource, TKey, TElement>(List<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey>? comparer) where TKey : notnull
    {
        var d = new ConcurrentDictionary<TKey, TElement>(-1, source.Count, comparer);
        foreach (var element in source)
        {
            d[keySelector(element)] = elementSelector(element);
        }
        return d;
    }

    public static ValueTask<ConcurrentDictionary<TKey, TSource>> ToConcurrentDictionaryAsync<TSource, TKey>(this IAsyncEnumerable<TSource> source, Func<TSource, TKey> keySelector, CancellationToken cancellationToken = default) where TKey : notnull =>
        ToConcurrentDictionaryAsync(source, keySelector, comparer: null, cancellationToken);

    public static ValueTask<ConcurrentDictionary<TKey, TSource>> ToConcurrentDictionaryAsync<TSource, TKey>(this IAsyncEnumerable<TSource> source, Func<TSource, TKey> keySelector, IEqualityComparer<TKey>? comparer, CancellationToken cancellationToken = default) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
        return Core(source, keySelector, comparer, cancellationToken);

        static async ValueTask<ConcurrentDictionary<TKey, TSource>> Core(IAsyncEnumerable<TSource> source, Func<TSource, TKey> keySelector, IEqualityComparer<TKey>? comparer, CancellationToken cancellationToken)
        {
            var d = new ConcurrentDictionary<TKey, TSource>(comparer);
            await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                var key = keySelector(item);
                d[key] = item;
            }
            return d;
        }
    }

    public static ValueTask<ConcurrentDictionary<TKey, TElement>> ToConcurrentDictionaryAsync<TSource, TKey, TElement>(this IAsyncEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, CancellationToken cancellationToken = default) where TKey : notnull =>
        ToConcurrentDictionaryAsync(source, keySelector, elementSelector, comparer: null, cancellationToken);

    public static ValueTask<ConcurrentDictionary<TKey, TElement>> ToConcurrentDictionaryAsync<TSource, TKey, TElement>(this IAsyncEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey>? comparer, CancellationToken cancellationToken = default) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(elementSelector);
        return Core(source, keySelector, elementSelector, comparer, cancellationToken);

        static async ValueTask<ConcurrentDictionary<TKey, TElement>> Core(IAsyncEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey>? comparer, CancellationToken cancellationToken)
        {
            var d = new ConcurrentDictionary<TKey, TElement>(comparer);
            await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                var key = keySelector(item);
                var value = elementSelector(item);
                d[key] = value;
            }
            return d;
        }
    }
}
