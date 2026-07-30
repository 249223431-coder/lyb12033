using System.Collections.Concurrent;
using System.Linq;
using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.TickGenerators;
using S7TrendMonitor.Models;

namespace S7TrendMonitor.Chart;

/// <summary>
/// 单条游标线的信息：包含竖线、每条趋势曲线上的标记点和数值标签。
/// </summary>
public class CursorInfo
{
    public int Id { get; set; }
    public VerticalLine Line { get; set; } = null!;
    public string ColorHex { get; set; } = "";

    /// <summary>每条趋势曲线对应的标记点，Key=VariableId</summary>
    public Dictionary<int, Marker> Markers { get; } = new();

    /// <summary>每条趋势曲线对应的数值文本标签，Key=VariableId</summary>
    public Dictionary<int, Text> Labels { get; } = new();
}

/// <summary>
/// 游标位置数据，传递给外部回调。
/// </summary>
public class CursorData
{
    public int Id { get; set; }
    public double TimestampMs { get; set; }
    public string ColorHex { get; set; } = "";
    public List<(int varId, string name, double value)> Values { get; set; } = new();
}

/// <summary>
/// 趋势图渲染服务：封装 ScottPlot 的 <see cref="Plot"/> 对象，管理多个 <see cref="VariableSeries"/>。
/// </summary>
/// <remarks>
/// X 轴为 Unix 毫秒时间戳，通过自定义刻度格式化器显示为 HH:mm:ss。
/// 支持多条可拖动游标线，每条游标在图表上显示各趋势曲线的交点标记和数值。
/// </remarks>
public class TrendChartService
{
    private readonly Plot _plot;
    private readonly Dictionary<int, VariableSeries> _series = new();

    private int _timeWindowSeconds = 300;
    private bool _pauseRendering;

    // ===== 归一化显示模式 =====
    // 开启后每条曲线按各自数据范围缩放到 0~1，解决多变量量程差异过大的显示问题
    private bool _normalizeMode;

    // ===== 多游标线 =====
    private readonly List<CursorInfo> _cursors = new();
    private int _nextCursorId = 1;

    /// <summary>游标颜色池，循环使用。</summary>
    private static readonly string[] CursorColors =
    {
        "#E74C3C", // 红
        "#3498DB", // 蓝
        "#2ECC71", // 绿
        "#F39C12", // 橙
        "#9B59B6", // 紫
        "#1ABC9C", // 青
    };

    /// <summary>
    /// 游标位置变化回调。参数为所有可见游标的数据列表。
    /// </summary>
    public Action<List<CursorData>>? CursorMovedCallback { get; set; }

    /// <summary>
    /// 显示刷新回调。RefreshChart 在完成坐标轴配置后调用它以触发实际重绘。
    /// </summary>
    public Action? DisplayRefreshCallback { get; set; }

    public TrendChartService(Plot plot)
    {
        _plot = plot ?? throw new ArgumentNullException(nameof(plot));
        ConfigureChart();
    }

    private void ConfigureChart()
    {
        // X 轴：Unix 毫秒 -> HH:mm:ss
        var tickGen = new NumericAutomatic();
        tickGen.LabelFormatter = FormatTimestamp;
        _plot.Axes.Bottom.TickGenerator = tickGen;
        _plot.Axes.Bottom.Label.Text = "时间";

        _plot.Axes.Left.Label.Text = "数值";

        // 图表标题为空，显示图例
        _plot.Title(string.Empty);
        _plot.ShowLegend();

        // 设置中文字体，解决图例中文名称乱码（显示方框）问题
        // Font.Automatic() 只在调用时检测已有文本，后续添加的图例项不会继承中文字体
        // 改用 Font.Set() 显式指定全局字体，确保所有文本（含后续添加的图例项）都使用中文字体
        _plot.Font.Set("Microsoft YaHei UI");
        _plot.Legend.FontName = "Microsoft YaHei UI";
        _plot.Legend.FontSize = 10;
    }

