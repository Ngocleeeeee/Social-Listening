using BrandRadar.Shared.Constants;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace BrandRadar.Shared.Messaging;

/// <summary>Single shared connection + idempotent topology (exchange, ingest queue, DLQ).</summary>
public sealed class RabbitMqConnection(IOptions<RabbitMqOptions> options) : IAsyncDisposable
{
    private readonly RabbitMqOptions _o = options.Value;
    private IConnection? _conn;

    public async Task<IConnection> GetAsync(CancellationToken ct = default)
    {
        if (_conn is { IsOpen: true }) return _conn;
        var factory = new ConnectionFactory
        {
            HostName = _o.Host, Port = _o.Port, UserName = _o.UserName,
            Password = _o.Password, VirtualHost = _o.VirtualHost
        };
        _conn = await factory.CreateConnectionAsync(ct);
        return _conn;
    }

    public static async Task DeclareTopologyAsync(IChannel ch, CancellationToken ct = default)
    {
        await ch.ExchangeDeclareAsync(Rabbit.Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
        await ch.ExchangeDeclareAsync(Rabbit.Dlx, ExchangeType.Topic, durable: true, cancellationToken: ct);

        await ch.QueueDeclareAsync(Rabbit.IngestDlq, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await ch.QueueBindAsync(Rabbit.IngestDlq, Rabbit.Dlx, Rabbit.RouteRaw, cancellationToken: ct);

        var args = new Dictionary<string, object?> { ["x-dead-letter-exchange"] = Rabbit.Dlx };
        await ch.QueueDeclareAsync(Rabbit.IngestQueue, durable: true, exclusive: false, autoDelete: false, arguments: args, cancellationToken: ct);
        await ch.QueueBindAsync(Rabbit.IngestQueue, Rabbit.Exchange, Rabbit.RouteRaw, cancellationToken: ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_conn is not null) await _conn.DisposeAsync();
    }
}
