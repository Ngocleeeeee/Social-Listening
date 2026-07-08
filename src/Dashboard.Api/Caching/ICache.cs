namespace Dashboard.Api.Caching;

/// <summary>Cache-aside helper. Redis-backed; degrades gracefully to the factory if Redis is down.</summary>
public interface ICache
{
    Task<T> GetOrSetAsync<T>(string key, TimeSpan ttl, Func<Task<T>> factory);
}
