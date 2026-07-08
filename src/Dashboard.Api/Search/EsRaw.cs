using System.Text;
using System.Text.Json;

namespace Dashboard.Api.Search;

/// <summary>
/// Thin raw Elasticsearch REST client (HTTP + JSON). Used for native aggregations that run inside
/// Elasticsearch over the whole dataset — avoids fetching documents into app memory. Robust & simple:
/// no typed DSL. Returns null on any failure so callers can fall back gracefully.
/// </summary>
public interface IEsRaw
{
    Task<JsonDocument?> SearchAsync(string index, string jsonBody, CancellationToken ct = default);
}

public sealed class EsRaw(IHttpClientFactory httpFactory, IConfiguration cfg, ILogger<EsRaw> logger) : IEsRaw
{
    private readonly string _baseUrl = (cfg["Elasticsearch:Uri"] ?? "http://elasticsearch:9200").TrimEnd('/');

    public async Task<JsonDocument?> SearchAsync(string index, string jsonBody, CancellationToken ct = default)
    {
        try
        {
            var client = httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            var resp = await client.PostAsync($"{_baseUrl}/{index}/_search?ignore_unavailable=true", content, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ES raw search failed");
            return null;
        }
    }
}
