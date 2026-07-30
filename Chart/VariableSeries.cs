using ScottPlot;
using ScottPlot.DataSources;

namespace S7TrendMonitor.Chart;

/// <summary>
/// 管理单个变量在 ScottPlot 图表中的数据系列。
/// </summary>
/// <remarks>
/// ScottPlot 5 中 <see cref="ScottPlot.Plottables.Scatter.Data"/> 没有公开 setter，
/// 且 <see cref="ScatterSourceDoubleArray"/> 不暴露可写的 Xs/Ys。
/// 因此这里使用 <see cref="ScatterSourceGenericList{T1, T2}"/> 直接包装本地列表：
/// 向 _xs/_ys 追加/移除点后，图线在下一次 Refresh 时即可自动反映，无需重建散点对象。
/// </remarks>
public class VariableSeries
{
    public int VariableId { get; }
    public string DisplayName { get; set; }
    public string ColorHex { get; set; }
    public double ScaleMin { get; set; }
    public double ScaleMax { get; set; }

    private readonly List<double> _xs = new();
    private readonly List<double> _ys = new();       // 显示用 Y（归一化模式下为 0~1，统一模式下为原始值）
    private readonly List<double> _rawYs = new();     // 原始 Y 值（始终保留，供游标/变量表显示真实值）

    /// <summary>最大保留点数，超过后从头部移除最旧数据。</summary>
    private const int MaxPoints = 5000;

    private Plot? _plot;
    private ScatterSourceGenericList<double, double>? _source;
    private ScottPlot.Plottables.Scatter? _scatter;

    /// <summary>底层散点图线对象（创建前为 null）。</summary>
    public ScottPlot.Plottables.Scatter? Scatter => _scatter;

    public VariableSeries(int variableId, string displayName, string colorHex, double scaleMin, double scaleMax)
    {
        VariableId = variableId;
        DisplayName = displayName;
        ColorHex = colorHex;
        ScaleMin = scaleMin;
        ScaleMax = scaleMax;
    }

    /// <summary>添加一个采样点（timestampMs 为 Unix 毫秒时间戳）。</summary>
    public void AddPoint(double timestampMs, double value)
    {
        _xs.Add(timestampMs);
        _rawYs.Add(value);
        _ys.Add(value);

        // 限制最大点数，超过 5000 点时移除旧数据
        if (_xs.Count > MaxPoints)
        {
            _xs.RemoveAt(0);
            _rawYs.RemoveAt(0);
            _ys.RemoveAt(0);
        }
    }

    /// <summary>在指定 Plot 上创建散点图线。内部使用列表数据源，数据与本地列表实时共享。</summary>
    public void CreateScatter(Plot plot)
    {
        _plot = plot ?? throw new ArgumentNullException(nameof(plot));

        // 直接构造列表数据源，确保图线与 _xs/_ys 共享同一引用
        _source = new ScatterSourceGenericList<double, double>(_xs, _ys);
        _scatter = plot.Add.Scatter(_source);
        ApplyStyle();
    }

    /// <summary>应用/刷新图线样式（颜色、线宽、标记、图例文本）。</summary>
    public void ApplyStyle()
    {
        if (_scatter is null) return;

        var color = ColorHex.StartsWith("#", StringComparison.Ordinal)
            ? ScottPlot.Color.FromHex(ColorHex)
            : ScottPlot.Color.FromHex("#" + ColorHex);

        _scatter.Color = color;
        _scatter.LineWidth = 1.5f;
        _scatter.MarkerSize = 0;
        _scatter.LegendText = DisplayName;
    }

    /// <summary>
    /// 将最新数据刷新到图线。由于数据源直接包装本地列表，数据已实时共享，
    /// 此方法主要用于在外部属性（颜色/名称等）变化后重新应用样式。
    /// </summary>
    public void UpdateScatter()
    {
        ApplyStyle();
    }

    /// <summary>从 Plot 中移除本图线（用于删除变量时清理）。</summary>
    public void RemoveFromPlot()
    {
        if (_scatter is not null && _plot is not null)
        {
            _plot.Remove(_scatter);
        }

        _scatter = null;
        _source = null;
        _plot = null;
    }

    /// <summary>清空所有缓存数据。</summary>
    public void Clear()
    {
        _xs.Clear();
        _ys.Clear();
        _rawYs.Clear();
    }

    /// <summary>
    /// 根据归一化模式重建显示用 Y 列表。
    /// 归一化模式下将原始值映射到 [0,1] 区间；统一模式下直接复制原始值。
    /// </summary>
    /// <param name="normalize">是否归一化</param>
    /// <param name="normMin">归一化下限（原始值域）</param>
    /// <param name="normMax">归一化上限（原始值域）</param>
    public void UpdateDisplayValues(bool normalize, double normMin, double normMax)
    {
        _ys.Clear();
        if (normalize)
        {
            double range = normMax - normMin;
            if (Math.Abs(range) < 1e-9) range = 1; // 防止除零
            foreach (var raw in _rawYs)
            {
                _ys.Add((raw - normMin) / range);
            }
        }
        else
        {
            _ys.AddRange(_rawYs);
        }
    }

    public int Count => _xs.Count;

    /// <summary>只读 X 序列（Unix 毫秒），供趋势服务计算可见窗口 Y 范围。</summary>
    public IReadOnlyList<double> Xs => _xs;

    /// <summary>只读 Y 序列（显示值），供趋势服务计算可见窗口 Y 范围。</summary>
    public IReadOnlyList<double> Ys => _ys;

    /// <summary>只读原始 Y 序列（真实值），供游标/变量表显示实际数值。</summary>
    public IReadOnlyList<double> RawYs => _rawYs;
}
