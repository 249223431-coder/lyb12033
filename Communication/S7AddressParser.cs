using S7TrendMonitor.Models;

namespace S7TrendMonitor.Communication;

/// <summary>
/// S7地址解析器
/// 支持的地址格式：
///   DB区域: DB1.DBD10 / DB1.DBW10 / DB1.DBB10 / DB1.DBX10.0
///   M/I/Q区域: MD10 / MW10 / MB10 / M10.0 / ID10 / IW10 / QW10 等
/// 类型后缀推断: X=Bit, B=Byte, W=Word, D=Real(用户可在变量配置中修改为DInt/DWord)
/// </summary>
public static class S7AddressParser
{
    /// <summary>
    /// 解析S7地址字符串为结构化地址对象
    /// </summary>
    public static ParsedS7Address Parse(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("地址不能为空", nameof(address));

        string trimmed = address.Trim();
        string upper = trimmed.ToUpperInvariant();

        ParsedS7Address result;
        if (upper.StartsWith("DB"))
            result = ParseDbAddress(upper);
        else
            result = ParseAreaAddress(upper);

        result.OriginalAddress = trimmed;
        Validate(result);
        return result;
    }

    /// <summary>
    /// 尝试解析S7地址，失败返回false
    /// </summary>
    public static bool TryParse(string address, out ParsedS7Address? parsed)
    {
        try
        {
            parsed = Parse(address);
            return true;
        }
        catch
        {
            parsed = null;
            return false;
        }
    }

    /// <summary>
    /// 解析DB区域地址，格式: DB{n}.DB{X|B|W|D}{offset}[.{bit}]
    /// </summary>
    private static ParsedS7Address ParseDbAddress(string address)
    {
        var parts = address.Split('.');
        if (parts.Length < 2)
            throw new FormatException($"DB地址格式无效: {address}");

        // 解析DB号
        var dbPart = parts[0];
        if (!dbPart.StartsWith("DB") || dbPart.Length <= 2 ||
            !int.TryParse(dbPart.Substring(2), out int dbNumber) || dbNumber < 1)
            throw new FormatException($"DB号无效: {address}");

        // 解析类型与偏移，形如 DBD10 / DBW10 / DBB10 / DBX10
        var typePart = parts[1];
        if (!typePart.StartsWith("DB") || typePart.Length < 4)
            throw new FormatException($"DB变量类型无效: {address}");

        char typeChar = typePart[2];
        string offsetStr = typePart.Substring(3);

        if (!int.TryParse(offsetStr, out int byteOffset) || byteOffset < 0)
            throw new FormatException($"字节偏移无效: {address}");

        var result = new ParsedS7Address
        {
            AreaType = "DB",
            DbNumber = dbNumber,
            ByteOffset = byteOffset,
            VarType = InferVarType(typeChar)
        };

        // 位地址额外解析 bit 偏移
        if (typeChar == 'X' && parts.Length >= 3)
        {
            if (!int.TryParse(parts[2], out int bit) || bit < 0 || bit > 7)
                throw new FormatException($"位偏移无效: {address}");
            result.BitOffset = bit;
        }

        return result;
    }

    /// <summary>
    /// 解析M/I/Q区域地址，格式: {M|I|Q}{W|B|D}{offset} 或 {M|I|Q}{offset}.{bit}
    /// </summary>
    private static ParsedS7Address ParseAreaAddress(string address)
    {
        if (address.Length < 2)
            throw new FormatException($"地址格式无效: {address}");

        char areaChar = address[0];
        string areaType = areaChar switch
        {
            'M' => "M",
            'I' => "I",
            'Q' => "Q",
            _ => throw new FormatException($"未知区域标识: {areaChar}")
        };

        var result = new ParsedS7Address { AreaType = areaType, DbNumber = 0 };
        string rest = address.Substring(1);

        if (rest.Length == 0)
            throw new FormatException($"地址缺少偏移: {address}");

        char secondChar = rest[0];

        if (secondChar == 'W' || secondChar == 'B' || secondChar == 'D')
        {
            // 形如 MW10 / MB10 / MD10 / IW10
            result.VarType = InferVarType(secondChar);
            string offsetStr = rest.Substring(1);
            if (!int.TryParse(offsetStr, out int byteOffset) || byteOffset < 0)
                throw new FormatException($"字节偏移无效: {address}");
            result.ByteOffset = byteOffset;
        }
        else
        {
            // 形如 M10.0 (位地址) 或 M10 (按位处理, 默认位0)
            result.VarType = S7VarType.Bit;
            var dotParts = rest.Split('.');
            if (!int.TryParse(dotParts[0], out int byteOffset) || byteOffset < 0)
                throw new FormatException($"字节偏移无效: {address}");
            result.ByteOffset = byteOffset;

            if (dotParts.Length >= 2)
            {
                if (!int.TryParse(dotParts[1], out int bit) || bit < 0 || bit > 7)
                    throw new FormatException($"位偏移无效: {address}");
                result.BitOffset = bit;
            }
        }

        return result;
    }

    /// <summary>
    /// 根据后缀字符推断默认变量类型
    /// </summary>
    private static S7VarType InferVarType(char typeChar) => typeChar switch
    {
        'X' => S7VarType.Bit,
        'B' => S7VarType.Byte,
        'W' => S7VarType.Word,
        'D' => S7VarType.Real,   // DBD 默认推断为 Real，用户可在配置中改为 DInt/DWord
        _ => throw new FormatException($"未知类型后缀: {typeChar}")
    };

    /// <summary>
    /// 校验地址合法性。
    /// S7 PLC 不强制字节对齐——同一DB块中混合类型时，
    /// 偏移可以是非4的倍数（例如Int在DBD0，Real在DBD2）。
    /// 仅校验位地址范围和偏移非负。
    /// </summary>
    private static void Validate(ParsedS7Address addr)
    {
        if (addr.ByteOffset < 0)
            throw new FormatException(
                $"字节偏移不能为负数: {addr.OriginalAddress}");

        if (addr.VarType == S7VarType.Bit)
        {
            if (addr.BitOffset < 0 || addr.BitOffset > 7)
                throw new FormatException(
                    $"位偏移必须在0-7之间: {addr.OriginalAddress} (当前位={addr.BitOffset})");
        }
    }
}
