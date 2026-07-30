using S7TrendMonitor.Models;

namespace S7TrendMonitor.Communication;

/// <summary>
/// PLC 通信接口抽象，封装连接管理与变量批量读取。
/// </summary>
/// <remarks>
/// 连接相关方法返回 <see cref="Task{Boolean}"/>：成功返回 true，失败返回 false（不抛异常）。
/// <see cref="ReadVariablesAsync"/> 返回原始读数字典，由调用方（采样服务）负责构建 <see cref="SampleBatch"/>。
/// </remarks>
public interface IPlcCommunication : IDisposable
{
    /// <summary>建立连接。成功返回 true，失败返回 false。</summary>
    Task<bool> ConnectAsync();

    /// <summary>断开连接。</summary>
    void Disconnect();

    /// <summary>当前是否已连接。</summary>
    bool IsConnected { get; }

    /// <summary>
    /// 确保已连接：若已连接直接返回 true，否则尝试连接。
    /// 成功返回 true，失败返回 false（不抛异常）。
    /// </summary>
    Task<bool> EnsureConnectedAsync();

    /// <summary>
    /// 批量读取变量，返回“变量Id -> 数值”字典。
    /// 读取失败的变量以 0.0 记录（不单独抛异常）。
    /// </summary>
    Task<Dictionary<int, double>> ReadVariablesAsync(List<(int id, ParsedS7Address addr)> variables);

    /// <summary>人类可读的连接描述（用于 UI 显示）。</summary>
    string ConnectionDescription { get; }
}
