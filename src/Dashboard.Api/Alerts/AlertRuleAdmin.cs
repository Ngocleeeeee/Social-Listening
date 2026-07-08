using Dapper;
using Npgsql;

namespace Dashboard.Api.Alerts;

public sealed record AlertRuleDto(
    int Id, string Name, int? BrandId, string Type, double Threshold,
    int WindowMinutes, int CooldownMinutes, string Channel, string? Target,
    bool Enabled, DateTime? LastFiredAt);   // Npgsql trả timestamptz về DateTime (không phải DateTimeOffset)

public sealed record UpsertRuleRequest(
    string Name, int? BrandId, string Type, double Threshold,
    int WindowMinutes, int CooldownMinutes, string Channel, string? Target, bool Enabled);

public interface IAlertRuleAdmin
{
    Task<IReadOnlyCollection<AlertRuleDto>> ListAsync(CancellationToken ct = default);
    Task<int> CreateAsync(UpsertRuleRequest r, CancellationToken ct = default);
    Task<bool> UpdateAsync(int id, UpsertRuleRequest r, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task SetEnabledAsync(int id, bool enabled, CancellationToken ct = default);
}

/// <summary>CRUD luật cảnh báo qua raw SQL (Dapper). Analysis.Worker đọc lại từ cùng DB (cache 30s).</summary>
public sealed class AlertRuleAdmin(NpgsqlDataSource dataSource) : IAlertRuleAdmin
{
    public async Task<IReadOnlyCollection<AlertRuleDto>> ListAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT "Id","Name","BrandId","Type","Threshold","WindowMinutes","CooldownMinutes",
                   "Channel","Target","Enabled","LastFiredAt"
            FROM alert_rules ORDER BY "Enabled" DESC, "Id";
            """;
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        return (await conn.QueryAsync<AlertRuleDto>(new CommandDefinition(sql, cancellationToken: ct))).AsList();
    }

    public async Task<int> CreateAsync(UpsertRuleRequest r, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO alert_rules
              ("Name","BrandId","Type","Threshold","WindowMinutes","CooldownMinutes","Channel","Target","Enabled","CreatedAt")
            VALUES (@Name,@BrandId,@Type,@Threshold,@WindowMinutes,@CooldownMinutes,@Channel,@Target,@Enabled, now())
            RETURNING "Id";
            """;
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(sql, r, cancellationToken: ct));
    }

    public async Task<bool> UpdateAsync(int id, UpsertRuleRequest r, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE alert_rules SET
              "Name"=@Name,"BrandId"=@BrandId,"Type"=@Type,"Threshold"=@Threshold,
              "WindowMinutes"=@WindowMinutes,"CooldownMinutes"=@CooldownMinutes,
              "Channel"=@Channel,"Target"=@Target,"Enabled"=@Enabled
            WHERE "Id"=@id;
            """;
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var n = await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            id, r.Name, r.BrandId, r.Type, r.Threshold, r.WindowMinutes, r.CooldownMinutes, r.Channel, r.Target, r.Enabled
        }, cancellationToken: ct));
        return n > 0;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition("DELETE FROM alert_rules WHERE \"Id\"=@id;", new { id }, cancellationToken: ct));
    }

    public async Task SetEnabledAsync(int id, bool enabled, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE alert_rules SET \"Enabled\"=@enabled WHERE \"Id\"=@id;", new { id, enabled }, cancellationToken: ct));
    }
}
