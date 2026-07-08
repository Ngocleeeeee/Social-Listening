using BrandRadar.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BrandRadar.Shared.Persistence;

public sealed class BrandRadarDbContext(DbContextOptions<BrandRadarDbContext> options) : DbContext(options)
{
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<BrandKeyword> BrandKeywords => Set<BrandKeyword>();
    public DbSet<Mention> Mentions => Set<Mention>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Brand>(e => { e.ToTable("brands"); e.HasKey(x => x.Id); e.Property(x => x.Name).HasMaxLength(128).IsRequired(); });
        b.Entity<BrandKeyword>(e =>
        {
            e.ToTable("brand_keywords"); e.HasKey(x => x.Id);
            e.Property(x => x.Keyword).HasMaxLength(128).IsRequired();
            e.HasIndex(x => x.Keyword);
        });
        b.Entity<Mention>(e =>
        {
            e.ToTable("mentions"); e.HasKey(x => x.Id);
            e.HasIndex(x => x.ExternalId).IsUnique();
            e.HasIndex(x => x.Sentiment);
            e.HasIndex(x => x.Source);
            e.HasIndex(x => x.Fingerprint);
            e.HasIndex(x => new { x.BrandId, x.PublishedAt });
            e.Property(x => x.Source).HasMaxLength(128);
            e.Property(x => x.Sentiment).HasMaxLength(16);
        });
        b.Entity<Alert>(e => { e.ToTable("alerts"); e.HasKey(x => x.Id); e.HasIndex(x => x.CreatedAt); });
        b.Entity<AlertRule>(e =>
        {
            e.ToTable("alert_rules"); e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(128).IsRequired();
            e.Property(x => x.Type).HasMaxLength(16);
            e.Property(x => x.Channel).HasMaxLength(16);
            e.HasIndex(x => x.Enabled);
        });
    }
}
