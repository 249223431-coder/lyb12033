using ScottPlot.WinForms;
using S7TrendMonitor.Chart;
using S7TrendMonitor.Communication;
using S7TrendMonitor.Config;
using S7TrendMonitor.DataAcquisition;
using S7TrendMonitor.Models;
using S7TrendMonitor.Storage;
using S7TrendMonitor.Utils;

namespace S7TrendMonitor.Forms;

public partial class MainForm : Form
{
    private readonly ConfigManager _configManager = new();
    private ConnectionConfig _connectionConfig = new();
    private AppSettings _appSettings = new();

    private DatabaseService? _database;
    private DatabaseInitializer? _dbInit;
    private DataRetentionService? _retentionService;

    private IPlcCommunication? _plcService;
    private SamplingService? _samplingService;
    private TrendChartService? _chartService;

    private readonly FormsPlot _formsPlot = new();
    private readonly ListView _lvVariables = new();
    private readonly System.Windows.Forms.Timer _renderTimer = new();
    private readonly System.Windows.Forms.Timer _retentionTimer = new();

    private readonly ToolStrip _toolStrip = new();
    private readonly StatusStrip _statusStrip = new();

    private readonly ToolStripStatusLabel _lblStatusConn = new();
    private readonly ToolStripStatusLabel _lblStatusSampling = new();
    private readonly ToolStripStatusLabel _lblStatusVars = new();
    private readonly ToolStripStatusLabel _lblStatusError = new();

    private List<VariableConfig> _variables = new();
    private bool _chartPaused;

    // 游标拖拽状态
    private int? _draggingCursorId;
    private readonly Label _lblCursorInfo = new();

    // 变量表更新频率控制
    private int _tableUpdateCounter;

    // 变量表列索引常量
    private const int ColEnabled = 0;
    private const int ColAddress = 1;
    private const int ColName = 2;
    private const int ColType = 3;
    private const int ColScaleMin = 4;
    private const int ColScaleMax = 5;
    private const int ColColor = 6;
    private const int ColCurrentValue = 7;
    private const int ColCursorStart = 8;

    public MainForm()
    {
        InitializeComponent();
        Load += MainForm_Load;
        FormClosing += MainForm_FormClosing;
    }

    private void InitializeComponent()
    {
        Text = "S7趋势监控工具 - 西门子S7-300/400";
        ClientSize = new Size(1200, 750);
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;

        // 设置窗口图标（从嵌入资源加载）
        try
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream("S7TrendMonitor.app.ico");
            if (stream != null)
                Icon = new Icon(stream);
        }
        catch { /* 图标加载失败不影响运行 */ }

        // === 工具栏 ===
        _toolStrip.ImageScalingSize = new Size(20, 20);
        _toolStrip.Dock = DockStyle.Top;

        var btnConnection = new ToolStripButton("连接设置") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        btnConnection.Click += (s, e) => OpenConnectionForm();
        _toolStrip.Items.Add(btnConnection);

        _toolStrip.Items.Add(new ToolStripSeparator());

        var btnAddVar = new ToolStripButton("添加变量") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        btnAddVar.Click += (s, e) => AddVariable();
        _toolStrip.Items.Add(btnAddVar);

        var btnEditVar = new ToolStripButton("编辑变量") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        btnEditVar.Click += (s, e) => EditVariable();
        _toolStrip.Items.Add(btnEditVar);

        var btnDelVar = new ToolStripButton("删除变量") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        btnDelVar.Click += (s, e) => DeleteVariable();
        _toolStrip.Items.Add(btnDelVar);

        _toolStrip.Items.Add(new ToolStripSeparator());

        var btnStart = new ToolStripButton("开始采样") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        btnStart.Click += (s, e) => StartSampling();
        _toolStrip.Items.Add(btnStart);

        var btnStop = new ToolStripButton("停止采样") { DisplayStyle = ToolStripItemDisplayStyle.Text, Enabled = false };
        btnStop.Click += (s, e) => StopSampling();
        _toolStrip.Items.Add(btnStop);

        var btnPause = new ToolStripButton("暂停图表") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        btnPause.Click += (s, e) => TogglePauseChart();
        _toolStrip.Items.Add(btnPause);

