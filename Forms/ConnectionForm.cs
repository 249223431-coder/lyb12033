using S7TrendMonitor.Models;

namespace S7TrendMonitor.Forms;

public partial class ConnectionForm : Form
{
    private static readonly TimeSpan ConnectionTestTimeout = TimeSpan.FromSeconds(5);

    public ConnectionConfig Config { get; private set; }

    private readonly RadioButton _rbEthernet = new();
    private readonly RadioButton _rbMpiUsb = new();
    private readonly RadioButton _rbMpiCp5611 = new();

    private readonly GroupBox _grpEthernet = new();
    private readonly ComboBox _cmbCpuType = new();
    private readonly TextBox _txtIp = new();
    private readonly NumericUpDown _numRack = new();
    private readonly NumericUpDown _numSlot = new();

    private readonly GroupBox _grpMpi = new();
    private readonly ComboBox _cmbComPort = new();
    private readonly NumericUpDown _numMpiAddr = new();
    private readonly ComboBox _cmbBaudRate = new();

    private readonly NumericUpDown _numRackCommon = new();
    private readonly NumericUpDown _numSlotCommon = new();

    private readonly Button _btnTest = new();
    private readonly Button _btnOk = new();
    private readonly Button _btnCancel = new();

    private readonly Label _lblStatus = new();

    public ConnectionForm(ConnectionConfig config)
    {
        Config = config ?? new ConnectionConfig();
        InitializeComponent();
        LoadConfig();
    }

