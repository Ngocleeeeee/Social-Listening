using System.Net.Http;
using Analysis.Worker;
using BrandRadar.Shared;
using BrandRadar.Shared.Logging;
using BrandRadar.Shared.Persistence;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Options;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

Log.Logger = SerilogConfig.Create(builder.Configuration, "Analysis.Worker").CreateLogger();
builder.Services.AddSerilog();

builder.Services.AddBrandRadarPersistence(builder.Configuration);
builder.Services.AddBrandRadarMessaging(builder.Configuration);
builder.Services.AddKafkaBus(builder.Configuration);
builder.Services.AddSentiment(builder.Configuration);
builder.Services.AddSingleton<BrandMatcher>();
builder.Services.Configure<CrisisOptions>(builder.Configuration.GetSection(CrisisOptions.SectionName));
builder.Services.AddHttpClient("resilient").AddStandardResilienceHandler();
builder.Services.AddSingleton<CrisisDetector>();

// Alert Rules Engine + notification channels (Strategy pattern)
builder.Services.AddSingleton<Analysis.Worker.Notifications.INotificationChannel, Analysis.Worker.Notifications.InAppChannel>();
builder.Services.AddSingleton<Analysis.Worker.Notifications.INotificationChannel, Analysis.Worker.Notifications.WebhookChannel>();
builder.Services.AddSingleton<Analysis.Worker.Notifications.NotificationDispatcher>();
builder.Services.AddSingleton<RuleEngine>();

builder.Services.Configure<ElasticOptions>(builder.Configuration.GetSection(ElasticOptions.SectionName));
builder.Services.AddSingleton(sp =>
{
    var uri = sp.GetRequiredService<IOptions<ElasticOptions>>().Value.Uri;
    return new ElasticsearchClient(new ElasticsearchClientSettings(new Uri(uri)));
});

builder.Services.AddHostedService<AnalysisConsumer>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BrandRadarDbContext>();
    await BrandSeeder.SeedAsync(db);
}

// Create Elasticsearch index template (explicit keyword/date mapping) so native aggregations + sort
// work correctly at scale. Idempotent, best-effort.
try
{
    var esUrl = (builder.Configuration["Elasticsearch:Uri"] ?? "http://elasticsearch:9200").TrimEnd('/');
    const string template = """
    {
      "index_patterns": ["mentions"],
      "template": {
        "mappings": {
          "properties": {
            "sentiment": { "type": "keyword" },
            "brand":     { "type": "keyword" },
            "source":    { "type": "keyword" },
            "lang":      { "type": "keyword" },
            "topics":    { "type": "keyword" },
            "fingerprint": { "type": "keyword" },
            "title":     { "type": "text" },
            "content":   { "type": "text" },
            "sentimentScore": { "type": "double" },
            "publishedAt": { "type": "date" },
            "analyzedAt":  { "type": "date" }
          }
        }
      }
    }
    """;
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    using var content = new StringContent(template, System.Text.Encoding.UTF8, "application/json");
    await http.PutAsync($"{esUrl}/_index_template/mentions-template", content);
    Log.Information("Elasticsearch index template ensured");
}
catch (Exception ex) { Log.Warning(ex, "could not create ES index template"); }

host.Run();
