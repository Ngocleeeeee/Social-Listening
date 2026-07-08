using StackExchange.Redis;

namespace Dashboard.Api.Caching;

/// <summary>Persists "acknowledged" crisis alerts in a Redis set (graceful if Redis is down).</summary>
public interface IAlertAck
{
    Task AckAsync(string id);
    Task<HashSet<string>> AckedAsync();
}

public sealed class AlertAck(IConnectionMultiplexer? redis) : IAlertAck
{
    private const string Key = "ack:alerts";

    public async Task AckAsync(string id)
    {
        if (redis is { IsConnected: true }) await redis.GetDatabase().SetAddAsync(Key, id);
    }

    public async Task<HashSet<string>> AckedAsync()
    {
        if (redis is not { IsConnected: true }) return new();
        var members = await redis.GetDatabase().SetMembersAsync(Key);
        return members.Select(m => m.ToString()).ToHashSet();
    }
}
