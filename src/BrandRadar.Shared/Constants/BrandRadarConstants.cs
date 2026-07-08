namespace BrandRadar.Shared.Constants;

/// <summary>RabbitMQ topology for the mention ingest pipeline.</summary>
public static class Rabbit
{
    public const string Exchange = "mentions.exchange";
    public const string Dlx = "mentions.exchange.dlx";
    public const string IngestQueue = "mentions.ingest";
    public const string IngestDlq = "mentions.ingest.dlq";
    public const string RouteRaw = "mention.raw";
}

/// <summary>Kafka topics for realtime streaming to the dashboard.</summary>
public static class KafkaTopics
{
    public const string AnalyzedMention = "analyzed-mention";
    public const string Alerts = "alerts";
}

/// <summary>Elasticsearch indexes.</summary>
public static class Indexes
{
    public const string Mentions = "mentions";
    public const string Alerts = "alerts";
}
