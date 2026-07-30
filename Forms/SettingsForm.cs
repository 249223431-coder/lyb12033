using S7TrendMonitor.Models;

namespace S7TrendMonitor.Forms;

public partial class SettingsForm : Form
{
    public AppSettings Settings { get; private set; }

    private readonly NumericUpDown _numInterval = new();
    private readonly NumericUpDown _numRetention = new();
    private readonly NumericUpDown _numWindow = new();
    private readonly CheckBox _chkAutoStart = new();
    private readonly Button _btnOk = new();
    private readonly Button _btnCancel = new();

    public SettingsForm(AppSettings settings)
    {
        Settings = settings ?? new AppSettings();
        InitializeComponent();
        LoadSettings();
    }

    private void InitializeComponent()
    {
        Text = "设置";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(360, 230);

        int y = 20;
        int labelX = 20;
        int labelW = 130;
        int inputX = 160;

        var lblInterval = new Label { Text = "采样间隔(ms):", Location = new Point(labelX, y + 3), Size = new Size(labelW, 20), TextAlign = ContentAlignment.MiddleRight };
        _numInterval.Location = new Point(inputX, y);
        _numInterval.Size = new Size(100, 23);
        _numInterval.Minimum = 50;
        _numInterval.Maximum = 60000;
        _numInterval.Increment = 50;
        y += 35;

        var lblRetention = new Label { Text = "数据保留(小时):", Location = new Point(labelX, y + 3), Size = new Size(labelW, 20), TextAlign = ContentAlignment.MiddleRight };
        _numRetention.Location = new Point(inputX, y);
        _numRetention.Size = new Size(100, 23);
        _numRetention.Minimum = 1;
        _numRetention.Maximum = 720;
        y += 35;

        var lblWindow = new Label { Text = "图表窗口(秒):", Location = new Point(labelX, y + 3), Size = new Size(labelW, 20), TextAlign = ContentAlignment.MiddleRight };
        _numWindow.Location = new Point(inputX, y);
        _numWindow.Size = new Size(100, 23);
        _numWindow.Minimum = 10;
        _numWindow.Maximum = 86400;
        _numWindow.Increment = 10;
        y += 35;

        _chkAutoStart.Text = "启动后自动开始采样";
        _chkAutoStart.Location = new Point(labelX, y);
        _chkAutoStart.AutoSize = true;
        y += 40;

        _btnOk.Text = "确定";
        _btnOk.Location = new Point(140, y);
        _btnOk.Size = new Size(75, 30);
        _btnOk.Click += BtnOk_Click;

        _btnCancel.Text = "取消";
        _btnCancel.Location = new Point(225, y);
        _btnCancel.Size = new Size(75, 30);
        _btnCancel.DialogResult = DialogResult.Cancel;

        Controls.AddRange(new Control[]
        {
            lblInterval, _numInterval,
            lblRetention, _numRetention,
            lblWindow, _numWindow,
            _chkAutoStart,
            _btnOk, _btnCancel
        });

        AcceptButton = _btnOk;
        CancelButton = _btnCancel;
    }

    private void LoadSettings()
    {
        _numInterval.Value = Settings.SamplingIntervalMs;
        _numRetention.Value = Settings.DataRetentionHours;
        _numWindow.Value = Settings.ChartWindowSeconds;
        _chkAutoStart.Checked = Settings.AutoStartSampling;
    }

    private void BtnOk_Click(object? sender, EventArgs e)
    {
        Settings.SamplingIntervalMs = (int)_numInterval.Value;
        Settings.DataRetentionHours = (int)_numRetention.Value;
        Settings.ChartWindowSeconds = (int)_numWindow.Value;
        Settings.AutoStartSampling = _chkAutoStart.Checked;
        DialogResult = DialogResult.OK;
    }
}
