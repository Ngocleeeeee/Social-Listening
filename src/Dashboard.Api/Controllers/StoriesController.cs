using Dashboard.Api.Caching;
using Dashboard.Api.Search;
using Microsoft.AspNetCore.Mvc;

namespace Dashboard.Api.Controllers;

[ApiController]
[Route("api/stories")]
public sealed class StoriesController(IMentionQueryService q, ICache cache) : ControllerBase
{
    /// <summary>GET /api/stories?brand= — mentions clustered by content fingerprint (dedup coverage).</summary>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? brand, CancellationToken ct)
        => Ok(await cache.GetOrSetAsync($"stories:{brand}", TimeSpan.FromSeconds(8), () => q.StoriesAsync(brand, ct)));
}
