using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Dashboard.Api.Caching;

public sealed class RedisCache(IConnectionMultiplexer? redis, ILogger<RedisCache> logger) : ICache
{
    public async Task<T> GetOrSetAsync<T>(string key, TimeSpan ttl, Func<Task<T>> factory)
    {
        // No Redis (or down) => just run the factory.
        if (redis is not { IsConnected: true })
            return await factory();

        try
        {
            var db = redis.GetDatabase();
            var cached = await db.StringGetAsync(key);
            if (cached.HasValue)
            {
                var hit = JsonSerializer.Deserialize<T>(cached!);
                if (hit is not null) return hit;
            }

            var fresh = await factory();
            await db.StringSetAsync(key, JsonSerializer.Serialize(fresh), ttl);
            return fresh;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis cache miss/error for {Key}; using source", key);
            return await factory();
        }
    }
}
