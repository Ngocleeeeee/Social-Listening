using BrandRadar.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Analysis.Worker;

/// <summary>Loads brand keywords once and matches mention text to a brand (case-insensitive contains).</summary>
public sealed class BrandMatcher(IServiceScopeFactory scopeFactory)
{
    private List<(int BrandId, string Brand, string Keyword)> _keywords = new();
    private DateTimeOffset _loadedAt = DateTimeOffset.MinValue;

    private async Task EnsureLoadedAsync()
    {
        if (_keywords.Count > 0 && DateTimeOffset.UtcNow - _loadedAt < TimeSpan.FromMinutes(1)) return;
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrandRadarDbContext>();
        _keywords = await db.BrandKeywords
            .Join(db.Brands, k => k.BrandId, b => b.Id, (k, b) => new { b.Id, b.Name, k.Keyword })
            .Select(x => new ValueTuple<int, string, string>(x.Id, x.Name, x.Keyword))
            .ToListAsync();
        _loadedAt = DateTimeOffset.UtcNow;
    }

    public async Task<(int? BrandId, string? Brand)> MatchAsync(string text)
    {
        await EnsureLoadedAsync();
        foreach (var (id, brand, kw) in _keywords)
            if (text.Contains(kw, StringComparison.OrdinalIgnoreCase))
                return (id, brand);
        return (null, null);
    }
}
