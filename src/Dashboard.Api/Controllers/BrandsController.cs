using Dashboard.Api.Brands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dashboard.Api.Controllers;

[ApiController]
[Route("api/brands")]
public sealed class BrandsController(IBrandAdmin admin) : ControllerBase
{
    /// <summary>GET /api/brands — list brands with keywords.</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await admin.ListAsync(ct));

    /// <summary>POST /api/brands — add a brand (auto-crawled + matched from now on).</summary>
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBrandRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("Name is required");
        var id = await admin.CreateAsync(req, ct);
        return Ok(new { id });
    }

    /// <summary>DELETE /api/brands/{id}.</summary>
    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await admin.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>POST /api/brands/{id}/keywords — add a keyword to a brand.</summary>
    [Authorize]
    [HttpPost("{id:int}/keywords")]
    public async Task<IActionResult> AddKeyword(int id, [FromBody] AddKeywordBody body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Keyword)) return BadRequest("Keyword is required");
        await admin.AddKeywordAsync(id, body.Keyword.Trim(), ct);
        return NoContent();
    }

    /// <summary>DELETE /api/brands/{id}/keywords?keyword=... — remove one keyword.</summary>
    [Authorize]
    [HttpDelete("{id:int}/keywords")]
    public async Task<IActionResult> RemoveKeyword(int id, [FromQuery] string keyword, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return BadRequest("keyword is required");
        await admin.RemoveKeywordAsync(id, keyword, ct);
        return NoContent();
    }
}

public sealed record AddKeywordBody(string Keyword);
