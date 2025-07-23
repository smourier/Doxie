using Lucene.Net.Documents;
using static Lucene.Net.Documents.Field;

namespace Doxie.Model;

// represents a document that will be indexed (sort of a wrapper on lucene's document) with helpers method.
public class IndexDocument
{
    private readonly Document _document = [];

    public IndexDocument(string defaultFieldName)
    {
        ArgumentNullException.ThrowIfNull(defaultFieldName);
        DefaultFieldName = defaultFieldName;
    }

    public string DefaultFieldName { get; }
    public virtual bool AddStringFieldsToDefaultFieldValue { get; set; } = true;
    public virtual StringBuilder? DefaultFieldValue { get; set; }

    // add corpus field
    public Document FinishAndGetDocument()
    {
        var s = DefaultFieldValue?.ToString();
        if (!string.IsNullOrWhiteSpace(s))
        {
            _document.Add(new TextField(DefaultFieldName, s, Store.NO));
        }
        return _document;
    }

    // catch-all add field method, depending on the value type
    public virtual void AddField(string name, object value, bool store = false)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (value == null)
            return;

        if (value is string s)
        {
            AddField(name, s, store);
            return;
        }

        if (value is int i32)
        {
            AddField(name, i32, store);
            return;
        }

        if (value is long i64)
        {
            AddField(name, i64, store);
            return;
        }

        if (value is float fl)
        {
            AddField(name, fl, store);
            return;
        }

        if (value is double dbl)
        {
            AddField(name, dbl, store);
            return;
        }

        s = value.ToString()!;
        if (string.IsNullOrWhiteSpace(s))
            return;

        AddField(name, s, store);
    }

    // add a string field
    public virtual void AddField(string name, string value, bool store = false)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentNullException(nameof(name));

        if (string.IsNullOrWhiteSpace(value))
            return;

        _document.Add(new TextField(name, value, store ? Store.YES : Store.NO));

        if (AddStringFieldsToDefaultFieldValue)
        {
            DefaultFieldValue ??= new StringBuilder();
            DefaultFieldValue.Append(value);
        }
    }

    public virtual void AddField(string name, int value, bool store = false)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentNullException(nameof(name));

        _document.Add(new Int32Field(name, value, store ? Store.YES : Store.NO));
    }

    public virtual void AddField(string name, long value, bool store = false)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentNullException(nameof(name));

        _document.Add(new Int64Field(name, value, store ? Store.YES : Store.NO));
    }

    public virtual void AddField(string name, float value, bool store = false)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentNullException(nameof(name));

        _document.Add(new SingleField(name, value, store ? Store.YES : Store.NO));
    }

    public virtual void AddField(string name, double value, bool store = false)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentNullException(nameof(name));

        _document.Add(new DoubleField(name, value, store ? Store.YES : Store.NO));
    }

    public virtual void AddUntokenizedField(string name, string value, bool store = false)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentNullException(nameof(name));

        _document.Add(new StringField(name, value, store ? Store.YES : Store.NO));
    }
}
