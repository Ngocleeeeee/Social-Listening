namespace BrandRadar.Shared.Sentiment;

public sealed class SentimentOptions
{
    public const string SectionName = "Sentiment";
    public string Provider { get; set; } = "lexicon";   // "lexicon" | "nlp" | "ollama"
    public NlpOptions Nlp { get; set; } = new();
    public OllamaOptions Ollama { get; set; } = new();
}

public sealed class NlpOptions
{
    public string Url { get; set; } = "http://sentiment-nlp:8000";
}

public sealed class OllamaOptions
{
    public string Url { get; set; } = "http://ollama:11434";
    public string Model { get; set; } = "llama3.2:1b";
}