    private static string FormatTimestamp(double ms)
    {
        if (double.IsNaN(ms) || ms < 0) return string.Empty;
        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds((long)ms).LocalDateTime.ToString("HH:mm:ss");
        }
        catch
        {
            return ms.ToString("0");
        }
    }

    /// <summary>根据变量配置列表更新图线（新增/删除/更新）。</summary>
    public void UpdateVariableSeries(List<VariableConfig> configs)
    {
        if (configs is null) return;

        var enabled = configs
            .Where(c => c.IsEnabled)
            .OrderBy(c => c.SortOrder)
            .ToList();

        var wantedIds = enabled.Select(c => c.Id).ToHashSet();

        // 删除不再启用的变量
        var toRemove = _series.Keys.Where(k => !wantedIds.Contains(k)).ToList();
        foreach (var id in toRemove)
        {
            _series[id].RemoveFromPlot();
            _series[id].Clear();
            _series.Remove(id);
        }

        // 新增或更新
        foreach (var cfg in enabled)
        {
            if (_series.TryGetValue(cfg.Id, out var existing))
            {
                existing.DisplayName = cfg.DisplayName;
                existing.ColorHex = cfg.ColorHex;
                existing.ScaleMin = cfg.ScaleMin;
                existing.ScaleMax = cfg.ScaleMax;
                existing.UpdateScatter();
            }
            else
            {
                var series = new VariableSeries(cfg.Id, cfg.DisplayName, cfg.ColorHex, cfg.ScaleMin, cfg.ScaleMax);
                series.CreateScatter(_plot);
                _series[cfg.Id] = series;
            }
        }

        // 同步更新所有游标的标记点
        foreach (var cursor in _cursors)
        {
            // 移除已删除变量的标记和标签
            var idsToRemove = cursor.Markers.Keys.Where(k => !wantedIds.Contains(k)).ToList();
            foreach (var id in idsToRemove)
            {
                _plot.Remove(cursor.Markers[id]);
                _plot.Remove(cursor.Labels[id]);
                cursor.Markers.Remove(id);
                cursor.Labels.Remove(id);
            }

            // 为新变量创建标记和标签
            foreach (var cfg in enabled)
            {
                if (!cursor.Markers.ContainsKey(cfg.Id) && _series.TryGetValue(cfg.Id, out var series))
                {
                    CreateMarkerForCursor(cursor, cfg.Id, series);
                }
            }

            UpdateCursorMarkers(cursor);
        }
    }

    /// <summary>从队列消费采样数据，添加到对应的 VariableSeries。</summary>
    public void ConsumeUpdates(ConcurrentQueue<SampleBatch> queue)
    {
        if (queue is null) return;

        while (queue.TryDequeue(out var batch))
        {
            double ts = batch.TimestampMs;
            foreach (var kv in batch.Values)
            {
                if (_series.TryGetValue(kv.Key, out var series))
                {
                    series.AddPoint(ts, kv.Value);
                }
            }
        }
    }

    /// <summary>刷新图表显示：设置 X 轴时间窗口、Y 轴自动缩放，并触发重绘。</summary>
    public void RefreshChart()
    {
        if (_pauseRendering)
        {
            if (_normalizeMode)
                UpdateNormalizedDisplay(minMs: 0, maxMs: 0, paused: true);
            else
                foreach (var s in _series.Values)
                    s.UpdateDisplayValues(false, 0, 1);
            UpdateAllCursorMarkers();
            DisplayRefreshCallback?.Invoke();
            return;
        }

        // 计算可见 X 窗口：以最新数据点为右端
        double maxMs = 0;
        foreach (var s in _series.Values)
        {
            if (s.Count > 0)
            {
                double last = s.Xs[s.Xs.Count - 1];
                if (last > maxMs) maxMs = last;
            }
        }

        if (maxMs == 0)
        {
            maxMs = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }

        double minMs = maxMs - _timeWindowSeconds * 1000.0;

        _plot.Axes.SetLimitsX(minMs, maxMs);

        if (_normalizeMode)
        {
            // 归一化模式：每条曲线按各自范围缩放到 [0,1]，Y 轴固定为百分比
            UpdateNormalizedDisplay(minMs, maxMs, paused: false);
            _plot.Axes.SetLimitsY(-0.05, 1.05);
        }
        else
        {
            // 统一模式：恢复原始值，按数据范围统一设置 Y 轴
            foreach (var s in _series.Values)
                s.UpdateDisplayValues(false, 0, 1);

            if (!TrySetYLimitsFromData(minMs, maxMs))
            {
                if (!TrySetYLimitsFromScale())
                {
                    _plot.Axes.AutoScaleY();
                }
            }
        }

        UpdateAllCursorMarkers();
        DisplayRefreshCallback?.Invoke();
    }

    /// <summary>
    /// 归一化模式下，为每条曲线计算可见数据范围并更新显示值。
    /// 优先使用用户配置的 ScaleMin/ScaleMax（非默认值时），否则从可见数据自动计算。
    /// </summary>
    private void UpdateNormalizedDisplay(double minMs, double maxMs, bool paused)
    {
        foreach (var kvp in _series)
        {
            var series = kvp.Value;
            if (series.Count == 0) continue;

            double normMin, normMax;

            // 检查是否设置了非默认量程（ScaleMin≠0 或 ScaleMax≠100）
            bool useManual = series.ScaleMin != 0 || series.ScaleMax != 100;
            if (useManual && series.ScaleMax > series.ScaleMin)
            {
                normMin = series.ScaleMin;
                normMax = series.ScaleMax;
            }
            else
            {
                // 自动计算可见数据范围
                double yMin = double.MaxValue, yMax = double.MinValue;
                var xs = series.Xs;
                var rawYs = series.RawYs;

                if (paused)
                {
                    // 暂停时使用全部数据
                    for (int i = 0; i < rawYs.Count; i++)
                    {
                        double y = rawYs[i];
                        if (y < yMin) yMin = y;
                        if (y > yMax) yMax = y;
                    }
                }
                else
                {
                    for (int i = 0; i < xs.Count; i++)
                    {
                        if (xs[i] < minMs) continue;
                        if (xs[i] > maxMs) break;
                        double y = rawYs[i];
                        if (y < yMin) yMin = y;
                        if (y > yMax) yMax = y;
                    }
                }

                if (yMin > yMax) { yMin = 0; yMax = 1; }

                double span = yMax - yMin;
                double pad = span <= 0
                    ? Math.Max(Math.Abs(yMax) * 0.1, 1.0)
                    : span * 0.1;
                normMin = yMin - pad;
                normMax = yMax + pad;
            }

            series.UpdateDisplayValues(true, normMin, normMax);
        }
    }

    private bool TrySetYLimitsFromData(double minMs, double maxMs)
    {
        double yMin = double.MaxValue;
        double yMax = double.MinValue;
        bool found = false;

        foreach (var s in _series.Values)
        {
            var xs = s.Xs;
            var ys = s.Ys;
            for (int i = 0; i < xs.Count; i++)
            {
                double x = xs[i];
                if (x < minMs) continue;
                if (x > maxMs) break;

                double y = ys[i];
                if (y < yMin) yMin = y;
                if (y > yMax) yMax = y;
                found = true;
            }
        }

        if (!found) return false;

        double span = yMax - yMin;
        double pad = span <= 0
            ? Math.Max(Math.Abs(yMax) * 0.1, 1.0)
            : span * 0.1;

        _plot.Axes.SetLimitsY(yMin - pad, yMax + pad);
        return true;
    }

    private bool TrySetYLimitsFromScale()
    {
        if (_series.Count == 0) return false;

        double yMin = double.MaxValue;
        double yMax = double.MinValue;
        foreach (var s in _series.Values)
        {
            if (s.ScaleMin < yMin) yMin = s.ScaleMin;
            if (s.ScaleMax > yMax) yMax = s.ScaleMax;
        }

        if (yMin > yMax) return false;

        _plot.Axes.SetLimitsY(yMin, yMax);
        return true;
    }

    // ===== 多游标线功能 =====

    /// <summary>添加一条新游标线，返回游标ID。</summary>
    public int AddCursor()
    {
        int id = _nextCursorId++;
        string color = CursorColors[(_cursors.Count) % CursorColors.Length];

        var line = _plot.Add.VerticalLine(0);
        line.Color = ScottPlot.Color.FromHex(color);
        line.LineWidth = 2;
        line.LinePattern = ScottPlot.LinePattern.Dashed;
        line.IsDraggable = true;
        line.IsVisible = true;
        line.LabelText = $"游标{id}";
        line.LabelFontSize = 10;
        line.LabelBackgroundColor = ScottPlot.Color.FromHex(color);
        line.LabelFontColor = ScottPlot.Colors.White;

        // 放在可见窗口中偏左位置，避免多条游标重叠
        var range = _plot.Axes.GetLimits().Rect;
        double spacing = range.Width * 0.05;
        line.X = range.HorizontalCenter - (_cursors.Count * spacing);

        var cursor = new CursorInfo
        {
            Id = id,
            Line = line,
            ColorHex = color,
        };

        // 为每条趋势线创建标记和标签
        foreach (var kvp in _series)
        {
            CreateMarkerForCursor(cursor, kvp.Key, kvp.Value);
        }

        _cursors.Add(cursor);
        UpdateCursorMarkers(cursor);
        NotifyCursorMoved();
        DisplayRefreshCallback?.Invoke();

        return id;
    }

    /// <summary>移除指定游标。</summary>
    public void RemoveCursor(int id)
    {
        var cursor = _cursors.FirstOrDefault(c => c.Id == id);
        if (cursor == null) return;

        _plot.Remove(cursor.Line);
        foreach (var marker in cursor.Markers.Values)
            _plot.Remove(marker);
        foreach (var label in cursor.Labels.Values)
            _plot.Remove(label);

        _cursors.Remove(cursor);
        NotifyCursorMoved();
        DisplayRefreshCallback?.Invoke();
    }

    /// <summary>移除所有游标。</summary>
    public void RemoveAllCursors()
    {
        foreach (var cursor in _cursors.ToList())
        {
            _plot.Remove(cursor.Line);
            foreach (var marker in cursor.Markers.Values)
                _plot.Remove(marker);
            foreach (var label in cursor.Labels.Values)
                _plot.Remove(label);
        }
        _cursors.Clear();
        NotifyCursorMoved();
        DisplayRefreshCallback?.Invoke();
    }

    /// <summary>可见游标数量。</summary>
    public int CursorCount => _cursors.Count;

    /// <summary>是否有游标。</summary>
    public bool HasCursors => _cursors.Count > 0;

    /// <summary>获取所有游标的数据。</summary>
    public List<CursorData> GetAllCursorData()
    {
        var result = new List<CursorData>();
        foreach (var cursor in _cursors)
        {
            var data = new CursorData
            {
                Id = cursor.Id,
                TimestampMs = cursor.Line.X,
                ColorHex = cursor.ColorHex,
            };

            foreach (var kvp in _series)
            {
                if (kvp.Value.Count == 0) continue;
                double value = FindNearestRawValue(kvp.Value, cursor.Line.X);
                data.Values.Add((kvp.Key, kvp.Value.DisplayName, value));
            }

            result.Add(data);
        }
        return result;
    }

    /// <summary>获取各变量的最新值（无游标时使用）。</summary>
    public Dictionary<int, (string name, double value)> GetLatestValues()
    {
        var result = new Dictionary<int, (string name, double value)>();
        foreach (var kvp in _series)
        {
            if (kvp.Value.Count == 0) continue;
            result[kvp.Key] = (kvp.Value.DisplayName, kvp.Value.RawYs[kvp.Value.Count - 1]);
        }
        return result;
    }

    /// <summary>设置指定游标的位置。</summary>
    public void SetCursorPosition(int cursorId, double xMs)
    {
        var cursor = _cursors.FirstOrDefault(c => c.Id == cursorId);
        if (cursor == null) return;

        cursor.Line.X = xMs;
        UpdateCursorMarkers(cursor);
        NotifyCursorMoved();
    }

    /// <summary>根据坐标矩形找到游标ID（用于拖拽命中检测）。</summary>
    public int? FindCursorAtRect(CoordinateRect rect)
    {
        for (int i = _cursors.Count - 1; i >= 0; i--)
        {
            if (_cursors[i].Line.IsUnderMouse(rect))
                return _cursors[i].Id;
        }
        return null;
    }

    /// <summary>创建游标在指定趋势曲线上的标记点和数值标签。</summary>
    private void CreateMarkerForCursor(CursorInfo cursor, int variableId, VariableSeries series)
    {
        // 标记点
        var marker = _plot.Add.Marker(0, 0);
        marker.MarkerColor = ScottPlot.Color.FromHex(series.ColorHex);
        marker.MarkerSize = 7;
        marker.MarkerShape = MarkerShape.FilledCircle;
        marker.IsVisible = false;

        // 数值文本标签
        var label = _plot.Add.Text("", 0, 0);
        label.LabelText = "";
        label.LabelFontColor = ScottPlot.Color.FromHex(series.ColorHex);
        label.LabelFontSize = 9;
        label.LabelBold = true;
        label.LabelBackgroundColor = ScottPlot.Colors.White;
        label.LabelStyle.Alignment = Alignment.LowerLeft;
        label.IsVisible = false;

        cursor.Markers[variableId] = marker;
        cursor.Labels[variableId] = label;
    }

    /// <summary>更新指定游标的所有标记点位置和标签。</summary>
    private void UpdateCursorMarkers(CursorInfo cursor)
    {
        // 获取 Y 轴范围用于计算标签偏移
        var limits = _plot.Axes.GetLimits();
        double yRange = limits.Rect.Height;
        double yOffset = yRange * 0.02; // 2% of Y range

        foreach (var kvp in _series)
        {
            int varId = kvp.Key;
            var series = kvp.Value;

            if (!cursor.Markers.TryGetValue(varId, out var marker))
            {
                CreateMarkerForCursor(cursor, varId, series);
                marker = cursor.Markers[varId];
            }

            var label = cursor.Labels[varId];

            if (series.Count == 0)
            {
                marker.IsVisible = false;
                label.IsVisible = false;
                continue;
            }

            // 标记点位置使用显示值（归一化模式下为 0~1），标签文本使用真实值
            double displayValue = FindNearestDisplayValue(series, cursor.Line.X);
            double rawValue = FindNearestRawValue(series, cursor.Line.X);

            // 更新标记点位置
            marker.Location = new Coordinates(cursor.Line.X, displayValue);
            marker.IsVisible = true;

            // 更新文本标签位置（略高于标记点）
            label.Location = new Coordinates(cursor.Line.X, displayValue + yOffset);
            label.LabelText = rawValue.ToString("F2");
            label.IsVisible = true;
        }

        cursor.Line.LabelText = FormatTimestamp(cursor.Line.X);
    }

    /// <summary>更新所有游标的标记点。</summary>
    private void UpdateAllCursorMarkers()
    {
        foreach (var cursor in _cursors)
        {
            UpdateCursorMarkers(cursor);
        }
    }

    /// <summary>通知外部游标位置已变化。</summary>
    private void NotifyCursorMoved()
    {
        var data = GetAllCursorData();
        CursorMovedCallback?.Invoke(data);
    }

    /// <summary>在指定 series 中查找时间最接近 targetX 的原始 Y 值（供游标标签/变量表显示）。</summary>
    private static double FindNearestRawValue(VariableSeries series, double targetX)
    {
        return FindNearest(series.Xs, series.RawYs, targetX);
    }

    /// <summary>在指定 series 中查找时间最接近 targetX 的显示 Y 值（供标记点定位）。</summary>
    private static double FindNearestDisplayValue(VariableSeries series, double targetX)
    {
        return FindNearest(series.Xs, series.Ys, targetX);
    }

    /// <summary>通用二分查找：在 xs 中找到最接近 targetX 的索引，返回对应 ys 的值。</summary>
    private static double FindNearest(IReadOnlyList<double> xs, IReadOnlyList<double> ys, double targetX)
    {
        int lo = 0, hi = xs.Count - 1;

        if (targetX <= xs[lo]) return ys[lo];
        if (targetX >= xs[hi]) return ys[hi];

        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (xs[mid] < targetX)
                lo = mid + 1;
            else
                hi = mid;
        }

        if (lo > 0)
        {
            double diffAfter = xs[lo] - targetX;
            double diffBefore = targetX - xs[lo - 1];
            return diffBefore <= diffAfter ? ys[lo - 1] : ys[lo];
        }

        return ys[lo];
    }

    /// <summary>清除所有图线数据。</summary>
    public void ClearAll()
    {
        // 先移除所有游标标记
        foreach (var cursor in _cursors)
        {
            foreach (var marker in cursor.Markers.Values)
                _plot.Remove(marker);
            foreach (var label in cursor.Labels.Values)
                _plot.Remove(label);
            cursor.Markers.Clear();
            cursor.Labels.Clear();
        }

        foreach (var s in _series.Values)
        {
            s.RemoveFromPlot();
            s.Clear();
        }

        _series.Clear();

        UpdateAllCursorMarkers();
    }

    /// <summary>设置 X 轴显示窗口（秒）。</summary>
    public void SetTimeWindow(int seconds)
    {
        _timeWindowSeconds = Math.Max(1, seconds);
    }

    /// <summary>暂停/恢复渲染。</summary>
    public void PauseRendering(bool pause)
    {
        _pauseRendering = pause;
    }

    /// <summary>
    /// 设置归一化显示模式。
    /// 开启后每条曲线按各自数据范围缩放到 0~100%，所有曲线均能清晰显示。
    /// 游标标记和变量表仍显示真实值。
    /// </summary>
    public void SetNormalizeMode(bool enabled)
    {
        _normalizeMode = enabled;

        if (_normalizeMode)
        {
            _plot.Axes.Left.Label.Text = "归一化值";
            var yTickGen = new NumericAutomatic();
            yTickGen.LabelFormatter = v => $"{v * 100:F0}%";
            _plot.Axes.Left.TickGenerator = yTickGen;
        }
        else
        {
            _plot.Axes.Left.Label.Text = "数值";
            _plot.Axes.Left.TickGenerator = new NumericAutomatic();
        }
    }

    /// <summary>当前是否处于归一化模式。</summary>
    public bool IsNormalized => _normalizeMode;
}
