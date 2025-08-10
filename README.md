# Doxie
A search engine, local to your machine, focused on source (or textual information) code. The application is written using WPF .NET 9, and the indexing and querying engine is powered by Apache Lucene.NET.

<img width="1003" height="801" alt="Screenshot 2025-08-10 175636" src="https://github.com/user-attachments/assets/a0ecb097-7bf4-458b-b88c-1f08764d287d" />

# Querying
The query window will give you the relative paths that matched the query, and you can click on a result and display the source. The source is supposed to be present on your machine (it's *not* stored in the index).

The selected source code is displayed using the [Monaco Editor](https://microsoft.github.io/monaco-editor/) so it's capable of some syntax coloring. All hits in source window are surrounded by yellow boxes and you can navigate from hit to hit.

By default, no wildcard is appended automatically in the query field, so when you type "IShellItem", it will only match "IShellItem" words in the index. Just use "IShellItem*" to match all texts starting with "IShellItem". You can also use a leading wildcard, ie: "\*IShellItem*".

Since the index uses Lucene, you can refer to [Apache Lucene - Query Parser Syntax](https://lucene.apache.org/core/2_9_4/queryparsersyntax.html) for all syntax options.

<img width="1187" height="857" alt="Screenshot 2025-08-10 175737" src="https://github.com/user-attachments/assets/a76bd32e-debd-4565-b9c6-feb22d3d1df0" />
