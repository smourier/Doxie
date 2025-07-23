namespace Doxie.Cli;

internal class Program
{
    static async Task Main()
    {
        Console.WriteLine("Doxie.Cli - Copyright © 2024-" + DateTime.Now.Year + " Simon Mourier. All rights reserved.");

        var name = "test" + DoxieIndex.FileExtension;
        if (File.Exists(name))
        {
            File.Delete(name);
        }
        var index = DoxieIndex.OpenWrite(name);
        var result = await index.AddToIndex(new IndexCreationRequest(@"D:\temp\dotnet\SmoSession"));
        if (result.Exception != null)
        {
            Console.WriteLine(result.Exception);
        }
    }
}
