using BrandRadar.Shared.Contracts;

namespace BrandRadar.Shared.Sentiment;

public sealed record SentimentResult(SentimentLabel Label, double Score);

/// <summary>Swappable sentiment engine (lexicon or LLM), chosen by config.</summary>
public interface ISentimentAnalyzer
{
    Task<SentimentResult> AnalyzeAsync(string text, CancellationToken ct = default);
    IReadOnlyList<string> ExtractTopics(string text, int max = 5);
}
