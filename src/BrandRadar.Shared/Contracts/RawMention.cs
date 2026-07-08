using System.Text.Json.Serialization;

namespace BrandRadar.Shared.Contracts;

/// <summary>A collected item before analysis (published to RabbitMQ by Collector.Worker).</summary>
public sealed class RawMention
{
    [JsonPropertyName("id")] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [JsonPropertyName("source")] public string Source { get; set; } = string.Empty;
    [JsonPropertyName("author")] public string? Author { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
    [JsonPropertyName("url")] public string? Url { get; set; }
    [JsonPropertyName("publishedAt")] public DateTimeOffset PublishedAt { get; set; } = DateTimeOffset.UtcNow;
    [JsonPropertyName("collectedAt")] public DateTimeOffset CollectedAt { get; set; } = DateTimeOffset.UtcNow;
}
