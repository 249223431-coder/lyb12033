namespace S7TrendMonitor.Models;

public class ConnectionConfig
{
    // 连接类型
    public ConnectionType ConnectionType { get; set; } = ConnectionType.Ethernet;

    // 以太网参数
    public string CpuType { get; set; } = "S7300";
    public string IpAddress { get; set; } = "192.168.0.1";
    public short Rack { get; set; } = 0;
    public short Slot { get; set; } = 2;

    // MPI参数
    public MpiHardwareType MpiHardware { get; set; } = MpiHardwareType.PcAdapterUsb;
    public string ComPort { get; set; } = "COM3";
    public int MpiBaudRate { get; set; } = 187500;
    public int MpiAddress { get; set; } = 2;
}

public enum ConnectionType { Ethernet, MpiPcAdapter, MpiCp5611 }
public enum MpiHardwareType { PcAdapterUsb, Cp5611 }
