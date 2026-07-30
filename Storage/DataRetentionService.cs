using S7TrendMonitor.Utils;

namespace S7TrendMonitor.Storage;

/// <summary>
/// 数据保留清理服务，定期删除超过保留时间的采样数据
/// </summary>
public class DataRetentionService
{
    private readonly DatabaseService _dbService;
    private readonly int _retentionHours;

    /// <summary>
    /// 创建数据保留清理服务
    /// </summary>
    /// <param name="dbService">数据库操作服务</param>
    /// <param name="retentionHours">数据保留小时数</param>
    public DataRetentionService(DatabaseService dbService, int retentionHours)
    {
        _dbService = dbService;
        _retentionHours = retentionHours;
    }

    /// <summary>
    /// 清理超过保留时间的采样数据
    /// </summary>
    /// <returns>删除的行数</returns>
    public async Task<int> CleanupAsync()
    {
        var cutoff = DateTimeOffset.Now.ToUnixTimeMilliseconds() - _retentionHours * 3600L * 1000;
        var deletedCount = await _dbService.DeleteSamplesBeforeAsync(cutoff);

        if (deletedCount > 0)
        {
            Logger.Info($"数据保留清理：删除了 {deletedCount} 条过期采样数据（截止时间戳: {cutoff}）");
        }

        return deletedCount;
    }
}
