using System.Net.Http.Json;
using System.Text.Json.Serialization;
using BrandRadar.Shared.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BrandRadar.Shared.Sentiment;

/// <summary>
/// Sentiment via a local LLM (Ollama). Classifies Vietnamese/English text into
/// Positive/Neutral/Negative. Falls back to the lexicon analyzer if the model is unavailable,
/// so the pipeline never stalls. Topic extraction still uses the lexicon.
/// </summary>
public sealed class LlmSentimentAnalyzer(
    IHttpClientFactory httpFactory,
    LexiconSentimentAnalyzer fallback,
    IOptions<SentimentOptions> options,
    ILogger<LlmSentimentAnalyzer> logger) : ISentimentAnalyzer
{
    private readonly OllamaOptions _o = options.Value.Ollama;

    private sealed record GenRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("stream")] bool Stream);

    private sealed record GenResponse([property: JsonPropertyName("response")] string? Response);

    public async Task<SentimentResult> AnalyzeAsync(string text, CancellationToken ct = default)
    {
        try
        {
            var prompt =
                "Bạn là bộ phân loại cảm xúc. Hãy phân loại thái độ của đoạn văn sau đối với thương hiệu/chủ thể được nhắc đến, " +
                "chỉ trả về đúng MỘT từ trong: Positive, Neutral, Negative. Không giải thích.\n\n" +
                "Đoạn văn: \"" + text.Replace("\"", "'") + "\"\n\nTrả lời:";

            var client = httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            var resp = await client.PostAsJsonAsync(
                $"{_o.Url.TrimEnd('/')}/api/generate",
                new GenRequest(_o.Model, prompt, false), ct);
            resp.EnsureSuccessStatusCode();

            var body = await resp.Content.ReadFromJsonAsync<GenResponse>(ct);
            var answer = (body?.Response ?? "").Trim();

            var label = answer.Contains("Negative", StringComparison.OrdinalIgnoreCase) ? SentimentLabel.Negative
                      : answer.Contains("Positive", StringComparison.OrdinalIgnoreCase) ? SentimentLabel.Positive
                      : answer.Contains("Neutral", StringComparison.OrdinalIgnoreCase) ? SentimentLabel.Neutral
                      : (SentimentLabel?)null ?? throw new InvalidOperationException($"LLM unparseable: '{answer}'");

            var score = label == SentimentLabel.Positive ? 0.8 : label == SentimentLabel.Negative ? -0.8 : 0;
            return new SentimentResult(label, score);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LLM sentiment failed; falling back to lexicon");
            return await fallback.AnalyzeAsync(text, ct);
        }
    }

    public IReadOnlyList<string> ExtractTopics(string text, int max = 5) => fallback.ExtractTopics(text, max);
}
