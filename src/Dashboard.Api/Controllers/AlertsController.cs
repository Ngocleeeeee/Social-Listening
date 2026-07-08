using Dashboard.Api.Caching;
using Dashboard.Api.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dashboard.Api.Controllers;

[ApiController]
[Route("api/alerts")]
public sealed class AlertsController(IMentionQueryService q, IAlertAck ack) : ControllerBase
{
    /// <summary>GET /api/alerts — recent crisis alerts (from Elasticsearch), with acknowledged flag.</summary>
    [HttpGet]
    public async Task<IActionResult> Recent(CancellationToken ct)
    {
        var alerts = await q.AlertsAsync(ct);
        var acked = await ack.AckedAsync();
        foreach (var a in alerts) a.Acknowledged = acked.Contains(a.Id);
        return Ok(alerts);
    }

    /// <summary>GET /api/alerts/summary?brand= — tổng hợp tin tiêu cực 24h (headlines + từ khoá + narrative).</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] string? brand, CancellationToken ct)
        => Ok(await q.SummaryAsync(brand, ct));

    /// <summary>POST /api/alerts/{id}/ack — mark an alert as handled.</summary>
    [Authorize]
    [HttpPost("{id}/ack")]
    public async Task<IActionResult> Ack(string id)
    {
        await ack.AckAsync(id);
        return NoContent();
    }
}
