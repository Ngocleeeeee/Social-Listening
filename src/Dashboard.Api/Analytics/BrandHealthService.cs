using System.Text.Json;
using BrandRadar.Shared.Constants;
using Dashboard.Api.Search;

namespace Dashboard.Api.Analytics;

/// <summary>Sức khỏe & thị phần thảo luận của một thương hiệu trong 24h gần nhất.</summary>
public sealed record BrandHealth(
    string Brand,
    int Mentions,          // số mention 24h
    int Previous,          // số mention 24h trước đó (để tính đà)
    int Positive,
    int Neutral,
    int Negative,
    double ShareOfVoice,   // % thị phần thảo luận so với các brand đang theo dõi (0..100)
    double NetSentiment,   // (pos - neg)/total, trong [-1..1]
    double VolumeChange,   // % thay đổi lượng thảo luận so với kỳ trước
    int Score,             // Brand Health Index 0..100
    string Grade,          // A/B/C/D
    string Insight);       // câu insight tự sinh

/// <summary>
/// Brand Health & Competitive Intelligence — biến dữ liệu thô thành KPI ra quyết định.
///
/// Bài toán thực tế (đúng nghiệp vụ "Market &amp; Competitors Research" + "auto-generated insights"):
/// khách hàng không muốn đọc từng mention mà cần (1) một điểm sức khỏe thương hiệu, (2) thị phần
/// thảo luận so với đối thủ (Share of Voice), (3) insight tự động. Tất cả tính bằng MỘT native ES
/// aggregation (terms theo brand + sub-agg sentiment + filter cửa sổ thời gian) chạy trong ES trên
/// toàn dataset — không kéo document về app.
/// </summary>
public interface IBrandHealthService
{
    Task<IReadOnlyList<BrandHealth>> RankAsync(CancellationToken ct = default);
}

public sealed class BrandHealthService(IEsRaw esRaw, ILogger<BrandHealthService> logger) : IBrandHealthService
{
    public async Task<IReadOnlyList<BrandHealth>> RankAsync(CancellationToken ct = default)
    {
        const string body = """
        {
          "size": 0,
          "aggs": {
            "total_cur": { "filter": { "range": { "analyzedAt": { "gte": "now-24h" } } } },
            "brands": {
              "terms": { "field": "brand", "size": 30 },
              "aggs": {
                "cur":  { "filter": { "range": { "analyzedAt": { "gte": "now-24h" } } },
                          "aggs": { "sent": { "terms": { "field": "sentiment", "size": 5 } } } },
                "prev": { "filter": { "range": { "analyzedAt": { "gte": "now-48h", "lt": "now-24h" } } } }
              }
            }
          }
        }
        """;

        using var doc = await esRaw.SearchAsync(Indexes.Mentions, body, ct);
        if (doc is null) return [];

        try
        {
            var aggs = doc.RootElement.GetProperty("aggregations");
            var totalCur = Math.Max(1, aggs.GetProperty("total_cur").GetProperty("doc_count").GetInt32());

            var list = new List<BrandHealth>();
            foreach (var b in aggs.GetProperty("brands").GetProperty("buckets").EnumerateArray())
            {
                var brand = b.GetProperty("key").GetString() ?? "?";
                var cur = b.GetProperty("cur");
                var mentions = cur.GetProperty("doc_count").GetInt32();
                var prev = b.GetProperty("prev").GetProperty("doc_count").GetInt32();

                int pos = 0, neu = 0, neg = 0;
                foreach (var s in cur.GetProperty("sent").GetProperty("buckets").EnumerateArray())
                {
                    var c = s.GetProperty("doc_count").GetInt32();
                    switch (s.GetProperty("key").GetString())
                    {
                        case "Positive": pos = c; break;
                        case "Negative": neg = c; break;
                        default: neu = c; break;
                    }
                }

                if (mentions == 0) continue;

                var total = pos + neu + neg;
                var net = total == 0 ? 0 : (double)(pos - neg) / total;      // [-1..1]
                var negShare = total == 0 ? 0 : (double)neg / total;         // [0..1]
                var sov = 100.0 * mentions / totalCur;                       // % thị phần
                var momentum = Math.Clamp((mentions - prev) / (double)Math.Max(prev, 1), -1, 1);
                var volumeChange = prev == 0 ? (mentions > 0 ? 100.0 : 0) : 100.0 * (mentions - prev) / prev;

                // Brand Health Index: nền 50, cộng theo cảm xúc thuần, trừ theo áp lực tiêu cực,
                // và đà thảo luận khuếch đại theo hướng cảm xúc (tăng khi đang tích cực = tốt, và ngược lại).
                var score = 50 + 40 * net - 25 * negShare + 10 * (momentum * net);
                var bhi = (int)Math.Round(Math.Clamp(score, 0, 100));
                var grade = bhi >= 75 ? "A" : bhi >= 60 ? "B" : bhi >= 45 ? "C" : "D";

                list.Add(new BrandHealth(brand, mentions, prev, pos, neu, neg,
                    Math.Round(sov, 1), Math.Round(net, 2), Math.Round(volumeChange, 0),
                    bhi, grade, Insight(brand, negShare, volumeChange, sov, net)));
            }

            return list.OrderByDescending(x => x.Score).ThenByDescending(x => x.Mentions).ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "brand health parse failed");
            return [];
        }
    }

    /// <summary>Insight tự sinh (rule-based) — ưu tiên tín hiệu rủi ro trước, rồi cơ hội.</summary>
    private static string Insight(string brand, double negShare, double volumeChange, double sov, double net)
    {
        if (negShare >= 0.4)
            return $"⚠ Rủi ro: {negShare:P0} thảo luận tiêu cực — cần theo dõi khủng hoảng.";
        if (volumeChange >= 80 && net < 0)
            return $"⚠ Lượng thảo luận tăng mạnh (+{volumeChange:F0}%) theo hướng tiêu cực.";
        if (volumeChange >= 80 && net >= 0)
            return $"↑ Lan tỏa tốt: thảo luận tăng +{volumeChange:F0}% với cảm xúc tích cực.";
        if (sov >= 30)
            return $"★ Dẫn đầu thị phần thảo luận ({sov:F0}%).";
        if (net >= 0.4)
            return "Cảm xúc rất tích cực, thương hiệu đang được đón nhận tốt.";
        if (volumeChange <= -50)
            return $"↓ Lượng thảo luận giảm {Math.Abs(volumeChange):F0}% — độ quan tâm đang hạ nhiệt.";
        return "Ổn định, không có biến động đáng kể trong 24h.";
    }
}
