using System.Security.Cryptography;
using System.Text;

namespace black_widow;

public class CrawledPage {
    public string Url { get; set; } = string.Empty;
    public string RawHtml { get; set; } = string.Empty;
    public string CleanText { get; set; } = string.Empty;
    public DateTime CrawledAt { get; set; } = DateTime.UtcNow;
    
    public string ContentHash => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CleanText)));
}