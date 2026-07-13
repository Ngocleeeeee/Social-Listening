using BrandRadar.Shared.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Collector.Worker;

/// <summary>
/// Periodically pulls real RSS feeds and publishes each new article as a RawMention to RabbitMQ
/// for the Analysis.Worker to consume.
/// </summary>
public sealed class CollectorService(
    IMentionPublisher publisher,
    RssCollector rss,
    BrandFeeds brandFeeds,
    IOptions<CollectorOptions> options,
    ILogger<CollectorService> logger) : BackgroundService
{
    private readonly CollectorOptions _o = options.Value;

    // Stories already published in earlier cycles. RSS hands back the same items every poll, so
    // without this every cycle re-floods RabbitMQ (and the NLP service) with known duplicates.
    // The Analysis worker's DB check is the durable safety net, so a bounded clear-on-overflow is
    // fine: at worst a story is re-published once and skipped downstream.
    private readonly HashSet<string> _published = new();
    private const int SeenCap = 50_000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Collector started ({Count} static feeds + dynamic brand feeds)", _o.RssFeeds.Length);

        // small delay so RabbitMQ is ready
        try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); } catch { return; }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(30, _o.RssIntervalSeconds)));
        do
        {
            if (_published.Count > SeenCap) _published.Clear();

            // Static category feeds + Google News feeds built live from brand keywords in the DB.
            var dynamicFeeds = await brandFeeds.BuildAsync(stoppingToken);
            var published = 0;
            foreach (var feed in _o.RssFeeds.Concat(dynamicFeeds).Distinct())
            {
                foreach (var m in await rss.FetchAsync(feed, stoppingToken))
                {
                    if (!_published.Add(m.Id)) continue; // already sent this story in an earlier cycle
                    try { await publisher.PublishRawAsync(m, stoppingToken); published++; }
                    catch (Exception ex) { _published.Remove(m.Id); logger.LogWarning(ex, "publish failed"); }
                }
            }
            logger.LogInformation("Collector cycle: {Published} new mentions published", published);
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
