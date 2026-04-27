using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace RetroModemBridge;

public sealed class MainForm : Form
{
    private readonly ComboBox _comPortCombo = new();
    private readonly ComboBox _baudCombo = new();
    private readonly TextBox _defaultPortText = new();
    private readonly CheckBox _dtrCheck = new();
    private readonly CheckBox _rtsCheck = new();
    private readonly CheckBox _echoCheck = new();
    private readonly CheckBox _telnetFilterCheck = new();
    private readonly CheckBox _rememberComPortCheck = new();
    private readonly CheckBox _startupSoundCheck = new();
    private readonly ToolTip _toolTips = new();
    private readonly Button _refreshButton = new();
    private readonly Button _startButton = new();
    private readonly Button _stopButton = new();
    private readonly Button _clearLogButton = new();
    private readonly Button _copyLogButton = new();
    private readonly Button _saveLogButton = new();
    private readonly Button _saveDirectoryButton = new();
    private readonly Button _addDirectoryButton = new();
    private readonly Button _deleteDirectoryButton = new();
    private readonly Button _editDirectoryButton = new();
    private readonly Button _copyDialCommandButton = new();
    private readonly Button _openTerminalButton = new();
    private readonly Button _importGuideButton = new();
    private readonly Button _importDirectoryButton = new();
    private readonly Button _exportDirectoryButton = new();
    private readonly TextBox _logBox = new();
    private readonly Label _statusLabel = new();
    private readonly Label _lineStatusLabel = new();
    private readonly Label _trafficLabel = new();
    private readonly Label _lastCommandLabel = new();
    private readonly Label _currentConnectionLabel = new();
    private readonly ModemLightsPanel _lightsPanel = new();
    private readonly DataGridView _directoryGrid = new();
    private readonly BindingList<BbsEntry> _directory = new();
    private readonly ModemBridge _bridge = new();
    private readonly AppSettings _settings;
    private readonly System.Windows.Forms.Timer _statusTimer = new();

    private static readonly int[] BaudRates =
    {
        300, 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200
    };

    public MainForm()
    {
        _settings = AppSettings.Load();
        foreach (var entry in _settings.DialDirectory)
            _directory.Add(entry);

        InitializeComponent();
        WireEvents();
        RefreshComPorts();
        ApplySettingsToUi();
        UpdateStatusDisplays();
    }


private void InitializeComponent()
{
    Text = "RetroModem Bridge v3 Beta";
    StartPosition = FormStartPosition.CenterScreen;
    MinimumSize = new Size(1180, 820);
    Size = new Size(1536, 966);
    Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
    BackColor = Color.FromArgb(247, 247, 248);

    var root = new TableLayoutPanel
    {
        Dock = DockStyle.Fill,
        ColumnCount = 1,
        RowCount = 4,
        Padding = new Padding(16),
        BackColor = Color.FromArgb(247, 247, 248)
    };
    root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
    root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    Controls.Add(root);

    root.Controls.Add(BuildHeaderPanel(), 0, 0);
    root.Controls.Add(BuildSettingsGroup(), 0, 1);

    var split = new SplitContainer
    {
        Dock = DockStyle.Fill,
        Orientation = Orientation.Vertical,
        Margin = new Padding(0, 0, 0, 10),
        BackColor = Color.FromArgb(247, 247, 248),
        BorderStyle = BorderStyle.None,
        FixedPanel = FixedPanel.None,
        SplitterWidth = 6
    };
    split.Panel1.BackColor = Color.FromArgb(247, 247, 248);
    split.Panel2.BackColor = Color.FromArgb(247, 247, 248);
    split.Panel1.Controls.Add(BuildDirectoryGroup());
    split.Panel2.Controls.Add(BuildLogGroup());
    root.Controls.Add(split, 0, 2);

    Shown += (_, _) => SafeSetSplitterDistance(split);
    split.SizeChanged += (_, _) => SafeSetSplitterDistance(split);

    root.Controls.Add(BuildBottomPanel(), 0, 3);
}



    private static void SafeSetSplitterDistance(SplitContainer split)
    {
        if (split.Width <= 100)
            return;

        // Do not set Panel1MinSize or Panel2MinSize during startup. On some systems,
        // WinForms applies those before the SplitContainer has a real width, which can
        // crash the app before the window appears.
        const int desiredPanel1Min = 420;
        const int desiredPanel2Min = 280;
        var max = split.Width - desiredPanel2Min - split.SplitterWidth;
        var min = Math.Min(desiredPanel1Min, Math.Max(0, split.Width - split.SplitterWidth - 80));

        if (max <= min)
        {
            var fallback = Math.Max(0, split.Width / 2);
            if (fallback > 0 && fallback < split.Width - split.SplitterWidth)
                split.SplitterDistance = fallback;
            return;
        }

        var preferred = (int)(split.Width * 0.58);
        var distance = Math.Max(min, Math.Min(preferred, max));

        if (distance > 0 && distance < split.Width - split.SplitterWidth && split.SplitterDistance != distance)
            split.SplitterDistance = distance;
    }

