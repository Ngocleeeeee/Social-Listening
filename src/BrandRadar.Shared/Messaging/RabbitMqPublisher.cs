using System.Text.Json;
using BrandRadar.Shared.Constants;
using BrandRadar.Shared.Contracts;
using RabbitMQ.Client;

namespace BrandRadar.Shared.Messaging;

public sealed class RabbitMqPublisher(RabbitMqConnection connection) : IMentionPublisher
{
    public async Task PublishRawAsync(RawMention mention, CancellationToken ct = default)
    {
        var conn = await connection.GetAsync(ct);
        await using var ch = await conn.CreateChannelAsync(cancellationToken: ct);
        await RabbitMqConnection.DeclareTopologyAsync(ch, ct);

        var body = JsonSerializer.SerializeToUtf8Bytes(mention);
        var props = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = mention.Id
        };
        await ch.BasicPublishAsync(Rabbit.Exchange, Rabbit.RouteRaw, mandatory: false,
            basicProperties: props, body: body, cancellationToken: ct);
    }
}
