using S7.Net;
using S7.Net.Types;
using S7TrendMonitor.Models;
using S7TrendMonitor.Utils;

namespace S7TrendMonitor.Communication;

/// <summary>
/// 以太网通信引擎，基于 S7NetPlus (Plc类) 实现
/// 适用于 S7-300/400/1200/1500 的以太网(TSAP/ISO-on-TCP)连接
/// </summary>
public class EthernetPlcService : IPlcCommunication
{
    private readonly string _cpuType;
    private readonly string _ipAddress;
    private readonly short _rack;
    private readonly short _slot;

    private Plc? _plc;
    private bool _isConnected;

    /// <summary>单个PDU请求中变量项数上限，避免超出PDU容量</summary>
    private const int MaxItemsPerRequest = 20;

    public EthernetPlcService(string cpuType, string ipAddress, short rack, short slot)
    {
        _cpuType = cpuType;
        _ipAddress = ipAddress;
        _rack = rack;
        _slot = slot;
    }

    public string ConnectionDescription => $"以太网 {_ipAddress}:{_rack}/{_slot}";

    public bool IsConnected => _isConnected && _plc != null;

    public async Task<bool> ConnectAsync()
    {
        try
        {
            await Task.Run(() =>
            {
                try { _plc?.Close(); } catch { /* 忽略关闭异常 */ }

                var cpu = ParseCpuType(_cpuType);
                _plc = new Plc(cpu, _ipAddress, _rack, _slot);
                _plc.Open();   // 失败时抛出异常
                _isConnected = true;
                Logger.Info($"已连接到PLC: {ConnectionDescription}");
            });
            return _isConnected;
        }
        catch (Exception ex)
        {
            _isConnected = false;
            _plc = null;
            Logger.Error($"连接PLC失败: {ConnectionDescription}", ex);
            return false;
        }
    }

    public async Task<bool> EnsureConnectedAsync()
    {
        if (_isConnected && _plc != null)
            return true;
        return await ConnectAsync();
    }

    public void Disconnect()
    {
        try
        {
            _plc?.Close();
        }
        catch (Exception ex)
        {
            Logger.Error("断开PLC连接时出错", ex);
        }
        finally
        {
            _isConnected = false;
            _plc = null;
        }
    }

    public async Task<Dictionary<int, double>> ReadVariablesAsync(List<(int id, ParsedS7Address addr)> variables)
    {
        var result = new Dictionary<int, double>();
        if (variables == null || variables.Count == 0)
            return result;

        if (!IsConnected)
            throw new InvalidOperationException($"PLC未连接: {ConnectionDescription}");

        await Task.Run(() =>
        {
            // 优先尝试 ReadMultipleVars 批量读取（性能更优）
            bool batchOk = TryReadMultipleVars(variables, result);
            if (!batchOk)
            {
                // 批量失败则回退到逐个读取
                Logger.Warning($"批量读取失败，回退到逐个读取: {ConnectionDescription}");
                ReadOneByOne(variables, result);
            }
        });

        return result;
    }