    private Control BuildHeaderPanel()
    {
        var card = CreateCardPanel();
        card.Margin = new Padding(0, 0, 0, 18);
        card.Padding = new Padding(20, 12, 20, 12);
        card.Height = 204;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.White
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));
        card.Controls.Add(layout);

        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 20, 0)
        };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.Controls.Add(left, 0, 0);

        var logoPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 0, 4)
        };

        var logoImage = LoadHeaderLogo();
        if (logoImage is not null)
        {
            var logoPicture = new PictureBox
            {
                Dock = DockStyle.Left,
                Width = 720,
                Image = logoImage,
                SizeMode = PictureBoxSizeMode.Zoom,
                Margin = new Padding(0),
                BackColor = Color.White
            };
            logoPanel.Controls.Add(logoPicture);
        }
        else
        {
            logoPanel.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                Text = "RetroModem Bridge",
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(Font.FontFamily, 28F, FontStyle.Bold),
                Margin = new Padding(0),
                BackColor = Color.White
            });
        }
        left.Controls.Add(logoPanel, 0, 0);

        _lightsPanel.Dock = DockStyle.Fill;
        _lightsPanel.MinimumSize = new Size(520, 32);
        _lightsPanel.Height = 32;
        _lightsPanel.Margin = new Padding(12, 0, 10, 0);
        _toolTips.SetToolTip(_lightsPanel,
            "Modem lights show what the bridge and serial port are doing.\n\n" +
            "DTR: vintage computer says it is ready.\n" +
            "RTS: vintage computer wants to send data.\n" +
            "CTS: serial adapter says it is okay to send.\n" +
            "DSR: serial device says it is ready.\n" +
            "DCD: carrier detected, usually means connected.\n" +
            "RX: data received.\n" +
            "TX: data sent.\n" +
            "ONLINE: connected to a BBS.");
        left.Controls.Add(_lightsPanel, 0, 1);

        var controls = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.White,
            Margin = new Padding(30, 24, 10, 0)
        };
        controls.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        controls.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        controls.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        controls.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var startStopPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 0, 16)
        };

        _startButton.Text = "▶  START BRIDGE";
        _startButton.Width = 220;
        _startButton.Height = 66;
        _startButton.Font = new Font(Font.FontFamily, 13F, FontStyle.Bold);
        _startButton.Margin = new Padding(0, 0, 18, 0);
        StylePrimaryButton(_startButton, Color.FromArgb(31, 185, 77), Color.FromArgb(26, 156, 65));

        _stopButton.Text = "■  STOP";
        _stopButton.Width = 160;
        _stopButton.Height = 66;
        _stopButton.Font = new Font(Font.FontFamily, 13F, FontStyle.Bold);
        _stopButton.Enabled = false;
        _stopButton.Margin = new Padding(0, 0, 0, 0);
        StyleSecondaryButton(_stopButton, Color.FromArgb(239, 239, 239), Color.FromArgb(175, 175, 175), Color.FromArgb(38, 38, 38));

        startStopPanel.Controls.Add(_startButton);
        startStopPanel.Controls.Add(_stopButton);
        controls.Controls.Add(startStopPanel, 0, 0);

        var headerHint = new Label
        {
            Text = "Start the bridge, then dial from the retro computer terminal.",
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(70, 70, 70),
            Margin = new Padding(4, 4, 0, 0),
            BackColor = Color.White
        };
        controls.Controls.Add(headerHint, 0, 1);

        var infoGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 4,
            Margin = new Padding(4, 16, 0, 0),
            BackColor = Color.White
        };
        infoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        infoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (var i = 0; i < 4; i++)
            infoGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        AddStatusRow(infoGrid, 0, "Line status", _lineStatusLabel);
        AddStatusRow(infoGrid, 1, "Traffic", _trafficLabel);
        AddStatusRow(infoGrid, 2, "Last command", _lastCommandLabel);
        AddStatusRow(infoGrid, 3, "Current connection", _currentConnectionLabel);
        controls.Controls.Add(infoGrid, 0, 2);

        layout.Controls.Add(controls, 1, 0);

        ApplyBridgeButtonColors(false);
        return card;
    }

    private static void AddStatusRow(TableLayoutPanel grid, int row, string title, Label valueLabel)
    {
        var titleLabel = new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(46, 46, 46),
            Margin = new Padding(0, 0, 12, 8),
            BackColor = Color.White
        };

        valueLabel.AutoSize = true;
        valueLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
        valueLabel.ForeColor = Color.FromArgb(70, 70, 70);
        valueLabel.Margin = new Padding(0, 0, 0, 8);
        valueLabel.BackColor = Color.White;

        grid.Controls.Add(titleLabel, 0, row);
        grid.Controls.Add(valueLabel, 1, row);
    }

    private static Image? LoadHeaderLogo()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Assets", "retromodem-bridge-logo.png"),
            Path.Combine(AppContext.BaseDirectory, "retromodem-bridge-logo.png"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets", "retromodem-bridge-logo.png")
        };

        foreach (var path in candidates)
        {
            if (!File.Exists(path))
                continue;

            using var image = Image.FromFile(path);
            return new Bitmap(image);
        }

        return null;
    }

    private static Control CreateHeaderInfoBlock(string title, Label valueLabel)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, 12, 0),
            BackColor = Color.White
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var titleLabel = new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(60, 60, 60),
            Margin = new Padding(0, 0, 0, 2),
            BackColor = Color.White
        };

        valueLabel.AutoSize = true;
        valueLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
        valueLabel.ForeColor = Color.FromArgb(75, 75, 75);
        valueLabel.Margin = new Padding(0, 0, 0, 0);
        valueLabel.BackColor = Color.White;

        panel.Controls.Add(titleLabel, 0, 0);
        panel.Controls.Add(valueLabel, 0, 1);
        return panel;
    }

    private void ConfigureOptionTooltips()
    {
        _toolTips.AutoPopDelay = 12000;
        _toolTips.InitialDelay = 400;
        _toolTips.ReshowDelay = 100;
        _toolTips.ShowAlways = true;

        _toolTips.SetToolTip(_dtrCheck,
            "DTR means Data Terminal Ready.\n\nThink of it as the vintage computer saying, \"I'm here and ready.\" Some modems and serial adapters expect this to be on.");

        _toolTips.SetToolTip(_rtsCheck,
            "RTS means Request To Send.\n\nThink of it as the vintage computer saying, \"I want to send data.\" Leave this on unless your serial setup acts weird.");

        _toolTips.SetToolTip(_echoCheck,
            "Echo means the bridge repeats what you type back to the terminal.\n\nTurn this on if your terminal does not show the AT commands you type. Leave it off if you see double letters.");

        _toolTips.SetToolTip(_telnetFilterCheck,
            "Telnet filter hides special internet/Telnet control codes from your vintage computer.\n\nThis helps prevent weird garbage characters or connection hangs on some BBSes.");
    }


    private Control BuildSettingsGroup()
    {
        var card = CreateCardPanel();
        card.Margin = new Padding(0, 0, 0, 18);
        card.Padding = new Padding(20, 16, 20, 16);
        card.Height = 206;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Color.White
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 63F));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 37F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        card.Controls.Add(root);

        var title = CreateSectionTitle("Serial setup");
        root.Controls.Add(title, 0, 0);
        root.SetColumnSpan(title, 2);

        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 2,
            Margin = new Padding(0, 6, 18, 0),
            BackColor = Color.White
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _comPortCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _comPortCombo.Dock = DockStyle.Top;
        StyleComboBox(_comPortCombo);
        _rememberComPortCheck.Text = "Remember COM port";
        _rememberComPortCheck.AutoSize = true;
        _rememberComPortCheck.Margin = new Padding(0, 10, 0, 0);
        StyleCheckBox(_rememberComPortCheck);
        fields.Controls.Add(CreateFieldPanel("COM port", _comPortCombo, _rememberComPortCheck), 0, 0);

        _baudCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _baudCombo.Dock = DockStyle.Top;
        StyleComboBox(_baudCombo);
        if (_baudCombo.Items.Count == 0)
        {
            foreach (var baud in BaudRates)
                _baudCombo.Items.Add(baud.ToString());
        }
        fields.Controls.Add(CreateFieldPanel("Baud", _baudCombo), 1, 0);

        _defaultPortText.Dock = DockStyle.Top;
        StyleTextInput(_defaultPortText, 36);
        fields.Controls.Add(CreateFieldPanel("Default port", _defaultPortText), 2, 0);

        var options = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0, 8, 0, 0),
            BackColor = Color.White
        };

        var optionsLabel = new Label
        {
            Text = "Options:",
            AutoSize = true,
            Margin = new Padding(0, 6, 14, 0),
            Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold),
            BackColor = Color.White
        };
        options.Controls.Add(optionsLabel);

        _dtrCheck.Text = "DTR";
        _rtsCheck.Text = "RTS";
        _echoCheck.Text = "Echo";
        _telnetFilterCheck.Text = "Telnet filter";
        foreach (var check in new[] { _dtrCheck, _rtsCheck, _echoCheck, _telnetFilterCheck })
        {
            check.AutoSize = true;
            check.Margin = new Padding(0, 4, 22, 0);
            StyleCheckBox(check);
            options.Controls.Add(check);
        }
        ConfigureOptionTooltips();
        fields.SetColumnSpan(options, 3);
        fields.Controls.Add(options, 0, 1);
        root.Controls.Add(fields, 0, 1);

        var hint = new Label
        {
            Text = "Dial examples:\nAT\nATDT darkrealms.ca:23\nATDT1 or ATDT coco\nATDL, ATH, AT&V",
            AutoSize = true,
            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
            ForeColor = Color.FromArgb(70, 70, 70),
            Margin = new Padding(16, 12, 0, 0),
            BackColor = Color.White
        };
        root.Controls.Add(hint, 1, 1);

        return card;
    }

    private Control BuildStatusGroup()
    {
        var statusGroup = new GroupBox
        {
            Text = "Modem lights",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 0, 10)
        };

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 5
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 142));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        statusGroup.Controls.Add(grid);

        grid.SetColumnSpan(_lightsPanel, 2);
        _lightsPanel.Dock = DockStyle.Fill;
        _lightsPanel.Margin = new Padding(0, 0, 0, 10);
        grid.Controls.Add(_lightsPanel, 0, 0);

        grid.Controls.Add(CreateLabel("Line status"), 0, 1);
        _lineStatusLabel.AutoSize = true;
        grid.Controls.Add(_lineStatusLabel, 1, 1);

        grid.Controls.Add(CreateLabel("Traffic"), 0, 2);
        _trafficLabel.AutoSize = true;
        grid.Controls.Add(_trafficLabel, 1, 2);

        grid.Controls.Add(CreateLabel("Last command"), 0, 3);
        _lastCommandLabel.AutoSize = true;
        grid.Controls.Add(_lastCommandLabel, 1, 3);

        grid.Controls.Add(CreateLabel("Current connection"), 0, 4);
        _currentConnectionLabel.AutoSize = true;
        grid.Controls.Add(_currentConnectionLabel, 1, 4);

        return statusGroup;
    }


