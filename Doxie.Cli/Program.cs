namespace Doxie.Cli;

internal class Program
{
    static async Task Main()
    {
        Console.WriteLine("Doxie.Cli - Copyright © 2024-" + DateTime.Now.Year + " Simon Mourier. All rights reserved.");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            cts.Cancel();
            e.Cancel = true;
        };


        var name = "test" + Model.Index.FileExtension;
        if (File.Exists(name))
        {
            File.Delete(name);
        }

        using (var index = Model.Index.OpenWrite(name))
        {
            index.FileIndexing += (s, e) =>
            {
                Console.WriteLine($"Indexing file: {e.FilePath} count: {e.Batch.NumberOfDocuments} elapsed: {e.Batch.Duration} files/sec: {e.Batch.ProcessedFilesPerSecond}");
            };

            var request = new IndexScanRequest(index.EnsureDirectory(@"C:\Program Files (x86)\Windows Kits\10\Include\10.0.26100.0\um"))
            {
                CancellationTokenSource = cts
            };
            index.EnsureIncludedFileExtension(".c");
            index.EnsureIncludedFileExtension(".cpp");
            index.EnsureIncludedFileExtension(".h");
            index.EnsureIncludedFileExtension(".hpp");
            index.EnsureIncludedFileExtension(".idl");
            index.EnsureIncludedFileExtension(".inc");
            index.EnsureIncludedFileExtension(".inl");
            index.EnsureIncludedFileExtension(".hlsl");
            index.EnsureIncludedFileExtension(".hlsli");

            var indexResult = await index.Scan(request);
            if (indexResult.Exception != null)
            {
                Console.WriteLine(indexResult.Exception);
            }
        }

        using (var query = Model.Index.OpenRead(name))
        {
            Console.WriteLine($"Index was created by Doxie version {query.Version} (UTC).");

            foreach (var directory in query.Directories)
            {
                Console.WriteLine($" Directory: {directory}");
                var i = 0;
                foreach (var batch in directory.Batches)
                {
                    Console.WriteLine($"  Batch #{i++} has indexed {batch.NumberOfDocuments} documents.");
                    Console.WriteLine($"  Indexing skipped files: {batch.NumberOfSkippedFiles}");
                    Console.WriteLine($"  Indexing skipped folders: {batch.NumberOfSkippedDirectories}");
                    Console.WriteLine($"  Indexing has skipped the following non text extensions: {string.Join(", ", batch.NonIndexedFileExtensions)}");
                    Console.WriteLine($"  Indexing took {batch.Duration}.");
                    if (batch.Options.HasFlag(IndexDirectoryBatchOptions.IndexingWasCancelled))
                    {
                        Console.WriteLine($"  Indexing was cancelled.");
                    }
                }
            }

            var queryResult = query.Search("polling", IndexSearchResultItem.CreateItem);
            foreach (var doc in queryResult.Items)
            {
                Console.WriteLine($"Found: {doc}");
            }
        }
    }
}
