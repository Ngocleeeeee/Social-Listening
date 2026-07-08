using BrandRadar.Shared.Messaging;
using BrandRadar.Shared.Persistence;
using BrandRadar.Shared.Sentiment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BrandRadar.Shared;

public static class DependencyInjection
{
    public static IServiceCollection AddBrandRadarPersistence(this IServiceCollection s, IConfiguration cfg)
    {
        s.AddDbContext<BrandRadarDbContext>(o => o.UseNpgsql(cfg.GetConnectionString("Postgres")));
        return s;
    }

    public static IServiceCollection AddBrandRadarMessaging(this IServiceCollection s, IConfiguration cfg)
    {
        s.Configure<RabbitMqOptions>(cfg.GetSection(RabbitMqOptions.SectionName));
        s.AddSingleton<RabbitMqConnection>();
        s.AddSingleton<IMentionPublisher, RabbitMqPublisher>();
        return s;
    }

    public static IServiceCollection AddKafkaBus(this IServiceCollection s, IConfiguration cfg)
    {
        s.Configure<KafkaOptions>(cfg.GetSection(KafkaOptions.SectionName));
        s.AddSingleton<IEventBus, KafkaEventBus>();
        return s;
    }

    public static IServiceCollection AddSentiment(this IServiceCollection s, IConfiguration cfg)
    {
        s.Configure<SentimentOptions>(cfg.GetSection(SentimentOptions.SectionName));
        s.AddSingleton<LexiconSentimentAnalyzer>();

        var provider = cfg[$"{SentimentOptions.SectionName}:Provider"] ?? "lexicon";
        if (string.Equals(provider, "nlp", StringComparison.OrdinalIgnoreCase))
        {
            s.AddHttpClient();
            s.AddSingleton<ISentimentAnalyzer, NlpSentimentAnalyzer>();
        }
        else if (string.Equals(provider, "ollama", StringComparison.OrdinalIgnoreCase))
        {
            s.AddHttpClient();
            s.AddSingleton<ISentimentAnalyzer, LlmSentimentAnalyzer>();
        }
        else
        {
            s.AddSingleton<ISentimentAnalyzer>(sp => sp.GetRequiredService<LexiconSentimentAnalyzer>());
        }
        return s;
    }
}