private Control BuildDirectoryGroup()
{
    var card = CreateCardPanel();
    card.Padding = new Padding(10, 10, 10, 10);

    var root = new TableLayoutPanel
    {
        Dock = DockStyle.Fill,
        ColumnCount = 1,
        RowCount = 4,
        BackColor = Color.White
    };
    root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
    root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    card.Controls.Add(root);

    root.Controls.Add(CreateSectionTitle("BBS Directory / Dial Aliases"), 0, 0);

    var toolbar = new FlowLayoutPanel
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = true,
        Margin = new Padding(0, 6, 0, 8),
        BackColor = Color.White
    };

    _importGuideButton.Text = "📄  Import Guide";
    _addDirectoryButton.Text = "＋  Add";
    _editDirectoryButton.Text = "✎  Edit";
    _deleteDirectoryButton.Text = "🗑  Delete";
    _copyDialCommandButton.Text = "✆  Dial";
    _openTerminalButton.Text = "▣  Terminal";

    StylePrimaryButton(_importGuideButton, Color.FromArgb(15, 104, 211), Color.FromArgb(12, 88, 178), 158, 36, 9.5F);
    StyleSecondaryButton(_addDirectoryButton, Color.White, Color.FromArgb(185, 190, 198), Color.FromArgb(45, 45, 45), 82, 36, 9.5F);
    StyleSecondaryButton(_editDirectoryButton, Color.White, Color.FromArgb(185, 190, 198), Color.FromArgb(45, 45, 45), 82, 36, 9.5F);
    StyleSecondaryButton(_deleteDirectoryButton, Color.White, Color.FromArgb(185, 190, 198), Color.FromArgb(45, 45, 45), 96, 36, 9.5F);
    StyleSecondaryButton(_copyDialCommandButton, Color.White, Color.FromArgb(185, 190, 198), Color.FromArgb(45, 45, 45), 82, 36, 9.5F);
    StyleSecondaryButton(_openTerminalButton, Color.White, Color.FromArgb(15, 104, 211), Color.FromArgb(15, 104, 211), 112, 36, 9.5F);

    foreach (var button in new[] { _importGuideButton, _addDirectoryButton, _editDirectoryButton, _deleteDirectoryButton, _copyDialCommandButton, _openTerminalButton })
    {
        button.Margin = new Padding(0, 0, 8, 0);
        toolbar.Controls.Add(button);
    }
    root.Controls.Add(toolbar, 0, 1);

    _directoryGrid.Dock = DockStyle.Fill;
    _directoryGrid.AutoGenerateColumns = false;
    _directoryGrid.AllowUserToAddRows = false;
    _directoryGrid.AllowUserToDeleteRows = false;
    _directoryGrid.AllowUserToResizeRows = false;
    _directoryGrid.AllowUserToResizeColumns = true;
    _directoryGrid.MultiSelect = false;
    _directoryGrid.RowHeadersVisible = false;
    _directoryGrid.EnableHeadersVisualStyles = false;
    _directoryGrid.BackgroundColor = Color.White;
    _directoryGrid.BorderStyle = BorderStyle.FixedSingle;
    _directoryGrid.GridColor = Color.FromArgb(228, 232, 238);
    _directoryGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
    _directoryGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
    _directoryGrid.RowTemplate.Height = 36;
    _directoryGrid.ColumnHeadersHeight = 34;
    _directoryGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.White;
    _directoryGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(45, 45, 45);
    _directoryGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
    _directoryGrid.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
    _directoryGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(244, 247, 252);
    _directoryGrid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(45, 45, 45);
    _directoryGrid.DefaultCellStyle.BackColor = Color.White;
    _directoryGrid.DefaultCellStyle.ForeColor = Color.FromArgb(56, 56, 56);
    _directoryGrid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(252, 252, 253);
    _directoryGrid.DataSource = _directory;
    if (_directoryGrid.Columns.Count == 0)
    {
        _directoryGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "", Width = 36, ReadOnly = true, SortMode = DataGridViewColumnSortMode.NotSortable });
        _directoryGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BbsEntry.Alias), HeaderText = "Alias", Width = 100 });
        _directoryGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BbsEntry.Name), HeaderText = "Name", Width = 180 });
        _directoryGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BbsEntry.Host), HeaderText = "Host", Width = 245 });
        _directoryGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BbsEntry.Port), HeaderText = "Port", Width = 80 });
        _directoryGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BbsEntry.Notes), HeaderText = "Notes", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
    }
    _directoryGrid.CellFormatting -= DirectoryGridOnCellFormatting;
    _directoryGrid.CellFormatting += DirectoryGridOnCellFormatting;
    root.Controls.Add(_directoryGrid, 0, 2);

    var tip = new Label
    {
        Text = "💡  Tip: Use the BBS Directory to save aliases, then dial from the vintage computer with ATDT1 or ATDT coco.",
        AutoSize = true,
        Font = new Font("Segoe UI", 10F, FontStyle.Regular),
        ForeColor = Color.FromArgb(108, 108, 108),
        Margin = new Padding(0, 14, 0, 0),
        BackColor = Color.White
    };
    root.Controls.Add(tip, 0, 3);

    return card;
}


