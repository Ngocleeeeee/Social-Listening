using System.Xml.Linq;
using BrandRadar.Shared.Contracts;
using Microsoft.Extensions.Logging;

namespace Collector.Worker;

/// <summary>Fetches RSS 2.0 (&lt;item&gt;) or Atom (&lt;entry&gt;) feeds and maps entries to RawMention.</summary>
public sealed class RssCollector(IHttpClientFactory http, ILogger<RssCollector> logger)
{
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    public async Task<IReadOnlyList<RawMention>> FetchAsync(string feedUrl, CancellationToken ct)
    {
        var result = new List<RawMention>();
        try
        {
            using var client = http.CreateClient("resilient");
            client.Timeout = TimeSpan.FromSeconds(15);
            // Some sources (e.g. Reddit) reject requests without a browser-like User-Agent.
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; BrandRadar/1.0)");
            var xml = await client.GetStringAsync(feedUrl, ct);
            var doc = XDocument.Parse(xml);
            var host = new Uri(feedUrl).Host;

            // RSS 2.0
            foreach (var item in doc.Descendants("item"))
            {
                var title = (string?)item.Element("title") ?? "";
                var desc = (string?)item.Element("description") ?? "";
                var link = (string?)item.Element("link");
                var published = DateTimeOffset.TryParse((string?)item.Element("pubDate"), out var p) ? p : DateTimeOffset.UtcNow;
                Add(result, host, title, desc, link, published);
            }

            // Atom (Reddit, etc.)
            foreach (var entry in doc.Descendants(Atom + "entry"))
            {
                var title = (string?)entry.Element(Atom + "title") ?? "";
                var body = (string?)entry.Element(Atom + "content") ?? (string?)entry.Element(Atom + "summary") ?? "";
                var link = entry.Elements(Atom + "link").FirstOrDefault()?.Attribute("href")?.Value;
                var dateStr = (string?)entry.Element(Atom + "updated") ?? (string?)entry.Element(Atom + "published");
                var published = DateTimeOffset.TryParse(dateStr, out var p) ? p : DateTimeOffset.UtcNow;
                Add(result, host, title, body, link, published);
            }

            logger.LogInformation("Feed {Feed}: {Count} items", feedUrl, result.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Feed fetch failed: {Feed}", feedUrl);
        }
        return result;
    }

    private static void Add(List<RawMention> list, string host, string title, string body, string? link, DateTimeOffset published)
    {
        var cleanTitle = StripHtml(title);
        if (string.IsNullOrWhiteSpace(cleanTitle)) return; // skip empty items

        var source = host.Replace("www.", "");
        // Dedup key = source + title (NOT the URL): some feeds (Google News, redirects) hand back a
        // different link every poll, which would defeat ExternalId dedup and spam the live feed.
        // source+title is stable across fetches yet still distinguishes the same story across outlets.
        var id = StableId(source + "|" + cleanTitle.ToLowerInvariant());

        list.Add(new RawMention
        {
            Id = id,
            Source = source,
            Title = cleanTitle,
            Content = StripHtml(string.IsNullOrWhiteSpace(body) ? title : body),
            Url = link,
            PublishedAt = published
        });
    }

    private static string StableId(string s)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(hash)[..24].ToLowerInvariant();
    }

    private static string StripHtml(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var decoded = System.Net.WebUtility.HtmlDecode(System.Net.WebUtility.HtmlDecode(s));
        var noTags = System.Text.RegularExpressions.Regex.Replace(decoded, "<.*?>", " ");
        return System.Text.RegularExpressions.Regex.Replace(noTags, @"\s+", " ").Trim();
    }
}
