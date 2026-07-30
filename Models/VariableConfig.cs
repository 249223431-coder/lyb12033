namespace S7TrendMonitor.Models;

public class VariableConfig
{
    public int Id { get; set; }
    public string Address { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string DataType { get; set; } = "Real";
    public double ScaleMin { get; set; } = 0;
    public double ScaleMax { get; set; } = 100;
    public string ColorHex { get; set; } = "#1E90FF";
    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
