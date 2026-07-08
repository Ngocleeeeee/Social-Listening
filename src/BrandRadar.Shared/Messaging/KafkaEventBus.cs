using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace BrandRadar.Shared.Messaging;

public sealed class KafkaEventBus : IEventBus, IDisposable
{
    private readonly IProducer<string, string> _producer;

    public KafkaEventBus(IOptions<KafkaOptions> options)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            Acks = Acks.Leader,
            AllowAutoCreateTopics = true,
            MessageTimeoutMs = 5000
        };
        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishAsync<T>(string topic, string key, T value, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(value);
        await _producer.ProduceAsync(topic, new Message<string, string> { Key = key, Value = json }, ct);
    }

    public void Dispose()
    {
        try { _producer.Flush(TimeSpan.FromSeconds(3)); } catch { /* ignore */ }
        _producer.Dispose();
    }
}