    private void InitializeComponent()
    {
        Text = "PLC连接设置";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(420, 560);

        // === 连接方式选择 ===
        var grpType = new GroupBox { Text = "连接方式", Location = new Point(12, 12), Size = new Size(396, 100) };
        _rbEthernet.Text = "以太网 (TCP/IP)";
        _rbEthernet.Location = new Point(16, 28);
        _rbEthernet.Size = new Size(200, 24);
        _rbEthernet.Checked = true;
        _rbEthernet.CheckedChanged += (s, e) => UpdatePanels();

        _rbMpiUsb.Text = "MPI - PC Adapter USB";
        _rbMpiUsb.Location = new Point(16, 52);
        _rbMpiUsb.Size = new Size(200, 24);
        _rbMpiUsb.CheckedChanged += (s, e) => UpdatePanels();

        _rbMpiCp5611.Text = "MPI - CP5611卡";
        _rbMpiCp5611.Location = new Point(16, 76);
        _rbMpiCp5611.Size = new Size(200, 24);
        _rbMpiCp5611.CheckedChanged += (s, e) => UpdatePanels();

        grpType.Controls.AddRange(new Control[] { _rbEthernet, _rbMpiUsb, _rbMpiCp5611 });

        // === 以太网参数 ===
        _grpEthernet.Text = "以太网参数";
        _grpEthernet.Location = new Point(12, 118);
        _grpEthernet.Size = new Size(396, 120);

        var lblCpu = new Label { Text = "CPU类型:", Location = new Point(16, 28), Size = new Size(75, 20), TextAlign = ContentAlignment.MiddleRight };
        _cmbCpuType.Location = new Point(100, 25);
        _cmbCpuType.Size = new Size(120, 23);
        _cmbCpuType.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbCpuType.Items.AddRange(new object[] { "S7300", "S7400", "S71200", "S71500" });

        var lblIp = new Label { Text = "IP地址:", Location = new Point(16, 58), Size = new Size(75, 20), TextAlign = ContentAlignment.MiddleRight };
        _txtIp.Location = new Point(100, 55);
        _txtIp.Size = new Size(140, 23);

        var lblRack = new Label { Text = "机架号:", Location = new Point(250, 28), Size = new Size(55, 20), TextAlign = ContentAlignment.MiddleRight };
        _numRack.Location = new Point(315, 25);
        _numRack.Size = new Size(60, 23);
        _numRack.Minimum = 0;
        _numRack.Maximum = 15;

        var lblSlot = new Label { Text = "槽位号:", Location = new Point(250, 58), Size = new Size(55, 20), TextAlign = ContentAlignment.MiddleRight };
        _numSlot.Location = new Point(315, 55);
        _numSlot.Size = new Size(60, 23);
        _numSlot.Minimum = 0;
        _numSlot.Maximum = 15;

        _grpEthernet.Controls.AddRange(new Control[] { lblCpu, _cmbCpuType, lblIp, _txtIp, lblRack, _numRack, lblSlot, _numSlot });

        // === MPI参数 ===
        _grpMpi.Text = "MPI参数";
        _grpMpi.Location = new Point(12, 244);
        _grpMpi.Size = new Size(396, 120);

        var lblCom = new Label { Text = "串口号:", Location = new Point(16, 28), Size = new Size(75, 20), TextAlign = ContentAlignment.MiddleRight };
        _cmbComPort.Location = new Point(100, 25);
        _cmbComPort.Size = new Size(100, 23);
        _cmbComPort.DropDownStyle = ComboBoxStyle.DropDownList;

        var lblMpiAddr = new Label { Text = "MPI地址:", Location = new Point(16, 58), Size = new Size(75, 20), TextAlign = ContentAlignment.MiddleRight };
        _numMpiAddr.Location = new Point(100, 55);
        _numMpiAddr.Size = new Size(60, 23);
        _numMpiAddr.Minimum = 0;
        _numMpiAddr.Maximum = 126;

        var lblBaud = new Label { Text = "波特率:", Location = new Point(16, 88), Size = new Size(75, 20), TextAlign = ContentAlignment.MiddleRight };
        _cmbBaudRate.Location = new Point(100, 85);
        _cmbBaudRate.Size = new Size(120, 23);
        _cmbBaudRate.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbBaudRate.Items.AddRange(new object[] { "187500", "19.2k", "9.6k" });
        _cmbBaudRate.SelectedIndex = 0;

        _grpMpi.Controls.AddRange(new Control[] { lblCom, _cmbComPort, lblMpiAddr, _numMpiAddr, lblBaud, _cmbBaudRate });

        // === 通用参数（机架/槽位）===
        var grpCommon = new GroupBox { Text = "通用参数", Location = new Point(12, 370), Size = new Size(396, 70) };
        var lblRackC = new Label { Text = "机架号:", Location = new Point(16, 28), Size = new Size(75, 20), TextAlign = ContentAlignment.MiddleRight };
        _numRackCommon.Location = new Point(100, 25);
        _numRackCommon.Size = new Size(60, 23);
        _numRackCommon.Minimum = 0;
        _numRackCommon.Maximum = 15;

        var lblSlotC = new Label { Text = "槽位号:", Location = new Point(200, 28), Size = new Size(75, 20), TextAlign = ContentAlignment.MiddleRight };
        _numSlotCommon.Location = new Point(285, 25);
        _numSlotCommon.Size = new Size(60, 23);
        _numSlotCommon.Minimum = 0;
        _numSlotCommon.Maximum = 15;

        grpCommon.Controls.AddRange(new Control[] { lblRackC, _numRackCommon, lblSlotC, _numSlotCommon });

        // === 按钮 ===
        _btnTest.Text = "测试连接";
        _btnTest.Location = new Point(12, 455);
        _btnTest.Size = new Size(100, 32);
        _btnTest.Click += BtnTest_Click;

        _btnOk.Text = "确定";
        _btnOk.Location = new Point(230, 455);
        _btnOk.Size = new Size(80, 32);
        _btnOk.Click += BtnOk_Click;

        _btnCancel.Text = "取消";
        _btnCancel.Location = new Point(320, 455);
        _btnCancel.Size = new Size(80, 32);
        _btnCancel.DialogResult = DialogResult.Cancel;

        // === 状态标签 ===
        _lblStatus.Location = new Point(12, 495);
        _lblStatus.Size = new Size(396, 50);
        _lblStatus.ForeColor = Color.Gray;

        Controls.AddRange(new Control[] {
            grpType, _grpEthernet, _grpMpi, grpCommon,
            _btnTest, _btnOk, _btnCancel, _lblStatus
        });

        AcceptButton = _btnOk;
        CancelButton = _btnCancel;
    }

    private void LoadConfig()
    {
        // 加载串口列表
        _cmbComPort.Items.Clear();
        _cmbComPort.Items.AddRange(GetAvailablePorts());

        switch (Config.ConnectionType)
        {
            case ConnectionType.Ethernet:
                _rbEthernet.Checked = true;
                break;
            case ConnectionType.MpiPcAdapter:
                _rbMpiUsb.Checked = true;
                break;
            case ConnectionType.MpiCp5611:
                _rbMpiCp5611.Checked = true;
                break;
        }

        // 以太网参数
        _cmbCpuType.SelectedItem = Config.CpuType;
        if (_cmbCpuType.SelectedIndex < 0) _cmbCpuType.SelectedIndex = 0;
        _txtIp.Text = Config.IpAddress;
        _numRack.Value = Config.Rack;
        _numSlot.Value = Config.Slot;

        // MPI参数
        if (!string.IsNullOrEmpty(Config.ComPort))
        {
            int idx = _cmbComPort.Items.IndexOf(Config.ComPort);
            if (idx >= 0) _cmbComPort.SelectedIndex = idx;
        }
        if (_cmbComPort.SelectedIndex < 0 && _cmbComPort.Items.Count > 0)
            _cmbComPort.SelectedIndex = 0;

        _numMpiAddr.Value = Config.MpiAddress;

        int baudIdx = Config.MpiBaudRate switch
        {
            9600 => 2,
            19200 => 1,
            _ => 0
        };
        _cmbBaudRate.SelectedIndex = baudIdx;

        // 通用参数
        _numRackCommon.Value = Config.Rack;
        _numSlotCommon.Value = Config.Slot;

        UpdatePanels();
    }

