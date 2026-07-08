using Dapper;
using Npgsql;

namespace Dashboard.Api.Brands;

public sealed record BrandDto(int Id, string Name, List<string> Keywords);
public sealed record CreateBrandRequest(string Name, List<string> Keywords);

public interface IBrandAdmin
{
    Task<IReadOnlyCollection<BrandDto>> ListAsync(CancellationToken ct = default);
    Task<int> CreateAsync(CreateBrandRequest req, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task AddKeywordAsync(int brandId, string keyword, CancellationToken ct = default);
    Task RemoveKeywordAsync(int brandId, string keyword, CancellationToken ct = default);
}

/// <summary>Brand/keyword management via raw SQL (Dapper). Collector + Analysis pick changes up from the same DB.</summary>
public sealed class BrandAdmin(NpgsqlDataSource dataSource) : IBrandAdmin
{
    public async Task<IReadOnlyCollection<BrandDto>> ListAsync(CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var brands = (await conn.QueryAsync<(int Id, string Name)>(
            new CommandDefinition("SELECT \"Id\", \"Name\" FROM brands ORDER BY \"Name\";", cancellationToken: ct))).AsList();
        var kws = (await conn.QueryAsync<(int BrandId, string Keyword)>(
            new CommandDefinition("SELECT \"BrandId\", \"Keyword\" FROM brand_keywords;", cancellationToken: ct))).AsList();

        return brands.Select(b => new BrandDto(b.Id, b.Name,
            kws.Where(k => k.BrandId == b.Id).Select(k => k.Keyword).ToList())).ToList();
    }

    public async Task<int> CreateAsync(CreateBrandRequest req, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var id = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "INSERT INTO brands (\"Name\") VALUES (@Name) RETURNING \"Id\";", new { req.Name }, cancellationToken: ct));

        var keywords = (req.Keywords ?? new()).Select(k => k.Trim()).Where(k => k.Length > 0).Distinct().ToList();
        if (keywords.Count == 0) keywords.Add(req.Name); // default keyword = brand name
        foreach (var k in keywords)
            await conn.ExecuteAsync(new CommandDefinition(
                "INSERT INTO brand_keywords (\"BrandId\", \"Keyword\") VALUES (@BrandId, @Keyword);",
                new { BrandId = id, Keyword = k }, cancellationToken: ct));
        return id;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition("DELETE FROM brand_keywords WHERE \"BrandId\" = @id;", new { id }, cancellationToken: ct));
        await conn.ExecuteAsync(new CommandDefinition("DELETE FROM brands WHERE \"Id\" = @id;", new { id }, cancellationToken: ct));
    }

    public async Task AddKeywordAsync(int brandId, string keyword, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "INSERT INTO brand_keywords (\"BrandId\", \"Keyword\") VALUES (@brandId, @keyword);",
            new { brandId, keyword }, cancellationToken: ct));
    }

    public async Task RemoveKeywordAsync(int brandId, string keyword, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM brand_keywords WHERE \"BrandId\" = @brandId AND \"Keyword\" = @keyword;",
            new { brandId, keyword }, cancellationToken: ct));
    }
}