        var btnClear = new ToolStripButton("清空图表") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        btnClear.Click += (s, e) => ClearChart();
        _toolStrip.Items.Add(btnClear);

        _toolStrip.Items.Add(new ToolStripSeparator());

        var btnAddCursor = new ToolStripButton("添加游标") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        btnAddCursor.Click += (s, e) => AddCursor();
        _toolStrip.Items.Add(btnAddCursor);

        var btnClearCursors = new ToolStripButton("清除游标") { DisplayStyle = ToolStripItemDisplayStyle.Text, Enabled = false };
        btnClearCursors.Click += (s, e) => ClearAllCursors();
        _toolStrip.Items.Add(btnClearCursors);

        var btnResetView = new ToolStripButton("重置视图") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        btnResetView.Click += (s, e) => ResetView();
        _toolStrip.Items.Add(btnResetView);

        _toolStrip.Items.Add(new ToolStripSeparator());

        var btnNormalize = new ToolStripButton("独立量程") { DisplayStyle = ToolStripItemDisplayStyle.Text, Checked = false };
        btnNormalize.Click += (s, e) => ToggleNormalizeMode();
        _toolStrip.Items.Add(btnNormalize);

        _toolStrip.Items.Add(new ToolStripSeparator());

        var btnSettings = new ToolStripButton("设置") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        btnSettings.Click += (s, e) => OpenSettings();
        _toolStrip.Items.Add(btnSettings);

        // === 图表 ===
        _formsPlot.Dock = DockStyle.Fill;

        // 启用双缓冲减少闪烁
        _formsPlot.DoubleBuffered();

