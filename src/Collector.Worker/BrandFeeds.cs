using Dapper;
using Npgsql;

namespace Collector.Worker;

/// <summary>
/// Builds Google News RSS search feeds dynamically from the brand keywords stored in PostgreSQL,
/// so brands added at runtime are automatically crawled — no redeploy, no hardcoded list.
/// </summary>
public sealed class BrandFeeds(NpgsqlDataSource dataSource)
{
    public async Task<IReadOnlyList<string>> BuildAsync(CancellationToken ct)
    {
        var feeds = new List<string>();
        try
        {
            await using var conn = await dataSource.OpenConnectionAsync(ct);
            var keywords = (await conn.QueryAsync<string>(
                new CommandDefinition("SELECT DISTINCT \"Keyword\" FROM brand_keywords;", cancellationToken: ct))).AsList();

            foreach (var kw in keywords)
            {
                var q = Uri.EscapeDataString($"\"{kw}\"");
                feeds.Add($"https://news.google.com/rss/search?q={q}&hl=vi&gl=VN&ceid=VN:vi");
                feeds.Add($"https://www.reddit.com/search.rss?q={q}&sort=new&limit=25"); // Atom feed
            }
        }
        catch { /* DB not ready yet — skip this cycle */ }
        return feeds;
    }
}