private Control BuildLogGroup()
{
    var card = CreateCardPanel();
    card.Padding = new Padding(18, 16, 18, 16);

    var root = new TableLayoutPanel
    {
        Dock = DockStyle.Fill,
        ColumnCount = 1,
        RowCount = 3,
        BackColor = Color.White
    };
    root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
    root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    card.Controls.Add(root);

    var header = new TableLayoutPanel
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        ColumnCount = 2,
        RowCount = 1,
        BackColor = Color.White
    };
    header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
    header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
    header.Controls.Add(CreateSectionTitle("Live log"), 0, 0);

    _clearLogButton.Text = "🗑  Clear";
    StyleSecondaryButton(_clearLogButton, Color.White, Color.FromArgb(185, 190, 198), Color.FromArgb(45, 45, 45), 112, 40, 10.5F);
    _clearLogButton.Margin = new Padding(0, 0, 0, 0);
    header.Controls.Add(_clearLogButton, 1, 0);
    root.Controls.Add(header, 0, 0);

    _logBox.Multiline = true;
    _logBox.ReadOnly = true;
    _logBox.ScrollBars = ScrollBars.Vertical;
    _logBox.WordWrap = false;
    _logBox.Dock = DockStyle.Fill;
    _logBox.Font = new Font("Consolas", 11F, FontStyle.Regular, GraphicsUnit.Point);
    _logBox.BackColor = Color.White;
    _logBox.BorderStyle = BorderStyle.FixedSingle;
    _logBox.Margin = new Padding(0, 10, 0, 12);
    root.Controls.Add(_logBox, 0, 1);

    var logButtons = new FlowLayoutPanel
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.LeftToRight,
        AutoSize = true,
        Margin = new Padding(0, 0, 0, 0),
        BackColor = Color.White
    };
    _copyLogButton.Text = "📋  Copy log";
    _saveLogButton.Text = "↓  Save log";
    StyleSecondaryButton(_copyLogButton, Color.White, Color.FromArgb(185, 190, 198), Color.FromArgb(45, 45, 45), 150, 54, 11F);
    StyleSecondaryButton(_saveLogButton, Color.White, Color.FromArgb(185, 190, 198), Color.FromArgb(45, 45, 45), 150, 54, 11F);
    logButtons.Controls.Add(_copyLogButton);
    logButtons.Controls.Add(_saveLogButton);
    root.Controls.Add(logButtons, 0, 2);

    return card;
}


