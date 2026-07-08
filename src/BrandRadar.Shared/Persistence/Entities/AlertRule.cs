namespace BrandRadar.Shared.Persistence.Entities;

/// <summary>
/// Luật cảnh báo cấu hình được (Crisis Management). Người dùng tự định nghĩa điều kiện + kênh gửi,
/// thay cho ngưỡng hard-code. RuleEngine trong Analysis.Worker đánh giá theo từng mention mới.
/// </summary>
public sealed class AlertRule
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>null = áp dụng cho MỌI thương hiệu.</summary>
    public int? BrandId { get; set; }

    /// <summary>"negative" = số mention tiêu cực; "volume" = tổng lượng nhắc; "negshare" = % tiêu cực.</summary>
    public string Type { get; set; } = "negative";

    /// <summary>Ngưỡng kích hoạt (số lượng, hoặc % 0..100 với type "negshare").</summary>
    public double Threshold { get; set; } = 5;

    public int WindowMinutes { get; set; } = 60;
    public int CooldownMinutes { get; set; } = 30;

    /// <summary>"inapp" = realtime trên dashboard; "slack"/"webhook" = POST tới Target.</summary>
    public string Channel { get; set; } = "inapp";
    public string? Target { get; set; }

    public bool Enabled { get; set; } = true;
    public DateTimeOffset? LastFiredAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
