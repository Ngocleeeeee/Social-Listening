namespace Analysis.Worker;

public sealed class CrisisOptions
{
    public const string SectionName = "Crisis";
    public int WindowMinutes { get; set; } = 15;
    public int NegativeThreshold { get; set; } = 5;
    public int CooldownMinutes { get; set; } = 15;
    public string WebhookUrl { get; set; } = "";   // Slack-compatible; empty => disabled
    public int SpikeWindowMinutes { get; set; } = 60;
    public int SpikeMultiplier { get; set; } = 3;    // current >= N× previous window
    public int SpikeMinCount { get; set; } = 8;
}
