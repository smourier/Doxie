# Doxie
A search engine, local to your machine, focused on source (or textual information) code.

<img width="1002" height="803" alt="image" src="https://github.com/user-attachments/assets/fbb72ff0-ccb2-4d8f-a905-ef7d818e2025" />

# Indexing
The application is written using the following technologies:

* .NET 9
* WPF (dark mode, high DPI support) for the UI framework
* Lucene.NET for  the indexing and querying engine
* SQLite (through [SQLNado](https://github.com/smourier/SQLNado))

An index is a .doxidx file which contains everything. This file is a SQLite file, Doxie stores all the Lucene index in it (it's a Lucene SQLite directory implementation).
You can copy the .doxidx file somewhere else if file paths are valid in both places where you open Doxie.

And index contains any number of indexed local directories, and all files in these directories are indexed using the same criteria:
* The file's path or name must match one of items from ""File inclusions or exclusions" section. If this item is an exclusion criteria, it must not match it.
* The file's containing directory name must not be listed in the "Excluded directory names" section.

To create an index you must:

* Use the "File" menu, select "Open or create an index..."
* Add one or more directories using the "Add a directory to index..." button, choose a directory
* Click on this directory's "Scan" button in the list
* The index will be created but by default, but no files will be put in it, since no file extensions are defined to be included in the index. Each time the directory is scanned, a "batch" will be associated (like a log of what happened). Older batch's related indexed data is deleted. Click on a directory to see its batches listed (latest first).
* So, you can either add extensions specifically using the "Add..." in the ""File inclusions or exclusions" section, or since the directory was scanned, use the "View" button in the "Non-Indexed file extensions" columns of the latest batch, and choose what extensions you want to add from there (file extensions are annotated with a "perceived" type that is more a hint):

<img width="997" height="801" alt="image" src="https://github.com/user-attachments/assets/c1eafa9d-b216-4f70-b438-28f9ac7554f6" />

Once you have added inclusion (and maybe exclusions) patterns, you can re-scan a specific directory or all directories using the "Scan all directories" button. Remember inclusions/exclusons and directory exclusion settings are global to the index, so to all directories.

Other points of interest:
* Doxie reopens the last used index automatically when the app is ran.
* You can also drag & drop a .doxidx file on Doxie's window to open it.
* From command line, you can also run `"doxie.exe <some file path>"` to open it.
* All path treatments should be case-insensitive.
* You can also exclude directory names from indexing, like "obj", "bin", "debug", "release", using the using the "Add..." in the "Excluded directory names" section.
* You can add sub-directories of a parent directory in one shot using the "Add multiple directories to index..." button.
* To re-use inclusions/exclusions pattern sets, you can copy a doxidx file somewhere else, open it and remove all directories from there.
* You can use the Doxie project as a .NET reference and write other tools. Check the Doxie.Cli project for a small sample of this.

<img width="999" height="800" alt="image" src="https://github.com/user-attachments/assets/20ee2088-11e6-4669-8578-385281fa1604" />


If you want for example to index all .js files but no .min.js files, you can add ".js", and "\*.min.js" and check 'Is exclusion' for the later. Note "\*.min.js" starts with a "\*" wich means "end with .min.js", instead of using the file extension in Windows terms.

<img width="336" height="239" alt="image" src="https://github.com/user-attachments/assets/0a3311d9-0f10-4222-805c-ac4fbfa0073f" />

Exclusions are shown with a red background:

<img width="463" height="213" alt="image" src="https://github.com/user-attachments/assets/c22a6354-483f-44cd-be78-f9ee6dee691f" />

# Querying
The query window will give you the relative paths that matched the query, and you can click on a result and display the source. The source is supposed to be present on your machine (it's *not* stored in the index). Right clicking on a path will display a context menu that allows you to open it using the Windows Shell or open its containing folder.

The selected source code is displayed using the [Monaco Editor](https://microsoft.github.io/monaco-editor/) so it's capable of some syntax coloring, and has a minimap for quick overview, among other features. All hits in source window are surrounded by yellow boxes and you can navigate from hit to hit.

By default, no wildcard is appended automatically in the query field, so when you type "IShellItem", it will only match "IShellItem" words in the index. Just use "IShellItem*" to match all texts starting with "IShellItem". You can also use a leading wildcard, ie: "\*IShellItem*".

<img width="1181" height="853" alt="image" src="https://github.com/user-attachments/assets/b3b6421d-1f7a-4cbd-850a-153f78411ed0" />

Since the index uses Lucene, you can refer to [Apache Lucene - Query Parser Syntax](https://lucene.apache.org/core/2_9_4/queryparsersyntax.html) for all query syntax options.

For example in the following example, the "embedded sample code AND path:rea\*" query will search on various texts and on the relative path (parsed as a list of tokens). Note the AND token *must* be UPPERCASE for it to work:

<img width="1278" height="820" alt="Screenshot 2025-08-13 130624" src="https://github.com/user-attachments/assets/96016ea6-d523-4e03-9297-aaa71d289a97" />

Available text fields are "path" (the relative path) and "ext" (the file's extension).
