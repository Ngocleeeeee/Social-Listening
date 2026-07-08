using Dashboard.Api.Caching;
using Dashboard.Api.ReadModel;
using Dashboard.Api.Search;
using Microsoft.AspNetCore.Mvc;

namespace Dashboard.Api.Controllers;

[ApiController]
[Route("api/stats")]
public sealed class StatsController(IMentionQueryService q, ICache cache, ISnapshotStore snapshot) : ControllerBase
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(5);

    [HttpGet("overview")]
    public async Task<IActionResult> Overview([FromQuery] string? brand, CancellationToken ct)
        => Ok(await cache.GetOrSetAsync($"stats:overview:{brand}", Ttl, () => q.OverviewAsync(brand, ct)));

    [HttpGet("top")]
    public async Task<IActionResult> Top([FromQuery] string field = "source", [FromQuery] string? brand = null, CancellationToken ct = default)
        => Ok(await cache.GetOrSetAsync($"stats:top:{field}:{brand}", Ttl, () => q.TopAsync(field, brand, ct)));

    [HttpGet("timeseries")]
    public async Task<IActionResult> Timeseries([FromQuery] string? brand, CancellationToken ct)
        => Ok(await cache.GetOrSetAsync($"stats:ts:{brand}", Ttl, () => q.TimeseriesAsync(brand, ct)));

    [HttpGet("trend")]
    public async Task<IActionResult> Trend([FromQuery] string? brand, CancellationToken ct)
        => Ok(await cache.GetOrSetAsync($"stats:trend:{brand}", Ttl, () => q.TrendAsync(brand, ct)));

    /// <summary>GET /api/stats/dashboard — consolidated read-model (1 call). All-brands served from in-memory snapshot.</summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard([FromQuery] string? brand, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(brand) && snapshot.Current is { } snap) return Ok(snap);
        return Ok(await cache.GetOrSetAsync($"stats:dashboard:{brand}", Ttl, () => q.ComputeDashboardAsync(brand, ct)));
    }
}
