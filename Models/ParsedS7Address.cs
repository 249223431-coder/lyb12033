namespace S7TrendMonitor.Models;

/// <summary>
/// S7地址解析后的结构化数据
/// </summary>
public class ParsedS7Address
{
    /// <summary>区域类型：DB, M, I, Q</summary>
    public string AreaType { get; set; } = "DB";
    
    /// <summary>DB块号（仅DB区域有效）</summary>
    public int DbNumber { get; set; }
    
    /// <summary>字节偏移</summary>
    public int ByteOffset { get; set; }
    
    /// <summary>位偏移（仅Bool类型有效，0-7）</summary>
    public int BitOffset { get; set; }
    
    /// <summary>S7变量类型</summary>
    public S7VarType VarType { get; set; }
    
    /// <summary>原始地址字符串</summary>
    public string OriginalAddress { get; set; } = "";
    
    /// <summary>占用字节数</summary>
    public int ByteSize => VarType switch
    {
        S7VarType.Bit => 1,
        S7VarType.Byte => 1,
        S7VarType.Word => 2,
        S7VarType.Int => 2,
        S7VarType.DWord => 4,
        S7VarType.DInt => 4,
        S7VarType.Real => 4,
        _ => 0
    };
}

/// <summary>
/// S7变量数据类型
/// </summary>
public enum S7VarType
{
    Bit,      // DBX / M.X / I.X / Q.X (Bool)
    Byte,     // DBB / MB / IB / QB
    Word,     // DBW (无符号16位)
    Int,      // DBW (有符号16位)
    DWord,    // DBD (无符号32位)
    DInt,     // DBD (有符号32位)
    Real      // DBD (浮点32位)
}
