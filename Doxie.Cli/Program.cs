using System.Threading;

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


        var name = "test" + DoxieIndex.FileExtension;
        if (File.Exists(name))
        {
            File.Delete(name);
        }

        var index = DoxieIndex.OpenWrite(name);
        index.FileIndexing += (s, e) =>
        {
            Console.WriteLine($"Indexing file: {e.FilePath} count: {e.IndexedFilesCount} elapsed: {e.ElapsedTime} files/sec: {e.ProcessedFilesPerSecond}");
        };

        var request = new IndexCreationRequest(@"E:\github\microsoft\VCSamples")
        {
            CancellationTokenSource = cts
        };

        var indexResult = await index.AddToIndex(request);
        if (indexResult.Exception != null)
        {
            Console.WriteLine(indexResult.Exception);
        }

        var query = DoxieIndex.OpenRead(name);
        Console.WriteLine($"Index was created by Doxie version {query.Version} at {query.CreationDateUtc} (UTC).");
        Console.WriteLine($"Index has {query.CountOfDocuments} documents.");
        Console.WriteLine($"Indexing has skipped the following non text extensions: {string.Join(", ", query.NonTextExtensions)}");
        Console.WriteLine($"Indexing took {query.TotalDurationSeconds} seconds.");
        if (query.IndexingWasCancelled)
        {
            Console.WriteLine($"Indexing was cancelled.");
        }

        var queryResult = query.Search<DoxieSearchResultItem>("polling");
        foreach (var doc in queryResult.Items)
        {
            Console.WriteLine($"Found: {doc}");
        }
    }
}
