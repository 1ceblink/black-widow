using System.Diagnostics;
using black_widow;

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

string target = "https://example.com/";
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
