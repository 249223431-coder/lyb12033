using S7TrendMonitor.Models;

namespace S7TrendMonitor.Communication;

/// <summary>
/// 批量读取优化器
/// 将以太网模式下的多个变量按DB号分组，每组限制最大字节数，生成批量读取请求。
/// 对于MPI模式不适用（MPI需逐个读取）。
/// </summary>
public static class BatchReadOptimizer
{
    public static List<List<(int id, ParsedS7Address addr)>> OptimizeBatches(
        List<(int id, ParsedS7Address addr)> variables, int maxBytesPerBatch = 180)
    {
        var batches = new List<List<(int id, ParsedS7Address addr)>>();
        var byDb = variables.GroupBy(v => v.addr.AreaType == "DB" ? v.addr.DbNumber : -1);

        foreach (var group in byDb)
        {
            var currentBatch = new List<(int id, ParsedS7Address addr)>();
            int currentBytes = 0;

            foreach (var item in group.OrderBy(v => v.addr.ByteOffset))
            {
                if (currentBytes + item.addr.ByteSize > maxBytesPerBatch && currentBatch.Count > 0)
                {
                    batches.Add(currentBatch);
                    currentBatch = new List<(int id, ParsedS7Address addr)>();
                    currentBytes = 0;
                }
                currentBatch.Add(item);
                currentBytes += item.addr.ByteSize;
            }

            if (currentBatch.Count > 0)
                batches.Add(currentBatch);
        }

        return batches;
    }
}
