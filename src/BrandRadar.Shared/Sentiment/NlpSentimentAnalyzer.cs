using System.Net.Http.Json;
using System.Text.Json.Serialization;
using BrandRadar.Shared.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BrandRadar.Shared.Sentiment;

/// <summary>
/// Sentiment via a dedicated NLP microservice (transformer model, multilingual). Calls
/// POST /analyze and maps the label. Falls back to the lexicon analyzer if the service is
/// unavailable so the pipeline never stalls. Topic extraction stays lexicon-based.
/// </summary>
public sealed class NlpSentimentAnalyzer(
    IHttpClientFactory httpFactory,
    LexiconSentimentAnalyzer fallback,
    IOptions<SentimentOptions> options,
    ILogger<NlpSentimentAnalyzer> logger) : ISentimentAnalyzer
{
    private readonly NlpOptions _o = options.Value.Nlp;

    private sealed record Req([property: JsonPropertyName("text")] string Text);
    private sealed record Res(
        [property: JsonPropertyName("label")] string? Label,
        [property: JsonPropertyName("score")] double Score);

    public async Task<SentimentResult> AnalyzeAsync(string text, CancellationToken ct = default)
    {
        try
        {
            var client = httpFactory.CreateClient("resilient");
            client.Timeout = TimeSpan.FromSeconds(20);

            var resp = await client.PostAsJsonAsync($"{_o.Url.TrimEnd('/')}/analyze", new Req(text), ct);
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadFromJsonAsync<Res>(ct);

            var label = (body?.Label) switch
            {
                "Positive" => SentimentLabel.Positive,
                "Negative" => SentimentLabel.Negative,
                _ => SentimentLabel.Neutral
            };
            return new SentimentResult(label, Math.Round(body?.Score ?? 0, 3));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "NLP sentiment failed; falling back to lexicon");
            return await fallback.AnalyzeAsync(text, ct);
        }
    }

    public IReadOnlyList<string> ExtractTopics(string text, int max = 5) => fallback.ExtractTopics(text, max);
}
