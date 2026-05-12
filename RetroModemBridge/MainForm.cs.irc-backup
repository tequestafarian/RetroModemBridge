using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.IO.Compression;
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
    private readonly Button _addDoorGameButton = new();
    private readonly Button _deleteDirectoryButton = new();
    private readonly Button _editDirectoryButton = new();
    private readonly Button _copyDialCommandButton = new();
    private readonly Button _favoriteButton = new();
    private readonly Button _testConnectionButton = new();
    private readonly Button _historyButton = new();
    private readonly Button _setupWizardButton = new();
    private readonly Button _openTerminalButton = new();
    private readonly Button _sessionMirrorButton = new();
    private readonly Button _moreDirectoryButton = new();
    private readonly Button _importGuideButton = new();
    private readonly Button _importDirectoryButton = new();
    private readonly Button _exportDirectoryButton = new();
    private readonly Button _testFavoritesButton = new();
    private readonly Button _supportBundleButton = new();
    private readonly Button _profilesButton = new();
    private readonly Button _updateGuideButton = new();
    private readonly Button _randomBbsButton = new();
    private readonly TextBox _directorySearchText = new();
    private readonly ComboBox _directoryFilterCombo = new();
    private readonly TextBox _logBox = new();
    private readonly Label _statusLabel = new();
    private readonly Label _lineStatusLabel = new();
    private readonly Label _trafficLabel = new();
    private readonly Label _lastCommandLabel = new();
    private readonly Label _currentConnectionLabel = new();
    private readonly ModemLightsPanel _lightsPanel = new();
    private readonly TabControl _directoryTabs = new();
    private readonly DataGridView _directoryGrid = new();
    private readonly DataGridView _doorGamesGrid = new();
    private readonly BindingList<BbsEntry> _directory = new();
    private readonly ModemBridge _bridge = new();
    private readonly AppSettings _settings;
    private readonly System.Windows.Forms.Timer _statusTimer = new();
    private bool _applyingSettingsToUi;

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
        ApplyDirectoryFilter();
        UpdateStatusDisplays();
    }


private void InitializeComponent()
{
    Text = "RetroModem Bridge v3.4";
    Icon = AppIconHelper.LoadAppIcon();
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
        card.Height = 254;

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
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
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
        _lightsPanel.MinimumSize = new Size(520, 84);
        _lightsPanel.Height = 84;
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
        RowCount = 5,
        BackColor = Color.White
    };
    root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
    root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    card.Controls.Add(root);

    root.Controls.Add(CreateSectionTitle("Dial Directory"), 0, 0);

    var toolbar = new FlowLayoutPanel
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = false,
        Margin = new Padding(0, 6, 0, 8),
        BackColor = Color.White
    };

    _importGuideButton.Text = "📄 Import Guide";
    _addDirectoryButton.Text = "＋ Add BBS";
    _addDoorGameButton.Text = "🎮 Add Door";
    _editDirectoryButton.Text = "✎ Edit";
    _deleteDirectoryButton.Text = "🗑 Delete";
    _copyDialCommandButton.Text = "✆ Dial";
    _favoriteButton.Text = "★ Favorite";
    _testConnectionButton.Text = "✓ Test";
    _sessionMirrorButton.Text = "▣ Mirror";
    _moreDirectoryButton.Text = "⋯ More";

    _historyButton.Text = "Dial History";
    _setupWizardButton.Text = "First Time Setup";
    _openTerminalButton.Text = "Open Selected in Terminal";
    _importDirectoryButton.Text = "Import Directory";
    _exportDirectoryButton.Text = "Export Directory";
    _testFavoritesButton.Text = "Test All Favorites";
    _supportBundleButton.Text = "Export Support Bundle";
    _profilesButton.Text = "Profiles";
    _updateGuideButton.Text = "Update / Merge BBS Guide";
    _randomBbsButton.Text = "Random Favorite";

    StylePrimaryButton(_importGuideButton, Color.FromArgb(15, 104, 211), Color.FromArgb(12, 88, 178), 128, 34, 9.3F);
    StyleSecondaryButton(_addDirectoryButton, Color.White, Color.FromArgb(185, 190, 198), Color.FromArgb(45, 45, 45), 78, 34, 9.3F);
    StyleSecondaryButton(_addDoorGameButton, Color.White, Color.FromArgb(185, 190, 198), Color.FromArgb(45, 45, 45), 112, 34, 9.3F);
    StyleSecondaryButton(_editDirectoryButton, Color.White, Color.FromArgb(185, 190, 198), Color.FromArgb(45, 45, 45), 78, 34, 9.3F);
    StyleSecondaryButton(_deleteDirectoryButton, Color.White, Color.FromArgb(185, 190, 198), Color.FromArgb(45, 45, 45), 92, 34, 9.3F);
    StyleSecondaryButton(_copyDialCommandButton, Color.White, Color.FromArgb(185, 190, 198), Color.FromArgb(45, 45, 45), 78, 34, 9.3F);
    StyleSecondaryButton(_favoriteButton, Color.White, Color.FromArgb(185, 190, 198), Color.FromArgb(45, 45, 45), 100, 34, 9.3F);
    StyleSecondaryButton(_testConnectionButton, Color.White, Color.FromArgb(185, 190, 198), Color.FromArgb(45, 45, 45), 78, 34, 9.3F);
    StylePrimaryButton(_sessionMirrorButton, Color.FromArgb(15, 104, 211), Color.FromArgb(12, 88, 178), 104, 34, 9.3F);
    StyleSecondaryButton(_moreDirectoryButton, Color.White, Color.FromArgb(185, 190, 198), Color.FromArgb(45, 45, 45), 82, 34, 9.3F);

    var moreMenu = new ContextMenuStrip();
    moreMenu.Items.Add("Add Local Door Game", null, (_, _) => _addDoorGameButton.PerformClick());
    moreMenu.Items.Add("Copy Dial Command", null, (_, _) => _copyDialCommandButton.PerformClick());
    moreMenu.Items.Add("Dial History", null, (_, _) => _historyButton.PerformClick());
    moreMenu.Items.Add("Test All Favorites", null, (_, _) => _testFavoritesButton.PerformClick());
    moreMenu.Items.Add("Random Favorite", null, (_, _) => _randomBbsButton.PerformClick());
    moreMenu.Items.Add(new ToolStripSeparator());
    moreMenu.Items.Add("Profiles", null, (_, _) => _profilesButton.PerformClick());
    moreMenu.Items.Add("First Time Setup", null, (_, _) => _setupWizardButton.PerformClick());
    moreMenu.Items.Add("Open Selected in Built-in Terminal", null, (_, _) => _openTerminalButton.PerformClick());
    moreMenu.Items.Add(new ToolStripSeparator());
    moreMenu.Items.Add("Update / Merge BBS Guide", null, (_, _) => _updateGuideButton.PerformClick());
    moreMenu.Items.Add("Import Directory", null, (_, _) => _importDirectoryButton.PerformClick());
    moreMenu.Items.Add("Export Directory", null, (_, _) => _exportDirectoryButton.PerformClick());
    moreMenu.Items.Add(new ToolStripSeparator());
    moreMenu.Items.Add("Export Support Bundle", null, (_, _) => _supportBundleButton.PerformClick());
    _moreDirectoryButton.Click += (_, _) => moreMenu.Show(_moreDirectoryButton, new Point(0, _moreDirectoryButton.Height));

    foreach (var button in new[] { _importGuideButton, _sessionMirrorButton, _addDirectoryButton, _addDoorGameButton, _editDirectoryButton, _deleteDirectoryButton, _favoriteButton, _testConnectionButton, _moreDirectoryButton })
    {
        button.Margin = new Padding(0, 0, 6, 0);
        toolbar.Controls.Add(button);
    }

    _toolTips.SetToolTip(_importGuideButton, "Import the bundled Telnet BBS Guide list into your BBS directory.");
    _toolTips.SetToolTip(_addDoorGameButton, "Add a local BBS door game. Example: dial ATDT USURPER from the CoCo.");
    _toolTips.SetToolTip(_sessionMirrorButton, "Open a live mirror of what the retro computer sees. You can optionally type into the active session.");
    _toolTips.SetToolTip(_moreDirectoryButton, "More directory tools, including import/export, setup, history, and the built-in terminal.");

    root.Controls.Add(toolbar, 0, 1);


    var filterPanel = new FlowLayoutPanel
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = false,
        Margin = new Padding(0, 0, 0, 8),
        BackColor = Color.White
    };

    var searchLabel = new Label
    {
        Text = "Search",
        AutoSize = true,
        Margin = new Padding(0, 7, 8, 0),
        ForeColor = Color.FromArgb(80, 80, 80),
        BackColor = Color.White
    };

    _directorySearchText.Width = 220;
    _directorySearchText.Height = 30;
    _directorySearchText.PlaceholderText = "name, alias, host, notes";

    _directoryFilterCombo.DropDownStyle = ComboBoxStyle.DropDownList;
    _directoryFilterCombo.Width = 170;
    _directoryFilterCombo.Items.AddRange(new object[]
    {
        "All BBSes",
        "Favorites only",
        "Online only",
        "Failed recently",
        "ANSI only",
        "Recently dialed"
    });
    _directoryFilterCombo.SelectedIndex = 0;

    filterPanel.Controls.Add(searchLabel);
    filterPanel.Controls.Add(_directorySearchText);
    filterPanel.Controls.Add(new Label { Text = "Filter", AutoSize = true, Margin = new Padding(12, 7, 8, 0), ForeColor = Color.FromArgb(80, 80, 80), BackColor = Color.White });
    filterPanel.Controls.Add(_directoryFilterCombo);
    root.Controls.Add(filterPanel, 0, 2);


    ConfigureDirectoryGrid(_directoryGrid, doorGameGrid: false);
    ConfigureDirectoryGrid(_doorGamesGrid, doorGameGrid: true);

    var bbsPage = new TabPage("BBS Directory") { BackColor = Color.White, Padding = new Padding(0) };
    var doorPage = new TabPage("Door Games") { BackColor = Color.White, Padding = new Padding(0) };
    bbsPage.Controls.Add(_directoryGrid);
    doorPage.Controls.Add(_doorGamesGrid);

    _directoryTabs.Dock = DockStyle.Fill;
    _directoryTabs.Margin = new Padding(0);
    _directoryTabs.TabPages.Clear();
    _directoryTabs.TabPages.Add(bbsPage);
    _directoryTabs.TabPages.Add(doorPage);
    _directoryTabs.SelectedIndexChanged += (_, _) => ApplyDirectoryFilter();
    root.Controls.Add(_directoryTabs, 0, 3);

    var tip = new Label
    {
        Text = "💡  Tip: BBS Directory and Door Games now have separate tabs. Dial either type from the vintage computer, like ATDT coco or ATDT USURPER.",
        AutoSize = true,
        Font = new Font("Segoe UI", 10F, FontStyle.Regular),
        ForeColor = Color.FromArgb(108, 108, 108),
        Margin = new Padding(0, 14, 0, 0),
        BackColor = Color.White
    };
    root.Controls.Add(tip, 0, 4);

    return card;
}


