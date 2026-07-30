namespace S7TrendMonitor.Models;

/// <summary>
/// 一次采样得到的数据批次
/// </summary>
public class SampleBatch
{
    public long TimestampMs { get; set; }
    public DateTime Timestamp => DateTimeOffset.FromUnixTimeMilliseconds(TimestampMs).LocalDateTime;
    public Dictionary<int, double> Values { get; set; } = new();
    public Dictionary<int, bool> ReadErrors { get; set; } = new();
}
