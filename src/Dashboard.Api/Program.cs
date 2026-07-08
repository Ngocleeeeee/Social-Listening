using BrandRadar.Shared.Messaging;
using BrandRadar.Shared.Logging;
using Dashboard.Api.Realtime;
using Dashboard.Api.Reporting;
using Dashboard.Api.Search;
using Dashboard.Api.Caching;
using System.Text;
using System.Threading.RateLimiting;
using Elastic.Clients.Elasticsearch;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;
using Prometheus;
using Serilog;
using Serilog.Context;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = SerilogConfig.Create(builder.Configuration, "Dashboard.Api").CreateLogger();
builder.Host.UseSerilog();

var esUri = builder.Configuration["Elasticsearch:Uri"] ?? "http://elasticsearch:9200";
builder.Services.AddSingleton(new ElasticsearchClient(new ElasticsearchClientSettings(new Uri(esUri))));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<Dashboard.Api.Ai.ILlmSummarizer, Dashboard.Api.Ai.OllamaSummarizer>();
builder.Services.AddSingleton<IEsRaw, EsRaw>();
builder.Services.AddScoped<IMentionQueryService, MentionQueryService>();
builder.Services.AddScoped<Dashboard.Api.Analytics.IBrandHealthService, Dashboard.Api.Analytics.BrandHealthService>();
builder.Services.AddSingleton<Dashboard.Api.ReadModel.ISnapshotStore, Dashboard.Api.ReadModel.SnapshotStore>();
builder.Services.AddHostedService<Dashboard.Api.ReadModel.SnapshotRefresher>();

// Reporting + brand admin via raw SQL (Dapper) on PostgreSQL
var pgConn = builder.Configuration.GetConnectionString("Postgres")!;
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(pgConn));
builder.Services.AddScoped<IReportQueries, ReportQueries>();
builder.Services.AddScoped<Dashboard.Api.Brands.IBrandAdmin, Dashboard.Api.Brands.BrandAdmin>();
builder.Services.AddScoped<Dashboard.Api.Alerts.IAlertRuleAdmin, Dashboard.Api.Alerts.AlertRuleAdmin>();

// Redis cache (graceful) — completes the ES + Kafka + Redis stack
var redisConn = builder.Configuration["Redis"];
if (!string.IsNullOrWhiteSpace(redisConn))
{
    try { builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect($"{redisConn},abortConnect=false")); }
    catch { /* start without cache */ }
}
builder.Services.AddSingleton<ICache>(sp =>
    new RedisCache(sp.GetService<IConnectionMultiplexer>(), sp.GetRequiredService<ILogger<RedisCache>>()));
builder.Services.AddSingleton<IAlertAck>(sp => new AlertAck(sp.GetService<IConnectionMultiplexer>()));

// Realtime: Kafka -> SignalR bridge
builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName));
builder.Services.AddSignalR();
builder.Services.AddHostedService<LiveConsumer>();

// JWT auth (protects write endpoints)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true, ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            ValidateLifetime = true
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddProblemDetails();
builder.Services.AddResponseCompression(o => o.EnableForHttps = true);

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("api", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromSeconds(60), QueueLimit = 0 }));
});

builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddHealthChecks()
    .AddNpgSql(pgConn, name: "postgres", tags: new[] { "ready" })
    .AddRedis(redisConn ?? "localhost:6379", name: "redis", tags: new[] { "ready" });

var app = builder.Build();

app.UseExceptionHandler();
app.UseResponseCompression();

// Correlation id on every request (echoed back + attached to logs)
app.Use(async (ctx, next) =>
{
    var cid = ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    ctx.Response.Headers["X-Correlation-Id"] = cid;
    using (LogContext.PushProperty("CorrelationId", cid)) await next();
});

app.UseSerilogRequestLogging();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseHttpMetrics();     // Prometheus HTTP metrics
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers().RequireRateLimiting("api");
app.MapHub<LiveHub>("/hubs/live");
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });
app.MapHealthChecks("/health");
app.MapMetrics();         // Prometheus /metrics

app.Run();
