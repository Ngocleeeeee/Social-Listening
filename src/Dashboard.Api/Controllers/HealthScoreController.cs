using Dashboard.Api.Analytics;
using Dashboard.Api.Caching;
using Microsoft.AspNetCore.Mvc;

namespace Dashboard.Api.Controllers;

/// <summary>
/// Brand Health &amp; Competitive Intelligence. GET /api/health — bảng xếp hạng sức khỏe thương hiệu
/// (Brand Health Index), thị phần thảo luận (Share of Voice) và insight tự động. Cache 15s vì đây là
/// một aggregation nặng chạy trên toàn dataset.
/// </summary>
[ApiController]
[Route("api/health")]
public sealed class HealthScoreController(IBrandHealthService svc, ICache cache) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Rank(CancellationToken ct)
        => Ok(await cache.GetOrSetAsync("brand-health", TimeSpan.FromSeconds(15), () => svc.RankAsync(ct)));
}
