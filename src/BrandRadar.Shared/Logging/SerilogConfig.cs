using Microsoft.Extensions.Configuration;
using Serilog;

namespace BrandRadar.Shared.Logging;

public static class SerilogConfig
{
    public static LoggerConfiguration Create(IConfiguration cfg, string app) =>
        new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", app)
            .WriteTo.Console();
}
