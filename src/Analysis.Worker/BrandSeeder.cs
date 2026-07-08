using BrandRadar.Shared.Persistence;
using BrandRadar.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Analysis.Worker;

/// <summary>
/// Idempotently seeds Vietnamese brands frequently mentioned in the news, so real RSS articles
/// actually match a brand. Adds missing brands/keywords without wiping existing data.
/// </summary>
public static class BrandSeeder
{
    private static readonly Dictionary<string, string[]> Desired = new()
    {
        ["VinFast"] = ["VinFast", "VF3", "VF8", "VF9"],
        ["Vingroup"] = ["Vingroup", "Vin Group"],
        ["Viettel"] = ["Viettel"],
        ["FPT"] = ["FPT"],
        ["Vietnam Airlines"] = ["Vietnam Airlines"],
        ["Vietjet"] = ["Vietjet"],
        ["Bamboo Airways"] = ["Bamboo Airways"],
        ["Vietcombank"] = ["Vietcombank", "VCB"],
        ["Techcombank"] = ["Techcombank"],
        ["MoMo"] = ["MoMo", "Ví MoMo"],
        ["Thế Giới Di Động"] = ["Thế Giới Di Động", "MWG"],
        ["Hòa Phát"] = ["Hòa Phát", "HPG"],
        ["Masan"] = ["Masan"],
        ["Shopee"] = ["Shopee"],
        ["Lazada"] = ["Lazada"],
        ["Tiki"] = ["Tiki"],
        ["Grab"] = ["Grab"],
        ["VNG"] = ["VNG"]
    };

    public static async Task SeedAsync(BrandRadarDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        // EnsureCreated KHÔNG thêm bảng mới vào DB đã tồn tại → tự tạo alert_rules nếu thiếu.
        // Idempotent, không mất dữ liệu, tránh phải down -v (vốn gây dội lại tab Trực tiếp).
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS alert_rules (
                "Id" serial PRIMARY KEY,
                "Name" varchar(128) NOT NULL,
                "BrandId" integer NULL,
                "Type" varchar(16),
                "Threshold" double precision NOT NULL DEFAULT 0,
                "WindowMinutes" integer NOT NULL DEFAULT 60,
                "CooldownMinutes" integer NOT NULL DEFAULT 30,
                "Channel" varchar(16),
                "Target" text NULL,
                "Enabled" boolean NOT NULL DEFAULT true,
                "LastFiredAt" timestamptz NULL,
                "CreatedAt" timestamptz NOT NULL DEFAULT now()
            );
            """);
        var existing = await db.Brands.Include(b => b.Keywords).ToListAsync();

        foreach (var (name, kws) in Desired)
        {
            var brand = existing.FirstOrDefault(b => b.Name == name);
            if (brand is null)
            {
                db.Brands.Add(new Brand { Name = name, Keywords = kws.Select(k => new BrandKeyword { Keyword = k }).ToList() });
            }
            else
            {
                foreach (var k in kws)
                    if (!brand.Keywords.Any(x => x.Keyword == k))
                        brand.Keywords.Add(new BrandKeyword { Keyword = k });
            }
        }
        await db.SaveChangesAsync();

        // Seed one example alert rule (distinct from the built-in crisis detector) so the feature is
        // visible out of the box. Type "negshare" = % tiêu cực, không trùng luật built-in.
        if (!await db.AlertRules.AnyAsync())
        {
            db.AlertRules.Add(new AlertRule
            {
                Name = "Tỷ lệ tiêu cực cao (mọi thương hiệu)",
                BrandId = null, Type = "negshare", Threshold = 50, WindowMinutes = 120,
                CooldownMinutes = 60, Channel = "inapp", Enabled = true
            });
            await db.SaveChangesAsync();
        }
    }
}