    private void UpdatePanels()
    {
        if (_rbEthernet.Checked)
        {
            _grpEthernet.Visible = true;
            _grpMpi.Visible = false;
        }
        else
        {
            _grpEthernet.Visible = false;
            _grpMpi.Visible = true;

            // CP5611不需要串口号
            bool needComPort = _rbMpiUsb.Checked;
            foreach (Control c in _grpMpi.Controls)
            {
                if (c.Text == "串口号:" || c == _cmbComPort)
                    c.Visible = needComPort;
            }
        }
    }

    private static string[] GetAvailablePorts()
    {
        try
        {
            return System.IO.Ports.SerialPort.GetPortNames();
        }
        catch
        {
            return new[] { "COM1", "COM2", "COM3" };
        }
    }

    private int GetBaudRate()
    {
        return _cmbBaudRate.SelectedIndex switch
        {
            1 => 19200,
            2 => 9600,
            _ => 187500
        };
    }

    private async void BtnTest_Click(object? sender, EventArgs e)
    {
        SaveToConfig();
        _lblStatus.Text = "正在测试连接...";
        _lblStatus.ForeColor = Color.Gray;
        _btnTest.Enabled = false;

        try
        {
            var plc = Communication.PlcServiceFactory.Create(Config);
            bool? ok = await ConnectWithTimeoutAsync(plc, ConnectionTestTimeout);
            if (ok == true)
            {
                _lblStatus.Text = $"✓ 连接成功！\n{plc.ConnectionDescription}";
                _lblStatus.ForeColor = Color.Green;
                plc.Disconnect();
                plc.Dispose();
            }
            else if (ok == false)
            {
                _lblStatus.Text = $"✗ 连接失败\n{plc.ConnectionDescription}";
                _lblStatus.ForeColor = Color.Red;
                plc.Disconnect();
                plc.Dispose();
            }
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"✗ 连接异常: {ex.Message}";
            _lblStatus.ForeColor = Color.Red;
        }
        finally
        {
            _btnTest.Enabled = true;
        }
    }

    private async Task<bool?> ConnectWithTimeoutAsync(Communication.IPlcCommunication plc, TimeSpan timeout)
    {
        Task<bool> connectTask = plc.ConnectAsync();
        Task delayTask = Task.Delay(timeout);
        Task completedTask = await Task.WhenAny(connectTask, delayTask);

        if (completedTask == connectTask)
            return await connectTask;

        _lblStatus.Text = $"✗ 连接超时（超过{timeout.TotalSeconds:0}秒）\n{plc.ConnectionDescription}";
        _lblStatus.ForeColor = Color.Red;

        _ = Task.Run(() =>
        {
            try
            {
                plc.Disconnect();
                plc.Dispose();
            }
            catch
            {
                // 后台清理失败不影响界面恢复
            }
        });

        return null;
    }

    private void BtnOk_Click(object? sender, EventArgs e)
    {
        SaveToConfig();
        DialogResult = DialogResult.OK;
    }

    private void SaveToConfig()
    {
        if (_rbEthernet.Checked)
            Config.ConnectionType = ConnectionType.Ethernet;
        else if (_rbMpiUsb.Checked)
            Config.ConnectionType = ConnectionType.MpiPcAdapter;
        else
            Config.ConnectionType = ConnectionType.MpiCp5611;

        Config.CpuType = _cmbCpuType.SelectedItem?.ToString() ?? "S7300";
        Config.IpAddress = _txtIp.Text.Trim();
        if (_rbEthernet.Checked)
        {
            Config.Rack = (short)_numRack.Value;
            Config.Slot = (short)_numSlot.Value;
        }
        else
        {
            Config.Rack = (short)_numRackCommon.Value;
            Config.Slot = (short)_numSlotCommon.Value;
        }

        Config.ComPort = _cmbComPort.SelectedItem?.ToString() ?? "COM3";
        Config.MpiBaudRate = GetBaudRate();
        Config.MpiAddress = (int)_numMpiAddr.Value;
    }
}
