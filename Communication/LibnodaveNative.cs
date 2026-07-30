using System.Runtime.InteropServices;

namespace S7TrendMonitor.Communication;

/// <summary>
/// libnodave 原生库 P/Invoke 声明
/// 用于通过MPI协议（PC Adapter USB / CP5611）与S7 PLC通信
/// </summary>
internal static class LibnodaveNative
{
    private const string DllName = "libnodave.dll";

    // ===== 协议常量 =====

    /// <summary>MPI协议</summary>
    public const int daveProtoMPI = 10;

    // ===== 串口波特率常量 =====

    public const int daveSpeed9k6 = 0;
    public const int daveSpeed19k2 = 1;
    public const int daveSpeed187k5 = 2;

    // ===== 区域常量 =====

    /// <summary>DB块区域</summary>
    public const int daveDB = 0x84;

    /// <summary>输入区(I)</summary>
    public const int daveInputs = 0x81;

    /// <summary>输出区(Q)</summary>
    public const int daveOutputs = 0x82;

    /// <summary>标志位区(M)</summary>
    public const int daveFlags = 0x83;

    // ===== 串口文件描述符结构 =====

    /// <summary>
    /// libnodave 串口文件描述符结构
    /// rfd/wfd 分别为读/写句柄（PC Adapter USB 时两者相同）
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DaveOSSerialType
    {
        public int rfd;
        public int wfd;
    }

    // ===== 适配器/接口管理函数 =====

    /// <summary>
    /// 打开串口（用于 PC Adapter USB）
    /// </summary>
    /// <param name="port">串口名，如 "COM3"</param>
    /// <param name="baud">波特率字符串，如 "187500"</param>
    /// <param name="parity">校验位: 0=无, 'E'=偶, 'O'=奇</param>
    /// <returns>串口文件描述符结构</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true, CharSet = CharSet.Ansi)]
    public static extern DaveOSSerialType setPort(string port, string baud, int parity);

    /// <summary>
    /// 打开 S7online 访问点（用于 CP5611 卡）
    /// </summary>
    /// <param name="accessPoint">访问点名称，通常为 "S7ONLINE"</param>
    /// <param name="pg">PG槽位，一般传0</param>
    /// <returns>文件描述符句柄</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true, CharSet = CharSet.Ansi)]
    public static extern int openS7online(string accessPoint, int pg);

    /// <summary>
    /// 关闭 S7online 访问点
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    public static extern int closeS7online(int handle);

    /// <summary>
    /// 创建 daveInterface 接口对象
    /// </summary>
    /// <param name="fds">串口文件描述符</param>
    /// <param name="name">接口名称（仅用于标识）</param>
    /// <param name="localMPI">本地MPI地址（通常0）</param>
    /// <param name="protocol">协议，如 daveProtoMPI</param>
    /// <param name="speed">波特率常量，如 daveSpeed187k5</param>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true, CharSet = CharSet.Ansi)]
    public static extern IntPtr daveNewInterface(DaveOSSerialType fds, string name, int localMPI, int protocol, int speed);

    /// <summary>
    /// 断开适配器连接
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    public static extern int daveDisconnectAdapter(IntPtr di);

    /// <summary>
    /// 释放适配器/接口对象
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    public static extern void daveFreeAdapter(IntPtr di);

    // ===== 连接管理函数 =====

    /// <summary>
    /// 创建到PLC的连接对象
    /// </summary>
    /// <param name="di">daveInterface 句柄</param>
    /// <param name="mpi">PLC的MPI地址</param>
    /// <param name="rack">机架号</param>
    /// <param name="slot">槽号</param>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    public static extern IntPtr daveNewConnection(IntPtr di, int mpi, int rack, int slot);

    /// <summary>
    /// 连接到PLC，返回0表示成功
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    public static extern int daveConnectPLC(IntPtr dc);

    /// <summary>
    /// 断开PLC连接
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    public static extern int daveDisconnectPLC(IntPtr dc);

    /// <summary>
    /// 释放连接对象
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    public static extern void daveFree(IntPtr dc);

    // ===== 数据读取函数 =====

    /// <summary>
    /// 从PLC读取字节
    /// </summary>
    /// <param name="dc">daveConnection 句柄</param>
    /// <param name="area">区域常量(daveDB/daveInputs/daveOutputs/daveFlags)</param>
    /// <param name="dbNum">DB块号(非DB区域传0)</param>
    /// <param name="start">起始字节偏移</param>
    /// <param name="len">读取长度</param>
    /// <param name="buffer">接收缓冲区</param>
    /// <returns>0表示成功</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    public static extern int daveReadBytes(IntPtr dc, int area, int dbNum, int start, int len, byte[] buffer);
}
