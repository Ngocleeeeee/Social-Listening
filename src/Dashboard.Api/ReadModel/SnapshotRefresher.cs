using Dashboard.Api.Search;
using Microsoft.Extensions.DependencyInjection;

namespace Dashboard.Api.ReadModel;

/// <summary>Warms the all-brands dashboard snapshot every few seconds (cache-warming read-model).</summary>
public sealed class SnapshotRefresher(IServiceScopeFactory scopeFactory, ISnapshotStore store, ILogger<SnapshotRefresher> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var q = scope.ServiceProvider.GetRequiredService<IMentionQueryService>();
                store.Current = await q.ComputeDashboardAsync(null, stoppingToken);
            }
            catch (Exception ex) { logger.LogWarning(ex, "snapshot refresh failed"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