private Control BuildBottomPanel()
{
    var bottomPanel = new TableLayoutPanel
    {
        Dock = DockStyle.Fill,
        ColumnCount = 2,
        RowCount = 1,
        BackColor = Color.FromArgb(247, 247, 248),
        Margin = new Padding(0)
    };
    bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
    bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

    _startupSoundCheck.Text = "Play startup sound";
    _startupSoundCheck.AutoSize = true;
    _startupSoundCheck.Anchor = AnchorStyles.Left;
    _startupSoundCheck.Margin = new Padding(4, 6, 0, 0);
    _startupSoundCheck.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
    _startupSoundCheck.ForeColor = Color.FromArgb(70, 70, 70);
    _startupSoundCheck.BackColor = Color.FromArgb(247, 247, 248);
    bottomPanel.Controls.Add(_startupSoundCheck, 0, 0);

    _statusLabel.Text = "Stopped.";
    _statusLabel.AutoSize = true;
    _statusLabel.Anchor = AnchorStyles.Right;
    _statusLabel.Margin = new Padding(8, 6, 0, 0);
    _statusLabel.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
    _statusLabel.ForeColor = Color.FromArgb(45, 45, 45);
    bottomPanel.Controls.Add(_statusLabel, 1, 0);
    return bottomPanel;
}

    private static Label CreateLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(0, 4, 8, 0)
    };



