namespace BrandRadar.Shared.Persistence.Entities;

public sealed class Mention
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ExternalId { get; set; } = string.Empty;
    public int? BrandId { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? Author { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string Lang { get; set; } = "vi";
    public string Sentiment { get; set; } = "Neutral";
    public double SentimentScore { get; set; }
    public string? Topics { get; set; }            // comma-separated
    public string? Fingerprint { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public DateTimeOffset CollectedAt { get; set; } = DateTimeOffset.UtcNow;
}
