
using System.Net.Sockets;

namespace RetroModemBridge;

public sealed class TerminalForm : Form
{
    private readonly BbsEntry _entry;
    private readonly AnsiTerminalControl _terminal = new();
    private readonly Button _connectButton = new();
    private readonly Button _disconnectButton = new();
    private readonly Button _clearButton = new();
    private readonly Label _statusLabel = new();
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _readerCts;
    private readonly System.Text.StringBuilder _ansiPromptBuffer = new();
    private DateTime _lastAnsiAutoReplyUtc = DateTime.MinValue;

    public TerminalForm(BbsEntry entry)
    {
        _entry = entry;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = $"RetroModem Bridge Terminal - {_entry.Name}";
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
            Text = $"{_entry.Name}  ({_entry.Host}:{_entry.Port})",
            AutoSize = true,
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4)
        };
        top.Controls.Add(title, 0, 0);

        var hint = new Label
        {
            Text = "Built-in ANSI terminal preview. Click inside the black terminal area and type normally.",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 0, 0, 0)
        };
        top.Controls.Add(hint, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(12, 0, 0, 0)
        };

        _connectButton.Text = "Connect";
        _connectButton.Width = 105;
        _connectButton.Height = 34;
        _connectButton.Click += async (_, _) => await ConnectAsync();

        _disconnectButton.Text = "Disconnect";
        _disconnectButton.Width = 110;
        _disconnectButton.Height = 34;
        _disconnectButton.Enabled = false;
        _disconnectButton.Click += (_, _) => Disconnect();

        _clearButton.Text = "Clear";
        _clearButton.Width = 85;
        _clearButton.Height = 34;
        _clearButton.Click += (_, _) => _terminal.Clear();

        buttons.Controls.Add(_connectButton);
        buttons.Controls.Add(_disconnectButton);
        buttons.Controls.Add(_clearButton);

        top.Controls.Add(buttons, 1, 0);
        top.SetRowSpan(buttons, 2);
        root.Controls.Add(top, 0, 0);

        _terminal.Dock = DockStyle.Fill;
        _terminal.SendBytesRequested += SendBytes;
        root.Controls.Add(_terminal, 0, 1);

        _statusLabel.Text = "Disconnected.";
        _statusLabel.AutoSize = true;
        _statusLabel.ForeColor = Color.DimGray;
        _statusLabel.Margin = new Padding(0, 8, 0, 0);
        root.Controls.Add(_statusLabel, 0, 2);

        Shown += async (_, _) => await ConnectAsync();
        FormClosing += (_, _) => Disconnect();
    }

    private async Task ConnectAsync()
    {
        if (_client is { Connected: true })
            return;

        try
        {
            SetStatus("Connecting...");
            _connectButton.Enabled = false;

            _client = new TcpClient();
            await _client.ConnectAsync(_entry.Host, _entry.Port);
            _stream = _client.GetStream();
            _readerCts = new CancellationTokenSource();

            _disconnectButton.Enabled = true;
            SetStatus($"Connected to {_entry.Host}:{_entry.Port}.");
            _terminal.Focus();

            _ = Task.Run(() => ReaderLoopAsync(_readerCts.Token));
        }
        catch (Exception ex)
        {
            SetStatus("Connect failed: " + ex.Message);
            Disconnect();
        }
        finally
        {
            if (_client is not { Connected: true })
                _connectButton.Enabled = true;
        }
    }

    private async Task ReaderLoopAsync(CancellationToken token)
    {
        var buffer = new byte[4096];
        var telnet = new TelnetNegotiator();

        try
        {
            while (!token.IsCancellationRequested && _stream is not null)
            {
                var read = await _stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token);
                if (read <= 0)
                    break;

                var output = telnet.FilterIncoming(buffer.AsSpan(0, read), SendBytes);
                if (output.Length > 0)
                {
                    _terminal.WriteBytes(output);
                    MaybeAutoAnswerAnsiPrompt(output);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            BeginInvoke(new Action(() => SetStatus("Connection closed: " + ex.Message)));
        }
        finally
        {
            BeginInvoke(new Action(Disconnect));
        }
    }

    private void MaybeAutoAnswerAnsiPrompt(byte[] bytes)
    {
        // Some BBSes ask a text prompt such as "Can you display ANSI color?".
        // Answering "Y" helps the built-in terminal get color ANSI screens
        // without the user having to type the response manually every time.
        var text = System.Text.Encoding.ASCII.GetString(bytes).ToLowerInvariant();
        _ansiPromptBuffer.Append(text);

        if (_ansiPromptBuffer.Length > 2000)
            _ansiPromptBuffer.Remove(0, _ansiPromptBuffer.Length - 2000);

        var buffer = _ansiPromptBuffer.ToString();
        var looksLikeAnsiPrompt =
            buffer.Contains("can you display ansi") ||
            buffer.Contains("ansi color") ||
            buffer.Contains("ansi graphics") ||
            buffer.Contains("do you want ansi") ||
            buffer.Contains("use ansi") ||
            buffer.Contains("ansi? [") ||
            buffer.Contains("ansi (y/n)") ||
            buffer.Contains("ansi y/n");

        if (!looksLikeAnsiPrompt)
            return;

        // Avoid repeatedly answering if the same prompt text remains in the buffer.
        if ((DateTime.UtcNow - _lastAnsiAutoReplyUtc).TotalSeconds < 4)
            return;

        _lastAnsiAutoReplyUtc = DateTime.UtcNow;
        SendBytes(System.Text.Encoding.ASCII.GetBytes("Y\r"));
        BeginInvoke(new Action(() => SetStatus("ANSI auto-detect answered Yes.")));
    }

    private void SendBytes(byte[] bytes)
    {
        try
        {
            if (_stream is null || _client is not { Connected: true })
                return;

            _stream.Write(bytes, 0, bytes.Length);
            _stream.Flush();
        }
        catch (Exception ex)
        {
            SetStatus("Send failed: " + ex.Message);
            Disconnect();
        }
    }

    private void Disconnect()
    {
        try
        {
            _readerCts?.Cancel();
        }
        catch
        {
        }

        try
        {
            _stream?.Dispose();
            _client?.Close();
        }
        catch
        {
        }

        _stream = null;
        _client = null;
        _readerCts?.Dispose();
        _readerCts = null;

        _connectButton.Enabled = true;
        _disconnectButton.Enabled = false;

        if (!IsDisposed)
            SetStatus("Disconnected.");
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

    private sealed class TelnetNegotiator
    {
        private const byte Iac = 255;
        private const byte Sb = 250;
        private const byte Se = 240;
        private const byte Do = 253;
        private const byte Dont = 254;
        private const byte Will = 251;
        private const byte Wont = 252;

        private TelnetState _state = TelnetState.Data;
        private byte _pendingCommand;

        private enum TelnetState
        {
            Data,
            Command,
            Option,
            SubNegotiation,
            SubNegotiationCommand
        }

        public byte[] FilterIncoming(ReadOnlySpan<byte> input, Action<byte[]> sendReply)
        {
            var output = new List<byte>(input.Length);

            foreach (var b in input)
            {
                switch (_state)
                {
                    case TelnetState.Data:
                        if (b == Iac)
                            _state = TelnetState.Command;
                        else
                            output.Add(b);
                        break;

                    case TelnetState.Command:
                        if (b == Iac)
                        {
                            output.Add(Iac);
                            _state = TelnetState.Data;
                        }
                        else if (b is Do or Dont or Will or Wont)
                        {
                            _pendingCommand = b;
                            _state = TelnetState.Option;
                        }
                        else if (b == Sb)
                        {
                            _state = TelnetState.SubNegotiation;
                        }
                        else
                        {
                            _state = TelnetState.Data;
                        }
                        break;

                    case TelnetState.Option:
                        if (_pendingCommand == Do)
                            sendReply(new[] { Iac, Wont, b });
                        else if (_pendingCommand == Will)
                            sendReply(new[] { Iac, Dont, b });
                        _state = TelnetState.Data;
                        break;

                    case TelnetState.SubNegotiation:
                        if (b == Iac)
                            _state = TelnetState.SubNegotiationCommand;
                        break;

                    case TelnetState.SubNegotiationCommand:
                        _state = b == Se ? TelnetState.Data : TelnetState.SubNegotiation;
                        break;
                }
            }

            return output.ToArray();
        }
    }

}
