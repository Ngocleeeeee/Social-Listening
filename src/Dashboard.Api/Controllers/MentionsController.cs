using System.Text;
using Dashboard.Api.Search;
using Microsoft.AspNetCore.Mvc;

namespace Dashboard.Api.Controllers;

[ApiController]
[Route("api/mentions")]
public sealed class MentionsController(IMentionQueryService q) : ControllerBase
{
    /// <summary>GET /api/mentions — search + filter + paging.</summary>
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] MentionFilter filter, CancellationToken ct)
        => Ok(await q.SearchAsync(filter, ct));

    /// <summary>GET /api/mentions/count — total matching (for pagination).</summary>
    [HttpGet("count")]
    public async Task<IActionResult> Count([FromQuery] MentionFilter filter, CancellationToken ct)
        => Ok(new { total = await q.CountAsync(filter, ct) });

    /// <summary>GET /api/mentions/export — CSV of all matching mentions (up to 1000).</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] MentionFilter filter, CancellationToken ct)
    {
        filter.Page = 1; filter.Size = 1000;
        var items = await q.SearchAsync(filter, ct);
        var sb = new StringBuilder("sentiment,score,brand,source,publishedAt,title,url\n");
        string E(string? v) => $"\"{(v ?? "").Replace("\"", "\"\"")}\"";
        foreach (var m in items)
            sb.AppendLine(string.Join(",", E(m.Sentiment), m.SentimentScore, E(m.Brand), E(m.Source), E(m.PublishedAt.ToString("o")), E(m.Title), E(m.Url)));
        var bytes = Encoding.UTF8.GetBytes("﻿" + sb);
        return File(bytes, "text/csv", "mentions.csv");
    }
}
