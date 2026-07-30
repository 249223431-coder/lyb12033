using System.Runtime.InteropServices;
using S7TrendMonitor.Models;
using S7TrendMonitor.Utils;

namespace S7TrendMonitor.Communication;

/// <summary>
/// MPI通信引擎，基于 libnodave 原生库实现
/// 支持两种硬件：
///   - PC Adapter USB（通过串口）
///   - CP5611 卡（通过 S7online 访问点）
/// </summary>
public class MpiPlcService : IPlcCommunication
{
    private readonly MpiHardwareType _hardwareType;
    private readonly string? _comPort;
    private readonly int _baudRate;
    private readonly int _mpiAddress;
    private readonly short _rack;
    private readonly short _slot;

    // libnodave 句柄
    private LibnodaveNative.DaveOSSerialType _fds;
    private IntPtr _di = IntPtr.Zero;   // daveInterface
    private IntPtr _dc = IntPtr.Zero;   // daveConnection
    private bool _isConnected;

    public MpiPlcService(MpiHardwareType hardwareType, string? comPort,
        int baudRate, int mpiAddress, short rack, short slot)
    {
        _hardwareType = hardwareType;
        _comPort = comPort;
        _baudRate = baudRate;
        _mpiAddress = mpiAddress;
        _rack = rack;
        _slot = slot;
    }

    public string ConnectionDescription =>
        $"MPI {_hardwareType} 地址{_mpiAddress} {_baudRate}bps";

    public bool IsConnected => _isConnected;

    public async Task<bool> ConnectAsync()
    {
        try
        {
            return await Task.Run(() =>
            {
                int speed = MapBaudRate(_baudRate);

                // 1. 打开底层通信通道
                if (_hardwareType == MpiHardwareType.PcAdapterUsb)
                {
                    if (string.IsNullOrWhiteSpace(_comPort))
                        throw new InvalidOperationException("PC Adapter USB 模式需要指定串口名(ComPort)");

                    // parity=0 表示无校验
                    _fds = LibnodaveNative.setPort(_comPort!, _baudRate.ToString(), 0);
                    if (_fds.rfd <= 0)
                    {
                        Logger.Error($"打开串口失败: {_comPort}");
                        return false;
                    }
                }
                else // Cp5611
                {
                    int fd = LibnodaveNative.openS7online("S7ONLINE", 0);
                    if (fd <= 0)
                    {
                        Logger.Error("打开CP5611访问点 S7ONLINE 失败");
                        return false;
                    }
                    _fds = new LibnodaveNative.DaveOSSerialType { rfd = fd, wfd = fd };
                }

                // 2. 创建 daveInterface
                _di = LibnodaveNative.daveNewInterface(
                    _fds, "IF1", 0, LibnodaveNative.daveProtoMPI, speed);
                if (_di == IntPtr.Zero)
                {
                    Logger.Error("创建 daveInterface 失败");
                    ReleaseAdapter();
                    return false;
                }

                // 3. 创建到PLC的连接
                _dc = LibnodaveNative.daveNewConnection(_di, _mpiAddress, _rack, _slot);
                if (_dc == IntPtr.Zero)
                {
                    Logger.Error("创建 daveConnection 失败");
                    ReleaseAdapter();
                    return false;
                }

                // 4. 连接PLC，返回0表示成功
                int res = LibnodaveNative.daveConnectPLC(_dc);
                if (res != 0)
                {
                    Logger.Error($"连接PLC失败，libnodave错误码: {res}, {ConnectionDescription}");
                    ReleaseConnection();
                    ReleaseAdapter();
                    return false;
                }

                _isConnected = true;
                Logger.Info($"已连接到PLC: {ConnectionDescription}");
                return true;
            });
        }
        catch (Exception ex)
        {
            Logger.Error($"MPI连接异常: {ConnectionDescription}", ex);
            CleanupAll();
            return false;
        }
    }

    public async Task<bool> EnsureConnectedAsync()
    {
        if (_isConnected && _dc != IntPtr.Zero)
            return true;
        return await ConnectAsync();
    }

