using S7TrendMonitor.Communication;
using S7TrendMonitor.Models;

namespace S7TrendMonitor.Forms;

public partial class VariableEditForm : Form
{
    public VariableConfig Variable { get; private set; }

    private readonly TextBox _txtAddress = new();
    private readonly TextBox _txtName = new();
    private readonly ComboBox _cmbDataType = new();
    private readonly NumericUpDown _numScaleMin = new();
    private readonly NumericUpDown _numScaleMax = new();
    private readonly ComboBox _cmbColor = new();
    private readonly CheckBox _chkEnabled = new();
    private readonly Label _lblPreview = new();
    private readonly Button _btnOk = new();
    private readonly Button _btnCancel = new();

    private static readonly string[] ColorPresets = new[]
    {
        "#1E90FF", "#FF4500", "#32CD32", "#FFD700", "#FF69B4",
        "#9370DB", "#00CED1", "#FF8C00", "#DC143C", "#4169E1"
    };

    public VariableEditForm(VariableConfig? existing = null)
    {
        Variable = existing ?? new VariableConfig();
        InitializeComponent();
        LoadVariable();
    }

    private void InitializeComponent()
    {
        Text = Variable.Id > 0 ? "编辑变量" : "添加变量";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(400, 340);

        int y = 15;
        int labelX = 20;
        int labelW = 90;
        int inputX = 120;
        int inputW = 260;

        // 地址
        var lblAddress = new Label { Text = "S7地址:", Location = new Point(labelX, y + 3), Size = new Size(labelW, 20), TextAlign = ContentAlignment.MiddleRight };
        _txtAddress.Location = new Point(inputX, y);
        _txtAddress.Size = new Size(inputW, 23);
        _txtAddress.TextChanged += (s, e) => UpdatePreview();
        y += 35;

        // 变量名称
        var lblName = new Label { Text = "变量名称:", Location = new Point(labelX, y + 3), Size = new Size(labelW, 20), TextAlign = ContentAlignment.MiddleRight };
        _txtName.Location = new Point(inputX, y);
        _txtName.Size = new Size(inputW, 23);
        y += 35;

        // 数据类型
        var lblType = new Label { Text = "数据类型:", Location = new Point(labelX, y + 3), Size = new Size(labelW, 20), TextAlign = ContentAlignment.MiddleRight };
        _cmbDataType.Location = new Point(inputX, y);
        _cmbDataType.Size = new Size(120, 23);
        _cmbDataType.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbDataType.Items.AddRange(new object[] { "Real", "Int", "Word", "DInt", "DWord", "Byte", "Bit" });
        _cmbDataType.SelectedIndexChanged += (s, e) => UpdatePreview();
        y += 35;

        // 量程下限
        var lblMin = new Label { Text = "量程下限:", Location = new Point(labelX, y + 3), Size = new Size(labelW, 20), TextAlign = ContentAlignment.MiddleRight };
        _numScaleMin.Location = new Point(inputX, y);
        _numScaleMin.Size = new Size(100, 23);
        _numScaleMin.Minimum = -1000000;
        _numScaleMin.Maximum = 1000000;
        _numScaleMin.DecimalPlaces = 2;
        y += 35;

        // 量程上限
        var lblMax = new Label { Text = "量程上限:", Location = new Point(labelX, y + 3), Size = new Size(labelW, 20), TextAlign = ContentAlignment.MiddleRight };
        _numScaleMax.Location = new Point(inputX, y);
        _numScaleMax.Size = new Size(100, 23);
        _numScaleMax.Minimum = -1000000;
        _numScaleMax.Maximum = 1000000;
        _numScaleMax.DecimalPlaces = 2;
        y += 35;

        // 颜色
        var lblColor = new Label { Text = "曲线颜色:", Location = new Point(labelX, y + 3), Size = new Size(labelW, 20), TextAlign = ContentAlignment.MiddleRight };
        _cmbColor.Location = new Point(inputX, y);
        _cmbColor.Size = new Size(120, 23);
        _cmbColor.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbColor.Items.AddRange(ColorPresets);
        _cmbColor.DrawMode = DrawMode.OwnerDrawFixed;
        _cmbColor.DrawItem += CmbColor_DrawItem;
        y += 35;

        // 启用
        _chkEnabled.Text = "启用此变量";
        _chkEnabled.Location = new Point(inputX, y);
        _chkEnabled.AutoSize = true;
        _chkEnabled.Checked = true;
        y += 35;

        // 预览
        _lblPreview.Location = new Point(20, y);
        _lblPreview.Size = new Size(340, 25);
        _lblPreview.ForeColor = Color.Gray;
        y += 30;

        // 按钮
        _btnOk.Text = "确定";
        _btnOk.Location = new Point(200, y);
        _btnOk.Size = new Size(75, 30);
        _btnOk.Click += BtnOk_Click;

        _btnCancel.Text = "取消";
        _btnCancel.Location = new Point(285, y);
        _btnCancel.Size = new Size(75, 30);
        _btnCancel.DialogResult = DialogResult.Cancel;

        Controls.AddRange(new Control[]
        {
            lblAddress, _txtAddress,
            lblName, _txtName,
            lblType, _cmbDataType,
            lblMin, _numScaleMin,
            lblMax, _numScaleMax,
            lblColor, _cmbColor,
            _chkEnabled,
            _lblPreview,
            _btnOk, _btnCancel
        });

        AcceptButton = _btnOk;
        CancelButton = _btnCancel;
    }

