using BrandRadar.Shared.Contracts;

namespace BrandRadar.Shared.Messaging;

public interface IMentionPublisher
{
    Task PublishRawAsync(RawMention mention, CancellationToken ct = default);
}
