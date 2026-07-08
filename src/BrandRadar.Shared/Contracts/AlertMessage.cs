using System.Text.Json.Serialization;

namespace BrandRadar.Shared.Contracts;

/// <summary>Crisis alert streamed to Kafka + indexed to Elasticsearch.</summary>
public sealed class AlertMessage
{
    [JsonPropertyName("id")] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [JsonPropertyName("brandId")] public int? BrandId { get; set; }
    [JsonPropertyName("brand")] public string? Brand { get; set; }
    [JsonPropertyName("level")] public string Level { get; set; } = "warning";
    [JsonPropertyName("reason")] public string Reason { get; set; } = string.Empty;
    [JsonPropertyName("negativeCount")] public int NegativeCount { get; set; }
    [JsonPropertyName("windowStart")] public DateTimeOffset WindowStart { get; set; }
    [JsonPropertyName("windowEnd")] public DateTimeOffset WindowEnd { get; set; }
    [JsonPropertyName("@timestamp")] public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    [JsonPropertyName("acknowledged")] public bool Acknowledged { get; set; }
}
