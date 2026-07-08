using BrandRadar.Shared;
using BrandRadar.Shared.Logging;
using Collector.Worker;
using Npgsql;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

Log.Logger = SerilogConfig.Create(builder.Configuration, "Collector.Worker").CreateLogger();
builder.Services.AddSerilog();

builder.Services.Configure<CollectorOptions>(builder.Configuration.GetSection(CollectorOptions.SectionName));
builder.Services.AddHttpClient("resilient").AddStandardResilienceHandler();
builder.Services.AddSingleton<RssCollector>();
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("Postgres")!));
builder.Services.AddSingleton<BrandFeeds>();
builder.Services.AddBrandRadarMessaging(builder.Configuration);
builder.Services.AddHostedService<CollectorService>();

builder.Build().Run();
