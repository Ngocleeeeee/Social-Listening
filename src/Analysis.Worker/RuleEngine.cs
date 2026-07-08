using Analysis.Worker.Notifications;
using BrandRadar.Shared.Constants;
using BrandRadar.Shared.Contracts;
using BrandRadar.Shared.Persistence;
using BrandRadar.Shared.Persistence.Entities;
using Elastic.Clients.Elasticsearch;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Analysis.Worker;

/// <summary>
/// Alert Rules Engine — đánh giá các luật cảnh báo do người dùng cấu hình (bảng alert_rules) trên mỗi
/// mention mới. Mỗi luật có loại điều kiện, cửa sổ thời gian, ngưỡng, cooldown và kênh gửi riêng.
/// Đây là phần "cấu hình được" bổ sung cho crisis detection built-in.
///
/// Kỹ thuật: cache danh sách luật (refresh 30s) để tránh query DB mỗi event; cooldown lưu ở
/// LastFiredAt; khi kích hoạt thì tạo Alert → index ES → gửi qua NotificationDispatcher (Strategy).
/// </summary>
public sealed class RuleEngine(
    IServiceScopeFactory scopeFactory,
    ElasticsearchClient es,
    NotificationDispatcher dispatcher,
    ILogger<RuleEngine> logger)
{
    private List<AlertRule> _rules = new();
    private DateTimeOffset _loadedAt = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private async Task<List<AlertRule>> GetRulesAsync(BrandRadarDbContext db)
    {
        if (Fresh()) return _rules;
        await _lock.WaitAsync();
        try
        {
            if (Fresh()) return _rules;                       // double-check sau khi vào khoá
            _rules = await db.AlertRules.Where(r => r.Enabled).AsNoTracking().ToListAsync();
            _loadedAt = DateTimeOffset.UtcNow;
            return _rules;
        }
        finally { _lock.Release(); }

        bool Fresh() => _rules.Count > 0 && DateTimeOffset.UtcNow - _loadedAt < TimeSpan.FromSeconds(30);
    }

    public async Task EvaluateAsync(int brandId, string brand, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrandRadarDbContext>();
        var rules = await GetRulesAsync(db);

        var now = DateTimeOffset.UtcNow;
        foreach (var rule in rules.Where(r => r.BrandId is null || r.BrandId == brandId))
        {
            if (rule.LastFiredAt is { } last && now - last < TimeSpan.FromMinutes(rule.CooldownMinutes)) continue;

            var windowStart = now.AddMinutes(-rule.WindowMinutes);
            var q = db.Mentions.Where(m => m.BrandId == brandId && m.PublishedAt >= windowStart);

            var (fired, measured, level) = rule.Type switch
            {
                "volume"   => await VolumeAsync(q, rule, ct),
                "negshare" => await NegShareAsync(q, rule, ct),
                _          => await NegativeAsync(q, rule, ct),   // "negative"
            };
            if (!fired) continue;

            var reason = $"[{rule.Name}] {Describe(rule, measured, brand)}";
            var alert = new Alert
            {
                BrandId = brandId, Level = level, Reason = reason,
                NegativeCount = (int)measured, WindowStart = windowStart, WindowEnd = now
            };
            db.Alerts.Add(alert);

            // cập nhật cooldown (entity đang tracked qua truy vấn khác? dùng attach an toàn)
            var tracked = await db.AlertRules.FirstAsync(r => r.Id == rule.Id, ct);
            tracked.LastFiredAt = now;
            await db.SaveChangesAsync(ct);
            rule.LastFiredAt = now; // cập nhật cache cục bộ

            var msg = new AlertMessage
            {
                Id = alert.Id.ToString("N"), BrandId = brandId, Brand = brand, Level = level,
                Reason = reason, NegativeCount = (int)measured, WindowStart = windowStart, WindowEnd = now, CreatedAt = alert.CreatedAt
            };
            await es.IndexAsync(msg, i => i.Index(Indexes.Alerts).Id(msg.Id), ct);
            await dispatcher.DispatchAsync(rule.Channel, msg, rule.Target, ct);
            logger.LogWarning("RULE FIRED {Reason} via {Channel}", reason, rule.Channel);
        }
    }

    private static async Task<(bool, double, string)> NegativeAsync(IQueryable<Mention> q, AlertRule r, CancellationToken ct)
    {
        var neg = await q.CountAsync(m => m.Sentiment == "Negative", ct);
        var level = neg >= r.Threshold * 2 ? "critical" : "warning";
        return (neg >= r.Threshold, neg, level);
    }

    private static async Task<(bool, double, string)> VolumeAsync(IQueryable<Mention> q, AlertRule r, CancellationToken ct)
    {
        var total = await q.CountAsync(ct);
        return (total >= r.Threshold, total, "spike");
    }

    private static async Task<(bool, double, string)> NegShareAsync(IQueryable<Mention> q, AlertRule r, CancellationToken ct)
    {
        var total = await q.CountAsync(ct);
        if (total < 5) return (false, 0, "warning");
        var neg = await q.CountAsync(m => m.Sentiment == "Negative", ct);
        var pct = 100.0 * neg / total;
        return (pct >= r.Threshold, Math.Round(pct), pct >= r.Threshold * 1.5 ? "critical" : "warning");
    }

    private static string Describe(AlertRule r, double v, string brand) => r.Type switch
    {
        "volume"   => $"{brand}: {(int)v} lượt nhắc trong {r.WindowMinutes} phút (ngưỡng {r.Threshold:F0})",
        "negshare" => $"{brand}: {v:F0}% tiêu cực trong {r.WindowMinutes} phút (ngưỡng {r.Threshold:F0}%)",
        _          => $"{brand}: {(int)v} mention tiêu cực trong {r.WindowMinutes} phút (ngưỡng {r.Threshold:F0})",
    };
}