private void ConfigureDirectoryGrid(DataGridView grid, bool doorGameGrid)
{
    grid.Dock = DockStyle.Fill;
    grid.AutoGenerateColumns = false;
    grid.AllowUserToAddRows = false;
    grid.AllowUserToDeleteRows = false;
    grid.AllowUserToResizeRows = false;
    grid.AllowUserToResizeColumns = true;
    grid.MultiSelect = false;
    grid.RowHeadersVisible = false;
    grid.EnableHeadersVisualStyles = false;
    grid.BackgroundColor = Color.White;
    grid.BorderStyle = BorderStyle.FixedSingle;
    grid.GridColor = Color.FromArgb(228, 232, 238);
    grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
    grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
    grid.RowTemplate.Height = 36;
    grid.ColumnHeadersHeight = 34;
    grid.ColumnHeadersDefaultCellStyle.BackColor = Color.White;
    grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(45, 45, 45);
    grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
    grid.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
    grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(244, 247, 252);
    grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(45, 45, 45);
    grid.DefaultCellStyle.BackColor = Color.White;
    grid.DefaultCellStyle.ForeColor = Color.FromArgb(56, 56, 56);
    grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(252, 252, 253);
    grid.DataSource = _directory;

    if (grid.Columns.Count == 0)
    {
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "", Width = 36, ReadOnly = true, SortMode = DataGridViewColumnSortMode.NotSortable });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BbsEntry.Alias), HeaderText = "Alias", Width = 80 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BbsEntry.Name), HeaderText = "Name", Width = 170 });

        if (doorGameGrid)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BbsEntry.DoorExecutablePath), HeaderText = "Door EXE", Width = 260 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BbsEntry.DoorArguments), HeaderText = "Arguments", Width = 220 });
            grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(BbsEntry.DoorAutoEnterSingleKeys), HeaderText = "Auto Enter", Width = 90 });
            grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(BbsEntry.DoorPauseLongOutput), HeaderText = "More", Width = 60 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BbsEntry.DoorLinesPerPage), HeaderText = "Lines", Width = 60 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BbsEntry.LastResult), HeaderText = "Status", Width = 110, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BbsEntry.Notes), HeaderText = "Notes", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        }
        else
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BbsEntry.Host), HeaderText = "Host", Width = 210 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BbsEntry.Port), HeaderText = "Port", Width = 65 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BbsEntry.Category), HeaderText = "Category", Width = 120 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BbsEntry.SystemType), HeaderText = "System", Width = 100 });
            grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(BbsEntry.SupportsAnsi), HeaderText = "ANSI", Width = 55 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BbsEntry.LastResult), HeaderText = "Status", Width = 110, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BbsEntry.LastChecked), HeaderText = "Last checked", Width = 140, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BbsEntry.LastResponseMs), HeaderText = "ms", Width = 65, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BbsEntry.Notes), HeaderText = "Notes", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        }
    }

    grid.CellFormatting -= DirectoryGridOnCellFormatting;
    grid.CellFormatting += DirectoryGridOnCellFormatting;
}

