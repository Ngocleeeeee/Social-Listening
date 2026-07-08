using Dashboard.Api.Caching;
using Dashboard.Api.Reporting;
using Dashboard.Api.Search;
using Microsoft.AspNetCore.Mvc;

namespace Dashboard.Api.Controllers;

[ApiController]
[Route("api/report")]
public sealed class ReportController(IReportQueries reports, IMentionQueryService mentions, ICache cache) : ControllerBase
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(8);

    /// <summary>GET /api/report/brands — per-brand totals + sentiment (SQL GROUP BY, Redis-cached).</summary>
    [HttpGet("brands")]
    public async Task<IActionResult> Brands(CancellationToken ct)
        => Ok(await cache.GetOrSetAsync("report:brands", Ttl, () => reports.ByBrandAsync(ct)));

    /// <summary>GET /api/report/daily?days=14 — daily volume + negatives (SQL, Redis-cached).</summary>
    [HttpGet("daily")]
    public async Task<IActionResult> Daily([FromQuery] int days = 14, CancellationToken ct = default)
        => Ok(await cache.GetOrSetAsync($"report:daily:{days}", Ttl, () => reports.DailyAsync(days, ct)));

    /// <summary>GET /api/report/trending — brands rising fastest (24h vs previous 24h).</summary>
    [HttpGet("trending")]
    public async Task<IActionResult> Trending(CancellationToken ct)
        => Ok(await cache.GetOrSetAsync("report:trending", Ttl, () => mentions.TrendingAsync(ct)));
}
