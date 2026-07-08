using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BrandRadar.Shared.Contracts;

namespace BrandRadar.Shared.Sentiment;

/// <summary>
/// Rule-based sentiment using positive/negative word lists (VN + EN). Explainable, no API key.
/// Score in [-1, 1]; label thresholds at ±0.15. Also extracts salient keywords as "topics".
/// </summary>
public sealed class LexiconSentimentAnalyzer : ISentimentAnalyzer
{
    private static readonly HashSet<string> Positive = new(StringComparer.OrdinalIgnoreCase)
    {
        // Vietnamese (diacritics stripped at compare time)
        "tot","tuyet","tuyetvoi","hailong","thich","yeu","chatluong","uytin","nhanhchong","chuyennghiep",
        "than thien","re","xuatsac","ho tro","tin tuong","hieu qua","an tam","ngon","dep","on",
        // English
        "good","great","excellent","love","happy","satisfied","reliable","fast","professional","recommend",
        "amazing","awesome","best","perfect","nice","wonderful","trust","quality"
    };

    private static readonly HashSet<string> Negative = new(StringComparer.OrdinalIgnoreCase)
    {
        // Vietnamese
        "te","toi","xau","chán","thatvong","lua","lo","cham","kem","bucxuc","phananh","khieunai",
        "loi","hong","tequa","dodang","matday","khong the","tham hoa","scandal","tay chay","phanno",
        // English
        "bad","terrible","awful","hate","angry","disappointed","scam","slow","poor","complaint",
        "broken","worst","fail","failure","crisis","boycott","refund","fraud","delay","rude"
    };

    private static readonly HashSet<string> Stop = new(StringComparer.OrdinalIgnoreCase)
    {
        "va","la","cua","co","cho","mot","cac","nhung","de","voi","tai","tren","trong","khi","nay","do",
        "the","a","an","and","or","the","to","of","in","on","for","with","is","are","was","this","that",
        "toi","ban","minh","ho","no","rat","qua","cung","da","se","dang","bi","duoc","tu","den","va"
    };

    private static string Strip(string s)
    {
        var norm = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in norm)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd').Replace('Đ', 'D');
    }

    private static IEnumerable<string> Tokens(string text) =>
        Regex.Split(Strip(text).ToLowerInvariant(), @"[^a-z0-9]+").Where(t => t.Length > 1);

    public Task<SentimentResult> AnalyzeAsync(string text, CancellationToken ct = default)
    {
        int pos = 0, neg = 0;
        foreach (var t in Tokens(text))
        {
            if (Positive.Contains(t)) pos++;
            else if (Negative.Contains(t)) neg++;
        }
        var total = pos + neg;
        if (total == 0) return Task.FromResult(new SentimentResult(SentimentLabel.Neutral, 0));
        var score = (double)(pos - neg) / total;
        var label = score > 0.15 ? SentimentLabel.Positive : score < -0.15 ? SentimentLabel.Negative : SentimentLabel.Neutral;
        return Task.FromResult(new SentimentResult(label, Math.Round(score, 3)));
    }

    public IReadOnlyList<string> ExtractTopics(string text, int max = 5) =>
        Tokens(text)
            .Where(t => t.Length > 3 && !Stop.Contains(t) && !Positive.Contains(t) && !Negative.Contains(t))
            .GroupBy(t => t)
            .OrderByDescending(g => g.Count())
            .Take(max)
            .Select(g => g.Key)
            .ToList();
}