        // === 变量列表 ===
        var splitMain = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 500
        };

        // 上半部分：图表
        splitMain.Panel1.Controls.Add(_formsPlot);

        // 下半部分：变量列表
        _lvVariables.Dock = DockStyle.Fill;
        _lvVariables.View = View.Details;
        _lvVariables.FullRowSelect = true;
        _lvVariables.GridLines = true;
        // 启用双缓冲，消除闪烁
        _lvVariables.DoubleBuffered();
        _lvVariables.Columns.Add("启用", 50);
        _lvVariables.Columns.Add("地址", 120);
        _lvVariables.Columns.Add("名称", 150);
        _lvVariables.Columns.Add("类型", 60);
        _lvVariables.Columns.Add("量程下限", 80);
        _lvVariables.Columns.Add("量程上限", 80);
        _lvVariables.Columns.Add("颜色", 60);
        _lvVariables.Columns.Add("当前值", 80);
        _lvVariables.DoubleClick += (s, e) => EditVariable();
        splitMain.Panel2.Controls.Add(_lvVariables);

        // === 状态栏 ===
        _statusStrip.Dock = DockStyle.Bottom;
        _lblStatusConn.Text = "未连接";
        _lblStatusConn.Spring = true;
        _lblStatusConn.TextAlign = ContentAlignment.MiddleLeft;
        _lblStatusSampling.Text = "采样: 停止";
        _lblStatusSampling.Spring = true;
        _lblStatusSampling.TextAlign = ContentAlignment.MiddleLeft;
        _lblStatusVars.Text = "变量: 0";
        _lblStatusVars.Spring = true;
        _lblStatusVars.TextAlign = ContentAlignment.MiddleLeft;
        _lblStatusError.Text = "";
        _lblStatusError.Spring = true;
        _lblStatusError.TextAlign = ContentAlignment.MiddleLeft;
        _lblStatusError.ForeColor = Color.Red;
        _statusStrip.Items.AddRange(new ToolStripItem[]
        {
            _lblStatusConn, _lblStatusSampling, _lblStatusVars, _lblStatusError
        });

        // 游标信息标签（显示在变量列表上方）
        _lblCursorInfo.Dock = DockStyle.Top;
        _lblCursorInfo.Height = 22;
        _lblCursorInfo.BackColor = Color.FromArgb(245, 245, 245);
        _lblCursorInfo.ForeColor = Color.FromArgb(60, 60, 60);
        _lblCursorInfo.Font = new Font("Consolas", 9F);
        _lblCursorInfo.Text = "";
        _lblCursorInfo.Visible = false;
        splitMain.Panel2.Controls.Add(_lblCursorInfo);

        Controls.AddRange(new Control[] { splitMain, _toolStrip, _statusStrip });

        // 图表鼠标事件：游标拖拽
        _formsPlot.MouseDown += FormsPlot_MouseDown;
        _formsPlot.MouseUp += FormsPlot_MouseUp;
        _formsPlot.MouseMove += FormsPlot_MouseMove;
    }

    private async void MainForm_Load(object? sender, EventArgs e)
    {
        try
        {
            // 初始化目录和日志
            ConfigPaths.EnsureDirectories();
            Logger.Init(ConfigPaths.LogDir);
            Logger.Info("程序启动");

            // 加载配置
            _connectionConfig = _configManager.LoadConnectionConfig();
            _appSettings = _configManager.LoadAppSettings();

            // 初始化数据库
            _dbInit = new DatabaseInitializer(ConfigPaths.DatabasePath);
            _dbInit.Init();
            _database = new DatabaseService(ConfigPaths.DatabasePath);
            _retentionService = new DataRetentionService(_database, _appSettings.DataRetentionHours);

            // 初始化图表服务
            _chartService = new TrendChartService(_formsPlot.Plot);
            _chartService.DisplayRefreshCallback = () => _formsPlot.Refresh();
            _chartService.SetTimeWindow(_appSettings.ChartWindowSeconds);
            _chartService.CursorMovedCallback = OnCursorMoved;

            // 加载变量列表
            await ReloadVariablesAsync();

            // 初始化渲染定时器（30FPS）
            _renderTimer.Interval = 33;
            _renderTimer.Tick += RenderTimer_Tick;
            _renderTimer.Start();

            // 数据保留清理定时器（每小时执行一次）
            _retentionTimer.Interval = 3600000;
            _retentionTimer.Tick += async (s, e) => await RunRetentionCleanupAsync();
            _retentionTimer.Start();

            // 执行一次初始清理
            _ = RunRetentionCleanupAsync();

            Logger.Info("初始化完成");
        }
        catch (Exception ex)
        {
            Logger.Error("初始化失败", ex);
            MessageBox.Show($"初始化失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        try
        {
            _renderTimer.Stop();
            _retentionTimer.Stop();

            if (_samplingService != null)
                await _samplingService.StopAsync();

            _plcService?.Dispose();
            Logger.Info("程序关闭");
        }
        catch (Exception ex)
        {
            Logger.Error("关闭时出错", ex);
        }
    }

    // === 渲染定时器 ===
    private void RenderTimer_Tick(object? sender, EventArgs e)
    {
        if (_chartService == null) return;

        if (_samplingService != null)
        {
            _chartService.ConsumeUpdates(_samplingService.UpdateQueue);
        }

        _chartService.RefreshChart();

        // 变量表降低更新频率：每6帧（约200ms）更新一次，避免闪烁
        _tableUpdateCounter++;
        if (_tableUpdateCounter >= 6)
        {
            _tableUpdateCounter = 0;
            UpdateVariableTableValues();
        }
    }

    // === 连接设置 ===
    private void OpenConnectionForm()
    {
        using var form = new ConnectionForm(_connectionConfig);
        if (form.ShowDialog() == DialogResult.OK)
        {
            _connectionConfig = form.Config;
            _configManager.SaveConnectionConfig(_connectionConfig);
            _appSettings.LastConnectionType = _connectionConfig.ConnectionType.ToString();
            _configManager.SaveAppSettings(_appSettings);
            Logger.Info($"连接配置已保存: {_connectionConfig.ConnectionType}");
            UpdateStatusBar();
        }
    }

    // === 变量管理 ===
    private async Task ReloadVariablesAsync()
    {
        if (_database == null) return;

        _variables = await _database.GetVariablesAsync();
        RefreshVariableList();

        if (_chartService != null)
        {
            _chartService.UpdateVariableSeries(_variables);
        }

        UpdateSamplingVariables();
        UpdateStatusBar();
    }

    private void RefreshVariableList()
    {
        _lvVariables.BeginUpdate();
        _lvVariables.Items.Clear();

        // 重建列（基础列 + 游标列）
        _lvVariables.Columns.Clear();
        _lvVariables.Columns.Add("启用", 50);
        _lvVariables.Columns.Add("地址", 120);
        _lvVariables.Columns.Add("名称", 150);
        _lvVariables.Columns.Add("类型", 60);
        _lvVariables.Columns.Add("量程下限", 80);
        _lvVariables.Columns.Add("量程上限", 80);
        _lvVariables.Columns.Add("颜色", 60);
        _lvVariables.Columns.Add("当前值", 80);

        int cursorCount = _chartService?.CursorCount ?? 0;
        for (int i = 0; i < cursorCount; i++)
        {
            _lvVariables.Columns.Add($"游标{i + 1}", 80);
        }

        foreach (var v in _variables)
        {
            var item = new ListViewItem(v.IsEnabled ? "✓" : "");
            item.SubItems.Add(v.Address);
            item.SubItems.Add(v.DisplayName);
            item.SubItems.Add(v.DataType);
            item.SubItems.Add(v.ScaleMin.ToString("F2"));
            item.SubItems.Add(v.ScaleMax.ToString("F2"));
            // 颜色列：显示色块而非色号
            var colorSubItem = item.SubItems.Add("");
            try { colorSubItem.BackColor = ColorTranslator.FromHtml(v.ColorHex); }
            catch { colorSubItem.BackColor = Color.Gray; }
            colorSubItem.Text = "";
            // 必须设置 UseItemStyleForSubItems = false 才能让单个 SubItem 的 BackColor 生效
            item.UseItemStyleForSubItems = false;
            item.SubItems.Add("-"); // 当前值
            // 游标列占位
            for (int i = 0; i < cursorCount; i++)
            {
                item.SubItems.Add("-");
            }
            item.Tag = v;
            item.ForeColor = v.IsEnabled ? Color.Black : Color.Gray;
            _lvVariables.Items.Add(item);
        }

        _lvVariables.EndUpdate();
        UpdateVariableTableValues();
    }

    /// <summary>更新变量表中的数值列（当前值和各游标值）。</summary>
    /// <remarks>
    /// 不使用 BeginUpdate/EndUpdate，直接修改 SubItem.Text。
    /// 只在文本实际变化时才赋值，避免不必要的重绘和闪烁。
    /// </remarks>
    private void UpdateVariableTableValues()
    {
        if (_chartService == null || _lvVariables.Items.Count == 0) return;

        var latestValues = _chartService.GetLatestValues();
        var cursorDataList = _chartService.GetAllCursorData();

        foreach (ListViewItem item in _lvVariables.Items)
        {
            var config = item.Tag as VariableConfig;
            if (config == null) continue;

            // 更新"当前值"列
            if (item.SubItems.Count > ColCurrentValue)
            {
                string newText;
                if (latestValues.TryGetValue(config.Id, out var latest))
                    newText = latest.value.ToString("F2");
                else
                    newText = "-";

                // 只在文本变化时才更新，避免闪烁
                if (item.SubItems[ColCurrentValue].Text != newText)
                    item.SubItems[ColCurrentValue].Text = newText;
            }

            // 更新游标列
            for (int i = 0; i < cursorDataList.Count; i++)
            {
                int colIndex = ColCursorStart + i;
                if (colIndex >= item.SubItems.Count) continue;

                string newText = "-";
                foreach (var v in cursorDataList[i].Values)
                {
                    if (v.varId == config.Id)
                    {
                        newText = v.value.ToString("F2");
                        break;
                    }
                }

                if (item.SubItems[colIndex].Text != newText)
                    item.SubItems[colIndex].Text = newText;
            }
        }
    }

    private async void AddVariable()
    {
        using var form = new VariableEditForm();
        if (form.ShowDialog() == DialogResult.OK)
        {
            if (_database == null) return;
            form.Variable.SortOrder = _variables.Count;
            await _database.SaveVariableAsync(form.Variable);
            Logger.Info($"添加变量: {form.Variable.Address}");
            await ReloadVariablesAsync();
        }
    }

    private async void EditVariable()
    {
        if (_lvVariables.SelectedItems.Count == 0) return;
        var existing = _lvVariables.SelectedItems[0].Tag as VariableConfig;
        if (existing == null) return;

        using var form = new VariableEditForm(existing);
        if (form.ShowDialog() == DialogResult.OK)
        {
            if (_database == null) return;
            await _database.SaveVariableAsync(form.Variable);
            Logger.Info($"编辑变量: {form.Variable.Address}");
            await ReloadVariablesAsync();
        }
    }

    private async void DeleteVariable()
    {
        if (_lvVariables.SelectedItems.Count == 0) return;
        var existing = _lvVariables.SelectedItems[0].Tag as VariableConfig;
        if (existing == null) return;

        if (MessageBox.Show($"确认删除变量 {existing.Address} ({existing.DisplayName})?\n相关历史数据将一并删除。",
            "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        if (_database == null) return;
        await _database.DeleteVariableAsync(existing.Id);
        Logger.Info($"删除变量: {existing.Address}");
        await ReloadVariablesAsync();
    }

    // === 采样控制 ===
    private void StartSampling()
    {
        if (_variables.Count == 0)
        {
            MessageBox.Show("请先添加至少一个变量", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            // 创建PLC通信服务
            _plcService?.Dispose();
            _plcService = PlcServiceFactory.Create(_connectionConfig);

            // 创建采样服务
            _samplingService = new SamplingService(_plcService, _database!, _appSettings.SamplingIntervalMs);
            _samplingService.OnConnectionStateChanged += OnConnectionStateChanged;
            _samplingService.OnError += OnSamplingError;

            // 更新采样变量
            UpdateSamplingVariables();

            // 启动采样
            _samplingService.Start();

            // 更新按钮状态
            SetSamplingButtonState(true);

            Logger.Info($"采样已启动: {_plcService.ConnectionDescription}");
            UpdateStatusBar();
        }
        catch (Exception ex)
        {
            Logger.Error("启动采样失败", ex);
            MessageBox.Show($"启动采样失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void StopSampling()
    {
        if (_samplingService == null) return;

        try
        {
            await _samplingService.StopAsync();
            _plcService?.Disconnect();
            SetSamplingButtonState(false);

            Logger.Info("采样已停止");
            UpdateStatusBar();
        }
        catch (Exception ex)
        {
            Logger.Error("停止采样失败", ex);
        }
    }

    private void SetSamplingButtonState(bool running)
    {
        foreach (ToolStripItem item in _toolStrip.Items)
        {
            if (item is ToolStripButton btn)
            {
                if (btn.Text == "开始采样") btn.Enabled = !running;
                if (btn.Text == "停止采样") btn.Enabled = running;
            }
        }
    }

    private void UpdateSamplingVariables()
    {
        if (_samplingService == null) return;

        var parsedVars = new List<(int id, ParsedS7Address addr)>();
        foreach (var v in _variables.Where(v => v.IsEnabled))
        {
            if (S7AddressParser.TryParse(v.Address, out var parsed))
            {
                if (parsed != null)
                {
                    parsed.VarType = ParseDataType(v.DataType, parsed.VarType);
                    parsedVars.Add((v.Id, parsed));
                }
            }
            else
            {
                Logger.Warning($"变量地址解析失败，跳过: {v.Address} ({v.DisplayName})");
            }
        }

        _samplingService.UpdateVariables(parsedVars);
    }

    private static S7VarType ParseDataType(string dataType, S7VarType defaultType)
    {
        return dataType switch
        {
            "Real" => S7VarType.Real,
            "Int" => S7VarType.Int,
            "Word" => S7VarType.Word,
            "DInt" => S7VarType.DInt,
            "DWord" => S7VarType.DWord,
            "Byte" => S7VarType.Byte,
            "Bit" => S7VarType.Bit,
            _ => defaultType
        };
    }

    // === 图表控制 ===
    private void TogglePauseChart()
    {
        _chartPaused = !_chartPaused;
        _chartService?.PauseRendering(_chartPaused);

        foreach (ToolStripItem item in _toolStrip.Items)
        {
            if (item is ToolStripButton btn && btn.Text.StartsWith("暂停图表"))
            {
                btn.Text = _chartPaused ? "恢复图表" : "暂停图表";
                break;
            }
        }
    }

    private void ClearChart()
    {
        _chartService?.ClearAll();
        _formsPlot.Refresh();
        Logger.Info("图表已清空");
    }

    // === 多游标线 ===

    private void AddCursor()
    {
        if (_chartService == null) return;

        _chartService.AddCursor();
        RefreshVariableList();

        // 更新按钮状态
        foreach (ToolStripItem item in _toolStrip.Items)
        {
            if (item is ToolStripButton btn && btn.Text == "清除游标")
                btn.Enabled = _chartService.HasCursors;
        }
    }

    private void ClearAllCursors()
    {
        if (_chartService == null) return;

        _chartService.RemoveAllCursors();
        RefreshVariableList();
        _lblCursorInfo.Visible = false;
        _lblCursorInfo.Text = "";

        // 清除游标后恢复 ScottPlot 交互
        _formsPlot.UserInputProcessor.Enable();

        foreach (ToolStripItem item in _toolStrip.Items)
        {
            if (item is ToolStripButton btn && btn.Text == "清除游标")
                btn.Enabled = false;
        }
    }

    /// <summary>游标位置变化回调：更新信息标签和变量表。</summary>
    private void OnCursorMoved(List<CursorData> cursors)
    {
        if (InvokeRequired)
        {
            Invoke(() => OnCursorMoved(cursors));
            return;
        }

        if (cursors.Count == 0)
        {
            _lblCursorInfo.Visible = false;
            _lblCursorInfo.Text = "";
            UpdateVariableTableValues();
            return;
        }

        _lblCursorInfo.Visible = true;

        var parts = new List<string>();
        foreach (var cursor in cursors)
        {
            string timeStr;
            try
            {
                timeStr = DateTimeOffset.FromUnixTimeMilliseconds((long)cursor.TimestampMs).LocalDateTime.ToString("HH:mm:ss.fff");
            }
            catch
            {
                timeStr = cursor.TimestampMs.ToString("F0");
            }

            if (cursor.Values.Count == 0)
            {
                parts.Add($"游标{cursor.Id}: {timeStr} (暂无数据)");
            }
            else
            {
                var valueParts = cursor.Values.Select(v => $"{v.name}={v.value:F2}");
                parts.Add($"游标{cursor.Id}: {timeStr} | {string.Join("  ", valueParts)}");
            }
        }

        _lblCursorInfo.Text = "  " + string.Join("  ||  ", parts);

        // 同步更新变量表
        UpdateVariableTableValues();
    }

    // === 图表鼠标交互（游标拖拽）===
    //
    // 核心策略：游标线的 IsDraggable = true，ScottPlot 在 MouseDown 时
    // 识别到可拖动对象，不会启动平移。然后在 WinForms MouseDown 中
    // 调用 Disable() 阻止 ScottPlot 内置拖拽，改由我们手动处理。
    // MouseUp 时 Enable() 恢复交互，保证平移和缩放正常。

    private void FormsPlot_MouseDown(object? sender, MouseEventArgs e)
    {
        if (_chartService == null || !_chartService.HasCursors) return;

        var rect = _formsPlot.Plot.GetCoordinateRect(e.X, e.Y, radius: 10);
        var cursorId = _chartService.FindCursorAtRect(rect);
        if (cursorId.HasValue)
        {
            _draggingCursorId = cursorId;
            _formsPlot.UserInputProcessor.Disable();
        }
    }

    private void FormsPlot_MouseMove(object? sender, MouseEventArgs e)
    {
        if (_chartService == null) return;

        if (_draggingCursorId.HasValue)
        {
            // 拖拽中：更新游标位置
            var coord = _formsPlot.Plot.GetCoordinates(e.X, e.Y);
            _chartService.SetCursorPosition(_draggingCursorId.Value, coord.X);
            _formsPlot.Refresh();
        }
        else if (_chartService.HasCursors)
        {
            // 非拖拽：根据是否悬停在游标线上切换鼠标样式
            var rect = _formsPlot.Plot.GetCoordinateRect(e.X, e.Y, radius: 10);
            bool onCursor = _chartService.FindCursorAtRect(rect).HasValue;
            _formsPlot.Cursor = onCursor ? Cursors.SizeWE : Cursors.Default;
        }
    }

    private void FormsPlot_MouseUp(object? sender, MouseEventArgs e)
    {
        if (_draggingCursorId.HasValue)
        {
            _draggingCursorId = null;
            _formsPlot.UserInputProcessor.Enable();
            _formsPlot.Refresh();
        }
    }

    // === 重置视图 ===

    private void ResetView()
    {
        if (_chartService == null) return;

        // 如果处于暂停状态，先恢复
        if (_chartPaused)
        {
            TogglePauseChart();
        }

        // 手动触发一次完整刷新以重置坐标轴
        _chartService.PauseRendering(false);
        _chartService.RefreshChart();
        _formsPlot.Refresh();
        Logger.Info("视图已重置");
    }

    // === 独立量程/统一量程切换 ===

    private void ToggleNormalizeMode()
    {
        if (_chartService == null) return;

        bool newMode = !_chartService.IsNormalized;
        _chartService.SetNormalizeMode(newMode);

        // 更新按钮文字和状态
        foreach (ToolStripItem item in _toolStrip.Items)
        {
            if (item is ToolStripButton btn && (btn.Text == "独立量程" || btn.Text == "统一量程"))
            {
                btn.Text = newMode ? "统一量程" : "独立量程";
                btn.Checked = newMode;
                btn.BackColor = newMode ? Color.LightSkyBlue : Color.Empty;
                break;
            }
        }

        Logger.Info($"量程模式: {(newMode ? "独立量程(归一化)" : "统一量程")}");
    }

    // === 设置 ===
    private void OpenSettings()
    {
        using var form = new SettingsForm(_appSettings);
        if (form.ShowDialog() == DialogResult.OK)
        {
            _appSettings = form.Settings;
            _configManager.SaveAppSettings(_appSettings);

            if (_chartService != null)
                _chartService.SetTimeWindow(_appSettings.ChartWindowSeconds);

            if (_retentionService != null)
                _retentionService = new DataRetentionService(_database!, _appSettings.DataRetentionHours);

            Logger.Info("设置已保存");
        }
    }

    // === 事件回调 ===
    private void OnConnectionStateChanged(bool connected)
    {
        if (InvokeRequired)
        {
            Invoke(() => OnConnectionStateChanged(connected));
            return;
        }
        UpdateStatusBar();
    }

    private void OnSamplingError(string error)
    {
        if (InvokeRequired)
        {
            Invoke(() => OnSamplingError(error));
            return;
        }
        _lblStatusError.Text = error;
    }

    // === 状态栏 ===
    private void UpdateStatusBar()
    {
        string connDesc = _plcService?.ConnectionDescription ?? "未连接";
        bool connected = _plcService?.IsConnected ?? false;
        _lblStatusConn.Text = $"连接: {(connected ? "✓ " + connDesc : "未连接")}";
        _lblStatusConn.ForeColor = connected ? Color.Green : Color.Gray;

        bool sampling = _samplingService?.IsRunning ?? false;
        _lblStatusSampling.Text = $"采样: {(sampling ? "运行中" : "停止")} [{_appSettings.SamplingIntervalMs}ms]";

        _lblStatusVars.Text = $"变量: {_variables.Count(v => v.IsEnabled)}/{_variables.Count}";

        if (_lblStatusError.Text.Length > 100)
            _lblStatusError.Text = _lblStatusError.Text[..100] + "...";
    }

    // === 数据保留清理 ===
    private async Task RunRetentionCleanupAsync()
    {
        if (_retentionService == null) return;
        try
        {
            int deleted = await _retentionService.CleanupAsync();
            if (deleted > 0)
                Logger.Info($"数据清理: 删除{deleted}条过期数据");
        }
        catch (Exception ex)
        {
            Logger.Error("数据清理失败", ex);
        }
    }
}

/// <summary>
/// 控件双缓冲扩展方法，用于消除 ListView 等控件的闪烁。
/// </summary>
public static class ControlExtensions
{
    public static void DoubleBuffered(this Control control, bool enabled = true)
    {
        var prop = typeof(Control).GetProperty("DoubleBuffered",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        prop?.SetValue(control, enabled, null);
    }
}
