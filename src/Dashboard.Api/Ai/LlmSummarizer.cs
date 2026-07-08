using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Dashboard.Api.Ai;

/// <summary>Generates a natural-language crisis summary via a local LLM (Ollama). Optional + graceful.</summary>
public interface ILlmSummarizer
{
    Task<string?> SummarizeAsync(string? brand, IReadOnlyList<string> headlines, CancellationToken ct = default);
}

public sealed class OllamaSummarizer(IHttpClientFactory httpFactory, IConfiguration cfg, ILogger<OllamaSummarizer> logger) : ILlmSummarizer
{
    private sealed record GenReq([property: JsonPropertyName("model")] string Model, [property: JsonPropertyName("prompt")] string Prompt, [property: JsonPropertyName("stream")] bool Stream);
    private sealed record GenRes([property: JsonPropertyName("response")] string? Response);

    public async Task<string?> SummarizeAsync(string? brand, IReadOnlyList<string> headlines, CancellationToken ct = default)
    {
        var url = cfg["Llm:Url"];
        if (string.IsNullOrWhiteSpace(url) || headlines.Count == 0) return null;
        try
        {
            var model = cfg["Llm:Model"] ?? "llama3.2:1b";
            var prompt =
                $"Bạn là chuyên gia truyền thông. Dựa trên các tiêu đề tin tiêu cực về \"{brand}\" dưới đây, " +
                "viết 2-3 câu tiếng Việt tóm tắt tình hình khủng hoảng và gợi ý hướng xử lý. Ngắn gọn, không liệt kê.\n\n" +
                string.Join("\n", headlines.Take(8).Select(h => "- " + h));

            var client = httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            var resp = await client.PostAsJsonAsync($"{url.TrimEnd('/')}/api/generate", new GenReq(model, prompt, false), ct);
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadFromJsonAsync<GenRes>(ct);
            return string.IsNullOrWhiteSpace(body?.Response) ? null : body!.Response!.Trim();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LLM summary unavailable; using extractive only");
            return null;
        }
    }
}
