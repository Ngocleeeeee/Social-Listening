using System.Text.Json;
using BrandRadar.Shared.Constants;
using BrandRadar.Shared.Contracts;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;

namespace Dashboard.Api.Search;

public sealed class MentionFilter
{
    public string? Brand { get; set; }
    public string? Sentiment { get; set; }
    public string? Keyword { get; set; }
    public string? Lang { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public int Page { get; set; } = 1;
    public int Size { get; set; } = 20;
    /// <summary>"published" = theo thời gian đăng bài; mặc định "analyzed" = theo thời điểm thu thập.</summary>
    public string? Sort { get; set; }
    public int Offset => Math.Max(0, (Math.Max(1, Page) - 1) * Math.Clamp(Size, 1, 200));
}

public sealed record Bucket(string Key, int Count);
public sealed record Overview(int Total, int Positive, int Neutral, int Negative);
public sealed record TimePoint(string Time, int Total, int Negative);
public sealed record Trend(int Total, int PrevTotal, int Negative, int PrevNegative);
public sealed record BrandTrend(string Brand, int Current, int Previous);
public sealed record CrisisSummary(string? Brand, int NegativeCount, List<string> Headlines, List<string> Keywords, string? Narrative);
public sealed record Story(string Fingerprint, string Title, int SourceCount, List<string> Sources, string Sentiment, DateTimeOffset Latest, string? Url);
public sealed record DashboardSnapshot(
    Overview Overview,
    IReadOnlyCollection<Bucket> Sources,
    IReadOnlyCollection<Bucket> Topics,
    IReadOnlyCollection<TimePoint> Series,
    Trend Trend,
    IReadOnlyCollection<BrandTrend> Trending,
    DateTimeOffset UpdatedAt);

public interface IMentionQueryService
{
    Task<IReadOnlyCollection<AnalyzedMention>> SearchAsync(MentionFilter f, CancellationToken ct = default);
    Task<int> CountAsync(MentionFilter f, CancellationToken ct = default);
    Task<Overview> OverviewAsync(string? brand, CancellationToken ct = default);
    Task<IReadOnlyCollection<Bucket>> TopAsync(string field, string? brand, CancellationToken ct = default);
    Task<IReadOnlyCollection<TimePoint>> TimeseriesAsync(string? brand, CancellationToken ct = default);
    Task<Trend> TrendAsync(string? brand, CancellationToken ct = default);
    Task<IReadOnlyCollection<BrandTrend>> TrendingAsync(CancellationToken ct = default);
    Task<CrisisSummary> SummaryAsync(string? brand, CancellationToken ct = default);
    Task<IReadOnlyCollection<Story>> StoriesAsync(string? brand, CancellationToken ct = default);
    Task<DashboardSnapshot> ComputeDashboardAsync(string? brand, CancellationToken ct = default);
    Task<IReadOnlyCollection<BrandRadar.Shared.Contracts.AlertMessage>> AlertsAsync(CancellationToken ct = default);
}

public sealed class MentionQueryService(ElasticsearchClient client, IEsRaw esRaw, Dashboard.Api.Ai.ILlmSummarizer summarizer, ILogger<MentionQueryService> logger)
    : IMentionQueryService
{
    private static Query Build(MentionFilter f)
    {
        var must = new List<Query>();
        if (!string.IsNullOrWhiteSpace(f.Brand)) must.Add(new MatchQuery(new Field("brand")) { Query = f.Brand });
        if (!string.IsNullOrWhiteSpace(f.Sentiment)) must.Add(new MatchQuery(new Field("sentiment")) { Query = f.Sentiment });
        if (!string.IsNullOrWhiteSpace(f.Lang)) must.Add(new MatchQuery(new Field("lang")) { Query = f.Lang });
        if (!string.IsNullOrWhiteSpace(f.Keyword)) must.Add(new QueryStringQuery { Query = $"*{f.Keyword}*" });
        return must.Count == 0 ? new MatchAllQuery() : new BoolQuery { Must = must };
    }

    private bool Ok<T>(SearchResponse<T> r, string op)
    {
        if (r.IsValidResponse) return true;
        logger.LogWarning("ES {Op} invalid: {Info}", op, r.DebugInformation);
        return false;
    }

    public async Task<IReadOnlyCollection<AnalyzedMention>> SearchAsync(MentionFilter f, CancellationToken ct = default)
    {
        // Sort server-side for correct paging. Default = newest-arrived (matches the live feed);
        // "published" = newest published article first.
        var sortField = string.Equals(f.Sort, "published", StringComparison.OrdinalIgnoreCase) ? "publishedAt" : "analyzedAt";
        var r = await client.SearchAsync<AnalyzedMention>(new SearchRequest(Indexes.Mentions)
        {
            From = f.Offset, Size = f.Size, IgnoreUnavailable = true, AllowNoIndices = true, Query = Build(f),
            Sort = new[] { SortOptions.Field(sortField, new FieldSort { Order = SortOrder.Desc }) }
        }, ct);
        if (!Ok(r, "search")) return [];
        return r.Documents.ToList();
    }

    public async Task<int> CountAsync(MentionFilter f, CancellationToken ct = default)
    {
        var r = await client.SearchAsync<AnalyzedMention>(new SearchRequest(Indexes.Mentions)
        {
            Size = 0, IgnoreUnavailable = true, AllowNoIndices = true, Query = Build(f)
        }, ct);
        return Ok(r, "count") ? (int)r.Total : 0;
    }

    public async Task<IReadOnlyCollection<BrandTrend>> TrendingAsync(CancellationToken ct = default)
    {
        var docs = await FetchAsync(null, ct);
        var now = DateTimeOffset.UtcNow;
        return docs.Where(d => !string.IsNullOrEmpty(d.Brand))
            .GroupBy(d => d.Brand!)
            .Select(g => new BrandTrend(g.Key,
                g.Count(d => d.AnalyzedAt >= now.AddHours(-24)),
                g.Count(d => d.AnalyzedAt < now.AddHours(-24) && d.AnalyzedAt >= now.AddHours(-48))))
            .OrderByDescending(b => b.Current - b.Previous)
            .Take(8).ToList();
    }

    /// <summary>
    /// Dashboard aggregates via **native Elasticsearch aggregations** (terms/date_histogram/filters),
    /// computed inside ES over the whole dataset. Falls back to in-memory if ES/agg is unavailable.
    /// </summary>
    public async Task<DashboardSnapshot> ComputeDashboardAsync(string? brand, CancellationToken ct = default)
    {
        var query = string.IsNullOrWhiteSpace(brand)
            ? "{\"match_all\":{}}"
            : $"{{\"term\":{{\"brand\":{JsonSerializer.Serialize(brand)}}}}}";

        var body = $$"""
        {
          "size": 0,
          "track_total_hits": true,
          "query": {{query}},
          "aggs": {
            "by_sentiment": { "terms": { "field": "sentiment", "size": 5 } },
            "by_source":    { "terms": { "field": "source", "size": 10 } },
            "by_topics":    { "terms": { "field": "topics", "size": 12 } },
            "over_time":    { "date_histogram": { "field": "publishedAt", "fixed_interval": "1h" },
                              "aggs": { "neg": { "filter": { "term": { "sentiment": "Negative" } } } } },
            "cur24":  { "filter": { "range": { "analyzedAt": { "gte": "now-24h" } } },
                        "aggs": { "neg": { "filter": { "term": { "sentiment": "Negative" } } } } },
            "prev24": { "filter": { "range": { "analyzedAt": { "gte": "now-48h", "lt": "now-24h" } } },
                        "aggs": { "neg": { "filter": { "term": { "sentiment": "Negative" } } } } },
            "brands": { "terms": { "field": "brand", "size": 8 },
                        "aggs": { "cur":  { "filter": { "range": { "analyzedAt": { "gte": "now-24h" } } } },
                                  "prev": { "filter": { "range": { "analyzedAt": { "gte": "now-48h", "lt": "now-24h" } } } } } }
          }
        }
        """;

        using var doc = await esRaw.SearchAsync(Indexes.Mentions, body, ct);
        if (doc is null) return await ComputeDashboardInMemoryAsync(brand, ct);

        try
        {
            var root = doc.RootElement;
            var aggs = root.GetProperty("aggregations");
            var total = root.GetProperty("hits").GetProperty("total").GetProperty("value").GetInt32();

            int Sentiment(string k) => Buckets(aggs, "by_sentiment").Where(b => Key(b) == k).Select(DocCount).FirstOrDefault();
            var overview = new Overview(total, Sentiment("Positive"), Sentiment("Neutral"), Sentiment("Negative"));

            var sources = Buckets(aggs, "by_source").Select(b => new Bucket(Key(b), DocCount(b))).ToList();
            var topics = Buckets(aggs, "by_topics").Select(b => new Bucket(Key(b), DocCount(b))).ToList();

            var series = Buckets(aggs, "over_time").Select(b =>
            {
                var t = DateTimeOffset.FromUnixTimeMilliseconds(b.GetProperty("key").GetInt64()).UtcDateTime.ToString("yyyy-MM-dd HH:00");
                return new TimePoint(t, DocCount(b), b.GetProperty("neg").GetProperty("doc_count").GetInt32());
            }).TakeLast(48).ToList();

            int F(string agg) => aggs.GetProperty(agg).GetProperty("doc_count").GetInt32();
            int FNeg(string agg) => aggs.GetProperty(agg).GetProperty("neg").GetProperty("doc_count").GetInt32();
            var trend = new Trend(F("cur24"), F("prev24"), FNeg("cur24"), FNeg("prev24"));

            var trending = Buckets(aggs, "brands")
                .Select(b => new BrandTrend(Key(b),
                    b.GetProperty("cur").GetProperty("doc_count").GetInt32(),
                    b.GetProperty("prev").GetProperty("doc_count").GetInt32()))
                .OrderByDescending(x => x.Current - x.Previous).Take(8).ToList();

            return new DashboardSnapshot(overview, sources, topics, series, trend, trending, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "agg parse failed; falling back to in-memory");
            return await ComputeDashboardInMemoryAsync(brand, ct);
        }
    }

    private static IEnumerable<JsonElement> Buckets(JsonElement aggs, string name)
        => aggs.TryGetProperty(name, out var a) && a.TryGetProperty("buckets", out var b)
            ? b.EnumerateArray() : Enumerable.Empty<JsonElement>();
    private static string Key(JsonElement bucket) => bucket.GetProperty("key").ToString();
    private static int DocCount(JsonElement bucket) => bucket.GetProperty("doc_count").GetInt32();

    private async Task<DashboardSnapshot> ComputeDashboardInMemoryAsync(string? brand, CancellationToken ct = default)
    {
        var docs = await FetchAsync(brand, ct);
        var now = DateTimeOffset.UtcNow;

        int C(string s) => docs.Count(d => d.Sentiment == s);
        var overview = new Overview(docs.Count, C("Positive"), C("Neutral"), C("Negative"));

        var sources = docs.Where(d => !string.IsNullOrEmpty(d.Source)).GroupBy(d => d.Source)
            .Select(g => new Bucket(g.Key, g.Count())).OrderByDescending(b => b.Count).Take(10).ToList();
        var topics = docs.SelectMany(d => d.Topics).Where(k => !string.IsNullOrWhiteSpace(k)).GroupBy(k => k)
            .Select(g => new Bucket(g.Key, g.Count())).OrderByDescending(b => b.Count).Take(12).ToList();
        var series = docs.GroupBy(d => new DateTime(d.PublishedAt.Year, d.PublishedAt.Month, d.PublishedAt.Day, d.PublishedAt.Hour, 0, 0, DateTimeKind.Utc))
            .OrderBy(g => g.Key).TakeLast(48)
            .Select(g => new TimePoint(g.Key.ToString("yyyy-MM-dd HH:00"), g.Count(), g.Count(d => d.Sentiment == "Negative"))).ToList();

        var cur = docs.Where(d => d.AnalyzedAt >= now.AddHours(-24)).ToList();
        var prev = docs.Where(d => d.AnalyzedAt < now.AddHours(-24) && d.AnalyzedAt >= now.AddHours(-48)).ToList();
        var trend = new Trend(cur.Count, prev.Count, cur.Count(d => d.Sentiment == "Negative"), prev.Count(d => d.Sentiment == "Negative"));

        var trending = docs.Where(d => !string.IsNullOrEmpty(d.Brand)).GroupBy(d => d.Brand!)
            .Select(g => new BrandTrend(g.Key,
                g.Count(d => d.AnalyzedAt >= now.AddHours(-24)),
                g.Count(d => d.AnalyzedAt < now.AddHours(-24) && d.AnalyzedAt >= now.AddHours(-48))))
            .OrderByDescending(b => b.Current - b.Previous).Take(8).ToList();

        return new DashboardSnapshot(overview, sources, topics, series, trend, trending, now);
    }

    public async Task<IReadOnlyCollection<Story>> StoriesAsync(string? brand, CancellationToken ct = default)
    {
        var docs = await FetchAsync(brand, ct);
        return docs
            .Where(d => !string.IsNullOrEmpty(d.Fingerprint))
            .GroupBy(d => d.Fingerprint)
            .Select(g =>
            {
                var latest = g.OrderByDescending(d => d.PublishedAt).First();
                var sentiment = g.GroupBy(d => d.Sentiment).OrderByDescending(x => x.Count()).First().Key;
                var sources = g.Select(d => d.Source).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
                return new Story(g.Key, latest.Title, sources.Count, sources, sentiment, latest.PublishedAt, latest.Url);
            })
            .OrderByDescending(s => s.SourceCount).ThenByDescending(s => s.Latest)
            .Take(30).ToList();
    }

    public async Task<CrisisSummary> SummaryAsync(string? brand, CancellationToken ct = default)
    {
        var docs = await FetchAsync(brand, ct);
        var now = DateTimeOffset.UtcNow;
        var negatives = docs.Where(d => d.Sentiment == "Negative" && d.PublishedAt >= now.AddHours(-24))
            .OrderByDescending(d => d.PublishedAt).ToList();
        var headlines = negatives.Select(d => d.Title).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().Take(5).ToList();
        var keywords = negatives.SelectMany(d => d.Topics).Where(k => !string.IsNullOrWhiteSpace(k))
            .GroupBy(k => k).OrderByDescending(g => g.Count()).Select(g => g.Key).Take(8).ToList();
        var narrative = await summarizer.SummarizeAsync(brand, headlines, ct);
        return new CrisisSummary(brand, negatives.Count, headlines, keywords, narrative);
    }

    private async Task<List<AnalyzedMention>> FetchAsync(string? brand, CancellationToken ct)
    {
        var f = new MentionFilter { Brand = brand, Size = 2000 };
        var r = await client.SearchAsync<AnalyzedMention>(new SearchRequest(Indexes.Mentions)
        {
            Size = 2000, IgnoreUnavailable = true, AllowNoIndices = true, Query = Build(f)
        }, ct);
        return Ok(r, "aggregate") ? r.Documents.ToList() : [];
    }

    public async Task<Overview> OverviewAsync(string? brand, CancellationToken ct = default)
    {
        var docs = await FetchAsync(brand, ct);
        int C(string s) => docs.Count(d => d.Sentiment == s);
        return new Overview(docs.Count, C("Positive"), C("Neutral"), C("Negative"));
    }

    public async Task<Trend> TrendAsync(string? brand, CancellationToken ct = default)
    {
        var docs = await FetchAsync(brand, ct);
        var now = DateTimeOffset.UtcNow;
        var cur = docs.Where(d => d.AnalyzedAt >= now.AddHours(-24)).ToList();
        var prev = docs.Where(d => d.AnalyzedAt < now.AddHours(-24) && d.AnalyzedAt >= now.AddHours(-48)).ToList();
        return new Trend(cur.Count, prev.Count,
            cur.Count(d => d.Sentiment == "Negative"), prev.Count(d => d.Sentiment == "Negative"));
    }

    public async Task<IReadOnlyCollection<Bucket>> TopAsync(string field, string? brand, CancellationToken ct = default)
    {
        var docs = await FetchAsync(brand, ct);
        IEnumerable<string> Keys(AnalyzedMention m) => field switch
        {
            "topics" => m.Topics,
            "brand" => m.Brand is null ? [] : [m.Brand],
            _ => [m.Source]
        };
        return docs.SelectMany(Keys)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .GroupBy(k => k)
            .Select(g => new Bucket(g.Key, g.Count()))
            .OrderByDescending(b => b.Count).Take(10).ToList();
    }

    public async Task<IReadOnlyCollection<BrandRadar.Shared.Contracts.AlertMessage>> AlertsAsync(CancellationToken ct = default)
    {
        var r = await client.SearchAsync<BrandRadar.Shared.Contracts.AlertMessage>(new SearchRequest(Indexes.Alerts)
        {
            Size = 50, IgnoreUnavailable = true, AllowNoIndices = true, Query = new MatchAllQuery()
        }, ct);
        return Ok(r, "alerts") ? r.Documents.OrderByDescending(a => a.CreatedAt).ToList() : [];
    }

    public async Task<IReadOnlyCollection<TimePoint>> TimeseriesAsync(string? brand, CancellationToken ct = default)
    {
        var docs = await FetchAsync(brand, ct);
        return docs
            .GroupBy(d => new DateTime(d.PublishedAt.Year, d.PublishedAt.Month, d.PublishedAt.Day, d.PublishedAt.Hour, 0, 0, DateTimeKind.Utc))
            .OrderBy(g => g.Key)
            .TakeLast(48)
            .Select(g => new TimePoint(g.Key.ToString("yyyy-MM-dd HH:00"), g.Count(), g.Count(d => d.Sentiment == "Negative")))
            .ToList();
    }
}
