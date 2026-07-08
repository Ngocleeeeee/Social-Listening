using Dashboard.Api.Alerts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dashboard.Api.Controllers;

/// <summary>
/// Quản lý luật cảnh báo cấu hình được (Crisis Management). Đọc công khai; ghi cần đăng nhập (JWT).
/// Analysis.Worker đánh giá các luật này theo thời gian thực.
/// </summary>
[ApiController]
[Route("api/rules")]
public sealed class AlertRulesController(IAlertRuleAdmin admin) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await admin.ListAsync(ct));

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertRuleRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return BadRequest("Name is required");
        return Ok(new { id = await admin.CreateAsync(r, ct) });
    }

    [Authorize]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertRuleRequest r, CancellationToken ct)
        => await admin.UpdateAsync(id, r, ct) ? NoContent() : NotFound();

    [Authorize]
    [HttpPost("{id:int}/toggle")]
    public async Task<IActionResult> Toggle(int id, [FromQuery] bool enabled, CancellationToken ct)
    {
        await admin.SetEnabledAsync(id, enabled, ct);
        return NoContent();
    }

    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await admin.DeleteAsync(id, ct);
        return NoContent();
    }
}
