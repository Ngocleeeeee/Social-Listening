using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BrandRadar.Shared.Constants;
using BrandRadar.Shared.Contracts;
using BrandRadar.Shared.Messaging;
using BrandRadar.Shared.Persistence;
using BrandRadar.Shared.Persistence.Entities;
using BrandRadar.Shared.Sentiment;
using Elastic.Clients.Elasticsearch;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Analysis.Worker;

/// <summary>
/// Consumes RawMention from RabbitMQ, runs sentiment + topic + brand matching, then persists to
/// PostgreSQL and indexes into Elasticsearch. Prefetch enables parallel throughput; failures are
/// dead-lettered.
/// </summary>
public sealed class AnalysisConsumer(
    RabbitMqConnection connection,
    IServiceScopeFactory scopeFactory,
    ISentimentAnalyzer sentiment,
    BrandMatcher matcher,
    ElasticsearchClient es,
    IEventBus bus,
    CrisisDetector crisis,
    RuleEngine ruleEngine,
    IOptions<RabbitMqOptions> rabbitOptions,
    ILogger<AnalysisConsumer> logger) : BackgroundService
{
    private static readonly Regex ViDiacritics = new("[ăâđêôơưáàảãạấầẩẫậắằẳẵặéèẻẽẹếềểễệíìỉĩịóòỏõọốồổỗộớờởỡợúùủũụứừửữựýỳỷỹỵ]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var conn = await connection.GetAsync(stoppingToken);
        _channel = await conn.CreateChannelAsync(cancellationToken: stoppingToken);
        await RabbitMqConnection.DeclareTopologyAsync(_channel, stoppingToken);
        await _channel.BasicQosAsync(0, rabbitOptions.Value.PrefetchCount, false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnReceivedAsync;
        await _channel.BasicConsumeAsync(Rabbit.IngestQueue, autoAck: false, consumer, stoppingToken);
        logger.LogInformation("Analysis consuming {Queue}", Rabbit.IngestQueue);

        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        try
        {
            var raw = JsonSerializer.Deserialize<RawMention>(ea.Body.Span)
                      ?? throw new InvalidOperationException("bad payload");

            var text = $"{raw.Title} {raw.Content}";
            var sent = await sentiment.AnalyzeAsync(text);
            var topics = sentiment.ExtractTopics(text);
            var (brandId, brand) = await matcher.MatchAsync(text);
            var lang = ViDiacritics.IsMatch(text) ? "vi" : "en";
            var fingerprint = BrandRadar.Shared.Text.Fingerprint.Of(raw.Title); // cluster near-duplicate coverage
            var publishedUtc = raw.PublishedAt.ToUniversalTime(); // Postgres timestamptz requires UTC

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BrandRadarDbContext>();

            // Skip duplicates (RSS re-fetches the same items every poll) — don't re-index or re-stream.
            if (await db.Mentions.AnyAsync(m => m.ExternalId == raw.Id))
            {
                await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false);
                return;
            }

            db.Mentions.Add(new Mention
            {
                ExternalId = raw.Id, BrandId = brandId, Source = raw.Source, Author = raw.Author,
                Title = raw.Title, Content = raw.Content, Url = raw.Url, Lang = lang,
                Sentiment = sent.Label.ToString(), SentimentScore = sent.Score,
                Topics = string.Join(',', topics), Fingerprint = fingerprint, PublishedAt = publishedUtc
            });
            await db.SaveChangesAsync();

            var doc = new AnalyzedMention
            {
                Id = raw.Id, BrandId = brandId, Brand = brand, Source = raw.Source, Author = raw.Author,
                Title = raw.Title, Content = raw.Content, Url = raw.Url, Lang = lang,
                Sentiment = sent.Label.ToString(), SentimentScore = sent.Score,
                Topics = topics.ToList(), Fingerprint = fingerprint, PublishedAt = publishedUtc
            };
            await es.IndexAsync(doc, i => i.Index(Indexes.Mentions).Id(doc.Id));

            // Live feed = chỉ bài thật sự mới (đăng trong 6h). Bài cũ (vd cả backlog sau khi reset DB)
            // vẫn được lưu + index đầy đủ, nhưng KHÔNG dội vào tab "Trực tiếp" để feed khỏi nhảy loạn.
            var isFresh = publishedUtc >= DateTimeOffset.UtcNow.AddHours(-6);
            if (isFresh)
                await bus.PublishAsync(KafkaTopics.AnalyzedMention, brand ?? doc.Source, doc);
            if (brandId is int bid)
            {
                await crisis.EvaluateSpikeAsync(bid, brand ?? "?");        // built-in volume spike (any sentiment)
                if (sent.Label == SentimentLabel.Negative)
                    await crisis.EvaluateAsync(bid, brand ?? "?");         // built-in negative crisis
                await ruleEngine.EvaluateAsync(bid, brand ?? "?");         // user-configured alert rules
            }

            await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false);
        }
        catch (DbUpdateException)
        {
            // Same article arrived from two feeds within one poll cycle (race past the AnyAsync check).
            // The unique ExternalId index rejected the duplicate — treat as already-processed, don't re-stream.
            await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "analysis failed; dead-lettering");
            await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
        }
    }
}