    private void LoadVariable()
    {
        _txtAddress.Text = Variable.Address;
        _txtName.Text = Variable.DisplayName;
        _numScaleMin.Value = (decimal)Variable.ScaleMin;
        _numScaleMax.Value = (decimal)Variable.ScaleMax;
        _chkEnabled.Checked = Variable.IsEnabled;

        // 数据类型
        int typeIdx = _cmbDataType.Items.IndexOf(Variable.DataType);
        if (typeIdx < 0) typeIdx = 0;
        _cmbDataType.SelectedIndex = typeIdx;

        // 颜色
        int colorIdx = Array.IndexOf(ColorPresets, Variable.ColorHex?.ToUpperInvariant());
        if (colorIdx < 0) colorIdx = 0;
        _cmbColor.SelectedIndex = colorIdx;

        UpdatePreview();
    }

    private void UpdatePreview()
    {
        string addr = _txtAddress.Text.Trim();
        if (string.IsNullOrEmpty(addr))
        {
            _lblPreview.Text = "";
            return;
        }

        if (S7AddressParser.TryParse(addr, out var parsed))
        {
            string type = _cmbDataType.SelectedItem?.ToString() ?? "Real";
            _lblPreview.Text = $"✓ 解析成功: 区域={parsed!.AreaType}, DB={parsed.DbNumber}, 偏移={parsed.ByteOffset}, 类型={type}";
            _lblPreview.ForeColor = Color.Green;

            // 根据地址后缀自动推断数据类型
            if (parsed.VarType == Models.S7VarType.Real && _cmbDataType.SelectedIndex == 0)
            {
                _cmbDataType.SelectedItem = "Real";
            }
        }
        else
        {
            _lblPreview.Text = "✗ 地址格式无效，例如: DB1.DBD10, MW10, M10.0";
            _lblPreview.ForeColor = Color.Red;
        }
    }

    private void CmbColor_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        e.DrawBackground();

        string colorHex = _cmbColor.Items[e.Index]?.ToString() ?? "#1E90FF";
        Color c = ColorTranslator.FromHtml(colorHex);

        using var brush = new SolidBrush(c);
        e.Graphics.FillRectangle(brush, e.Bounds.X + 2, e.Bounds.Y + 2, 20, e.Bounds.Height - 4);

        using var textBrush = new SolidBrush(e.ForeColor);
        e.Graphics.DrawString(colorHex, e.Font!, textBrush, e.Bounds.X + 28, e.Bounds.Y + 2);

        e.DrawFocusRectangle();
    }

    private void BtnOk_Click(object? sender, EventArgs e)
    {
        string addr = _txtAddress.Text.Trim();
        if (string.IsNullOrEmpty(addr))
        {
            MessageBox.Show("请输入S7地址", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!S7AddressParser.TryParse(addr, out _))
        {
            MessageBox.Show("S7地址格式无效\n支持格式: DB1.DBD10, MW10, M10.0", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Variable.Address = addr;
        Variable.DisplayName = string.IsNullOrEmpty(_txtName.Text.Trim()) ? addr : _txtName.Text.Trim();
        Variable.DataType = _cmbDataType.SelectedItem?.ToString() ?? "Real";
        Variable.ScaleMin = (double)_numScaleMin.Value;
        Variable.ScaleMax = (double)_numScaleMax.Value;
        Variable.ColorHex = _cmbColor.SelectedItem?.ToString() ?? "#1E90FF";
        Variable.IsEnabled = _chkEnabled.Checked;

        DialogResult = DialogResult.OK;
    }
}
