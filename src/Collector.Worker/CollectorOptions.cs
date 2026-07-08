namespace Collector.Worker;

public sealed class CollectorOptions
{
    public const string SectionName = "Collector";
    public int RssIntervalSeconds { get; set; } = 60;
    public string[] RssFeeds { get; set; } = [];
}
