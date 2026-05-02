using System.Text;

namespace RetroModemBridge;

public sealed class SessionMirrorForm : Form
{
    private readonly ModemBridge _bridge;
    private readonly AnsiTerminalControl _terminal = new();
    private readonly CheckBox _enableInputCheck = new();
    private readonly CheckBox _localEchoCheck = new();
    private readonly Button _clearButton = new();
    private readonly Button _copyNoteButton = new();
    private readonly Label _statusLabel = new();

    public SessionMirrorForm(ModemBridge bridge)
    {
        _bridge = bridge;
        InitializeComponent();

        _bridge.SessionMirrorBytes += BridgeOnSessionMirrorBytes;
        _terminal.SendBytesRequested += TerminalOnSendBytesRequested;
    }

    private void InitializeComponent()
    {
        Text = "RetroModem Bridge - Session Mirror";
        Icon = AppIconHelper.LoadAppIcon();
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(860, 620);
        Size = new Size(980, 720);
        BackColor = Color.FromArgb(245, 245, 246);
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12),
            BackColor = Color.FromArgb(245, 245, 246)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Color.FromArgb(245, 245, 246),
            Margin = new Padding(0, 0, 0, 10)
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = "Session Mirror",
            AutoSize = true,
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4)
        };
        top.Controls.Add(title, 0, 0);

        var hint = new Label
        {
            Text = "Shows the live 80-column CoCo/ANSI session data being sent to the retro computer. Enable input to type into the active session from this window.",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 0, 0, 0)
        };
        top.Controls.Add(hint, 0, 1);

        var controls = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(12, 0, 0, 0)
        };

        _enableInputCheck.Text = "Enable Input";
        _enableInputCheck.AutoSize = true;
        _enableInputCheck.Margin = new Padding(0, 8, 14, 0);

        _localEchoCheck.Text = "Local Echo";
        _localEchoCheck.AutoSize = true;
        _localEchoCheck.Checked = true;
        _localEchoCheck.Margin = new Padding(0, 8, 14, 0);

        _clearButton.Text = "Clear";
        _clearButton.Width = 85;
        _clearButton.Height = 34;
        _clearButton.Click += (_, _) => _terminal.Clear();

        _copyNoteButton.Text = "Copy Help";
        _copyNoteButton.Width = 95;
        _copyNoteButton.Height = 34;
        _copyNoteButton.Click += (_, _) =>
        {
            Clipboard.SetText("Session Mirror shows what RetroModem Bridge sends to the retro computer. Check Enable Input to type into the active BBS/local menu session from the app.");
        };

        controls.Controls.Add(_enableInputCheck);
        controls.Controls.Add(_localEchoCheck);
        controls.Controls.Add(_clearButton);
        controls.Controls.Add(_copyNoteButton);

        top.Controls.Add(controls, 1, 0);
        top.SetRowSpan(controls, 2);
        root.Controls.Add(top, 0, 0);

        _terminal.Dock = DockStyle.Fill;
        root.Controls.Add(_terminal, 0, 1);

        _statusLabel.Text = "Mirror only. Check Enable Input to type from this window. Local Echo shows typed characters immediately.";
        _statusLabel.AutoSize = true;
        _statusLabel.ForeColor = Color.DimGray;
        _statusLabel.Margin = new Padding(0, 8, 0, 0);
        root.Controls.Add(_statusLabel, 0, 2);

        _enableInputCheck.CheckedChanged += (_, _) =>
        {
            _statusLabel.Text = _enableInputCheck.Checked
                ? "Input enabled. Keystrokes typed here will be sent to the current bridge session. Local Echo shows them immediately."
                : "Mirror only. Check Enable Input to type from this window. Local Echo shows typed characters immediately.";
            _terminal.Focus();
        };

        Shown += (_, _) => _terminal.Focus();
    }

    private void BridgeOnSessionMirrorBytes(byte[] bytes)
    {
        if (IsDisposed)
            return;

        _terminal.WriteBytes(bytes);
    }

    private void TerminalOnSendBytesRequested(byte[] bytes)
    {
        if (!_enableInputCheck.Checked)
            return;

        _bridge.SendSessionMirrorInput(bytes);

        // The real retro terminal often shows typed characters because its terminal
        // program echoes locally or the remote BBS echoes back. The mirror window
        // needs its own optional local echo so typing here is visible immediately.
        // If a remote BBS echoes characters twice, uncheck Local Echo.
        if (_localEchoCheck.Checked)
            _terminal.WriteBytes(bytes);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _bridge.SessionMirrorBytes -= BridgeOnSessionMirrorBytes;
        _terminal.SendBytesRequested -= TerminalOnSendBytesRequested;
        base.OnFormClosed(e);
    }
}