    /// <summary>
    /// 使用 ReadMultipleVars 批量读取
    /// </summary>
    private bool TryReadMultipleVars(List<(int id, ParsedS7Address addr)> variables, Dictionary<int, double> result)
    {
        try
        {
            // 构建 DataItem 列表
            var dataItems = new List<DataItem>(variables.Count);
            foreach (var (_, addr) in variables)
            {
                dataItems.Add(new DataItem
                {
                    DataType = MapDataType(addr.AreaType),
                    VarType = MapVarType(addr.VarType),
                    DB = addr.AreaType == "DB" ? addr.DbNumber : 0,
                    StartByteAdr = addr.ByteOffset,
                    BitAdr = (byte)(addr.VarType == S7VarType.Bit ? addr.BitOffset : 0),
                    Count = 1
                });
            }

            // 分块读取，避免单次PDU超出容量
            for (int i = 0; i < dataItems.Count; i += MaxItemsPerRequest)
            {
                int count = Math.Min(MaxItemsPerRequest, dataItems.Count - i);
                var chunk = dataItems.GetRange(i, count);
                _plc!.ReadMultipleVars(chunk);

                for (int j = 0; j < chunk.Count; j++)
                {
                    int varIndex = i + j;
                    result[variables[varIndex].id] = ConvertToDouble(chunk[j].Value, variables[varIndex].addr.VarType);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("ReadMultipleVars 批量读取异常", ex);
            // 读取过程中可能已部分写入result，清理以保证回退读取的一致性
            result.Clear();
            return false;
        }
    }

    /// <summary>
    /// 逐个读取回退方案：使用 ReadBytes 读取原始字节后手动转换
    /// </summary>
    private void ReadOneByOne(List<(int id, ParsedS7Address addr)> variables, Dictionary<int, double> result)
    {
        foreach (var (id, addr) in variables)
        {
            try
            {
                var dataType = MapDataType(addr.AreaType);
                int db = addr.AreaType == "DB" ? addr.DbNumber : 0;
                byte[] bytes = _plc!.ReadBytes(dataType, db, addr.ByteOffset, addr.ByteSize);
                result[id] = ConvertBytesToDouble(bytes, addr.VarType, addr.BitOffset);
            }
            catch (Exception ex)
            {
                Logger.Error($"读取变量失败: {addr.OriginalAddress}", ex);
                result[id] = 0.0;
            }
        }
    }

    // ===== 类型映射 =====

    private static DataType MapDataType(string areaType) => areaType.ToUpperInvariant() switch
    {
        "DB" => DataType.DataBlock,
        "M" => DataType.Memory,
        "I" => DataType.Input,
        "Q" => DataType.Output,
        _ => DataType.DataBlock
    };

    private static VarType MapVarType(S7VarType varType) => varType switch
    {
        S7VarType.Bit => VarType.Bit,
        S7VarType.Byte => VarType.Byte,
        S7VarType.Word => VarType.Word,
        S7VarType.Int => VarType.Int,
        S7VarType.DWord => VarType.DWord,
        S7VarType.DInt => VarType.DInt,
        S7VarType.Real => VarType.Real,
        _ => VarType.Byte
    };

    private static CpuType ParseCpuType(string cpuType)
    {
        if (string.IsNullOrWhiteSpace(cpuType))
            return CpuType.S7300;
        if (Enum.TryParse<CpuType>(cpuType, true, out var cpu))
            return cpu;
        return CpuType.S7300;
    }

    // ===== 数据转换 =====

    /// <summary>
    /// 将 S7NetPlus 返回的 object 转换为 double
    /// </summary>
    private static double ConvertToDouble(object? value, S7VarType varType)
    {
        if (value == null)
            return 0.0;

        try
        {
            // ReadMultipleVars 对 Count=1 可能返回单值或单元素数组，统一处理
            object v = value;
            if (value is Array arr && arr.Length == 1)
                v = arr.GetValue(0)!;

            return varType switch
            {
                S7VarType.Bit => v is bool b ? (b ? 1.0 : 0.0) : Convert.ToDouble(v),
                S7VarType.Byte => v is byte by ? by : Convert.ToDouble(v),
                S7VarType.Word => v is ushort u ? u : Convert.ToDouble(v),
                S7VarType.Int => v is short s ? s : Convert.ToDouble(v),
                S7VarType.DWord => v is uint ui ? ui : Convert.ToDouble(v),
                S7VarType.DInt => v is int iv ? iv : Convert.ToDouble(v),
                S7VarType.Real => v is float f ? f : Convert.ToDouble(v),
                _ => Convert.ToDouble(v)
            };
        }
        catch
        {
            return 0.0;
        }
    }

    /// <summary>
    /// 将原始字节数组(大端序)按变量类型转换为 double
    /// 用于 ReadBytes 回退路径
    /// </summary>
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
    /// 反转字节序（S7为大端序，Windows为小端序）
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
        Disconnect();
    }
}
