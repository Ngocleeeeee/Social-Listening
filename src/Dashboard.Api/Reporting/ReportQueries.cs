using Dapper;
using Npgsql;

namespace Dashboard.Api.Reporting;

/// <summary>
/// Reporting via raw, parameterised SQL (Dapper) directly on PostgreSQL — hand-tuned GROUP BY
/// aggregations, complementary to the Elasticsearch search path. EF Core created the tables with
/// quoted PascalCase columns, so identifiers are quoted here.
/// </summary>
public interface IReportQueries
{
    Task<IReadOnlyCollection<BrandReport>> ByBrandAsync(CancellationToken ct = default);
    Task<IReadOnlyCollection<DailyReport>> DailyAsync(int days, CancellationToken ct = default);
}

public sealed record BrandReport(string Brand, long Total, long Negative, long Positive);
public sealed record DailyReport(string Day, long Total, long Negative);

public sealed class ReportQueries(NpgsqlDataSource dataSource) : IReportQueries
{
    public async Task<IReadOnlyCollection<BrandReport>> ByBrandAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT b."Name" AS "Brand",
                   COUNT(*)                                              AS "Total",
                   COUNT(*) FILTER (WHERE m."Sentiment" = 'Negative')    AS "Negative",
                   COUNT(*) FILTER (WHERE m."Sentiment" = 'Positive')    AS "Positive"
            FROM mentions m
            JOIN brands b ON b."Id" = m."BrandId"
            GROUP BY b."Name"
            ORDER BY COUNT(*) DESC;
            """;
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        return (await conn.QueryAsync<BrandReport>(new CommandDefinition(sql, cancellationToken: ct))).AsList();
    }

    public async Task<IReadOnlyCollection<DailyReport>> DailyAsync(int days, CancellationToken ct = default)
    {
        const string sql = """
            SELECT to_char(date_trunc('day', m."PublishedAt"), 'YYYY-MM-DD') AS "Day",
                   COUNT(*)                                                   AS "Total",
                   COUNT(*) FILTER (WHERE m."Sentiment" = 'Negative')         AS "Negative"
            FROM mentions m
            WHERE m."PublishedAt" >= now() - (@days || ' days')::interval
            GROUP BY date_trunc('day', m."PublishedAt")
            ORDER BY date_trunc('day', m."PublishedAt");
            """;
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        return (await conn.QueryAsync<DailyReport>(new CommandDefinition(sql, new { days }, cancellationToken: ct))).AsList();
    }
}
