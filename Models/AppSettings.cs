namespace S7TrendMonitor.Models;

public class AppSettings
{
    public int SamplingIntervalMs { get; set; } = 1000;
    public int DataRetentionHours { get; set; } = 24;
    public int ChartWindowSeconds { get; set; } = 300;
    public bool AutoStartSampling { get; set; } = false;
    public bool PauseChartWhenFull { get; set; } = false;
    public string LastConnectionType { get; set; } = "Ethernet";
}
