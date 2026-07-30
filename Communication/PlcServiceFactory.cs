using S7TrendMonitor.Models;

namespace S7TrendMonitor.Communication;

/// <summary>
/// 通信引擎工厂，根据连接配置创建对应的通信服务实例
/// </summary>
public static class PlcServiceFactory
{
    public static IPlcCommunication Create(ConnectionConfig config)
    {
        return config.ConnectionType switch
        {
            ConnectionType.Ethernet => new EthernetPlcService(
                config.CpuType, config.IpAddress, config.Rack, config.Slot),
            ConnectionType.MpiPcAdapter => new MpiPlcService(
                MpiHardwareType.PcAdapterUsb, config.ComPort,
                config.MpiBaudRate, config.MpiAddress, config.Rack, config.Slot),
            ConnectionType.MpiCp5611 => new MpiPlcService(
                MpiHardwareType.Cp5611, null,
                config.MpiBaudRate, config.MpiAddress, config.Rack, config.Slot),
            _ => throw new ArgumentException("不支持的连接类型")
        };
    }
}
