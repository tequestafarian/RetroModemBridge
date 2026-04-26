namespace RetroModemBridge;

public sealed class MainForm : Form
{
    private readonly ComboBox _comPortCombo = new();
    private readonly ComboBox _baudCombo = new();
    private readonly TextBox _defaultPortText = new();
    private readonly CheckBox _dtrCheck = new();
    private readonly CheckBox _rtsCheck = new();
    private readonly CheckBox _echoCheck = new();
    private readonly Button _refreshButton = new();
    private readonly Button _startButton = new();
    private readonly Button _stopButton = new();
    private readonly Button _clearLogButton = new();
    private readonly TextBox _logBox = new();
    private readonly Label _statusLabel = new();
    private readonly Label _lineStatusLabel = new();
    private readonly ModemBridge _bridge = new();
    private readonly AppSettings _settings;
    private readonly System.Windows.Forms.Timer _lineStatusTimer = new();

    private static readonly int[] BaudRates =
    {
        300, 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200
    };

    public MainForm()
    {
        _settings = AppSettings.Load();
        InitializeComponent();
        WireEvents();
        RefreshComPorts();
        ApplySettingsToUi();
    }

    private void InitializeComponent()
    {
        Text = "RetroModem Bridge";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(840, 560);
        Size = new Size(980, 700);
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var title = new Label
        {
            Text = "RetroModem Bridge",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 14F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 2)
        };
        root.Controls.Add(title, 0, 0);

        var subtitle = new Label
        {
            Text = "Serial-to-TCP modem bridge for vintage computers",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        };
        root.Controls.Add(subtitle, 0, 1);

