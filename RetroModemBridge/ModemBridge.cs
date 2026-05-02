using System.IO.Ports;
using System.Net.Sockets;
using System.Text;

namespace RetroModemBridge;

public sealed class ModemBridge : IDisposable
{
    private readonly object _sync = new();
    private SerialPort? _serialPort;
    private TcpClient? _tcpClient;
    private NetworkStream? _networkStream;
    private CancellationTokenSource? _networkCts;
    private readonly StringBuilder _commandBuffer = new();
    private bool _localBbsSessionActive;
    private LocalBbsMenuMode _localBbsMenuMode = LocalBbsMenuMode.Main;
    private List<BbsEntry> _localBbsCurrentList = new();
    private bool _disposed;
    private string? _lastDialString;
    private long _serialRxBytes;
    private long _serialTxBytes;
    private long _tcpRxBytes;
    private long _tcpTxBytes;

    public event Action<string>? Log;
    public event Action<string>? StatusChanged;
    public event Action? TrafficChanged;
    public event Action? HistoryChanged;
    public event Action? DirectoryChanged;
    public event Action<byte[]>? SessionMirrorBytes;

    public bool IsSerialOpen => _serialPort?.IsOpen == true;
    public bool IsConnected => _tcpClient?.Connected == true;
    public bool EchoEnabled { get; set; }
    public bool TelnetFilteringEnabled { get; set; } = true;
    public int DefaultTcpPort { get; set; } = 23;
    public IList<BbsEntry> DialDirectory { get; set; } = Array.Empty<BbsEntry>();
    public IList<DialHistoryEntry> DialHistory { get; set; } = new List<DialHistoryEntry>();
    public string? CurrentConnection { get; private set; }
    public string? LastCommand { get; private set; }

    private void RaiseSessionMirrorBytes(byte[] bytes)
    {
        if (bytes.Length == 0)
            return;

        try
        {
            SessionMirrorBytes?.Invoke(bytes.ToArray());
        }
        catch
        {
        }
    }

