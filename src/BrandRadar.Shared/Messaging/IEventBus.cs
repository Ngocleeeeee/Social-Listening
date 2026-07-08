namespace BrandRadar.Shared.Messaging;

/// <summary>Publishes JSON events to Kafka (realtime stream to the dashboard).</summary>
public interface IEventBus
{
    Task PublishAsync<T>(string topic, string key, T value, CancellationToken ct = default);
}
