using System.Text.Json;
using BrandRadar.Shared.Constants;
using BrandRadar.Shared.Contracts;
using RabbitMQ.Client;

namespace BrandRadar.Shared.Messaging;

/// <summary>
/// Publishes RawMention to the ingest exchange. Registered as a singleton, it reuses a single
/// channel for the lifetime of the process instead of opening a channel and re-declaring topology
/// on every message — that churn was a full round-trip of broker calls per publish. A gate keeps
/// publishes serialised, since an IChannel must not be used concurrently.
/// </summary>
public sealed class RabbitMqPublisher(RabbitMqConnection connection) : IMentionPublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IChannel? _channel;

    public async Task PublishRawAsync(RawMention mention, CancellationToken ct = default)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(mention);
        var props = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = mention.Id
        };

        await _gate.WaitAsync(ct);
        try
        {
            var ch = await EnsureChannelAsync(ct);
            await ch.BasicPublishAsync(Rabbit.Exchange, Rabbit.RouteRaw, mandatory: false,
                basicProperties: props, body: body, cancellationToken: ct);
        }
        finally { _gate.Release(); }
    }

    // Caller holds _gate. Topology declaration is idempotent and runs once per (re)connected channel.
    private async Task<IChannel> EnsureChannelAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true }) return _channel;
        if (_channel is not null) await _channel.DisposeAsync();
        var conn = await connection.GetAsync(ct);
        _channel = await conn.CreateChannelAsync(cancellationToken: ct);
        await RabbitMqConnection.DeclareTopologyAsync(_channel, ct);
        return _channel;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null) await _channel.DisposeAsync();
        _gate.Dispose();
    }
}