    public async Task<Dictionary<int, double>> ReadVariablesAsync(List<(int id, ParsedS7Address addr)> variables)
    {
        var result = new Dictionary<int, double>();
        if (variables == null || variables.Count == 0)
            return result;

        if (!_isConnected || _dc == IntPtr.Zero)
            throw new InvalidOperationException($"PLC未连接: {ConnectionDescription}");

        // MPI模式逐个读取（无批量优化）
        await Task.Run(() =>
        {
            foreach (var (id, addr) in variables)
            {
                try
                {
                    int area = MapArea(addr.AreaType);
                    int dbNum = addr.AreaType == "DB" ? addr.DbNumber : 0;
                    byte[] buffer = new byte[addr.ByteSize];

                    int res = LibnodaveNative.daveReadBytes(
                        _dc, area, dbNum, addr.ByteOffset, addr.ByteSize, buffer);

                    if (res == 0)
                    {
                        result[id] = ConvertBytesToDouble(buffer, addr.VarType, addr.BitOffset);
                    }
                    else
                    {
                        Logger.Warning($"读取变量失败: {addr.OriginalAddress}, 错误码: {res}");
                        result[id] = 0.0;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"读取变量异常: {addr.OriginalAddress}", ex);
                    result[id] = 0.0;
                }
            }
        });

        return result;
    }

    public void Disconnect()
    {
        CleanupAll();
    }

    // ===== 资源释放 =====

    private void CleanupAll()
    {
        ReleaseConnection();
        ReleaseAdapter();
        if (_isConnected)
        {
            _isConnected = false;
            Logger.Info($"已断开PLC连接: {ConnectionDescription}");
        }
    }

    private void ReleaseConnection()
    {
        if (_dc != IntPtr.Zero)
        {
            try { LibnodaveNative.daveDisconnectPLC(_dc); }
            catch (Exception ex) { Logger.Warning($"daveDisconnectPLC 异常: {ex.Message}"); }

            try { LibnodaveNative.daveFree(_dc); }
            catch (Exception ex) { Logger.Warning($"daveFree 异常: {ex.Message}"); }

            _dc = IntPtr.Zero;
        }
    }

    private void ReleaseAdapter()
    {
        if (_di != IntPtr.Zero)
        {
            try { LibnodaveNative.daveDisconnectAdapter(_di); }
            catch (Exception ex) { Logger.Warning($"daveDisconnectAdapter 异常: {ex.Message}"); }

            try { LibnodaveNative.daveFreeAdapter(_di); }
            catch (Exception ex) { Logger.Warning($"daveFreeAdapter 异常: {ex.Message}"); }

            _di = IntPtr.Zero;
        }
    }

    // ===== 映射辅助 =====

    /// <summary>
    /// 区域类型映射到 libnodave 区域常量
    /// </summary>
    private static int MapArea(string areaType) => areaType.ToUpperInvariant() switch
    {
        "DB" => LibnodaveNative.daveDB,
        "M" => LibnodaveNative.daveFlags,
        "I" => LibnodaveNative.daveInputs,
        "Q" => LibnodaveNative.daveOutputs,
        _ => LibnodaveNative.daveDB
    };

    /// <summary>
    /// 波特率映射到 libnodave 速度常量
    /// </summary>
    private static int MapBaudRate(int baud) => baud switch
    {
        9600 => LibnodaveNative.daveSpeed9k6,
        19200 => LibnodaveNative.daveSpeed19k2,
        187500 => LibnodaveNative.daveSpeed187k5,
        _ => LibnodaveNative.daveSpeed187k5
    };

    // ===== 数据转换（S7大端序 -> 本地小端序）=====

    private static double ConvertBytesToDouble(byte[] bytes, S7VarType varType, int bitOffset)
    {
        if (bytes == null || bytes.Length == 0)
            return 0.0;

        try
        {
            return varType switch
            {
                S7VarType.Bit => (bytes[0] & (1 << bitOffset)) != 0 ? 1.0 : 0.0,
                S7VarType.Byte => bytes[0],
                S7VarType.Word => BitConverter.ToUInt16(Reverse(bytes, 2), 0),
                S7VarType.Int => BitConverter.ToInt16(Reverse(bytes, 2), 0),
                S7VarType.DWord => BitConverter.ToUInt32(Reverse(bytes, 4), 0),
                S7VarType.DInt => BitConverter.ToInt32(Reverse(bytes, 4), 0),
                S7VarType.Real => BitConverter.ToSingle(Reverse(bytes, 4), 0),
                _ => 0.0
            };
        }
        catch
        {
            return 0.0;
        }
    }

    /// <summary>
    /// 反转字节序（S7为大端序，Windows BitConverter为小端序）
    /// </summary>
    private static byte[] Reverse(byte[] bytes, int count)
    {
        var reversed = new byte[count];
        for (int i = 0; i < count; i++)
            reversed[i] = bytes[count - 1 - i];
        return reversed;
    }

    public void Dispose()
    {
        CleanupAll();
    }
}
