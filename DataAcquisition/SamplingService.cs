using System.Collections.Concurrent;
using S7TrendMonitor.Communication;
using S7TrendMonitor.Models;
using S7TrendMonitor.Storage;
using S7TrendMonitor.Utils;

namespace S7TrendMonitor.DataAcquisition;

/// <summary>
/// 采样调度服务：在后台循环中按固定间隔读取 PLC 变量，
/// 构建采样批次后异步写入数据库，并通过 <see cref="UpdateQueue"/> 供 UI 消费。
/// </summary>
public class SamplingService
{
    private readonly IPlcCommunication _communication;
    private readonly DatabaseService _database;
    private readonly int _samplingIntervalMs;

    /// <summary>要采样的变量列表（id + 解析后的 S7 地址）。</summary>
    private List<(int id, ParsedS7Address addr)> _variables = new();

    /// <summary>供 UI 消费的采样数据队列。</summary>
    public ConcurrentQueue<SampleBatch> UpdateQueue { get; } = new();

    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private volatile bool _running;

    /// <summary>采样循环是否正在运行。</summary>
    public bool IsRunning => _running;

    private bool _lastConnected;

    /// <summary>连接状态变化通知（参数为当前是否已连接）。</summary>
    public event Action<bool>? OnConnectionStateChanged;

    /// <summary>采样过程出错通知（参数为错误描述）。</summary>
    public event Action<string>? OnError;

    public SamplingService(IPlcCommunication communication, DatabaseService database, int samplingIntervalMs)
    {
        _communication = communication ?? throw new ArgumentNullException(nameof(communication));
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _samplingIntervalMs = samplingIntervalMs > 0
            ? samplingIntervalMs
            : throw new ArgumentOutOfRangeException(nameof(samplingIntervalMs), "采样间隔必须大于 0");
    }

    /// <summary>更新采样变量列表。</summary>
    public void UpdateVariables(List<(int id, ParsedS7Address addr)> variables)
    {
        _variables = variables ?? new List<(int id, ParsedS7Address addr)>();
    }

    /// <summary>启动采样循环（后台 Task）。</summary>
    public void Start()
    {
        if (_running) return;

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _running = true;
        _loopTask = Task.Run(() => SamplingLoopAsync(_cts.Token));
    }

    /// <summary>停止采样循环（立即返回，循环在下一个 tick 退出）。</summary>
    public void Stop()
    {
        if (!_running) return;

        _running = false;
        try
        {
            _cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // 忽略：CTS 已释放
        }
    }

    /// <summary>停止采样循环并等待循环任务结束。</summary>
    public async Task StopAsync()
    {
        Stop();

        if (_loopTask is not null)
        {
            try
            {
                await _loopTask.ConfigureAwait(false);
            }
            catch
            {
                // 循环内部的异常已在循环中处理，此处忽略
            }
        }

        _cts?.Dispose();
        _cts = null;
        _loopTask = null;
    }

    private async Task SamplingLoopAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_samplingIntervalMs));

        try
        {
            while (!token.IsCancellationRequested)
            {
                // 1. 等待 timer tick
                try
                {
                    await timer.WaitForNextTickAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                // 捕获当前变量列表快照，避免与 UpdateVariables 并发冲突
                var variables = _variables;
                if (variables.Count == 0)
                {
                    continue;
                }

                try
                {
                    // 2. 确保连接（返回 false 表示连接失败，不抛异常）
                    bool connected = await _communication.EnsureConnectedAsync().ConfigureAwait(false);
                    NotifyConnectionState(connected);

                    if (!connected)
                    {
                        // 连接失败：记录日志，继续下一轮
                        string desc = _communication.ConnectionDescription;
                        Logger.Warning($"PLC连接失败: {desc}");
                        OnError?.Invoke($"PLC连接失败: {desc}");
                        continue;
                    }

                    // 3. 读取数据（返回 变量Id -> 数值 字典）
                    var values = await _communication.ReadVariablesAsync(variables).ConfigureAwait(false);

                    // 4. 构建采样批次（统一打时间戳）
                    var batch = new SampleBatch
                    {
                        TimestampMs = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                        Values = values ?? new Dictionary<int, double>(),
                        ReadErrors = new Dictionary<int, bool>()
                    };

                    // 5. 异步写入数据库（不阻塞采样循环）
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _database.InsertSamplesAsync(batch).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("写入数据库失败", ex);
                        }
                    });

                    // 6. 入队供 UI 消费
                    UpdateQueue.Enqueue(batch);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // 7. 错误处理：连接失败或读取异常时记录日志，继续下一轮
                    Logger.Error("采样循环异常", ex);
                    NotifyConnectionState(_communication.IsConnected);
                    OnError?.Invoke(ex.Message);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常退出
        }
        catch (Exception ex)
        {
            Logger.Error("采样循环发生未预期异常", ex);
            OnError?.Invoke(ex.Message);
        }
        finally
        {
            _running = false;
            NotifyConnectionState(false);
        }
    }

    private void NotifyConnectionState(bool connected)
    {
        if (_lastConnected != connected)
        {
            _lastConnected = connected;
            OnConnectionStateChanged?.Invoke(connected);
        }
    }
}
