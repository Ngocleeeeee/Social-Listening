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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Collector started ({Count} static feeds + dynamic brand feeds)", _o.RssFeeds.Length);

        // small delay so RabbitMQ is ready
        try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); } catch { return; }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(30, _o.RssIntervalSeconds)));
        do
        {
            // Static category feeds + Google News feeds built live from brand keywords in the DB.
            var dynamicFeeds = await brandFeeds.BuildAsync(stoppingToken);
            foreach (var feed in _o.RssFeeds.Concat(dynamicFeeds).Distinct())
            {
                foreach (var m in await rss.FetchAsync(feed, stoppingToken))
                {
                    try { await publisher.PublishRawAsync(m, stoppingToken); }
                    catch (Exception ex) { logger.LogWarning(ex, "publish failed"); }
                }
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