    private void RaiseSessionMirrorText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        try
        {
            var port = _serialPort;
            var encoding = port?.Encoding ?? Encoding.GetEncoding(437);
            RaiseSessionMirrorBytes(encoding.GetBytes(text));
        }
        catch
        {
            RaiseSessionMirrorBytes(Encoding.ASCII.GetBytes(text));
        }
    }

    public void SendSessionMirrorInput(byte[] bytes)
    {
        if (bytes is null || bytes.Length == 0)
            return;

        try
        {
            if (IsConnected)
            {
                var stream = _networkStream;
                if (stream is not null && stream.CanWrite)
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush();
                    _tcpTxBytes += bytes.Length;
                    TrafficChanged?.Invoke();
                }

                return;
            }

            foreach (var value in bytes)
                ProcessCommandByte(value);
        }
        catch (Exception ex)
        {
            LogMessage("Session mirror input error: " + ex.Message);
        }
    }

    public void OpenSerial(string portName, int baudRate, bool dtrEnable, bool rtsEnable)
    {
        ThrowIfDisposed();
        CloseSerial();

        _serialRxBytes = 0;
        _serialTxBytes = 0;
        _tcpRxBytes = 0;
        _tcpTxBytes = 0;
        CurrentConnection = null;
        LastCommand = null;
        ResetLocalBbsSession();

        _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            DtrEnable = dtrEnable,
            RtsEnable = rtsEnable,
            NewLine = "\r",
            ReadTimeout = 250,
            WriteTimeout = 1000,
            Encoding = Encoding.GetEncoding(437)
        };

        _serialPort.DataReceived += SerialPortOnDataReceived;
        _serialPort.Open();

        LogMessage($"Opened {portName} at {baudRate} 8-N-1. DTR={(dtrEnable ? "on" : "off")}, RTS={(rtsEnable ? "on" : "off")}. ");
        SetStatus("Serial open. Waiting for AT commands.");
        TrafficChanged?.Invoke();
    }

    public void CloseSerial()
    {
        HangUp("Serial closed");

        if (_serialPort is not null)
        {
            try
            {
                _serialPort.DataReceived -= SerialPortOnDataReceived;
                if (_serialPort.IsOpen)
                    _serialPort.Close();
                _serialPort.Dispose();
            }
            catch (Exception ex)
            {
                LogMessage("Serial close error: " + ex.Message);
            }
            finally
            {
                _serialPort = null;
            }
        }

        SetStatus("Stopped.");
        TrafficChanged?.Invoke();
    }

    public string GetLineStatusText()
    {
        var port = _serialPort;
        if (port is null || !port.IsOpen)
            return "Line status: serial closed";

        try
        {
            return $"CTS={(port.CtsHolding ? "On" : "Off")}  DSR={(port.DsrHolding ? "On" : "Off")}  DCD={(port.CDHolding ? "On" : "Off")}  DTR={(port.DtrEnable ? "On" : "Off")}  RTS={(port.RtsEnable ? "On" : "Off")}";
        }
        catch (Exception ex)
        {
            return "Line status unavailable: " + ex.Message;
        }
    }

    public string GetTrafficText() =>
        $"Serial RX {_serialRxBytes:n0}  Serial TX {_serialTxBytes:n0}  TCP RX {_tcpRxBytes:n0}  TCP TX {_tcpTxBytes:n0}";

    private void SerialPortOnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            var port = _serialPort;
            if (port is null || !port.IsOpen)
                return;

            var count = port.BytesToRead;
            if (count <= 0)
                return;

            var buffer = new byte[count];
            var read = port.Read(buffer, 0, count);
            if (read <= 0)
                return;

            _serialRxBytes += read;
            TrafficChanged?.Invoke();

            if (IsConnected)
            {
                var stream = _networkStream;
                if (stream is not null && stream.CanWrite)
                {
                    stream.Write(buffer, 0, read);
                    stream.Flush();
                    _tcpTxBytes += read;
                    TrafficChanged?.Invoke();
                }
                return;
            }

            for (var i = 0; i < read; i++)
                ProcessCommandByte(buffer[i]);
        }
        catch (Exception ex)
        {
            LogMessage("Serial read error: " + ex.Message);
            HangUp("Serial read error");
        }
    }

    private void ProcessCommandByte(byte value)
    {
        var ch = (char)value;

        if (EchoEnabled)
            WriteSerialByte(value);

        if (ch == '\r' || ch == '\n')
        {
            var command = _commandBuffer.ToString().Trim();
            _commandBuffer.Clear();

            if (!string.IsNullOrWhiteSpace(command))
                _ = Task.Run(() => HandleCommandAsync(command));
            return;
        }

        if (ch == '\b' || value == 127)
        {
            if (_commandBuffer.Length > 0)
                _commandBuffer.Length--;
            return;
        }

        if (!char.IsControl(ch))
            _commandBuffer.Append(ch);
    }

    private async Task HandleCommandAsync(string rawCommand)
    {
        var command = rawCommand.Trim();
        var upper = command.ToUpperInvariant();
        LastCommand = command;

        LogMessage("Terminal > " + command);
        TrafficChanged?.Invoke();

        if (_localBbsSessionActive && !upper.StartsWith("AT", StringComparison.Ordinal))
        {
            await HandleLocalBbsInputAsync(command).ConfigureAwait(false);
            return;
        }

        if (upper == "A/")
        {
            if (!string.IsNullOrWhiteSpace(_lastDialString))
                await DialAsync(_lastDialString).ConfigureAwait(false);
            else
                SendResponse("ERROR");
            return;
        }

        if (upper == "AT" || upper == "ATZ" || upper == "AT&F")
        {
            if (upper is "ATZ" or "AT&F")
                LogMessage("Modem settings reset command accepted.");
            SendResponse("OK");
            return;
        }

        if (upper == "ATI" || upper == "ATI0")
        {
            SendResponse("RetroModem Bridge v3 Beta");
            SendResponse("BBS Companion features: HELP, TIME, MENU, BBSLIST, FAVORITES");
            SendResponse("OK");
            return;
        }

        if (upper == "AT&V")
        {
            SendResponse("RetroModem Bridge v3 Beta");
            SendResponse("Default TCP port: " + DefaultTcpPort);
            SendResponse("Echo: " + (EchoEnabled ? "on" : "off"));
            SendResponse("Telnet filtering: " + (TelnetFilteringEnabled ? "on" : "off"));
            SendResponse("Saved BBS entries: " + DialDirectory.Count);
            SendResponse("Dial history entries: " + DialHistory.Count);
            SendResponse("Last dial: " + (_lastDialString ?? "none"));
            SendResponse("OK");
            return;
        }

        if (upper.StartsWith("ATE", StringComparison.Ordinal))
        {
            EchoEnabled = upper != "ATE0";
            SendResponse("OK");
            LogMessage("Echo " + (EchoEnabled ? "enabled" : "disabled") + ".");
            return;
        }

        if (upper.StartsWith("ATH", StringComparison.Ordinal))
        {
            if (_localBbsSessionActive)
            {
                ResetLocalBbsSession();
                WriteSerialText(BuildAnsiGoodbyeScreen());
                LogMessage("Local BBS session closed with ATH.");
                SetStatus("Local BBS menu closed.");
                return;
            }

            HangUp("ATH received");
            SendResponse("OK");
            return;
        }

        if (upper == "ATDL")
        {
            if (string.IsNullOrWhiteSpace(_lastDialString))
            {
                SendResponse("ERROR");
                return;
            }

            await DialAsync(_lastDialString).ConfigureAwait(false);
            return;
        }

        if (upper.StartsWith("ATDT", StringComparison.Ordinal) || upper.StartsWith("ATD", StringComparison.Ordinal))
        {
            var dialString = command.StartsWith("ATDT", StringComparison.OrdinalIgnoreCase)
                ? command[4..].Trim()
                : command[3..].Trim();

            await DialAsync(dialString).ConfigureAwait(false);
            return;
        }

        SendResponse("ERROR");
    }

    private async Task DialAsync(string dialString)
    {
        if (string.IsNullOrWhiteSpace(dialString))
        {
            SendResponse("ERROR");
            return;
        }

        var started = DateTime.Now;
        HangUp("Preparing new dial command", silent: true);
        _lastDialString = dialString;

        if (TryHandleTextService(dialString, started))
            return;

        var resolved = ResolveDialString(dialString);
        if (resolved.AliasEntry is not null)
            LogMessage($"Dial alias {dialString} resolved to {resolved.AliasEntry.Name} {resolved.AliasEntry.Host}:{resolved.AliasEntry.Port}.");

        var parsed = ParseDialString(resolved.DialString, DefaultTcpPort);
        if (parsed is null)
        {
            SendResponse("ERROR");
            RecordHistory(dialString, string.Empty, 0, "Invalid dial target", started);
            return;
        }

        var (host, port) = parsed.Value;
        LogMessage($"Dialing {host}:{port}...");
        SetStatus($"Dialing {host}:{port}...");

        try
        {
            var client = new TcpClient();
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await client.ConnectAsync(host, port, timeoutCts.Token).ConfigureAwait(false);

            lock (_sync)
            {
                _tcpClient = client;
                _networkStream = client.GetStream();
                _networkCts = new CancellationTokenSource();
            }

            CurrentConnection = $"{host}:{port}";
            SendResponse("CONNECT");
            LogMessage($"Connected to {host}:{port}.");
            SetStatus($"Connected to {host}:{port}");
            MarkDirectoryResult(resolved.AliasEntry, "Connected");
            RecordHistory(dialString, host, port, "Connected", started);
            TrafficChanged?.Invoke();

            _ = Task.Run(() => PumpNetworkToSerialAsync(_networkCts.Token));
        }
        catch (Exception ex)
        {
            var result = "Failed: " + ex.Message;
            LogMessage("Connect failed: " + ex.Message);
            SetStatus("Connection failed.");
            SendResponse("NO CARRIER");
            MarkDirectoryResult(resolved.AliasEntry, result);
            RecordHistory(dialString, host, port, result, started);
            HangUp("Connect failed", silent: true);
        }
    }

    private bool TryHandleTextService(string dialString, DateTime started)
    {
        var key = dialString.Trim().ToUpperInvariant();
        if (key != "TIME" && key != "HELP" && key != "MENU" && key != "BBSLIST" && key != "FAVORITES")
            return false;

        LogMessage("Text service requested: " + key);

        if (key == "MENU")
        {
            StartLocalBbsSession();
            RecordHistory(dialString, key, 0, "Local BBS menu", started);
            SetStatus("Local BBS menu active.");
            return true;
        }

        WriteSerialText(BuildTextServiceResponse(key));
        RecordHistory(dialString, key, 0, "Text service", started);
        SetStatus("Text service sent: " + key);
        return true;
    }

    private string BuildTextServiceResponse(string key)
    {
        return key switch
        {
            "TIME" => BuildAnsiTimeScreen(includeMenuPrompt: false),
            "HELP" => BuildAnsiHelpScreen(includeMenuPrompt: false),
            "BBSLIST" => BuildAnsiBbsListScreen(DialDirectory, "BBS DIRECTORY", includeDialPrompt: false),
            "FAVORITES" => BuildAnsiBbsListScreen(DialDirectory.Where(e => e.IsFavorite), "FAVORITES", includeDialPrompt: false),
            _ => "\r\nERROR\r\n"
        };
    }

    private void StartLocalBbsSession()
    {
        HangUp("Starting local BBS menu", silent: true);
        _localBbsSessionActive = true;
        _localBbsMenuMode = LocalBbsMenuMode.Main;
        _localBbsCurrentList = new List<BbsEntry>();

        WriteSerialText("\r\nCONNECT\r\n");
        WriteSerialText(BuildAnsiMainMenuScreen(firstConnect: true));
    }

    private async Task HandleLocalBbsInputAsync(string input)
    {
        var choice = (input ?? string.Empty).Trim();
        var upper = choice.ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(choice))
        {
            WriteSerialText(BuildAnsiPrompt());
            return;
        }

        if (upper is "Q" or "QUIT" or "BYE" or "X" or "EXIT")
        {
            ResetLocalBbsSession();
            WriteSerialText(BuildAnsiGoodbyeScreen());
            LogMessage("Local BBS session closed.");
            SetStatus("Local BBS menu closed.");
            return;
        }

        if (upper is "M" or "MENU" or "MAIN")
        {
            _localBbsMenuMode = LocalBbsMenuMode.Main;
            _localBbsCurrentList.Clear();
            WriteSerialText(BuildAnsiMainMenuScreen(firstConnect: false));
            return;
        }

        if (_localBbsMenuMode is LocalBbsMenuMode.Directory or LocalBbsMenuMode.Favorites)
        {
            if (int.TryParse(choice, out var selectedNumber) && selectedNumber >= 1 && selectedNumber <= _localBbsCurrentList.Count)
            {
                var selected = _localBbsCurrentList[selectedNumber - 1];
                ResetLocalBbsSession();
                WriteSerialText(BuildAnsiDialingScreen(selected));
                await DialAsync(string.IsNullOrWhiteSpace(selected.Alias) ? selected.DialTarget : selected.Alias).ConfigureAwait(false);
                return;
            }

            WriteSerialText(BuildAnsiErrorLine("Enter a listed number to dial, M for menu, or Q to quit."));
            return;
        }

        switch (upper)
        {
            case "1":
            case "D":
            case "DIR":
            case "DIRECTORY":
                _localBbsMenuMode = LocalBbsMenuMode.Directory;
                _localBbsCurrentList = DialDirectory
                    .Where(e => !string.IsNullOrWhiteSpace(e.Host))
                    .OrderByDescending(e => e.IsFavorite)
                    .ThenBy(e => e.Name)
                    .ThenBy(e => e.Alias)
                    .Take(40)
                    .ToList();
                WriteSerialText(BuildAnsiBbsListScreen(_localBbsCurrentList, "BBS DIRECTORY", includeDialPrompt: true));
                break;

            case "2":
            case "F":
            case "FAV":
            case "FAVORITES":
                _localBbsMenuMode = LocalBbsMenuMode.Favorites;
                _localBbsCurrentList = DialDirectory
                    .Where(e => e.IsFavorite && !string.IsNullOrWhiteSpace(e.Host))
                    .OrderBy(e => e.Name)
                    .ThenBy(e => e.Alias)
                    .Take(40)
                    .ToList();
                WriteSerialText(BuildAnsiBbsListScreen(_localBbsCurrentList, "FAVORITES", includeDialPrompt: true));
                break;

            case "3":
            case "H":
            case "HISTORY":
                _localBbsMenuMode = LocalBbsMenuMode.Main;
                WriteSerialText(BuildAnsiHistoryScreen());
                break;

            case "4":
            case "T":
            case "TIME":
                _localBbsMenuMode = LocalBbsMenuMode.Main;
                WriteSerialText(BuildAnsiTimeScreen(includeMenuPrompt: true));
                break;

            case "5":
            case "?":
            case "HELP":
                _localBbsMenuMode = LocalBbsMenuMode.Main;
                WriteSerialText(BuildAnsiHelpScreen(includeMenuPrompt: true));
                break;

            case "6":
            case "A":
            case "ABOUT":
                _localBbsMenuMode = LocalBbsMenuMode.Main;
                WriteSerialText(BuildAnsiAboutScreen());
                break;

            default:
                WriteSerialText(BuildAnsiErrorLine("Unknown menu choice. Enter 1-6, M for menu, or Q to quit."));
                break;
        }
    }

    private void ResetLocalBbsSession()
    {
        _localBbsSessionActive = false;
        _localBbsMenuMode = LocalBbsMenuMode.Main;
        _localBbsCurrentList.Clear();
    }

    private string BuildAnsiMainMenuScreen(bool firstConnect)
    {
        var sb = new StringBuilder();
        const int innerWidth = 68;
        sb.Append(Ansi.Clear);
        sb.Append(Ansi.Home);
        sb.Append(BuildAnsiLogoScreen());

        if (firstConnect)
        {
            sb.Append(Ansi.Line(Ansi.Cyan, "  Connected to RetroModem Bridge local menu.\r\n"));
            sb.Append("\r\n");
        }

        sb.Append(Ansi.BrightBlue).Append("  ┌").Append(new string('─', innerWidth)).Append("┐\r\n");
        sb.Append(Ansi.BrightBlue).Append("  │")
          .Append(Ansi.BrightWhite).Append("  MAIN MENU".PadRight(innerWidth))
          .Append(Ansi.BrightBlue).Append("│\r\n");

        sb.Append(Ansi.BrightBlue).Append("  │")
          .Append(Ansi.Yellow).Append("  [1] ")
          .Append(Ansi.BrightWhite).Append("BBS Directory".PadRight(28))
          .Append(Ansi.Yellow).Append("  [4] ")
          .Append(Ansi.BrightWhite).Append("Time Service".PadRight(28))
          .Append(Ansi.BrightBlue).Append("│\r\n");

        sb.Append(Ansi.BrightBlue).Append("  │")
          .Append(Ansi.Yellow).Append("  [2] ")
          .Append(Ansi.BrightWhite).Append("Favorites".PadRight(28))
          .Append(Ansi.Yellow).Append("  [5] ")
          .Append(Ansi.BrightWhite).Append("Help / Commands".PadRight(28))
          .Append(Ansi.BrightBlue).Append("│\r\n");

        sb.Append(Ansi.BrightBlue).Append("  │")
          .Append(Ansi.Yellow).Append("  [3] ")
          .Append(Ansi.BrightWhite).Append("Dial History".PadRight(28))
          .Append(Ansi.Yellow).Append("  [6] ")
          .Append(Ansi.BrightWhite).Append("About This Bridge".PadRight(28))
          .Append(Ansi.BrightBlue).Append("│\r\n");

        sb.Append(Ansi.BrightBlue).Append("  │")
          .Append(Ansi.Red).Append("  [Q] ")
          .Append(Ansi.BrightWhite).Append("Disconnect".PadRight(28))
          .Append(Ansi.Cyan).Append("  [M] ")
          .Append(Ansi.BrightWhite).Append("Refresh this menu".PadRight(28))
          .Append(Ansi.BrightBlue).Append("│\r\n");

        sb.Append(Ansi.BrightBlue).Append("  └").Append(new string('─', innerWidth)).Append("┘\r\n");
        sb.Append(Ansi.Line(Ansi.Dim, $"  Boards: {DialDirectory.Count}   Favorites: {DialDirectory.Count(e => e.IsFavorite)}   History: {DialHistory.Count}\r\n"));
        sb.Append(BuildAnsiPrompt());
        return sb.ToString();
    }

    private string BuildAnsiHeader(string title, string subTitle)
    {
        var safeTitle = title.PadRight(38)[..38];
        var safeSubTitle = subTitle.PadRight(38)[..38];

        var sb = new StringBuilder();
        sb.Append(Ansi.BrightBlue).Append("\x1b[1m");
        sb.Append("  ╔════════════════════════════════════════╗\r\n");
        sb.Append("  ║ ").Append(Ansi.BrightWhite).Append(safeTitle).Append(Ansi.BrightBlue).Append(" ║\r\n");
        sb.Append("  ║ ").Append(Ansi.Magenta).Append(safeSubTitle).Append(Ansi.BrightBlue).Append(" ║\r\n");
        sb.Append("  ╚════════════════════════════════════════╝\r\n\r\n");
        return sb.ToString();
    }

    private string BuildAnsiLogoScreen()
    {
        var sb = new StringBuilder();
        const int innerWidth = 68;

        string[] artRows =
        {
            "╦═╗ ╔═╗ ╔╦╗ ╦═╗ ╔═╗ ╔╦╗ ╔═╗ ╔╦╗ ╔═╗ ╔╦╗    ╔╗  ╦═╗ ╦ ╔╦╗ ╔═╗ ╔═╗",
            "╠╦╝ ║╣   ║  ╠╦╝ ║ ║ ║║║ ║ ║ ║ ║ ║╣  ║║║    ╠╩╗ ╠╦╝ ║ ║ ║ ║ ╦ ║╣ ",
            "╩╚═ ╚═╝  ╩  ╩╚═ ╚═╝ ╩ ╩ ╚═╝ ╚═╝ ╚═╝ ╩ ╩    ╚═╝ ╩╚═ ╩ ╚═╝ ╚═╝ ╚═╝"
        };

        string[] rowColors =
        {
            Ansi.BrightYellow,
            Ansi.Yellow,
            Ansi.White
        };

        var titleText = "   ░▒▓ RetroModem Bridge ▓▒░      local bbs";
        var topBorder = "  ╔" + new string('═', innerWidth) + "╗\r\n";
        var bottomBorder = "  ╚" + new string('═', innerWidth) + "╝\r\n";

        sb.Append(Ansi.BrightBlue).Append(topBorder);
        sb.Append(Ansi.BrightBlue).Append("  ║");
        sb.Append(Ansi.Cyan).Append("   ░▒▓").Append(Ansi.BrightWhite).Append(" RetroModem Bridge ").Append(Ansi.Cyan).Append("▓▒░")
            .Append(Ansi.Magenta).Append("      local bbs");
        sb.Append(new string(' ', Math.Max(0, innerWidth - titleText.Length)));
        sb.Append(Ansi.BrightBlue).Append("║\r\n");

        for (var i = 0; i < artRows.Length; i++)
        {
            sb.Append(Ansi.BrightBlue).Append("  ║");
            sb.Append(rowColors[i]).Append(artRows[i].PadRight(innerWidth)).Append(Ansi.BrightBlue);
            sb.Append("║\r\n");
        }

        sb.Append(Ansi.BrightBlue).Append(bottomBorder);
        sb.Append("\r\n");
        return sb.ToString();
    }

    private string BuildAnsiBbsListScreen(IEnumerable<BbsEntry> entries, string title, bool includeDialPrompt)
    {
        var list = entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Host))
            .OrderByDescending(e => e.IsFavorite)
            .ThenBy(e => e.Name)
            .ThenBy(e => e.Alias)
            .Take(40)
            .ToList();

        var sb = new StringBuilder();
        sb.Append(Ansi.Clear);
        sb.Append(Ansi.Home);
        sb.Append(BuildAnsiHeader("RetroModem Bridge", title));

        if (list.Count == 0)
        {
            sb.Append(Ansi.Line(Ansi.Yellow, title == "FAVORITES" ? "  No favorites saved yet.\r\n" : "  No BBS entries saved yet.\r\n"));
        }
        else
        {
            for (var i = 0; i < list.Count; i++)
            {
                var entry = list[i];
                var number = (i + 1).ToString("00");
                var star = entry.IsFavorite ? "*" : " ";
                var name = Truncate(string.IsNullOrWhiteSpace(entry.Name) ? entry.DialTarget : entry.Name.Trim(), 24);
                var alias = Truncate(string.IsNullOrWhiteSpace(entry.Alias) ? entry.DialTarget : entry.Alias.Trim(), 12);
                var ansi = entry.SupportsAnsi ? "ANSI" : "TXT ";

                sb.Append(Ansi.Yellow).Append("  [").Append(number).Append("] ");
                sb.Append(entry.IsFavorite ? Ansi.Magenta : Ansi.Dim).Append(star).Append(' ');
                sb.Append(Ansi.BrightWhite).Append(name.PadRight(24));
                sb.Append(Ansi.Cyan).Append(' ').Append(alias.PadRight(12));
                sb.Append(Ansi.Green).Append(' ').Append(ansi);
                sb.Append(Ansi.Reset).Append("\r\n");
            }
        }

        sb.Append("\r\n");
        if (includeDialPrompt)
        {
            sb.Append(Ansi.Line(Ansi.Dim, "  Enter a listed number to dial that BBS.\r\n"));
            sb.Append(Ansi.Line(Ansi.Dim, "  M = main menu, Q = disconnect\r\n"));
            sb.Append(BuildAnsiPrompt());
        }
        else
        {
            sb.Append(Ansi.Line(Ansi.Dim, "  Dial from your terminal with ATDT ALIAS.\r\n\r\n"));
            sb.Append(Ansi.Line(Ansi.Green, "OK\r\n"));
        }

        return sb.ToString();
    }

    private string BuildAnsiHistoryScreen()
    {
        var list = DialHistory.Take(15).ToList();
        var sb = new StringBuilder();
        sb.Append(Ansi.Clear);
        sb.Append(Ansi.Home);
        sb.Append(BuildAnsiHeader("RetroModem Bridge", "DIAL HISTORY"));

        if (list.Count == 0)
        {
            sb.Append(Ansi.Line(Ansi.Yellow, "  No dial history yet.\r\n"));
        }
        else
        {
            foreach (var item in list)
            {
                var resultColor = item.Result.StartsWith("Connected", StringComparison.OrdinalIgnoreCase) ? Ansi.Green : Ansi.Red;
                sb.Append(Ansi.Cyan).Append("  ").Append(item.DialedAt.ToString("MM/dd HH:mm")).Append(' ');
                sb.Append(Ansi.BrightWhite).Append(Truncate(item.DialedText, 18).PadRight(18)).Append(' ');
                sb.Append(resultColor).Append(Truncate(item.Result, 24));
                sb.Append(Ansi.Reset).Append("\r\n");
            }
        }

        sb.Append("\r\n");
        sb.Append(Ansi.Line(Ansi.Dim, "  M = main menu, Q = disconnect\r\n"));
        sb.Append(BuildAnsiPrompt());
        return sb.ToString();
    }

    private string BuildAnsiTimeScreen(bool includeMenuPrompt)
    {
        var now = DateTime.Now;
        var sb = new StringBuilder();
        sb.Append(Ansi.Clear);
        sb.Append(Ansi.Home);
        sb.Append(BuildAnsiHeader("RetroModem Bridge", "TIME SERVICE"));
        sb.Append(Ansi.Line(Ansi.BrightWhite, "  " + now.ToString("dddd, MMMM d, yyyy") + "\r\n"));
        sb.Append(Ansi.Line(Ansi.Cyan, "  " + now.ToString("h:mm tt") + "\r\n\r\n"));
        sb.Append(Ansi.Line(Ansi.Dim, "  Local PC time from the Windows bridge machine.\r\n\r\n"));
        sb.Append(includeMenuPrompt ? BuildAnsiPrompt() : Ansi.Line(Ansi.Green, "OK\r\n"));
        return sb.ToString();
    }

    private string BuildAnsiHelpScreen(bool includeMenuPrompt)
    {
        var sb = new StringBuilder();
        sb.Append(Ansi.Clear);
        sb.Append(Ansi.Home);
        sb.Append(BuildAnsiHeader("RetroModem Bridge", "COMMAND HELP"));
        sb.Append(Ansi.Line(Ansi.Yellow, "  AT                  ") + Ansi.Line(Ansi.BrightWhite, "Test modem\r\n"));
        sb.Append(Ansi.Line(Ansi.Yellow, "  ATI                 ") + Ansi.Line(Ansi.BrightWhite, "Bridge info\r\n"));
        sb.Append(Ansi.Line(Ansi.Yellow, "  AT&V                ") + Ansi.Line(Ansi.BrightWhite, "Current settings\r\n"));
        sb.Append(Ansi.Line(Ansi.Yellow, "  ATDT ALIAS          ") + Ansi.Line(Ansi.BrightWhite, "Dial saved BBS alias\r\n"));
        sb.Append(Ansi.Line(Ansi.Yellow, "  ATDT HOST:PORT      ") + Ansi.Line(Ansi.BrightWhite, "Dial direct Telnet host\r\n"));
        sb.Append(Ansi.Line(Ansi.Yellow, "  ATDL                ") + Ansi.Line(Ansi.BrightWhite, "Redial last number\r\n"));
        sb.Append(Ansi.Line(Ansi.Yellow, "  ATH                 ") + Ansi.Line(Ansi.BrightWhite, "Hang up\r\n\r\n"));
        sb.Append(Ansi.Line(Ansi.Magenta, "  Local services: ATDT MENU, HELP, TIME, BBSLIST, FAVORITES\r\n\r\n"));
        sb.Append(includeMenuPrompt ? BuildAnsiPrompt() : Ansi.Line(Ansi.Green, "OK\r\n"));
        return sb.ToString();
    }

    private string BuildAnsiAboutScreen()
    {
        var sb = new StringBuilder();
        sb.Append(Ansi.Clear);
        sb.Append(Ansi.Home);
        sb.Append(BuildAnsiHeader("RetroModem Bridge", "ABOUT"));
        sb.Append(Ansi.Line(Ansi.BrightWhite, "  RetroModem Bridge turns a Windows PC into a\r\n"));
        sb.Append(Ansi.Line(Ansi.BrightWhite, "  Hayes-style serial to Telnet bridge.\r\n\r\n"));
        sb.Append(Ansi.Line(Ansi.Cyan, "  Use a vintage terminal program to dial Telnet BBSes\r\n"));
        sb.Append(Ansi.Line(Ansi.Cyan, "  with commands like ATDT DARKREALMS.\r\n\r\n"));
        sb.Append(Ansi.Line(Ansi.Dim, "  This ANSI local menu is generated inside the app.\r\n\r\n"));
        sb.Append(BuildAnsiPrompt());
        return sb.ToString();
    }

    private static string BuildAnsiDialingScreen(BbsEntry entry)
    {
        var alias = string.IsNullOrWhiteSpace(entry.Alias) ? entry.DialTarget : entry.Alias.Trim();
        return Ansi.Clear + Ansi.Home +
               Ansi.Line(Ansi.BrightBlue, "\r\n  Dialing selected BBS...\r\n") +
               Ansi.Line(Ansi.BrightWhite, "  " + entry.DisplayName + "\r\n") +
               Ansi.Line(Ansi.Cyan, "  ATDT " + alias + "\r\n\r\n") +
               Ansi.Reset;
    }

    private static string BuildAnsiGoodbyeScreen()
    {
        return Ansi.Clear + Ansi.Home +
               Ansi.Line(Ansi.BrightBlue, "\r\n  RetroModem Bridge Local BBS\r\n") +
               Ansi.Line(Ansi.Cyan, "  Thanks for calling.\r\n\r\n") +
               Ansi.Line(Ansi.Green, "NO CARRIER\r\n") +
               Ansi.Reset;
    }

    private static string BuildAnsiErrorLine(string message)
    {
        return Ansi.Line(Ansi.Red, "\r\n  " + message + "\r\n") + BuildAnsiPrompt();
    }

    private static string BuildAnsiPrompt()
    {
        return Ansi.Line(Ansi.Magenta, "\r\n  RMB> ") + Ansi.BrightWhite;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Length <= maxLength ? value : value[..Math.Max(0, maxLength - 1)] + "~";
    }

    private (string DialString, BbsEntry? AliasEntry) ResolveDialString(string dialString)
    {
        var cleaned = dialString.Trim();
        var entry = DialDirectory.FirstOrDefault(e =>
            (!string.IsNullOrWhiteSpace(e.Alias) && string.Equals(e.Alias.Trim(), cleaned, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(e.Name) && string.Equals(e.Name.Trim(), cleaned, StringComparison.OrdinalIgnoreCase)));

        if (entry is null)
            return (cleaned, null);

        return ($"{entry.Host}:{entry.Port}", entry);
    }

    private static (string Host, int Port)? ParseDialString(string dialString, int defaultPort)
    {
        var cleaned = dialString.Trim();

        if (cleaned.StartsWith("//", StringComparison.Ordinal))
            cleaned = cleaned[2..];

        cleaned = cleaned.Replace(" ", string.Empty);

        if (cleaned.StartsWith("telnet://", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned[9..];

        var host = cleaned;
        var port = defaultPort;

        var colonIndex = cleaned.LastIndexOf(':');
        if (colonIndex > 0 && colonIndex < cleaned.Length - 1)
        {
            host = cleaned[..colonIndex];
            if (!int.TryParse(cleaned[(colonIndex + 1)..], out port))
                return null;
        }

        if (string.IsNullOrWhiteSpace(host) || port < 1 || port > 65535)
            return null;

        return (host, port);
    }

    private void MarkDirectoryResult(BbsEntry? entry, string result)
    {
        if (entry is null)
            return;

        entry.LastDialed = DateTime.Now;
        entry.LastResult = result;
        DirectoryChanged?.Invoke();
    }

    private void RecordHistory(string dialText, string host, int port, string result, DateTime started)
    {
        var entry = new DialHistoryEntry
        {
            DialedAt = started,
            DialedText = dialText,
            Host = host,
            Port = port,
            Result = result,
            DurationSeconds = Math.Round((DateTime.Now - started).TotalSeconds, 1)
        };

        try
        {
            DialHistory.Insert(0, entry);
            while (DialHistory.Count > 250)
                DialHistory.RemoveAt(DialHistory.Count - 1);
        }
        catch
        {
            // Do not let history persistence break dialing.
        }

        HistoryChanged?.Invoke();
    }

    private async Task PumpNetworkToSerialAsync(CancellationToken token)
    {
        var buffer = new byte[4096];

        try
        {
            while (!token.IsCancellationRequested)
            {
                var stream = _networkStream;
                if (stream is null || !stream.CanRead)
                    break;

                var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token).ConfigureAwait(false);
                if (read <= 0)
                    break;

                _tcpRxBytes += read;
                var outbound = TelnetFilteringEnabled ? FilterTelnet(buffer, read, stream) : buffer.Take(read).ToArray();

                if (outbound.Length > 0)
                    RaiseSessionMirrorBytes(outbound);

                var port = _serialPort;
                if (outbound.Length > 0 && port is not null && port.IsOpen)
                {
                    port.Write(outbound, 0, outbound.Length);
                    _serialTxBytes += outbound.Length;
                }

                TrafficChanged?.Invoke();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            LogMessage("Network read error: " + ex.Message);
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                HangUp("Remote disconnected", silent: true);
                SendResponse("NO CARRIER");
                SetStatus("Remote disconnected.");
            }
        }
    }

    private byte[] FilterTelnet(byte[] buffer, int length, NetworkStream stream)
    {
        const byte Iac = 255;
        const byte Do = 253;
        const byte Dont = 254;
        const byte Will = 251;
        const byte Wont = 252;
        const byte Sb = 250;
        const byte Se = 240;

        var output = new List<byte>(length);

        for (var i = 0; i < length; i++)
        {
            var b = buffer[i];
            if (b != Iac)
            {
                output.Add(b);
                continue;
            }

            if (i + 1 >= length)
                break;

            var command = buffer[++i];
            if (command == Iac)
            {
                output.Add(Iac);
                continue;
            }

            if (command is Do or Dont or Will or Wont)
            {
                if (i + 1 >= length)
                    break;

                var option = buffer[++i];
                var responseCommand = command == Do ? Wont : command == Will ? Dont : (byte)0;
                if (responseCommand != 0)
                {
                    try
                    {
                        stream.Write(new[] { Iac, responseCommand, option }, 0, 3);
                        stream.Flush();
                        _tcpTxBytes += 3;
                    }
                    catch (Exception ex)
                    {
                        LogMessage("Telnet negotiation response failed: " + ex.Message);
                    }
                }

                continue;
            }

            if (command == Sb)
            {
                while (i + 1 < length)
                {
                    i++;
                    if (buffer[i] == Iac && i + 1 < length && buffer[i + 1] == Se)
                    {
                        i++;
                        break;
                    }
                }
            }
        }

        return output.ToArray();
    }

    public void HangUp(string reason, bool silent = false)
    {
        ResetLocalBbsSession();

        lock (_sync)
        {
            try { _networkCts?.Cancel(); } catch { }
            try { _networkStream?.Dispose(); } catch { }
            try { _tcpClient?.Close(); } catch { }

            _networkCts?.Dispose();
            _networkCts = null;
            _networkStream = null;
            _tcpClient = null;
        }

        CurrentConnection = null;
        TrafficChanged?.Invoke();

        if (!silent)
        {
            LogMessage("Hangup: " + reason);
            SetStatus(IsSerialOpen ? "Serial open. Waiting for AT commands." : "Stopped.");
        }
    }

    private void SendResponse(string response)
    {
        WriteSerialText("\r\n" + response + "\r\n");
        LogMessage("Bridge > " + response);
    }

    private void WriteSerialText(string text)
    {
        try
        {
            RaiseSessionMirrorText(text);

            var port = _serialPort;
            if (port is not null && port.IsOpen)
            {
                port.Write(text);
                _serialTxBytes += _serialPort?.Encoding.GetByteCount(text) ?? Encoding.ASCII.GetByteCount(text);
                TrafficChanged?.Invoke();
            }
        }
        catch (Exception ex)
        {
            LogMessage("Serial write error: " + ex.Message);
        }
    }

    private void WriteSerialByte(byte value)
    {
        try
        {
            RaiseSessionMirrorBytes(new[] { value });

            var port = _serialPort;
            if (port is not null && port.IsOpen)
            {
                port.Write(new[] { value }, 0, 1);
                _serialTxBytes++;
                TrafficChanged?.Invoke();
            }
        }
        catch (Exception ex)
        {
            LogMessage("Serial echo error: " + ex.Message);
        }
    }

    private void LogMessage(string message)
    {
        var stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        Log?.Invoke($"{stamp} {message}");
    }

    private void SetStatus(string status) => StatusChanged?.Invoke(status);

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ModemBridge));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        CloseSerial();
    }
}

internal enum LocalBbsMenuMode
{
    Main,
    Directory,
    Favorites
}

internal static class Ansi
{
    public const string Reset = "\x1b[0m";
    public const string Clear = "\x1b[2J";
    public const string Home = "\x1b[H";
    public const string BrightWhite = "\x1b[1;37m";
    public const string BrightBlue = "\x1b[1;34m";
    public const string Blue = "\x1b[34m";
    public const string Cyan = "\x1b[36m";
    public const string Green = "\x1b[32m";
    public const string Yellow = "\x1b[33m";
    public const string BrightYellow = "\x1b[1;33m";
    public const string White = "\x1b[37m";
    public const string Magenta = "\x1b[35m";
    public const string Red = "\x1b[31m";
    public const string Dim = "\x1b[2m";

    public static string Line(string color, string text) => color + text + Reset;
}

