using System.Diagnostics;

namespace black_widow;

public static class Program {
    public const string BwdPrefix = "🕷  • ";

    public static async Task Main(string[] args) {
        if (args.Length is 0) {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"\n{BwdPrefix}You must provide a target URL.");
            Console.WriteLine("Example:");
            Console.WriteLine("  dotnet run -- https://books.toscrape.com/");
            Console.WriteLine("  (or if running the compiled binary: ./bwd https://books.toscrape.com/)");
            Console.ResetColor();
            return;
        }
        
        string target = args[0];
        
        if (!Uri.TryCreate(target, UriKind.Absolute, out Uri? validatedUri) || 
            (validatedUri.Scheme != Uri.UriSchemeHttp && validatedUri.Scheme != Uri.UriSchemeHttps))
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine($"\n{BwdPrefix}'{target}' is not a valid HTTP/HTTPS URL.");
            Console.ResetColor();
            return;
        }
        
        var handler = new HttpClientHandler { AllowAutoRedirect = true };
        using var httpClient = new HttpClient(handler);

        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36");
        httpClient.Timeout = TimeSpan.FromSeconds(15);

        var engine = new CrawlEngine(httpClient, maxDegreeOfParallelism: 10);
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            Console.WriteLine("\n[Shutdown] Shutdown requested! Finishing current pages...");
            e.Cancel = true;
            cts.Cancel();
        };
        
        var stopwatch = Stopwatch.StartNew();
        try {
            await engine.StartAsync(target, cts.Token);
        }
        catch (OperationCanceledException) {
            Console.WriteLine("[Shutdown] Halted safely via Cancellation Token.");
        }
        finally {
            stopwatch.Stop();
            Console.WriteLine($"\nExecution completed in: {stopwatch.Elapsed.TotalSeconds:F2} seconds.");
        }
    }
}