        var settingsGroup = new GroupBox
        {
            Text = "Serial and modem settings",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 0, 10)
        };
        root.Controls.Add(settingsGroup, 0, 2);

        var settingsGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 7,
            RowCount = 4
        };
        settingsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        settingsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        settingsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        settingsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        settingsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        settingsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        settingsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        settingsGroup.Controls.Add(settingsGrid);

        settingsGrid.Controls.Add(CreateLabel("COM port"), 0, 0);
        _comPortCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _comPortCombo.Dock = DockStyle.Fill;
        settingsGrid.Controls.Add(_comPortCombo, 1, 0);

        settingsGrid.Controls.Add(CreateLabel("Baud"), 2, 0);
        _baudCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _baudCombo.Dock = DockStyle.Fill;
        foreach (var baud in BaudRates)
            _baudCombo.Items.Add(baud.ToString());
        settingsGrid.Controls.Add(_baudCombo, 3, 0);

        settingsGrid.Controls.Add(CreateLabel("Default port"), 4, 0);
        _defaultPortText.Dock = DockStyle.Fill;
        settingsGrid.Controls.Add(_defaultPortText, 5, 0);

        _refreshButton.Text = "Refresh";
        _refreshButton.AutoSize = true;
        _refreshButton.Margin = new Padding(8, 0, 0, 0);
        settingsGrid.Controls.Add(_refreshButton, 6, 0);

        _dtrCheck.Text = "DTR";
        _dtrCheck.AutoSize = true;
        _dtrCheck.Margin = new Padding(0, 10, 12, 0);
        settingsGrid.Controls.Add(_dtrCheck, 1, 1);

        _rtsCheck.Text = "RTS";
        _rtsCheck.AutoSize = true;
        _rtsCheck.Margin = new Padding(0, 10, 12, 0);
        settingsGrid.Controls.Add(_rtsCheck, 3, 1);

        _echoCheck.Text = "Echo commands back to terminal";
        _echoCheck.AutoSize = true;
        _echoCheck.Margin = new Padding(0, 10, 12, 0);
        settingsGrid.SetColumnSpan(_echoCheck, 3);
        settingsGrid.Controls.Add(_echoCheck, 4, 1);

        _lineStatusLabel.Text = "Line status: serial closed";
        _lineStatusLabel.AutoSize = true;
        _lineStatusLabel.Margin = new Padding(0, 10, 0, 0);
        settingsGrid.SetColumnSpan(_lineStatusLabel, 7);
        settingsGrid.Controls.Add(_lineStatusLabel, 0, 2);

        var hint = new Label
        {
            Text = "Tip: select the COM port for your USB serial adapter, then click Start Bridge and type AT on the retro computer terminal.",
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 0)
        };
        settingsGrid.SetColumnSpan(hint, 7);
        settingsGrid.Controls.Add(hint, 0, 3);

        var logGroup = new GroupBox
        {
            Text = "Live log",
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 0, 10)
        };
        root.Controls.Add(logGroup, 0, 3);

        _logBox.Multiline = true;
        _logBox.ReadOnly = true;
        _logBox.ScrollBars = ScrollBars.Vertical;
        _logBox.WordWrap = false;
        _logBox.Dock = DockStyle.Fill;
        _logBox.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);
        logGroup.Controls.Add(_logBox);

        var bottomPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1
        };
        bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        root.Controls.Add(bottomPanel, 0, 4);

        _startButton.Text = "Start Bridge";
        _startButton.Width = 110;
        _startButton.Height = 32;
        bottomPanel.Controls.Add(_startButton, 0, 0);

        _stopButton.Text = "Stop";
        _stopButton.Width = 100;
        _stopButton.Height = 32;
        _stopButton.Enabled = false;
        _stopButton.Margin = new Padding(8, 0, 0, 0);
        bottomPanel.Controls.Add(_stopButton, 1, 0);

        _clearLogButton.Text = "Clear log";
        _clearLogButton.Width = 100;
        _clearLogButton.Height = 32;
        _clearLogButton.Margin = new Padding(8, 0, 0, 0);
        bottomPanel.Controls.Add(_clearLogButton, 2, 0);

        _statusLabel.Text = "Stopped.";
        _statusLabel.AutoSize = true;
        _statusLabel.Anchor = AnchorStyles.Right;
        bottomPanel.Controls.Add(_statusLabel, 4, 0);
    }

    private static Label CreateLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(0, 4, 8, 0)
    };

    private void WireEvents()
    {
        _refreshButton.Click += (_, _) => RefreshComPorts();
        _startButton.Click += (_, _) => StartBridge();
        _stopButton.Click += (_, _) => StopBridge();
        _clearLogButton.Click += (_, _) => _logBox.Clear();
        _bridge.Log += AddLog;
        _bridge.StatusChanged += SetStatus;

        _lineStatusTimer.Interval = 750;
        _lineStatusTimer.Tick += (_, _) => _lineStatusLabel.Text = _bridge.GetLineStatusText();
        _lineStatusTimer.Start();

        FormClosing += (_, _) =>
        {
            SaveSettingsFromUi();
            _bridge.Dispose();
        };
    }

    private void RefreshComPorts()
    {
        var current = GetSelectedPortName() ?? _settings.ComPort;
        _comPortCombo.Items.Clear();

        foreach (var port in SerialPortDiscovery.GetPorts())
            _comPortCombo.Items.Add(port);

        SelectPort(current);

        if (_comPortCombo.SelectedIndex < 0 && _comPortCombo.Items.Count > 0)
            _comPortCombo.SelectedIndex = 0;

        AddLog("COM ports refreshed.");
    }

    private void ApplySettingsToUi()
    {
        SelectPort(_settings.ComPort);

        var baudText = _settings.BaudRate.ToString();
        _baudCombo.SelectedItem = _baudCombo.Items.Contains(baudText) ? baudText : "19200";
        _defaultPortText.Text = _settings.DefaultTcpPort.ToString();
        _dtrCheck.Checked = _settings.DtrEnable;
        _rtsCheck.Checked = _settings.RtsEnable;
        _echoCheck.Checked = _settings.EchoEnabled;
    }

    private void SaveSettingsFromUi()
    {
        _settings.ComPort = GetSelectedPortName();
        _settings.BaudRate = int.TryParse(_baudCombo.SelectedItem?.ToString(), out var baud) ? baud : 19200;
        _settings.DefaultTcpPort = int.TryParse(_defaultPortText.Text, out var port) ? port : 23;
        _settings.DtrEnable = _dtrCheck.Checked;
        _settings.RtsEnable = _rtsCheck.Checked;
        _settings.EchoEnabled = _echoCheck.Checked;
        _settings.Save();
    }

    private string? GetSelectedPortName()
    {
        return _comPortCombo.SelectedItem switch
        {
            SerialPortInfo info => info.PortName,
            string text when !string.IsNullOrWhiteSpace(text) => text.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(),
            _ => null
        };
    }

    private void SelectPort(string? portName)
    {
        if (string.IsNullOrWhiteSpace(portName))
            return;

        foreach (var item in _comPortCombo.Items)
        {
            if (item is SerialPortInfo info && string.Equals(info.PortName, portName, StringComparison.OrdinalIgnoreCase))
            {
                _comPortCombo.SelectedItem = item;
                return;
            }
        }
    }

    private void StartBridge()
    {
        var portName = GetSelectedPortName();
        if (string.IsNullOrWhiteSpace(portName))
        {
            MessageBox.Show(this, "Select a COM port first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!int.TryParse(_baudCombo.SelectedItem?.ToString(), out var baudRate))
        {
            MessageBox.Show(this, "Select a valid baud rate.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!int.TryParse(_defaultPortText.Text, out var defaultPort) || defaultPort < 1 || defaultPort > 65535)
        {
            MessageBox.Show(this, "Default TCP port must be between 1 and 65535.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            SaveSettingsFromUi();
            _bridge.DefaultTcpPort = defaultPort;
            _bridge.EchoEnabled = _echoCheck.Checked;
            _bridge.OpenSerial(portName, baudRate, _dtrCheck.Checked, _rtsCheck.Checked);

            SetControlsRunning(true);
        }
        catch (Exception ex)
        {
            AddLog("Start failed: " + ex.Message);
            MessageBox.Show(this, ex.Message, "Could not start bridge", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetControlsRunning(false);
        }
    }

    private void StopBridge()
    {
        _bridge.CloseSerial();
        SetControlsRunning(false);
    }

    private void SetControlsRunning(bool running)
    {
        _startButton.Enabled = !running;
        _stopButton.Enabled = running;
        _comPortCombo.Enabled = !running;
        _baudCombo.Enabled = !running;
        _defaultPortText.Enabled = !running;
        _dtrCheck.Enabled = !running;
        _rtsCheck.Enabled = !running;
        _echoCheck.Enabled = !running;
        _refreshButton.Enabled = !running;
    }

    private void AddLog(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(AddLog), message);
            return;
        }

        _logBox.AppendText(message + Environment.NewLine);
    }

    private void SetStatus(string status)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(SetStatus), status);
            return;
        }

        _statusLabel.Text = status;
    }
}
