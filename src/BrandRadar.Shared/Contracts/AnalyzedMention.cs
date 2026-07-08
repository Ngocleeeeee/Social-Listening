using System.Text.Json.Serialization;

namespace BrandRadar.Shared.Contracts;

/// <summary>Result after analysis — persisted to PostgreSQL and indexed into Elasticsearch.</summary>
public sealed class AnalyzedMention
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("brandId")] public int? BrandId { get; set; }
    [JsonPropertyName("brand")] public string? Brand { get; set; }
    [JsonPropertyName("source")] public string Source { get; set; } = string.Empty;
    [JsonPropertyName("author")] public string? Author { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
    [JsonPropertyName("url")] public string? Url { get; set; }
    [JsonPropertyName("lang")] public string Lang { get; set; } = "vi";
    [JsonPropertyName("sentiment")] public string Sentiment { get; set; } = "Neutral";
    [JsonPropertyName("sentimentScore")] public double SentimentScore { get; set; }
    [JsonPropertyName("topics")] public List<string> Topics { get; set; } = new();
    [JsonPropertyName("publishedAt")] public DateTimeOffset PublishedAt { get; set; }
    [JsonPropertyName("fingerprint")] public string Fingerprint { get; set; } = string.Empty;
    [JsonPropertyName("analyzedAt")] public DateTimeOffset AnalyzedAt { get; set; } = DateTimeOffset.UtcNow;
}
