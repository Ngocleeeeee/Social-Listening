using System.Net.Http.Json;
using BrandRadar.Shared.Constants;
using BrandRadar.Shared.Contracts;
using BrandRadar.Shared.Messaging;
using BrandRadar.Shared.Persistence;
using BrandRadar.Shared.Persistence.Entities;
using Elastic.Clients.Elasticsearch;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Analysis.Worker;

/// <summary>
/// Sliding-window crisis detection: when negative mentions for a brand exceed a threshold within
/// the window (and no recent alert exists), raises an Alert — persisted to PostgreSQL, indexed to
/// Elasticsearch, and streamed to Kafka for realtime dashboard alerting.
/// </summary>
public sealed class CrisisDetector(
    IServiceScopeFactory scopeFactory,
    IEventBus bus,
    ElasticsearchClient es,
    IHttpClientFactory httpFactory,
    IOptions<CrisisOptions> options,
    ILogger<CrisisDetector> logger)
{
    private readonly CrisisOptions _o = options.Value;

    public async Task EvaluateAsync(int brandId, string brand, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var windowStart = now.AddMinutes(-_o.WindowMinutes);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrandRadarDbContext>();

        var negCount = await db.Mentions.CountAsync(
            m => m.BrandId == brandId && m.Sentiment == "Negative" && m.PublishedAt >= windowStart, ct);

        if (negCount < _o.NegativeThreshold) return;

        var cooldownStart = now.AddMinutes(-_o.CooldownMinutes);
        var recentAlert = await db.Alerts.AnyAsync(a => a.BrandId == brandId && a.CreatedAt >= cooldownStart, ct);
        if (recentAlert) return;

        var level = negCount >= _o.NegativeThreshold * 2 ? "critical" : "warning";
        var reason = $"{negCount} mention tiêu cực về {brand} trong {_o.WindowMinutes} phút";

        var alert = new Alert
        {
            BrandId = brandId, Level = level, Reason = reason, NegativeCount = negCount,
            WindowStart = windowStart, WindowEnd = now
        };
        db.Alerts.Add(alert);
        await db.SaveChangesAsync(ct);

        var msg = new AlertMessage
        {
            Id = alert.Id.ToString("N"), BrandId = brandId, Brand = brand, Level = level,
            Reason = reason, NegativeCount = negCount, WindowStart = windowStart, WindowEnd = now,
            CreatedAt = alert.CreatedAt
        };
        await es.IndexAsync(msg, i => i.Index(Indexes.Alerts).Id(msg.Id), ct);
        await bus.PublishAsync(KafkaTopics.Alerts, brand, msg, ct);
        await NotifyWebhookAsync(level, reason, ct);

        logger.LogWarning("CRISIS [{Level}] {Reason}", level, reason);
    }

    /// <summary>Volume-spike detection: mentions this window vs previous window (any sentiment).</summary>
    public async Task EvaluateSpikeAsync(int brandId, string brand, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var winStart = now.AddMinutes(-_o.SpikeWindowMinutes);
        var prevStart = now.AddMinutes(-2 * _o.SpikeWindowMinutes);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrandRadarDbContext>();

        var cur = await db.Mentions.CountAsync(m => m.BrandId == brandId && m.PublishedAt >= winStart, ct);
        if (cur < _o.SpikeMinCount) return;
        var prev = await db.Mentions.CountAsync(m => m.BrandId == brandId && m.PublishedAt >= prevStart && m.PublishedAt < winStart, ct);
        if (cur < _o.SpikeMultiplier * Math.Max(prev, 1)) return;

        var cd = now.AddMinutes(-_o.CooldownMinutes);
        if (await db.Alerts.AnyAsync(a => a.BrandId == brandId && a.Level == "spike" && a.CreatedAt >= cd, ct)) return;

        var reason = $"Lượng nhắc {brand} tăng đột biến: {cur} trong {_o.SpikeWindowMinutes} phút (trước đó {prev})";
        var alert = new Alert { BrandId = brandId, Level = "spike", Reason = reason, NegativeCount = cur, WindowStart = winStart, WindowEnd = now };
        db.Alerts.Add(alert);
        await db.SaveChangesAsync(ct);

        var msg = new AlertMessage
        {
            Id = alert.Id.ToString("N"), BrandId = brandId, Brand = brand, Level = "spike",
            Reason = reason, NegativeCount = cur, WindowStart = winStart, WindowEnd = now, CreatedAt = alert.CreatedAt
        };
        await es.IndexAsync(msg, i => i.Index(Indexes.Alerts).Id(msg.Id), ct);
        await bus.PublishAsync(KafkaTopics.Alerts, brand, msg, ct);
        await NotifyWebhookAsync("spike", reason, ct);
        logger.LogInformation("SPIKE {Reason}", reason);
    }

    /// <summary>Best-effort crisis notification to a Slack-compatible webhook (if configured).</summary>
    private async Task NotifyWebhookAsync(string level, string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_o.WebhookUrl)) return;
        try
        {
            var client = httpFactory.CreateClient("resilient");
            client.Timeout = TimeSpan.FromSeconds(8);
            var payload = new { text = $"🚨 [{level}] BrandRadar cảnh báo khủng hoảng: {reason}" };
            await client.PostAsJsonAsync(_o.WebhookUrl, payload, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "webhook notify failed");
        }
    }
}
