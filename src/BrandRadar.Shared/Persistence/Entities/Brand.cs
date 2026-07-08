namespace BrandRadar.Shared.Persistence.Entities;

public sealed class Brand
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<BrandKeyword> Keywords { get; set; } = new();
}

public sealed class BrandKeyword
{
    public int Id { get; set; }
    public int BrandId { get; set; }
    public string Keyword { get; set; } = string.Empty;
}
