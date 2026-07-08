namespace Analysis.Worker;

public sealed class ElasticOptions
{
    public const string SectionName = "Elasticsearch";
    public string Uri { get; set; } = "http://elasticsearch:9200";
}
