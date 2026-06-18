using System.Collections.Concurrent;
using HtmlAgilityPack;

namespace black_widow;

public class CrawlEngine(HttpClient httpClient, int maxDegreeOfParallelism) {
    private readonly ConcurrentDictionary<string, byte> _discoveredUrls = new();
    private readonly ConcurrentQueue<string> _uncrawledUrls = new();

    public async Task StartAsync(string target, CancellationToken ct) {
        var normalizedUrl = NormalizeUrl(target);

        if (!string.IsNullOrEmpty(normalizedUrl)) {
            _uncrawledUrls.Enqueue(normalizedUrl);
        }
        
        Console.WriteLine($"Starting crawler with {maxDegreeOfParallelism} concurrent workers...");
        
        while (!_uncrawledUrls.IsEmpty && !ct.IsCancellationRequested) {
            List<string> currentBatch = new();
            while (_uncrawledUrls.TryDequeue(out var url)) {
                currentBatch.Add(url);
            }

            await Parallel.ForEachAsync(currentBatch, new ParallelOptions() {
                MaxDegreeOfParallelism = maxDegreeOfParallelism,
                CancellationToken = ct
            }, async (url, token) => {
                if (!_discoveredUrls.TryAdd(url, 0)) { return; }

                try {
                    await ProcessUrlAsync(url, token);
                }
                catch(Exception ex) {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[Error] Failed processing {url}: {ex.Message}");
                    Console.ResetColor();
                }
            });
        }
    }

    private async Task ProcessUrlAsync(string url, CancellationToken ct) {
        Console.WriteLine(Program.BwdPrefix + $"Crawling {url}");

        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode) { return; }
        string? contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType == null || 
            (!contentType.Contains("text/html") && !contentType.Contains("application/xhtml+xml"))) {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[Skipped] Non-HTML Content Type ({contentType}): {url}");
            Console.ResetColor();
            return; 
        }
        
        string rawHtml = await response.Content.ReadAsStringAsync(ct);
        var doc = new HtmlDocument();
        doc.LoadHtml(rawHtml);

        var freshUrls = ExtractUrls(url, doc);
        foreach (var link in freshUrls) {
            if (!_discoveredUrls.ContainsKey(link)) {
                _uncrawledUrls.Enqueue(link);
            }
        }

        string cleanText = ExtractCleanText(doc);
        var pageData = new CrawledPage() {
            Url = url,
            CleanText = cleanText,
            RawHtml = rawHtml
        };

        // OnPageProcessed(pageData);
    }

    private List<string> ExtractUrls(string url, HtmlDocument doc) {
        List<string> links = new();
        var anchorNodes = doc.DocumentNode.SelectNodes("//a[@href]");
        if (anchorNodes.Count is 0) { return links; }

        var baseUri = new Uri(url);

        foreach (var anchorNode in anchorNodes) {
            string link = anchorNode.GetAttributeValue("href", string.Empty);

            if (Uri.TryCreate(baseUri, link, out Uri? absoluteUri)) {
                if ((absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps)
                    && absoluteUri.Host == baseUri.Host) {
                    string normalized = NormalizeUrl(absoluteUri.AbsoluteUri);
                    if (!string.IsNullOrEmpty(normalized)) { links.Add(normalized); }
                }
            }
        }

        return links;
    }

    private string ExtractCleanText(HtmlDocument doc) {
        var nodesToRemove = doc.DocumentNode.SelectNodes("//script|//style|//nav|//footer|//header");
        if (nodesToRemove.Count is not 0) {
            foreach (var node in nodesToRemove) {
                node.Remove();
            }
        }
        
        string text = doc.DocumentNode.InnerText;
        return System.Net.WebUtility.HtmlDecode(text).Trim();
    }
    
    /// <summary>
    /// Normalizes URLs by stripping fragments like #about-us or tracking queries to avoid hitting same page  
    /// </summary>
    /// <param name="url">URL to normalize</param>
    /// <returns></returns>
    private string NormalizeUrl(string url) {
        try {
            var uri = new Uri(url.ToLowerInvariant().Trim());
            return $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";
        }
        catch {
            return string.Empty;
        }
    }
    
    private void OnPageProcessed(CrawledPage page) {
        // placeholder
        string preview = page.CleanText.Length > 60 ? page.CleanText[..60].Replace("\n", " ") + "..." : page.CleanText;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[Saved Payload] Hash: {page.ContentHash[..8]} | Snippet: {preview}");
        Console.ResetColor();
    }
}