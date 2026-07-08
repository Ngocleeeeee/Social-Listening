using System.Net.Http.Json;
using BrandRadar.Shared.Constants;
using BrandRadar.Shared.Contracts;
using BrandRadar.Shared.Messaging;
using Microsoft.Extensions.Logging;

namespace Analysis.Worker.Notifications;

/// <summary>Kênh gửi cảnh báo — Strategy pattern; thêm kênh mới không sửa RuleEngine.</summary>
public interface INotificationChannel
{
    string Name { get; }
    Task SendAsync(AlertMessage alert, string? target, CancellationToken ct = default);
}

/// <summary>In-app: đẩy lên Kafka → SignalR → hiển thị realtime trên dashboard.</summary>
public sealed class InAppChannel(IEventBus bus) : INotificationChannel
{
    public string Name => "inapp";
    public Task SendAsync(AlertMessage alert, string? target, CancellationToken ct = default)
        => bus.PublishAsync(KafkaTopics.Alerts, alert.Brand ?? "all", alert, ct);
}

/// <summary>Slack/webhook tương thích: POST JSON {text} tới Target. In-app luôn được gửi kèm.</summary>
public sealed class WebhookChannel(IEventBus bus, IHttpClientFactory httpFactory, ILogger<WebhookChannel> logger) : INotificationChannel
{
    public string Name => "webhook";
    public async Task SendAsync(AlertMessage alert, string? target, CancellationToken ct = default)
    {
        await bus.PublishAsync(KafkaTopics.Alerts, alert.Brand ?? "all", alert, ct); // vẫn hiện trên dashboard
        if (string.IsNullOrWhiteSpace(target)) return;
        try
        {
            var client = httpFactory.CreateClient("resilient");
            client.Timeout = TimeSpan.FromSeconds(8);
            await client.PostAsJsonAsync(target, new { text = $"🚨 [{alert.Level}] BrandRadar: {alert.Reason}" }, ct);
        }
        catch (Exception ex) { logger.LogWarning(ex, "webhook send failed: {Target}", target); }
    }
}

/// <summary>Chọn kênh theo tên; mặc định về in-app nếu tên lạ.</summary>
public sealed class NotificationDispatcher(IEnumerable<INotificationChannel> channels)
{
    private readonly Dictionary<string, INotificationChannel> _map =
        channels.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

    public Task DispatchAsync(string channel, AlertMessage alert, string? target, CancellationToken ct = default)
    {
        var ch = _map.GetValueOrDefault(channel ?? "inapp")
                 ?? _map.GetValueOrDefault("webhook")
                 ?? _map.Values.First();
        // "slack" là alias của webhook.
        if (string.Equals(channel, "slack", StringComparison.OrdinalIgnoreCase) && _map.TryGetValue("webhook", out var wh)) ch = wh;
        return ch.SendAsync(alert, target, ct);
    }
}
