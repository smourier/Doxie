namespace Doxie.Utilities;

public static class Extensions
{
    public static bool EqualsIgnoreCase(this string? thisString, string? text, bool trim = true)
    {
        if (trim)
        {
            thisString = thisString.Nullify();
            text = text.Nullify();
        }

        if (thisString == null)
            return text == null;

        if (text == null)
            return false;

        if (thisString.Length != text.Length)
            return false;

        return string.Compare(thisString, text, StringComparison.OrdinalIgnoreCase) == 0;
    }

    public static string? Nullify(this string? text)
    {
        if (text == null)
            return null;

        if (string.IsNullOrWhiteSpace(text))
            return null;

        var t = text.Trim();
        return t.Length == 0 ? null : t;
    }

    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?>? source) where T : class
    {
        if (source == null)
            return [];

        return source.Where(item => item != null)!;
    }

    // this method will update the list *inplace* with the items from the enumerable, witout resetting the whole list.
    // so particularly useful for ObservableCollection<T> or similar.
    public static void UpdateWith<T>(this IList<T>? list, IEnumerable<T>? items, Action<T, T>? update = null, IEqualityComparer<T>? equalityComparer = null)
    {
        if (list == null || items == null)
            return;

        equalityComparer ??= EqualityComparer<T>.Default;
        var removed = list.ToHashSet(equalityComparer); // copy
        foreach (var item in items)
        {
            removed.RemoveWhere(i => equalityComparer.Equals(i, item));
            var existing = list.FirstOrDefault(i => equalityComparer.Equals(i, item));
            if (existing != null)
            {
                update?.Invoke(existing, item);
                continue;
            }

            list.Add(item);
        }

        foreach (var item in removed)
        {
            list.Remove(item);
        }
    }

    public static int AddRange<T>(this ICollection<T>? collection, IEnumerable<T>? enumerable)
    {
        if (collection == null || enumerable == null)
            return 0;

        var i = 0;
        foreach (var item in enumerable)
        {
            collection.Add(item);
            i++;
        }
        return i;
    }

    public static int IndexOf<T>(this IEnumerable<T> source, Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        if (source == null)
            return -1;

        var index = 0;
        foreach (var item in source)
        {
            if (predicate(item))
                return index;

            index++;
        }
        return -1;
    }

    public static byte[] LoadFromResource(this Assembly assembly, string name)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(name);
        using var stream = assembly.GetManifestResourceStream(name) ?? throw new Exception($"Stream '{name}' was not found in assembly {assembly.FullName}.");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    // we don't want unspecified datetimes
    public static bool IsValid(this DateTime dt) => dt != DateTime.MinValue && dt != DateTime.MaxValue && dt.Kind != DateTimeKind.Unspecified;
    public static bool IsValid(this DateTime? dt) => dt.HasValue && IsValid(dt.Value);

    public static Guid ComputeGuidHash(this string? text)
    {
        if (text == null)
            return Guid.Empty;

        return new Guid(MD5.HashData(Encoding.UTF8.GetBytes(text)));
    }

    [return: NotNullIfNotNull(nameof(exception))]
    public static string? GetAllMessages(this Exception? exception) => GetAllMessages(exception, Environment.NewLine);

    [return: NotNullIfNotNull(nameof(exception))]
    public static string? GetAllMessages(this Exception? exception, string separator)
    {
        if (exception == null)
            return null;

        var sb = new StringBuilder();
        AppendMessages(sb, exception, separator);
        return sb.ToString().Replace("..", ".");
    }

    private static void AppendMessages(StringBuilder sb, Exception? e, string separator)
    {
        if (e == null)
            return;

        // this one is not interesting...
        if (e is not TargetInvocationException)
        {
            if (sb.Length > 0)
            {
                sb.Append(separator);
            }
            sb.Append(e.Message);
        }
        AppendMessages(sb, e.InnerException, separator);
    }

    public static string? GetInterestingExceptionMessage(this Exception? exception) => GetInterestingException(exception)?.Message;
    public static Exception? GetInterestingException(this Exception? exception)
    {
        if (exception is TargetInvocationException tie && tie.InnerException != null)
            return GetInterestingException(tie.InnerException);

        return exception;
    }

    public static bool SaveDefaultTemplate<T>() => SaveDefaultTemplate(typeof(T));
    public static bool SaveDefaultTemplate(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var control = Application.Current.FindResource(type);
        if (control == null)
            return false;

        using var writer = new XmlTextWriter($"{type.FullName}.DefaultTemplate.xml", Encoding.UTF8);
        writer.Formatting = Formatting.Indented;
        XamlWriter.Save(control, writer);
        return true;
    }

    public static long CopyTo(this Stream input, Stream output, long count = long.MaxValue, int bufferSize = 0x14000)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        if (count <= 0)
            throw new ArgumentException(null, nameof(count));

        if (bufferSize <= 0)
            throw new ArgumentException(null, nameof(bufferSize));

        if (count < bufferSize)
        {
            bufferSize = (int)count;
        }

        var bytes = new byte[bufferSize];
        var total = 0;
        do
        {
            var max = (int)Math.Min(count - total, bytes.Length);
            var read = input.Read(bytes, 0, max);
            if (read == 0)
                break;

            output.Write(bytes, 0, read);
            total += read;
            if (total == count)
                break;
        }
        while (true);
        return total;
    }

    public static void SafeDispose(this IDisposable? disposable)
    {
        if (disposable == null)
            return;

        try
        {
            disposable.Dispose();
        }
        catch
        {
            // continue;
        }
    }

    public static void Dispose(this IEnumerable? disposables, bool throwOnError = false)
    {
        if (disposables == null)
            return;

        if (throwOnError)
        {
            foreach (var disposable in disposables.OfType<IDisposable>())
            {
                disposable?.Dispose();
            }
        }
        else
        {
            foreach (var disposable in disposables.OfType<IDisposable>())
            {
                try
                {
                    disposable?.Dispose();
                }
                catch
                {
                    // continue
                }
            }
        }
    }

    public static void WithDispose(this IEnumerable? disposables, Action action, bool throwOnError = false)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (disposables == null)
        {
            action();
            return;
        }

        try
        {
            action();
        }
        finally
        {
            if (throwOnError)
            {
                foreach (var disposable in disposables.OfType<IDisposable>())
                {
                    disposable?.Dispose();
                }
            }
            else
            {
                foreach (var disposable in disposables.OfType<IDisposable>())
                {
                    try
                    {
                        disposable?.Dispose();
                    }
                    catch
                    {
                        // continue
                    }
                }
            }
        }
    }

    public static T WithDispose<T>(this IEnumerable? disposables, Func<T> func, bool throwOnError = false)
    {
        ArgumentNullException.ThrowIfNull(func);
        if (disposables == null)
            return func();

        try
        {
            return func();
        }
        finally
        {
            if (throwOnError)
            {
                foreach (var disposable in disposables.OfType<IDisposable>())
                {
                    disposable?.Dispose();
                }
            }
            else
            {
                foreach (var disposable in disposables.OfType<IDisposable>())
                {
                    try
                    {
                        disposable?.Dispose();
                    }
                    catch
                    {
                        // continue
                    }
                }
            }
        }
    }

    public static void WithDispose(this IDisposable? disposable, Action action, bool throwOnError = false)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (disposable == null)
        {
            action();
            return;
        }

        try
        {
            action();
        }
        finally
        {
            if (throwOnError)
            {
                disposable.Dispose();
            }
            else
            {
                try
                {
                    disposable.Dispose();
                }
                catch
                {
                    // continue
                }
            }
        }
    }

    public static T? GetDataContext<T>(this object? obj)
    {
        if (obj is not FrameworkElement element)
            return default;

        if (element.DataContext is T dataContext)
            return dataContext;

        return default;
    }

    public static void WrapDispatcher(this Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            action();
            return;
        }
        dispatcher.Invoke(action);
    }

    public static T WrapDispatcher<T>(this Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
            return func();

        return dispatcher.Invoke(func);
    }

    public static void OpenAsText(string? filePath)
    {
        if (filePath == null || !IOUtilities.PathIsFile(filePath))
            return;

        try
        {
            using var reg = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Notepad++", false);
            if (reg != null)
            {
                var psi = new ProcessStartInfo("notepad++.exe", filePath)
                {
                    UseShellExecute = true,
                    CreateNoWindow = true,
                };
                Process.Start(psi);
                return;
            }
        }
        catch
        {
            // continue
        }

        try
        {
            var psi = new ProcessStartInfo("notepad.exe", filePath)
            {
                UseShellExecute = true,
                CreateNoWindow = true,
            };
            Process.Start(psi);
        }
        catch
        {
            // continue
        }
    }

    public static void OpenFolderAndSelectPath(string? path)
    {
        if (path == null)
            return;

        OpenFoldersAndSelectPaths([path]);
    }

    public static void OpenFoldersAndSelectPaths(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        // get all items & group by folder (path)
        var items = Item.FromPaths(paths);
        try
        {
            foreach (var group in items.GroupBy(g => g.ToString()))
            {
                var pidls = group.Select(g => g.ChildPidl).ToArray();
                _ = Item.SHOpenFolderAndSelectItems(group.First().ParentPidl, group.Count(), pidls, 0);
            }
        }
        finally
        {
            items.ForEach(i => i.Dispose());
        }
    }

    private sealed class Item : IDisposable
    {
        public string? ParentPath;
        public nint ParentPidl;
        public nint ChildPidl;
        public override string ToString() => ParentPath ?? string.Empty;

        public static List<Item> FromPaths(IEnumerable<string> files)
        {
            var list = new List<Item>();
            foreach (var file in files)
            {
                // build IShellItem
                if (SHCreateItemFromParsingName(file, 0, typeof(IShellItem).GUID, out var unk) < 0)
                    continue;

                const uint SIGDN_DESKTOPABSOLUTEPARSING = 0x80028000;
                var item = (IShellItem)Marshal.GetObjectForIUnknown(unk);
                item.GetParent(out var parent);
                parent.GetDisplayName(SIGDN_DESKTOPABSOLUTEPARSING, out var path);

                // get parent & item relative pidls
                ((IParentAndItem)item).GetParentAndItem(out var parentPidl, 0, out var childPidl);
                list.Add(new Item { ParentPath = path, ParentPidl = parentPidl, ChildPidl = childPidl });
            }
            return list;
        }

        public void Dispose()
        {
            if (ParentPidl != 0)
            {
                Marshal.FreeCoTaskMem(ParentPidl);
            }

            if (ChildPidl != 0)
            {
                Marshal.FreeCoTaskMem(ChildPidl);
            }
        }

        [DllImport("shell32.dll")]
        public static extern int SHOpenFolderAndSelectItems(nint pidlFolder, int cidl, nint[] apidl, uint dwFlags);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHCreateItemFromParsingName(string pszPath, nint pbc, in Guid riid, out nint ppv);
    }

    // incomplete
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
    private partial interface IShellItem
    {
        void BindToHandler();

        [PreserveSig]
        int GetParent(out IShellItem ppsi);

        [PreserveSig]
        int GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
    }

    // incomplete
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("b3a4b685-b685-4805-99d9-5dead2873236")]
    private partial interface IParentAndItem
    {
        void SetParentAndItem();

        [PreserveSig]
        int GetParentAndItem(out nint ppidlParent, nint ppsf, out nint ppidlChild);
    }

    public static void OpenInExplorerFromParent(this string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            try
            {
                var psi = new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
                {
                    UseShellExecute = true,
                    CreateNoWindow = true,
                };
                Process.Start(psi);
            }
            catch
            {
                // continue
            }
        }
    }

    public static void OpenInExplorer(this string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = path,
                });
            }
            catch
            {
                // continue
            }
        }
    }
}
