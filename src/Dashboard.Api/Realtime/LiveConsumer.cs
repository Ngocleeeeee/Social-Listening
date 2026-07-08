using System.Text.Json;
using BrandRadar.Shared.Constants;
using BrandRadar.Shared.Contracts;
using BrandRadar.Shared.Messaging;
using Confluent.Kafka;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Dashboard.Api.Realtime;

/// <summary>
/// Consumes Kafka topics (analyzed-mention, alerts) and relays each event to browsers over SignalR
/// in real time — the pipeline "lights up" instantly without waiting for Elasticsearch reads.
/// </summary>
public sealed class LiveConsumer(
    IHubContext<LiveHub> hub,
    IOptions<KafkaOptions> options,
    ILogger<LiveConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        var config = new ConsumerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            GroupId = $"dashboard-live-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = true,
            AllowAutoCreateTopics = true
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(new[] { KafkaTopics.AnalyzedMention, KafkaTopics.Alerts });
        logger.LogInformation("Live consumer subscribed to {A}, {B}", KafkaTopics.AnalyzedMention, KafkaTopics.Alerts);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? cr;
                try { cr = consumer.Consume(TimeSpan.FromMilliseconds(500)); }
                catch (ConsumeException ex) { logger.LogWarning(ex, "consume error"); continue; }
                if (cr?.Message?.Value is null) continue;

                try
                {
                    if (cr.Topic == KafkaTopics.Alerts)
                    {
                        var alert = JsonSerializer.Deserialize<AlertMessage>(cr.Message.Value);
                        if (alert is not null) await hub.Clients.All.SendAsync("alert", alert, stoppingToken);
                    }
                    else
                    {
                        var m = JsonSerializer.Deserialize<AnalyzedMention>(cr.Message.Value);
                        if (m is not null) await hub.Clients.All.SendAsync("mention", m, stoppingToken);
                    }
                }
                catch (Exception ex) { logger.LogWarning(ex, "relay error"); }
            }
        }
        finally { consumer.Close(); }
    }
}
