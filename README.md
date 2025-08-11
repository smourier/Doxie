# Doxie
A search engine, local to your machine, focused on source (or textual information) code.

<img width="1003" height="801" alt="Screenshot 2025-08-10 175636" src="https://github.com/user-attachments/assets/a0ecb097-7bf4-458b-b88c-1f08764d287d" />

# Indexing
The application is written using the following technologies:

* .NET 9
* WPF (dark mode) for the UI framework
* Lucene.NET for  the indexing and querying engine
* SQLite (through [SQLNado](https://github.com/smourier/SQLNado))

An index is a .doxidx file which contains everything. This file is a SQLite file, Doxie stores all the Lucene index in it (it's a Lucene SQLite directory implementation).
You can copy the .doxidx file somewhere else if file paths are valid in both places where you open Doxie.

And index contains any number of indexed local directories, and all files in these directories are indexed using the same criteria:
* The file's extension must be listed in the "Included file extensions" section.
* The file's containing directory name must not be listed in the "Excluded directory names" section.

To create an index you must:

* Use the "File" menu, select "Open or create an index..."
* Add one or more directories using the "Add a directory to index..." button, choose a directory
* Click on this directory's "Scan" button in the list
* The index will be created but by default, but no files will be put in it, since no file extensions are defined to be included in the index. Each time the directory is scanned, a "batch" will be associated (like a log of what happened). Older batch's related indexed data is deleted. Click on a directory to see its batches listed (latest first).
* So, you can either add extensions specifically using the "Add..." in the "Included file extensions" section, or since the directory was scanned, use the "View" button in the "Non-Indexed file extensions" columns of the latest batch, and choose what extensions you want to add from there (file extensions are annotated with a "perceived" type that is more a hint):

<img width="997" height="801" alt="image" src="https://github.com/user-attachments/assets/c1eafa9d-b216-4f70-b438-28f9ac7554f6" />

Once you have added extensions, you can re-scan the directory all all directories using the "Scan all directories" button. Remember file extensions and directory exclusion settings are global to the index, so to all directories.


Other points of interest:
* Doxie reopens the last used index automatically when the app is ran.
* All path treatments should be case-insensitive.
* You can also exclude directory names from indexing, like "obj", "bin", "debug", "release", using the using the "Add..." in the "Excluded directory names" section.
* You can add sub-directories of a parent directory in one shot using the "Add multiple directories to index..." button.
<img width="999" height="800" alt="image" src="https://github.com/user-attachments/assets/20ee2088-11e6-4669-8578-385281fa1604" />


# Querying
The query window will give you the relative paths that matched the query, and you can click on a result and display the source. The source is supposed to be present on your machine (it's *not* stored in the index). Right clicking on a path will display a context menu that allows you to open it using the Windows Shell or open its containing folder.

The selected source code is displayed using the [Monaco Editor](https://microsoft.github.io/monaco-editor/) so it's capable of some syntax coloring, and has a minimap for quick overview, among other features. All hits in source window are surrounded by yellow boxes and you can navigate from hit to hit.

By default, no wildcard is appended automatically in the query field, so when you type "IShellItem", it will only match "IShellItem" words in the index. Just use "IShellItem*" to match all texts starting with "IShellItem". You can also use a leading wildcard, ie: "\*IShellItem*".

Since the index uses Lucene, you can refer to [Apache Lucene - Query Parser Syntax](https://lucene.apache.org/core/2_9_4/queryparsersyntax.html) for all query syntax options.

<img width="1187" height="857" alt="Screenshot 2025-08-10 175737" src="https://github.com/user-attachments/assets/a76bd32e-debd-4565-b9c6-feb22d3d1df0" />