private static Control CreateFieldPanel(string labelText, Control field, Control? belowField = null)
{
    var panel = new TableLayoutPanel
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        ColumnCount = 1,
        RowCount = belowField is null ? 2 : 3,
        Margin = new Padding(0, 0, 18, 0),
        BackColor = Color.White
    };
    panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    if (belowField is not null)
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

    var label = new Label
    {
        Text = labelText,
        AutoSize = true,
        Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
        Margin = new Padding(0, 0, 0, 8),
        BackColor = Color.White
    };
    panel.Controls.Add(label, 0, 0);
    panel.Controls.Add(field, 0, 1);
    if (belowField is not null)
        panel.Controls.Add(belowField, 0, 2);
    return panel;
}

    private void WireEvents()
    {
        _startButton.Click += (_, _) => StartBridge();
        _stopButton.Click += (_, _) => StopBridge();
        _clearLogButton.Click += (_, _) => _logBox.Clear();
        _copyLogButton.Click += (_, _) => CopyLog();
        _saveLogButton.Click += (_, _) => SaveLog();
        _addDirectoryButton.Click += (_, _) => _directory.Add(new BbsEntry { Alias = NextAlias(), Name = "New BBS", Host = "example.com", Port = 23 });
        _deleteDirectoryButton.Click += (_, _) => DeleteSelectedDirectoryRows();
        _editDirectoryButton.Click += (_, _) => EditSelectedDirectoryCell();
        _saveDirectoryButton.Click += (_, _) => { SaveSettingsFromUi(); AddLog("Directory saved."); };
        _copyDialCommandButton.Click += (_, _) => CopyDialCommand();
        _openTerminalButton.Click += (_, _) => OpenSelectedInTerminal();
        _importGuideButton.Click += (_, _) => ImportFromTelnetBbsGuide();
        _importDirectoryButton.Click += (_, _) => ImportDirectory();
        _exportDirectoryButton.Click += (_, _) => ExportDirectory();
        _comPortCombo.SelectedIndexChanged += (_, _) => SaveComPortPreference();
        _rememberComPortCheck.CheckedChanged += (_, _) => SaveComPortPreference();
        _startupSoundCheck.CheckedChanged += (_, _) => SaveStartupSoundPreference();
        Shown += (_, _) => PlayStartupSoundIfEnabled();

        _bridge.Log += AddLog;
        _bridge.StatusChanged += SetStatus;
        _bridge.TrafficChanged += UpdateStatusDisplays;

        _statusTimer.Interval = 500;
        _statusTimer.Tick += (_, _) => UpdateStatusDisplays();
        _statusTimer.Start();

        FormClosing += (_, _) =>
        {
            SaveSettingsFromUi();
            _bridge.Dispose();
        };
    }

    private void RefreshComPorts()
    {
        var current = GetSelectedPortName() ?? (_settings.RememberComPort ? _settings.ComPort : null);
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
        _telnetFilterCheck.Checked = _settings.TelnetFilteringEnabled;
        _rememberComPortCheck.Checked = _settings.RememberComPort;
        _startupSoundCheck.Checked = _settings.PlayStartupSound;
    }

    private void SaveSettingsFromUi()
    {
        _directoryGrid.EndEdit();
        _settings.ComPort = _rememberComPortCheck.Checked ? GetSelectedPortName() : null;
        _settings.RememberComPort = _rememberComPortCheck.Checked;
        _settings.BaudRate = int.TryParse(_baudCombo.SelectedItem?.ToString(), out var baud) ? baud : 19200;
        _settings.DefaultTcpPort = int.TryParse(_defaultPortText.Text, out var port) ? port : 23;
        _settings.DtrEnable = _dtrCheck.Checked;
        _settings.RtsEnable = _rtsCheck.Checked;
        _settings.EchoEnabled = _echoCheck.Checked;
        _settings.TelnetFilteringEnabled = _telnetFilterCheck.Checked;
        _settings.PlayStartupSound = _startupSoundCheck.Checked;
        _settings.DialDirectory = GetCleanDirectory();
        _settings.Save();
    }

    private void SaveComPortPreference()
    {
        if (_rememberComPortCheck.Checked)
            _settings.ComPort = GetSelectedPortName();
        else
            _settings.ComPort = null;

        _settings.RememberComPort = _rememberComPortCheck.Checked;

        try
        {
            _settings.Save();
        }
        catch (Exception ex)
        {
            AddLog("Could not save COM port preference: " + ex.Message);
        }
    }

    private void SaveStartupSoundPreference()
    {
        _settings.PlayStartupSound = _startupSoundCheck.Checked;

        try
        {
            _settings.Save();
            AddLog("Startup sound preference saved: " + (_settings.PlayStartupSound ? "enabled" : "disabled"));
        }
        catch (Exception ex)
        {
            AddLog("Could not save startup sound preference: " + ex.Message);
        }
    }

    private void PlayStartupSoundIfEnabled()
    {
        if (!_startupSoundCheck.Checked)
            return;

        try
        {
            var soundPath = Path.Combine(AppContext.BaseDirectory, "Assets", "startup-modem.mp3");
            if (!File.Exists(soundPath))
            {
                AddLog("Startup sound file not found: " + soundPath);
                return;
            }

            // MCI is built into Windows and can play MP3 files without adding a large audio dependency.
            mciSendString("close RetroModemStartupSound", null, 0, IntPtr.Zero);
            var openCommand = $"open \"{soundPath}\" type mpegvideo alias RetroModemStartupSound";
            var openResult = mciSendString(openCommand, null, 0, IntPtr.Zero);
            if (openResult != 0)
            {
                AddLog("Could not open startup sound. MCI error: " + openResult);
                return;
            }

            var playResult = mciSendString("play RetroModemStartupSound from 0 notify", null, 0, Handle);
            if (playResult != 0)
                AddLog("Could not play startup sound. MCI error: " + playResult);
        }
        catch (Exception ex)
        {
            AddLog("Startup sound failed: " + ex.Message);
        }
    }

    private List<BbsEntry> GetCleanDirectory()
    {
        return _directory
            .Where(e => !string.IsNullOrWhiteSpace(e.Alias) && !string.IsNullOrWhiteSpace(e.Host))
            .Select(e => new BbsEntry
            {
                Alias = e.Alias.Trim(),
                Name = e.Name.Trim(),
                Host = e.Host.Trim(),
                Port = e.Port < 1 || e.Port > 65535 ? 23 : e.Port,
                Notes = e.Notes.Trim()
            })
            .ToList();
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
        if (_bridge.IsSerialOpen)
            return;

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
            _bridge.TelnetFilteringEnabled = _telnetFilterCheck.Checked;
            _bridge.DialDirectory = _settings.DialDirectory;
            _bridge.OpenSerial(portName, baudRate, _dtrCheck.Checked, _rtsCheck.Checked);
            SaveComPortPreference();
            AddLog($"Saved COM port preference: {(_rememberComPortCheck.Checked ? portName : "disabled")}");

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

    private static void ApplyRoundedButton(Button button)
    {
        if (button.Width <= 0 || button.Height <= 0)
            return;

        const int radius = 14;
        var bounds = new Rectangle(0, 0, button.Width, button.Height);
        var path = new GraphicsPath();

        path.AddArc(bounds.X, bounds.Y, radius, radius, 180, 90);
        path.AddArc(bounds.Right - radius, bounds.Y, radius, radius, 270, 90);
        path.AddArc(bounds.Right - radius, bounds.Bottom - radius, radius, radius, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - radius, radius, radius, 90, 90);
        path.CloseFigure();

        button.Region?.Dispose();
        button.Region = new Region(path);
        path.Dispose();
    }


private void ApplyBridgeButtonColors(bool running)
{
    if (running)
    {
        StylePrimaryButton(_startButton, Color.FromArgb(31, 185, 77), Color.FromArgb(26, 156, 65), 220, 66, 13F);
        _startButton.Text = "●  BRIDGE RUNNING";
        StyleDangerButton(_stopButton, 160, 66, 13F);
    }
    else
    {
        StylePrimaryButton(_startButton, Color.FromArgb(31, 185, 77), Color.FromArgb(26, 156, 65), 220, 66, 13F);
        _startButton.Text = "▶  START BRIDGE";
        StyleSecondaryButton(_stopButton, Color.FromArgb(239, 239, 239), Color.FromArgb(175, 175, 175), Color.FromArgb(35, 35, 35), 160, 66, 13F);
        _stopButton.Text = "■  STOP";
    }
}

    private void SetControlsRunning(bool running)
    {
        ApplyBridgeButtonColors(running);
        _startButton.Enabled = true;
        _stopButton.Enabled = running;
        _comPortCombo.Enabled = !running;
        _baudCombo.Enabled = !running;
        _defaultPortText.Enabled = !running;
        _dtrCheck.Enabled = !running;
        _rtsCheck.Enabled = !running;
        _echoCheck.Enabled = !running;
        _telnetFilterCheck.Enabled = !running;
        _rememberComPortCheck.Enabled = !running;
        _directoryGrid.ReadOnly = running;
        _addDirectoryButton.Enabled = !running;
        _deleteDirectoryButton.Enabled = !running;
        _editDirectoryButton.Enabled = !running;
        _saveDirectoryButton.Enabled = !running;
        _copyDialCommandButton.Enabled = true;
        _openTerminalButton.Enabled = true;
        _importGuideButton.Enabled = !running;
        _importDirectoryButton.Enabled = !running;
    }

    private void UpdateStatusDisplays()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(UpdateStatusDisplays));
            return;
        }

        _lineStatusLabel.Text = _bridge.GetLineStatusText();
        _trafficLabel.Text = _bridge.GetTrafficText();
        _lastCommandLabel.Text = string.IsNullOrWhiteSpace(_bridge.LastCommand) ? "None" : _bridge.LastCommand;
        _currentConnectionLabel.Text = string.IsNullOrWhiteSpace(_bridge.CurrentConnection) ? "None" : _bridge.CurrentConnection;

        var line = _bridge.GetLineStatusText();
        SetLight("DTR", line.Contains("DTR=On", StringComparison.OrdinalIgnoreCase));
        SetLight("RTS", line.Contains("RTS=On", StringComparison.OrdinalIgnoreCase));
        SetLight("CTS", line.Contains("CTS=On", StringComparison.OrdinalIgnoreCase));
        SetLight("DSR", line.Contains("DSR=On", StringComparison.OrdinalIgnoreCase));
        SetLight("DCD", _bridge.IsConnected || line.Contains("DCD=On", StringComparison.OrdinalIgnoreCase));
        SetLight("ONLINE", _bridge.IsConnected);

        var traffic = _bridge.GetTrafficText();
        SetLight("RX", traffic.Contains("Serial RX 0", StringComparison.OrdinalIgnoreCase) == false || traffic.Contains("TCP RX 0", StringComparison.OrdinalIgnoreCase) == false);
        SetLight("TX", traffic.Contains("Serial TX 0", StringComparison.OrdinalIgnoreCase) == false || traffic.Contains("TCP TX 0", StringComparison.OrdinalIgnoreCase) == false);
    }

    private void SetLight(string name, bool on)
    {
        _lightsPanel.SetLight(name, on);
    }

    private void OpenSelectedInTerminal()
    {
        if (_directoryGrid.CurrentRow?.DataBoundItem is not BbsEntry entry || string.IsNullOrWhiteSpace(entry.Host))
        {
            MessageBox.Show(this, "Select a BBS entry first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (entry.Port < 1 || entry.Port > 65535)
            entry.Port = 23;

        var form = new TerminalForm(entry);
        form.Show(this);
    }

    private void CopyDialCommand()
    {
        if (_directoryGrid.CurrentRow?.DataBoundItem is not BbsEntry entry || string.IsNullOrWhiteSpace(entry.Alias))
        {
            MessageBox.Show(this, "Select a BBS entry with an alias first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var command = "ATDT" + entry.Alias.Trim();
        Clipboard.SetText(command);
        AddLog("Copied dial command: " + command);
        MessageBox.Show(this, command + " copied. Type this on the vintage computer terminal to dial this saved BBS.", "Dial command copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void CopyLog()
    {
        if (!string.IsNullOrWhiteSpace(_logBox.Text))
            Clipboard.SetText(_logBox.Text);
    }

    private void SaveLog()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "Text files (*.txt)|*.txt|Log files (*.log)|*.log|All files (*.*)|*.*",
            FileName = "RetroModemBridge-v3-beta-log.txt"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
            File.WriteAllText(dialog.FileName, _logBox.Text);
    }

    private void EditSelectedDirectoryCell()
    {
        if (_directoryGrid.CurrentCell is null)
            return;

        _directoryGrid.Focus();
        _directoryGrid.BeginEdit(true);
    }

    private void DeleteSelectedDirectoryRows()
    {
        var entries = _directoryGrid.SelectedRows
            .Cast<DataGridViewRow>()
            .Select(row => row.DataBoundItem as BbsEntry)
            .Where(entry => entry is not null)
            .Cast<BbsEntry>()
            .Distinct()
            .ToList();

        if (entries.Count == 0 && _directoryGrid.CurrentRow?.DataBoundItem is BbsEntry currentEntry)
            entries.Add(currentEntry);

        if (entries.Count == 0)
            return;

        var label = entries.Count == 1
            ? $"Delete '{entries[0].Name}' from your BBS Directory?"
            : $"Delete {entries.Count} selected BBS entries from your BBS Directory?";

        var result = MessageBox.Show(
            this,
            label + "\n\nThis only removes the saved listing from your directory.",
            "Confirm delete",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (result != DialogResult.Yes)
            return;

        foreach (var entry in entries)
            _directory.Remove(entry);

        SaveSettingsFromUi();
        AddLog(entries.Count == 1 ? "Deleted BBS directory entry." : $"Deleted {entries.Count} BBS directory entries.");
    }

    private void ImportFromTelnetBbsGuide()
    {
        using var dialog = new BbsGuideImportForm();
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var added = 0;
        var skipped = 0;
        var existing = GetCleanDirectory()
            .Select(e => $"{e.Host.Trim().ToLowerInvariant()}:{e.Port}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var guideEntry in dialog.SelectedEntries)
        {
            var key = $"{guideEntry.Host.Trim().ToLowerInvariant()}:{guideEntry.Port}";
            if (existing.Contains(key))
            {
                skipped++;
                continue;
            }

            var entry = guideEntry.ToDirectoryEntry(NextAlias());
            _directory.Add(entry);
            existing.Add(key);
            added++;
        }

        SaveSettingsFromUi();
        AddLog($"Telnet BBS Guide import complete. Added {added}, skipped {skipped} duplicate(s).");
        MessageBox.Show(this, $"Added {added} BBS entr{(added == 1 ? "y" : "ies")} to your dial alias directory. Skipped {skipped} duplicate(s).", "Import complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ImportDirectory()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var json = File.ReadAllText(dialog.FileName);
        var entries = JsonSerializer.Deserialize<List<BbsEntry>>(json) ?? [];
        _directory.Clear();
        foreach (var entry in entries)
            _directory.Add(entry);

        AddLog("Directory imported from " + dialog.FileName);
    }

    private void ExportDirectory()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            FileName = "bbs-list-v3-beta.json"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var json = JsonSerializer.Serialize(GetCleanDirectory(), new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(dialog.FileName, json);
        AddLog("Directory exported to " + dialog.FileName);
    }

    private string NextAlias()
    {
        var used = _directory.Select(e => e.Alias).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < 100; i++)
        {
            var alias = i.ToString();
            if (!used.Contains(alias))
                return alias;
        }

        return DateTime.Now.ToString("HHmmss");
    }


private void AddLog(string message)
{
    if (InvokeRequired)
    {
        BeginInvoke(new Action<string>(AddLog), message);
        return;
    }

    var line = $"[{DateTime.Now:HH:mm:ss}]  {message}";
    _logBox.AppendText(line + Environment.NewLine);
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


private static Panel CreateCardPanel()
{
    var panel = new Panel
    {
        Dock = DockStyle.Fill,
        BackColor = Color.White,
        Margin = new Padding(0),
        Padding = new Padding(16)
    };
    panel.Resize += (_, _) => ApplyRoundedRegion(panel, 14);
    panel.Paint += (_, e) =>
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
        using var path = CreateRoundedRectanglePath(rect, 14);
        using var fill = new SolidBrush(Color.White);
        using var pen = new Pen(Color.FromArgb(226, 228, 232));
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(pen, path);
    };
    return panel;
}

private static Label CreateSectionTitle(string text) => new()
{
    Text = text,
    AutoSize = true,
    Font = new Font("Segoe UI", 16F, FontStyle.Bold),
    ForeColor = Color.FromArgb(34, 34, 34),
    BackColor = Color.White,
    Margin = new Padding(0)
};

private static void StyleTextInput(TextBox textBox, int height = 18)
{
    textBox.BorderStyle = BorderStyle.FixedSingle;
    textBox.Font = new Font("Segoe UI", 11F, FontStyle.Regular);
    textBox.Margin = new Padding(0);
    textBox.Height = height;
}

private static void StyleComboBox(ComboBox comboBox)
{
    comboBox.FlatStyle = FlatStyle.Standard;
    comboBox.Font = new Font("Segoe UI", 11F, FontStyle.Regular);
    comboBox.Height = 36;
    comboBox.Margin = new Padding(0);
    comboBox.BackColor = Color.White;
}

private static void StyleCheckBox(CheckBox checkBox)
{
    checkBox.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);
    checkBox.BackColor = Color.White;
}

private static void StylePrimaryButton(Button button, Color back, Color border, int width = 0, int height = 0, float fontSize = 11F)
{
    if (width > 0) button.Width = width;
    if (height > 0) button.Height = height;
    button.FlatStyle = FlatStyle.Flat;
    button.FlatAppearance.BorderSize = 1;
    button.FlatAppearance.BorderColor = border;
    button.BackColor = back;
    button.ForeColor = Color.White;
    button.Font = new Font("Segoe UI", fontSize, FontStyle.Bold);
    button.Margin = new Padding(0, 0, 14, 0);
    button.TextAlign = ContentAlignment.MiddleCenter;
    button.Region = null;
}

private static void StyleSecondaryButton(Button button, Color back, Color border, Color foreColor, int width = 0, int height = 0, float fontSize = 11F)
{
    if (width > 0) button.Width = width;
    if (height > 0) button.Height = height;
    button.FlatStyle = FlatStyle.Flat;
    button.FlatAppearance.BorderSize = 1;
    button.FlatAppearance.BorderColor = border;
    button.BackColor = back;
    button.ForeColor = foreColor;
    button.Font = new Font("Segoe UI", fontSize, FontStyle.Regular);
    button.Margin = new Padding(0, 0, 14, 0);
    button.TextAlign = ContentAlignment.MiddleCenter;
    button.Region = null;
}

private static void StyleDangerButton(Button button, int width = 0, int height = 0, float fontSize = 11F)
{
    StyleSecondaryButton(button, Color.FromArgb(239, 239, 239), Color.FromArgb(175, 175, 175), Color.FromArgb(35, 35, 35), width, height, fontSize);
}

private static void RoundedControlResize(object? sender, EventArgs e)
{
    if (sender is Control control)
        ApplyRoundedRegion(control, 10);
}

private static void ApplyRoundedRegion(Control control, int radius)
{
    if (control.Width < 4 || control.Height < 4)
        return;

    using var path = CreateRoundedRectanglePath(new Rectangle(0, 0, control.Width - 1, control.Height - 1), radius);
    control.Region?.Dispose();
    control.Region = new Region(path);
}

private static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
{
    var path = new GraphicsPath();

    if (bounds.Width < 4 || bounds.Height < 4)
    {
        path.AddRectangle(bounds);
        return path;
    }

    var maxRadius = Math.Max(1, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2));
    var diameter = maxRadius * 2;
    path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
    path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
    path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
    path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
    path.CloseFigure();
    return path;
}

private void DirectoryGridOnCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
{
    if (e.RowIndex < 0 || e.ColumnIndex != 0)
        return;

    e.Value = e.RowIndex == 0 ? "★" : "☆";
    e.FormattingApplied = true;
}
    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern int mciSendString(string command, string? returnValue, int returnLength, IntPtr winHandle);

}
