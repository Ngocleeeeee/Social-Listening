namespace BrandRadar.Shared.Persistence.Entities;

public sealed class Alert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int? BrandId { get; set; }
    public string Level { get; set; } = "warning";     // warning | critical
    public string Reason { get; set; } = string.Empty;
    public int NegativeCount { get; set; }
    public DateTimeOffset WindowStart { get; set; }
    public DateTimeOffset WindowEnd { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