private DataGridView ActiveDirectoryGrid => _directoryTabs.SelectedTab?.Text == "Door Games" ? _doorGamesGrid : _directoryGrid;

private IEnumerable<DataGridView> DirectoryGrids
{
    get
    {
        yield return _directoryGrid;
        yield return _doorGamesGrid;
    }
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
        _addDirectoryButton.Click += (_, _) => { _directory.Add(new BbsEntry { Alias = NextAlias(), Name = "New BBS", Host = "example.com", Port = 23, EntryType = "Telnet" }); ApplyDirectoryFilter(); };
        _addDoorGameButton.Click += (_, _) => ShowAddDoorGameDialog();
        _deleteDirectoryButton.Click += (_, _) => DeleteSelectedDirectoryRows();
        _editDirectoryButton.Click += (_, _) => EditSelectedDirectoryCell();
        _saveDirectoryButton.Click += (_, _) => { SaveSettingsFromUi(); AddLog("Directory saved."); };
        _copyDialCommandButton.Click += (_, _) => CopyDialCommand();
        _favoriteButton.Click += (_, _) => ToggleFavorite();
        _testConnectionButton.Click += async (_, _) => await TestSelectedConnectionAsync();
        _historyButton.Click += (_, _) => ShowDialHistory();
        _setupWizardButton.Click += (_, _) => ShowBeginnerSetupWizard();
        _openTerminalButton.Click += (_, _) => OpenSelectedInTerminal();
        _sessionMirrorButton.Click += (_, _) => ShowSessionMirror();
        _importGuideButton.Click += (_, _) => ImportFromTelnetBbsGuide();
        _importDirectoryButton.Click += (_, _) => ImportDirectory();
        _exportDirectoryButton.Click += (_, _) => ExportDirectory();
        _testFavoritesButton.Click += async (_, _) => await TestAllFavoritesAsync();
        _supportBundleButton.Click += (_, _) => ExportSupportBundle();
        _profilesButton.Click += (_, _) => ShowProfiles();
        _updateGuideButton.Click += async (_, _) => await UpdateBbsGuideFromAppAsync();
        _randomBbsButton.Click += (_, _) => SelectRandomFavorite();
        _directorySearchText.TextChanged += (_, _) => ApplyDirectoryFilter();
        _directoryFilterCombo.SelectedIndexChanged += (_, _) => ApplyDirectoryFilter();
        _comPortCombo.SelectedIndexChanged += (_, _) => SaveComPortPreference();
        _rememberComPortCheck.CheckedChanged += (_, _) => SaveComPortPreference();
        _startupSoundCheck.CheckedChanged += (_, _) => SaveStartupSoundPreference();
        Shown += (_, _) => PlayStartupSoundIfEnabled();

        _bridge.Log += AddLog;
        _bridge.StatusChanged += SetStatus;
        _bridge.TrafficChanged += UpdateStatusDisplays;
        _bridge.HistoryChanged += SaveHistoryFromBridge;
        _bridge.DirectoryChanged += SaveDirectoryFromBridge;

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
        _applyingSettingsToUi = true;
        try
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
        finally
        {
            _applyingSettingsToUi = false;
        }
    }

    private void SaveSettingsFromUi()
    {
        foreach (var grid in DirectoryGrids) grid.EndEdit();
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
        if (_applyingSettingsToUi)
            return;

        _settings.PlayStartupSound = _startupSoundCheck.Checked;

        try
        {
            _settings.Save();
            AddLog("Startup sound preference saved: " + (_settings.PlayStartupSound ? "enabled" : "disabled"));

            // Give the checkbox immediate feedback: when someone turns the
            // startup sound on, play it once right away.  Turning it off stays quiet.
            if (_settings.PlayStartupSound)
                PlayStartupSoundIfEnabled();
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
            .Where(e => !string.IsNullOrWhiteSpace(e.Alias) && (!string.IsNullOrWhiteSpace(e.Host) || e.IsDoorGame))
            .Select(e => new BbsEntry
            {
                Alias = e.Alias.Trim(),
                Name = e.Name.Trim(),
                Host = e.IsDoorGame && string.IsNullOrWhiteSpace(e.Host) ? "local-door" : e.Host.Trim(),
                Port = e.IsDoorGame ? 0 : (e.Port < 1 || e.Port > 65535 ? 23 : e.Port),
                EntryType = e.IsDoorGame ? "Door" : (string.IsNullOrWhiteSpace(e.EntryType) ? "Telnet" : e.EntryType.Trim()),
                Category = e.Category.Trim(),
                SystemType = e.SystemType.Trim(),
                SupportsAnsi = e.SupportsAnsi,
                IsFavorite = e.IsFavorite,
                LastDialed = e.LastDialed,
                LastResult = e.LastResult.Trim(),
                LastChecked = e.LastChecked,
                LastResponseMs = e.LastResponseMs,
                Notes = e.Notes.Trim(),
                DoorExecutablePath = e.DoorExecutablePath.Trim(),
                DoorWorkingDirectory = e.DoorWorkingDirectory.Trim(),
                DoorArguments = e.DoorArguments.Trim(),
                DoorDropFileType = string.IsNullOrWhiteSpace(e.DoorDropFileType) ? "DOOR32.SYS" : e.DoorDropFileType.Trim(),
                DoorNodeNumber = e.DoorNodeNumber < 1 ? 1 : e.DoorNodeNumber,
                DoorUserName = string.IsNullOrWhiteSpace(e.DoorUserName) ? "CoCo Caller" : e.DoorUserName.Trim(),
                DoorAutoEnterSingleKeys = e.DoorAutoEnterSingleKeys,
                DoorPauseLongOutput = e.DoorPauseLongOutput,
                DoorLinesPerPage = e.DoorLinesPerPage < 5 ? 21 : e.DoorLinesPerPage,
                DoorMorePrompt = string.IsNullOrWhiteSpace(e.DoorMorePrompt) ? "-- More -- Space/Enter=next, B=back -- " : e.DoorMorePrompt,
                DoorMorePromptRow = e.DoorMorePromptRow < 1 ? 24 : e.DoorMorePromptRow
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
            _bridge.DialDirectory = _directory;
            _bridge.DialHistory = _settings.DialHistory;
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
        foreach (var grid in DirectoryGrids) grid.ReadOnly = running;
        _addDirectoryButton.Enabled = !running;
        _addDoorGameButton.Enabled = !running;
        _deleteDirectoryButton.Enabled = !running;
        _editDirectoryButton.Enabled = !running;
        _saveDirectoryButton.Enabled = !running;
        _copyDialCommandButton.Enabled = true;
        _favoriteButton.Enabled = !running;
        _testConnectionButton.Enabled = true;
        _historyButton.Enabled = true;
        _setupWizardButton.Enabled = true;
        _openTerminalButton.Enabled = true;
        _importGuideButton.Enabled = !running;
        _importDirectoryButton.Enabled = !running;
        _testFavoritesButton.Enabled = true;
        _supportBundleButton.Enabled = true;
        _profilesButton.Enabled = !running;
        _updateGuideButton.Enabled = !running;
        _randomBbsButton.Enabled = true;
        _directorySearchText.Enabled = true;
        _directoryFilterCombo.Enabled = true;
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


    private void ApplyDirectoryFilter()
    {
        try
        {
            var search = _directorySearchText.Text.Trim();
            var filter = _directoryFilterCombo.SelectedItem?.ToString() ?? "All BBSes";

            foreach (var grid in DirectoryGrids)
            {
                var showDoors = ReferenceEquals(grid, _doorGamesGrid);
                CurrencyManager? manager = null;
                if (grid.BindingContext[_directory] is CurrencyManager cm)
                {
                    manager = cm;
                    manager.SuspendBinding();
                }

                foreach (DataGridViewRow row in grid.Rows)
                {
                    if (row.DataBoundItem is not BbsEntry entry)
                        continue;

                    var isCorrectTab = showDoors ? entry.IsDoorGame : !entry.IsDoorGame;
                    var matchesSearch = string.IsNullOrWhiteSpace(search) ||
                        entry.Alias.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        entry.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        entry.Host.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        entry.DoorExecutablePath.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        entry.DoorArguments.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        entry.Category.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        entry.SystemType.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        entry.Notes.Contains(search, StringComparison.OrdinalIgnoreCase);

                    var matchesFilter = filter switch
                    {
                        "Favorites only" => entry.IsFavorite,
                        "Online only" => entry.LastResult.Contains("Online", StringComparison.OrdinalIgnoreCase) || entry.LastResult.Contains("Connected", StringComparison.OrdinalIgnoreCase) || entry.LastResult.Contains("Door ready", StringComparison.OrdinalIgnoreCase),
                        "Failed recently" => !string.IsNullOrWhiteSpace(entry.LastResult) && !entry.LastResult.Contains("Online", StringComparison.OrdinalIgnoreCase) && !entry.LastResult.Contains("Connected", StringComparison.OrdinalIgnoreCase) && !entry.LastResult.Contains("Door ready", StringComparison.OrdinalIgnoreCase),
                        "ANSI only" => entry.SupportsAnsi,
                        "Recently dialed" => entry.LastDialed is not null,
                        _ => true
                    };

                    row.Visible = isCorrectTab && matchesSearch && matchesFilter;
                }

                manager?.ResumeBinding();
            }
        }
        catch
        {
            // Filtering is a convenience feature. Do not let it interfere with bridge use.
        }
    }


    private void ShowAddDoorGameDialog() => ShowDoorGameDialog(null);

    private void ShowDoorGameDialog(BbsEntry? existingEntry)
    {
        var editing = existingEntry is not null;
        using var form = new Form
        {
            Text = editing ? "Edit Local Door Game" : "Add Local Door Game",
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(800, 620),
            MinimumSize = new Size(760, 560),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point)
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 14,
            Padding = new Padding(16)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        form.Controls.Add(root);

        static Label L(string text) => new() { Text = text, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 8, 0) };
        TextBox aliasBox = new() { Text = existingEntry?.Alias ?? "USURPER", Dock = DockStyle.Fill };
        TextBox nameBox = new() { Text = existingEntry?.Name ?? "Usurper Reborn", Dock = DockStyle.Fill };
        TextBox exeBox = new() { Text = existingEntry?.DoorExecutablePath ?? string.Empty, Dock = DockStyle.Fill };
        TextBox workBox = new() { Text = existingEntry?.DoorWorkingDirectory ?? string.Empty, Dock = DockStyle.Fill };
        TextBox argsBox = new() { Text = string.IsNullOrWhiteSpace(existingEntry?.DoorArguments) ? "--door32 {door32} --stdio" : existingEntry!.DoorArguments, Dock = DockStyle.Fill, PlaceholderText = "Example: --door32 {door32} --stdio" };
        NumericUpDown nodeBox = new() { Minimum = 1, Maximum = 99, Value = Math.Max(1, Math.Min(99, existingEntry?.DoorNodeNumber ?? 1)), Width = 80 };
        TextBox userBox = new() { Text = string.IsNullOrWhiteSpace(existingEntry?.DoorUserName) ? "CoCo Caller" : existingEntry!.DoorUserName, Dock = DockStyle.Fill };
        CheckBox autoEnterBox = new() { Text = "Auto-Enter after single keys", Checked = existingEntry?.DoorAutoEnterSingleKeys ?? true, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 0, 0) };
        CheckBox pagingBox = new() { Text = "Pause long output with More prompt", Checked = existingEntry?.DoorPauseLongOutput ?? true, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 0, 0) };
        NumericUpDown linesBox = new() { Minimum = 5, Maximum = 60, Value = Math.Max(5, Math.Min(60, existingEntry?.DoorLinesPerPage ?? 21)), Width = 80 };
        TextBox morePromptBox = new() { Text = string.IsNullOrWhiteSpace(existingEntry?.DoorMorePrompt) ? "-- More -- Space/Enter=next, B=back -- " : existingEntry!.DoorMorePrompt, Dock = DockStyle.Fill };
        NumericUpDown promptRowBox = new() { Minimum = 1, Maximum = 60, Value = Math.Max(1, Math.Min(60, existingEntry?.DoorMorePromptRow ?? 24)), Width = 80 };
        TextBox notesBox = new() { Text = existingEntry?.Notes ?? "Local door game. Dial with ATDT USURPER.", Dock = DockStyle.Fill };

        Button browseExe = new() { Text = "Browse...", AutoSize = true };
        browseExe.Click += (_, _) =>
        {
            using var picker = new OpenFileDialog { Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*", Title = "Select door game executable" };
            if (picker.ShowDialog(form) == DialogResult.OK)
            {
                exeBox.Text = picker.FileName;
                if (string.IsNullOrWhiteSpace(workBox.Text))
                    workBox.Text = Path.GetDirectoryName(picker.FileName) ?? string.Empty;
            }
        };

        Button browseWork = new() { Text = "Browse...", AutoSize = true };
        browseWork.Click += (_, _) =>
        {
            using var picker = new FolderBrowserDialog { Description = "Select door game working folder" };
            if (picker.ShowDialog(form) == DialogResult.OK)
                workBox.Text = picker.SelectedPath;
        };

        root.Controls.Add(L("Alias"), 0, 0); root.Controls.Add(aliasBox, 1, 0);
        root.Controls.Add(L("Display name"), 0, 1); root.Controls.Add(nameBox, 1, 1);
        root.Controls.Add(L("Door EXE"), 0, 2); root.Controls.Add(exeBox, 1, 2); root.Controls.Add(browseExe, 2, 2);
        root.Controls.Add(L("Working folder"), 0, 3); root.Controls.Add(workBox, 1, 3); root.Controls.Add(browseWork, 2, 3);
        root.Controls.Add(L("Arguments"), 0, 4); root.Controls.Add(argsBox, 1, 4); root.SetColumnSpan(argsBox, 2);
        root.Controls.Add(L("Node"), 0, 5); root.Controls.Add(nodeBox, 1, 5);
        root.Controls.Add(L("Door user"), 0, 6); root.Controls.Add(userBox, 1, 6); root.SetColumnSpan(userBox, 2);
        root.Controls.Add(L("Input assist"), 0, 7); root.Controls.Add(autoEnterBox, 1, 7); root.SetColumnSpan(autoEnterBox, 2);
        root.Controls.Add(L("Output paging"), 0, 8); root.Controls.Add(pagingBox, 1, 8); root.SetColumnSpan(pagingBox, 2);
        root.Controls.Add(L("Lines per page"), 0, 9); root.Controls.Add(linesBox, 1, 9);
        root.Controls.Add(L("More prompt"), 0, 10); root.Controls.Add(morePromptBox, 1, 10); root.SetColumnSpan(morePromptBox, 2);
        root.Controls.Add(L("Prompt row"), 0, 11); root.Controls.Add(promptRowBox, 1, 11);
        root.Controls.Add(L("Notes"), 0, 12); root.Controls.Add(notesBox, 1, 12); root.SetColumnSpan(notesBox, 2);

        var help = new Label
        {
            Text = "RetroModem Bridge creates DOOR32.SYS automatically. For Usurper Reborn, use: --door32 {door32} --stdio. Output paging pauses long text. Prompt row controls where the temporary More prompt appears. For a 24-line terminal, use row 24.",
            AutoSize = true,
            MaximumSize = new Size(690, 0),
            ForeColor = Color.FromArgb(85, 85, 85),
            Margin = new Padding(0, 12, 0, 8)
        };
        root.Controls.Add(help, 0, 13); root.SetColumnSpan(help, 3);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(0, 0, 16, 16) };
        var save = new Button { Text = editing ? "Save Changes" : "Save Door", DialogResult = DialogResult.OK, Width = 120 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90 };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        form.Controls.Add(buttons);
        form.AcceptButton = save;
        form.CancelButton = cancel;

        if (form.ShowDialog(this) != DialogResult.OK)
            return;

        var alias = aliasBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(alias))
            alias = NextAlias();

        var target = existingEntry ?? new BbsEntry();
        target.Alias = alias;
        target.Name = string.IsNullOrWhiteSpace(nameBox.Text) ? alias : nameBox.Text.Trim();
        target.Host = "local-door";
        target.Port = 0;
        target.EntryType = "Door";
        target.Category = "Door Games";
        target.SystemType = "Local Door";
        target.SupportsAnsi = true;
        target.IsFavorite = true;
        target.DoorExecutablePath = exeBox.Text.Trim();
        target.DoorWorkingDirectory = workBox.Text.Trim();
        target.DoorArguments = argsBox.Text.Trim();
        target.DoorDropFileType = "DOOR32.SYS";
        target.DoorNodeNumber = (int)nodeBox.Value;
        target.DoorUserName = string.IsNullOrWhiteSpace(userBox.Text) ? "CoCo Caller" : userBox.Text.Trim();
        target.DoorAutoEnterSingleKeys = autoEnterBox.Checked;
        target.DoorPauseLongOutput = pagingBox.Checked;
        target.DoorLinesPerPage = (int)linesBox.Value;
        target.DoorMorePrompt = string.IsNullOrWhiteSpace(morePromptBox.Text) ? "-- More -- Space/Enter=next, B=back -- " : morePromptBox.Text;
        target.DoorMorePromptRow = (int)promptRowBox.Value;
        target.Notes = notesBox.Text.Trim();

        if (!editing)
            _directory.Add(target);

        foreach (var grid in DirectoryGrids) grid.Refresh();
        ApplyDirectoryFilter();
        SaveSettingsFromUi();
        AddLog((editing ? "Updated" : "Added") + " local door game alias: ATDT " + alias);
    }

    private void ShowSessionMirror()
    {
        var form = new SessionMirrorForm(_bridge);
        form.Show(this);
    }

    private void OpenSelectedInTerminal()
    {
        if (ActiveDirectoryGrid.CurrentRow?.DataBoundItem is not BbsEntry entry || string.IsNullOrWhiteSpace(entry.Host))
        {
            MessageBox.Show(this, "Select a directory entry first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (entry.IsDoorGame)
        {
            MessageBox.Show(this, "Local door games launch from your retro computer with ATDT " + entry.Alias + ". The built-in terminal is for telnet BBS entries only.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (entry.Port < 1 || entry.Port > 65535)
            entry.Port = 23;

        var form = new TerminalForm(entry);
        form.Show(this);
    }

    private void ToggleFavorite()
    {
        if (ActiveDirectoryGrid.CurrentRow?.DataBoundItem is not BbsEntry entry)
        {
            MessageBox.Show(this, "Select a directory entry first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        entry.IsFavorite = !entry.IsFavorite;
        foreach (var grid in DirectoryGrids) grid.Refresh();
        SaveSettingsFromUi();
        AddLog((entry.IsFavorite ? "Added favorite: " : "Removed favorite: ") + entry.DisplayName);
    }

    private async Task TestSelectedConnectionAsync()
    {
        if (ActiveDirectoryGrid.CurrentRow?.DataBoundItem is not BbsEntry entry || string.IsNullOrWhiteSpace(entry.Host))
        {
            MessageBox.Show(this, "Select a directory entry first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (entry.IsDoorGame)
        {
            var ok = !string.IsNullOrWhiteSpace(entry.DoorExecutablePath) && File.Exists(entry.DoorExecutablePath);
            entry.LastChecked = DateTime.Now;
            entry.LastResult = ok ? "Door ready" : "Door EXE missing";
            foreach (var grid in DirectoryGrids) grid.Refresh();
            SaveSettingsFromUi();
            MessageBox.Show(this, ok ? "Door executable found. Dial it from the retro computer with ATDT " + entry.Alias + "." : "Door executable is missing. Edit the Door EXE path for this entry.", "Door game test", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            return;
        }

        await TestOneEntryAsync(entry, showMessage: true);
    }

    private async Task TestOneEntryAsync(BbsEntry entry, bool showMessage)
    {
        var port = entry.Port < 1 || entry.Port > 65535 ? 23 : entry.Port;
        AddLog($"Testing {entry.Host}:{port}...");
        _testConnectionButton.Enabled = false;

        try
        {
            var result = await ConnectionTester.TestDetailedAsync(entry.Host, port);
            entry.LastResult = result.Result;
            entry.LastChecked = result.CheckedAt;
            entry.LastResponseMs = result.ResponseMs;
            foreach (var grid in DirectoryGrids) grid.Refresh();
            ApplyDirectoryFilter();
            SaveSettingsFromUi();

            var ms = result.ResponseMs is null ? "" : $" ({result.ResponseMs} ms)";
            AddLog($"Connection test for {entry.Host}:{port}: {result.Result}{ms}");

            if (showMessage)
                MessageBox.Show(this, $"{entry.DisplayName}\n{entry.Host}:{port}\n\nResult: {result.Result}{ms}", "Connection test", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        finally
        {
            _testConnectionButton.Enabled = true;
        }
    }

    private async Task TestAllFavoritesAsync()
    {
        var favorites = _directory.Where(e => e.IsFavorite && !e.IsDoorGame && !string.IsNullOrWhiteSpace(e.Host)).ToList();
        if (favorites.Count == 0)
        {
            MessageBox.Show(this, "No favorites to test yet. Mark some BBSes as favorites first.", "Test favorites", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(this, $"Test {favorites.Count} favorite BBS entr{(favorites.Count == 1 ? "y" : "ies")} now?", "Test all favorites", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes)
            return;

        _testConnectionButton.Enabled = false;
        _testFavoritesButton.Enabled = false;

        try
        {
            foreach (var entry in favorites)
                await TestOneEntryAsync(entry, showMessage: false);

            SaveSettingsFromUi();
            MessageBox.Show(this, $"Finished testing {favorites.Count} favorite BBS entr{(favorites.Count == 1 ? "y" : "ies")}.", "Test all favorites", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        finally
        {
            _testConnectionButton.Enabled = true;
            _testFavoritesButton.Enabled = true;
        }
    }

    private void ShowDialHistory()
    {
        var form = new Form
        {
            Text = "Dial History",
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(820, 460),
            Font = Font
        };

        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = Color.White
        };

        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DialHistoryEntry.DialedAt), HeaderText = "Time", Width = 150 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DialHistoryEntry.DialedText), HeaderText = "Dialed", Width = 140 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DialHistoryEntry.Target), HeaderText = "Target", Width = 210 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DialHistoryEntry.Result), HeaderText = "Result", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DialHistoryEntry.DurationSeconds), HeaderText = "Seconds", Width = 80 });
        grid.DataSource = new BindingList<DialHistoryEntry>(_settings.DialHistory.Take(250).ToList());

        form.Controls.Add(grid);
        form.ShowDialog(this);
    }

    private void SaveHistoryFromBridge()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(SaveHistoryFromBridge));
            return;
        }

        try
        {
            _settings.Save();
        }
        catch (Exception ex)
        {
            AddLog("Could not save dial history: " + ex.Message);
        }
    }

    private void SaveDirectoryFromBridge()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(SaveDirectoryFromBridge));
            return;
        }

        try
        {
             foreach (var grid in DirectoryGrids) grid.Refresh();
            _settings.DialDirectory = GetCleanDirectory();
            _settings.Save();
        }
        catch (Exception ex)
        {
            AddLog("Could not save BBS directory status: " + ex.Message);
        }
    }

    private void ShowBeginnerSetupWizard()
    {
        var message =
            "RetroModem Bridge first-time setup checklist\n\n" +
            "1. Connect your retro computer serial cable to this Windows PC.\n" +
            "2. Choose the matching COM port.\n" +
            "3. Pick a baud rate. For a CoCo 3 with NetMate, try 19200. If that fails, try 9600.\n" +
            "4. Leave DTR and RTS on unless your adapter behaves strangely.\n" +
            "5. Leave Telnet Filter on for most Telnet BBSes.\n" +
            "6. Click Start Bridge.\n" +
            "7. On the retro terminal, type AT and press Enter. You should see OK.\n" +
            "8. Click Import Guide to add BBSes, then mark a few as Favorites.\n" +
            "9. Use Test or Test All Favorites to find working boards.\n" +
            "10. Try ATDT MENU from the retro computer.\n\n" +
            "Useful commands:\n" +
            "ATDT HELP, ATDT TIME, ATDT MENU, ATDT NEWS, ATDT RANDOM, ATDT FAVORITES\n\n" +
            "Tip: Open Mirror to see the session from Windows. Enable Input if you want to type from the app.";

        MessageBox.Show(this, message, "First Time Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SelectRandomFavorite()
    {
        var candidates = _directory
            .Where(e => e.IsFavorite && !e.IsDoorGame && !string.IsNullOrWhiteSpace(e.Host))
            .ToList();

        if (candidates.Count == 0)
        {
            MessageBox.Show(this, "No favorite BBSes yet. Mark a few entries as favorites first.", "Random favorite", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var entry = candidates[Random.Shared.Next(candidates.Count)];
        foreach (DataGridViewRow row in _directoryGrid.Rows)
        {
            if (ReferenceEquals(row.DataBoundItem, entry))
            {
                row.Visible = true;
                row.Selected = true;
                _directoryGrid.CurrentCell = row.Cells[Math.Min(1, row.Cells.Count - 1)];
                break;
            }
        }

        var command = "ATDT" + entry.Alias.Trim();
        Clipboard.SetText(command);
        AddLog("Random favorite selected: " + entry.DisplayName + " (" + command + ")");
        MessageBox.Show(this, $"{entry.DisplayName}\n{entry.Host}:{entry.Port}\n\nCopied dial command: {command}", "Random favorite", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ShowProfiles()
    {
        using var form = new Form
        {
            Text = "Retro Computer Profiles",
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(860, 460),
            Font = Font
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        form.Controls.Add(root);

        var help = new Label
        {
            Text = "Profiles save common serial settings for different retro computers or terminal programs.",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        };
        root.Controls.Add(help, 0, 0);

        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = true,
            AllowUserToDeleteRows = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = Color.White
        };

        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(RetroComputerProfile.Name), HeaderText = "Profile", Width = 180 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(RetroComputerProfile.ComPort), HeaderText = "COM", Width = 90 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(RetroComputerProfile.BaudRate), HeaderText = "Baud", Width = 90 });
        grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(RetroComputerProfile.DtrEnable), HeaderText = "DTR", Width = 50 });
        grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(RetroComputerProfile.RtsEnable), HeaderText = "RTS", Width = 50 });
        grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(RetroComputerProfile.EchoEnabled), HeaderText = "Echo", Width = 60 });
        grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(RetroComputerProfile.TelnetFilteringEnabled), HeaderText = "Telnet", Width = 70 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(RetroComputerProfile.Notes), HeaderText = "Notes", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

        var profiles = new BindingList<RetroComputerProfile>(_settings.Profiles);
        grid.DataSource = profiles;
        root.Controls.Add(grid, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0, 10, 0, 0)
        };

        var closeButton = new Button { Text = "Close", Width = 90, Height = 34 };
        var applyButton = new Button { Text = "Apply Selected", Width = 130, Height = 34 };
        var saveCurrentButton = new Button { Text = "Save Current as Profile", Width = 170, Height = 34 };

        closeButton.Click += (_, _) => form.Close();

        saveCurrentButton.Click += (_, _) =>
        {
            var profile = new RetroComputerProfile
            {
                Name = "New profile " + DateTime.Now.ToString("HHmm"),
                ComPort = GetSelectedPortName(),
                BaudRate = int.TryParse(_baudCombo.SelectedItem?.ToString(), out var baud) ? baud : 19200,
                DtrEnable = _dtrCheck.Checked,
                RtsEnable = _rtsCheck.Checked,
                EchoEnabled = _echoCheck.Checked,
                TelnetFilteringEnabled = _telnetFilterCheck.Checked,
                Notes = "Saved from current settings."
            };

            profiles.Add(profile);
            grid.Refresh();
        };

        applyButton.Click += (_, _) =>
        {
            if (grid.CurrentRow?.DataBoundItem is not RetroComputerProfile profile)
                return;

            SelectPort(profile.ComPort);
            _baudCombo.SelectedItem = profile.BaudRate.ToString();
            _dtrCheck.Checked = profile.DtrEnable;
            _rtsCheck.Checked = profile.RtsEnable;
            _echoCheck.Checked = profile.EchoEnabled;
            _telnetFilterCheck.Checked = profile.TelnetFilteringEnabled;
            AddLog("Applied profile: " + profile.Name);
            SaveSettingsFromUi();
            MessageBox.Show(this, "Applied profile: " + profile.Name, "Profiles", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        buttons.Controls.Add(closeButton);
        buttons.Controls.Add(applyButton);
        buttons.Controls.Add(saveCurrentButton);
        root.Controls.Add(buttons, 0, 2);

        form.FormClosing += (_, _) =>
        {
            _settings.Profiles = profiles.ToList();
            _settings.Save();
        };

        form.ShowDialog(this);
    }

    private void ExportSupportBundle()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "ZIP files (*.zip)|*.zip|All files (*.*)|*.*",
            FileName = $"RetroModemBridge-support-{DateTime.Now:yyyyMMdd-HHmmss}.zip"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "RetroModemBridgeSupport-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            var info = new
            {
                App = "RetroModem Bridge",
                Version = "v3.4",
                GeneratedAt = DateTime.Now,
                OS = Environment.OSVersion.ToString(),
                Is64BitOS = Environment.Is64BitOperatingSystem,
                DotNet = Environment.Version.ToString(),
                BaseDirectory = AppContext.BaseDirectory,
                SettingsPath = AppSettings.SettingsPath,
                SelectedPort = GetSelectedPortName(),
                BaudRate = _baudCombo.SelectedItem?.ToString(),
                Dtr = _dtrCheck.Checked,
                Rts = _rtsCheck.Checked,
                Echo = _echoCheck.Checked,
                TelnetFilter = _telnetFilterCheck.Checked,
                LineStatus = _bridge.GetLineStatusText(),
                Traffic = _bridge.GetTrafficText()
            };

            File.WriteAllText(Path.Combine(tempDir, "system-info.json"), JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }));
            File.WriteAllText(Path.Combine(tempDir, "live-log.txt"), _logBox.Text);
            File.WriteAllText(Path.Combine(tempDir, "bbs-directory.json"), JsonSerializer.Serialize(GetCleanDirectory(), new JsonSerializerOptions { WriteIndented = true }));
            File.WriteAllText(Path.Combine(tempDir, "dial-history.json"), JsonSerializer.Serialize(_settings.DialHistory.Take(500).ToList(), new JsonSerializerOptions { WriteIndented = true }));
            File.WriteAllText(Path.Combine(tempDir, "com-ports.txt"), string.Join(Environment.NewLine, SerialPortDiscovery.GetPorts().Select(p => p?.ToString() ?? string.Empty)));

            var readme =
                "RetroModem Bridge Support Bundle" + Environment.NewLine +
                "Generated: " + DateTime.Now + Environment.NewLine + Environment.NewLine +
                "Includes system-info.json, live-log.txt, bbs-directory.json, dial-history.json, and com-ports.txt." + Environment.NewLine +
                "Review before sharing publicly." + Environment.NewLine;

            File.WriteAllText(Path.Combine(tempDir, "README.txt"), readme);

            if (File.Exists(dialog.FileName))
                File.Delete(dialog.FileName);

            ZipFile.CreateFromDirectory(tempDir, dialog.FileName, CompressionLevel.Optimal, false);
            Directory.Delete(tempDir, recursive: true);

            AddLog("Support bundle exported to " + dialog.FileName);
            MessageBox.Show(this, "Support bundle exported successfully.", "Export Support Bundle", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not export support bundle", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CopyDialCommand()
    {
        if (ActiveDirectoryGrid.CurrentRow?.DataBoundItem is not BbsEntry entry || string.IsNullOrWhiteSpace(entry.Alias))
        {
            MessageBox.Show(this, "Select a directory entry with an alias first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var command = "ATDT" + entry.Alias.Trim();
        Clipboard.SetText(command);
        AddLog("Copied dial command: " + command);
        MessageBox.Show(this, command + " copied. Type this on the vintage computer terminal to dial this saved entry.", "Dial command copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            FileName = "RetroModemBridge-v3.4-log.txt"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
            File.WriteAllText(dialog.FileName, _logBox.Text);
    }

    private void EditSelectedDirectoryCell()
    {
        if (ActiveDirectoryGrid.CurrentRow?.DataBoundItem is BbsEntry entry && entry.IsDoorGame)
        {
            ShowDoorGameDialog(entry);
            return;
        }

        if (ActiveDirectoryGrid.CurrentCell is null)
            return;

        ActiveDirectoryGrid.Focus();
        ActiveDirectoryGrid.BeginEdit(true);
    }

    private void DeleteSelectedDirectoryRows()
    {
        var entries = ActiveDirectoryGrid.SelectedRows
            .Cast<DataGridViewRow>()
            .Select(row => row.DataBoundItem as BbsEntry)
            .Where(entry => entry is not null)
            .Cast<BbsEntry>()
            .Distinct()
            .ToList();

        if (entries.Count == 0 && ActiveDirectoryGrid.CurrentRow?.DataBoundItem is BbsEntry currentEntry)
            entries.Add(currentEntry);

        if (entries.Count == 0)
            return;

        var label = entries.Count == 1
            ? $"Delete '{entries[0].Name}' from your directory?"
            : $"Delete {entries.Count} selected entries from your directory?";

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
        ApplyDirectoryFilter();
        AddLog(entries.Count == 1 ? "Deleted directory entry." : $"Deleted {entries.Count} directory entries.");
    }

    private async Task UpdateBbsGuideFromAppAsync()
    {
        using var form = new Form
        {
            Text = "Update Telnet BBS Guide",
            Icon = AppIconHelper.LoadAppIcon(),
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(560, 330),
            MinimumSize = new Size(560, 330),
            Font = Font
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(16)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        form.Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = "Update the local Telnet BBS Guide used by the Import Guide and retro-side ATDT GUIDE browser.",
            AutoSize = true,
            MaximumSize = new Size(500, 0),
            Margin = new Padding(0, 0, 0, 10)
        }, 0, 0);

        var statusLabel = new Label
        {
            Text = TelnetBbsGuideUpdater.GetStatusText(),
            AutoSize = true,
            MaximumSize = new Size(500, 0),
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 0, 0, 10)
        };
        root.Controls.Add(statusLabel, 0, 1);

        var progress = new ProgressBar
        {
            Dock = DockStyle.Top,
            Height = 24,
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Margin = new Padding(0, 0, 0, 8)
        };

        var progressLabel = new Label
        {
            Text = "Choose an update source below.",
            AutoSize = true,
            MaximumSize = new Size(500, 0),
            ForeColor = Color.FromArgb(60, 60, 60)
        };

        var middle = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        middle.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        middle.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        middle.Controls.Add(progress, 0, 0);
        middle.Controls.Add(progressLabel, 0, 1);
        root.Controls.Add(middle, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0, 12, 0, 0)
        };

        var closeButton = new Button { Text = "Close", Width = 90, Height = 34 };
        var dailyButton = new Button { Text = "Download Daily", Width = 125, Height = 34 };
        var monthlyButton = new Button { Text = "Download Monthly", Width = 140, Height = 34 };
        var loadFileButton = new Button { Text = "Load ZIP/CSV", Width = 115, Height = 34 };

        closeButton.Click += (_, _) => form.Close();

        async Task RunDownloadAsync(GuideDownloadKind kind)
        {
            monthlyButton.Enabled = false;
            dailyButton.Enabled = false;
            loadFileButton.Enabled = false;
            closeButton.Enabled = false;
            progress.Value = 0;

            try
            {
                var progressReporter = new Progress<string>(status =>
                {
                    var parts = status.Split('|', 2);
                    if (parts.Length == 2 && int.TryParse(parts[0], out var percent))
                    {
                        progress.Value = Math.Max(progress.Minimum, Math.Min(progress.Maximum, percent));
                        progressLabel.Text = parts[1];
                    }
                    else
                    {
                        progressLabel.Text = status;
                    }
                });

                var result = await TelnetBbsGuideUpdater.DownloadAndInstallAsync(kind, progressReporter);
                statusLabel.Text = TelnetBbsGuideUpdater.GetStatusText();
                progress.Value = 100;
                progressLabel.Text = result.Message;
                AddLog("Updated Telnet BBS Guide: " + result.Message);
                MessageBox.Show(form, result.Message, "Guide updated", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                progressLabel.Text = "Update failed.";
                AddLog("Guide update failed: " + ex.Message);
                MessageBox.Show(form, ex.Message + "\n\nIf the site blocks automated downloads, download the ZIP in your browser and use Load ZIP/CSV.", "Guide update failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                monthlyButton.Enabled = true;
                dailyButton.Enabled = true;
                loadFileButton.Enabled = true;
                closeButton.Enabled = true;
            }
        }

        monthlyButton.Click += async (_, _) => await RunDownloadAsync(GuideDownloadKind.Monthly);
        dailyButton.Click += async (_, _) => await RunDownloadAsync(GuideDownloadKind.Daily);

        loadFileButton.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Telnet BBS Guide ZIP/CSV (*.zip;*.csv)|*.zip;*.csv|ZIP files (*.zip)|*.zip|CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog(form) != DialogResult.OK)
                return;

            try
            {
                progress.Value = 50;
                progressLabel.Text = "Installing selected file...";

                GuideUpdateResult result;
                if (Path.GetExtension(dialog.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                    result = TelnetBbsGuideUpdater.InstallZip(dialog.FileName, GuideDownloadKind.Monthly, "Manual file", null);
                else
                    result = TelnetBbsGuideUpdater.InstallCsv(dialog.FileName, GuideDownloadKind.Monthly, "Manual file", null);

                progress.Value = 100;
                statusLabel.Text = TelnetBbsGuideUpdater.GetStatusText();
                progressLabel.Text = result.Message;
                AddLog("Installed Telnet BBS Guide from file: " + dialog.FileName);
                MessageBox.Show(form, result.Message, "Guide installed", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                progressLabel.Text = "Install failed.";
                AddLog("Manual guide install failed: " + ex.Message);
                MessageBox.Show(form, ex.Message, "Could not install guide", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        buttons.Controls.Add(closeButton);
        buttons.Controls.Add(dailyButton);
        buttons.Controls.Add(monthlyButton);
        buttons.Controls.Add(loadFileButton);
        root.Controls.Add(buttons, 0, 3);

        form.ShowDialog(this);
    }

    private void ImportFromTelnetBbsGuide()
    {
        using var dialog = new BbsGuideImportForm(GetCleanDirectory());
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
            FileName = "bbs-list-v3.4.json"
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

    if (sender is DataGridView grid && e.RowIndex >= 0 && e.RowIndex < grid.Rows.Count && grid.Rows[e.RowIndex].DataBoundItem is BbsEntry entry)
        e.Value = entry.IsFavorite ? "★" : "☆";
    else
        e.Value = "☆";
    e.FormattingApplied = true;
}
    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern int mciSendString(string command, string? returnValue, int returnLength, IntPtr winHandle);

}
