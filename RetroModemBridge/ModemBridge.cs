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
    private List<BbsGuideEntry> _localGuideCurrentList = new();
    private BbsGuideEntry? _localGuideSelectedEntry;
    private int _localBbsPage;
    private string _localBbsSearchText = string.Empty;
    private const int LocalBbsPageSize = 15;
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
        if (key != "TIME" && key != "HELP" && key != "MENU" && key != "BBSLIST" && key != "FAVORITES" && key != "NEWS" && key != "FEATURED" && key != "RANDOM" && key != "ONLINE" && key != "GUIDE" && key != "IMPORT" && key != "UPDATEGUIDE" && key != "UPDATE")
            return false;

        LogMessage("Text service requested: " + key);

        if (key == "MENU")
        {
            StartLocalBbsSession();
            RecordHistory(dialString, key, 0, "Local BBS menu", started);
            SetStatus("Local BBS menu active.");
            return true;
        }

        if (key is "GUIDE" or "IMPORT")
        {
            StartLocalGuideSession();
            RecordHistory(dialString, key, 0, "Local guide browser", started);
            SetStatus("Local BBS Guide browser active.");
            return true;
        }

        if (key is "UPDATEGUIDE" or "UPDATE")
        {
            StartLocalGuideUpdateSession();
            RecordHistory(dialString, key, 0, "Local guide update", started);
            SetStatus("Local BBS Guide update active.");
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
            "ONLINE" => BuildAnsiBbsListScreen(DialDirectory.Where(e => e.LastResult.Contains("Online", StringComparison.OrdinalIgnoreCase) || e.LastResult.Contains("Connected", StringComparison.OrdinalIgnoreCase)), "RECENTLY ONLINE", includeDialPrompt: false),
            "FEATURED" => BuildAnsiFeaturedScreen(),
            "RANDOM" => BuildAnsiRandomScreen(),
            "NEWS" => BuildAnsiNewsScreen(),
            _ => "\r\nERROR\r\n"
        };
    }

    private void StartLocalBbsSession()
    {
        HangUp("Starting local BBS menu", silent: true);
        _localBbsSessionActive = true;
        _localBbsMenuMode = LocalBbsMenuMode.Main;
        _localBbsCurrentList = new List<BbsEntry>();
        _localGuideCurrentList = new List<BbsGuideEntry>();
        _localGuideSelectedEntry = null;

        WriteSerialText("\r\nCONNECT\r\n");
        WriteSerialText(BuildAnsiMainMenuScreen(firstConnect: true));
    }

    private void StartLocalGuideSession()
    {
        HangUp("Starting local BBS Guide browser", silent: true);
        _localBbsSessionActive = true;
        _localBbsMenuMode = LocalBbsMenuMode.Guide;
        _localBbsCurrentList = new List<BbsEntry>();
        _localGuideSelectedEntry = null;
        _localBbsSearchText = string.Empty;
        _localBbsPage = 0;
        RefreshLocalGuideCurrentList();

        WriteSerialText("\r\nCONNECT\r\n");
        WriteSerialText(BuildAnsiGuideListScreen());
    }

    private void StartLocalGuideUpdateSession()
    {
        HangUp("Starting local BBS Guide update", silent: true);
        _localBbsSessionActive = true;
        _localBbsMenuMode = LocalBbsMenuMode.UpdateGuide;
        _localBbsCurrentList = new List<BbsEntry>();
        _localGuideCurrentList = new List<BbsGuideEntry>();
        _localGuideSelectedEntry = null;
        _localBbsSearchText = string.Empty;
        _localBbsPage = 0;

        WriteSerialText("\r\nCONNECT\r\n");
        WriteSerialText(BuildAnsiGuideUpdateMenuScreen());
    }

    private async Task HandleLocalBbsInputAsync(string input)
    {
        var choice = (input ?? string.Empty).Trim();
        var upper = choice.ToUpperInvariant();

        if (_localBbsMenuMode == LocalBbsMenuMode.SearchGuide)
        {
            _localBbsSearchText = choice.Trim();
            _localBbsPage = 0;
            _localBbsMenuMode = LocalBbsMenuMode.Guide;
            RefreshLocalGuideCurrentList();
            WriteSerialText(BuildAnsiGuideListScreen());
            return;
        }

        if (_localBbsMenuMode is LocalBbsMenuMode.SearchDirectory or LocalBbsMenuMode.SearchFavorites)
        {
            _localBbsSearchText = choice.Trim();
            _localBbsPage = 0;

            _localBbsMenuMode = _localBbsMenuMode == LocalBbsMenuMode.SearchFavorites
                ? LocalBbsMenuMode.Favorites
                : LocalBbsMenuMode.Directory;

            RefreshLocalBbsCurrentList();
            WriteSerialText(BuildAnsiPagedBbsListScreen());
            return;
        }

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
            _localBbsSearchText = string.Empty;
            _localBbsPage = 0;
            WriteSerialText(BuildAnsiMainMenuScreen(firstConnect: false));
            return;
        }


        if (_localBbsMenuMode == LocalBbsMenuMode.UpdateGuide)
        {
            if (upper is "1" or "MONTHLY")
            {
                await RunLocalGuideUpdateAsync(GuideDownloadKind.Monthly).ConfigureAwait(false);
                return;
            }

            if (upper is "2" or "D" or "DAILY")
            {
                await RunLocalGuideUpdateAsync(GuideDownloadKind.Daily).ConfigureAwait(false);
                return;
            }

            if (upper is "3" or "S" or "STATUS")
            {
                WriteSerialText(BuildAnsiGuideStatusScreen());
                return;
            }

            WriteSerialText(BuildAnsiErrorLine("Choose 1 monthly, 2 daily, 3 status, M menu, or Q quit."));
            return;
        }


        if (_localBbsMenuMode == LocalBbsMenuMode.GuideDetails)
        {
            if (_localGuideSelectedEntry is null)
            {
                _localBbsMenuMode = LocalBbsMenuMode.Guide;
                WriteSerialText(BuildAnsiGuideListScreen());
                return;
            }

            if (upper is "A" or "ADD")
            {
                var added = AddGuideEntryToDirectory(_localGuideSelectedEntry);
                _localBbsMenuMode = LocalBbsMenuMode.Guide;
                _localGuideSelectedEntry = null;
                RefreshLocalGuideCurrentList();
                WriteSerialText(BuildAnsiGuideAddedScreen(added));
                return;
            }

            if (upper is "D" or "DIAL")
            {
                var selected = _localGuideSelectedEntry;
                ResetLocalBbsSession();
                WriteSerialText(BuildAnsiGuideDialingScreen(selected));
                await DialAsync(selected.Host + ":" + selected.Port).ConfigureAwait(false);
                return;
            }

            if (upper is "G" or "B" or "BACK" or "GUIDE")
            {
                _localBbsMenuMode = LocalBbsMenuMode.Guide;
                _localGuideSelectedEntry = null;
                WriteSerialText(BuildAnsiGuideListScreen());
                return;
            }

            WriteSerialText(BuildAnsiErrorLine("Use A to add, D to dial once, G to return to guide, or Q to quit."));
            return;
        }

        if (_localBbsMenuMode == LocalBbsMenuMode.Guide)
        {
            var totalPages = GetLocalGuideTotalPages();

            if (upper is "N" or "NEXT")
            {
                if (_localBbsPage < totalPages - 1)
                    _localBbsPage++;
                WriteSerialText(BuildAnsiGuideListScreen());
                return;
            }

            if (upper is "P" or "PREV" or "PREVIOUS")
            {
                if (_localBbsPage > 0)
                    _localBbsPage--;
                WriteSerialText(BuildAnsiGuideListScreen());
                return;
            }

            if (upper is "S" or "SEARCH" or "FIND")
            {
                _localBbsMenuMode = LocalBbsMenuMode.SearchGuide;
                WriteSerialText(BuildAnsiGuideSearchPromptScreen());
                return;
            }

            if (upper is "C" or "CLEAR")
            {
                _localBbsSearchText = string.Empty;
                _localBbsPage = 0;
                RefreshLocalGuideCurrentList();
                WriteSerialText(BuildAnsiGuideListScreen());
                return;
            }

            var visibleGuidePage = GetLocalGuidePageEntries();

            if (int.TryParse(choice, out var selectedNumber) && selectedNumber >= 1 && selectedNumber <= visibleGuidePage.Count)
            {
                _localGuideSelectedEntry = visibleGuidePage[selectedNumber - 1];
                _localBbsMenuMode = LocalBbsMenuMode.GuideDetails;
                WriteSerialText(BuildAnsiGuideEntryScreen(_localGuideSelectedEntry));
                return;
            }

            WriteSerialText(BuildAnsiErrorLine("Enter a listed number, N/P, S search, C clear, M menu, or Q quit."));
            return;
        }

        if (_localBbsMenuMode is LocalBbsMenuMode.Directory or LocalBbsMenuMode.Favorites)
        {
            var totalPages = GetLocalBbsTotalPages();

            if (upper is "N" or "NEXT")
            {
                if (_localBbsPage < totalPages - 1)
                    _localBbsPage++;
                WriteSerialText(BuildAnsiPagedBbsListScreen());
                return;
            }

            if (upper is "P" or "PREV" or "PREVIOUS")
            {
                if (_localBbsPage > 0)
                    _localBbsPage--;
                WriteSerialText(BuildAnsiPagedBbsListScreen());
                return;
            }

            if (upper is "S" or "SEARCH" or "FIND")
            {
                _localBbsMenuMode = _localBbsMenuMode == LocalBbsMenuMode.Favorites
                    ? LocalBbsMenuMode.SearchFavorites
                    : LocalBbsMenuMode.SearchDirectory;
                WriteSerialText(BuildAnsiSearchPromptScreen());
                return;
            }

            if (upper is "C" or "CLEAR")
            {
                _localBbsSearchText = string.Empty;
                _localBbsPage = 0;
                RefreshLocalBbsCurrentList();
                WriteSerialText(BuildAnsiPagedBbsListScreen());
                return;
            }

            var visiblePage = GetLocalBbsPageEntries();

            if (int.TryParse(choice, out var selectedNumber) && selectedNumber >= 1 && selectedNumber <= visiblePage.Count)
            {
                var selected = visiblePage[selectedNumber - 1];
                ResetLocalBbsSession();
                WriteSerialText(BuildAnsiDialingScreen(selected));
                await DialAsync(string.IsNullOrWhiteSpace(selected.Alias) ? selected.DialTarget : selected.Alias).ConfigureAwait(false);
                return;
            }

            WriteSerialText(BuildAnsiErrorLine("Enter a listed number, N/P, S search, C clear, M menu, or Q quit."));
            return;
        }

        switch (upper)
        {
            case "1":
            case "D":
            case "DIR":
            case "DIRECTORY":
                _localBbsMenuMode = LocalBbsMenuMode.Directory;
                _localBbsSearchText = string.Empty;
                _localBbsPage = 0;
                RefreshLocalBbsCurrentList();
                WriteSerialText(BuildAnsiPagedBbsListScreen());
                break;

            case "2":
            case "F":
            case "FAV":
            case "FAVORITES":
                _localBbsMenuMode = LocalBbsMenuMode.Favorites;
                _localBbsSearchText = string.Empty;
                _localBbsPage = 0;
                RefreshLocalBbsCurrentList();
                WriteSerialText(BuildAnsiPagedBbsListScreen());
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

            case "7":
            case "FEATURED":
            case "RANDOM":
                _localBbsMenuMode = LocalBbsMenuMode.Main;
                WriteSerialText(BuildAnsiRandomScreen());
                break;

            case "8":
            case "N":
            case "NEWS":
                _localBbsMenuMode = LocalBbsMenuMode.Main;
                WriteSerialText(BuildAnsiNewsScreen());
                break;

            case "9":
            case "G":
            case "GUIDE":
            case "IMPORT":
                _localBbsMenuMode = LocalBbsMenuMode.Guide;
                _localBbsSearchText = string.Empty;
                _localBbsPage = 0;
                RefreshLocalGuideCurrentList();
                WriteSerialText(BuildAnsiGuideListScreen());
                break;

            case "10":
            case "U":
            case "UPDATE":
            case "UPDATEGUIDE":
                _localBbsMenuMode = LocalBbsMenuMode.UpdateGuide;
                WriteSerialText(BuildAnsiGuideUpdateMenuScreen());
                break;

            default:
                WriteSerialText(BuildAnsiErrorLine("Unknown menu choice. Enter 1-9, U update, M menu, or Q quit."));
                break;
        }
    }

    private void ResetLocalBbsSession()
    {
        _localBbsSessionActive = false;
        _localBbsMenuMode = LocalBbsMenuMode.Main;
        _localBbsCurrentList.Clear();
        _localGuideCurrentList.Clear();
        _localGuideSelectedEntry = null;
        _localBbsSearchText = string.Empty;
        _localBbsPage = 0;
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
          .Append(Ansi.Yellow).Append("  [7] ")
          .Append(Ansi.BrightWhite).Append("Featured / Random".PadRight(28))
          .Append(Ansi.Yellow).Append("  [8] ")
          .Append(Ansi.BrightWhite).Append("News / Bulletin".PadRight(28))
          .Append(Ansi.BrightBlue).Append("│\r\n");

        sb.Append(Ansi.BrightBlue).Append("  │")
          .Append(Ansi.Yellow).Append("  [9] ")
          .Append(Ansi.BrightWhite).Append("Browse Import Guide".PadRight(28))
          .Append(Ansi.Yellow).Append("  [U] ")
          .Append(Ansi.BrightWhite).Append("Update Guide".PadRight(28))
          .Append(Ansi.BrightBlue).Append("│\r\n");

        sb.Append(Ansi.BrightBlue).Append("  │")
          .Append(Ansi.Red).Append("  [Q] ")
          .Append(Ansi.BrightWhite).Append("Disconnect".PadRight(61))
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


    private string BuildAnsiNewsScreen()
    {
        var sb = new StringBuilder();
        sb.Append(Ansi.Clear);
        sb.Append(Ansi.Home);
        sb.Append(BuildAnsiHeader("RetroModem Bridge", "NEWS / BULLETIN"));
        sb.Append(Ansi.Line(Ansi.BrightWhite, "  RetroModem Bridge Local Bulletin\r\n"));
        sb.Append(Ansi.Line(Ansi.Dim, "  --------------------------------\r\n\r\n"));
        sb.Append(Ansi.Line(Ansi.Cyan, "  New in this build:\r\n"));
        sb.Append(Ansi.Line(Ansi.BrightWhite, "  * Session Mirror with optional input and local echo\r\n"));
        sb.Append(Ansi.Line(Ansi.BrightWhite, "  * Test All Favorites and BBS health/status tracking\r\n"));
        sb.Append(Ansi.Line(Ansi.BrightWhite, "  * Retro computer profiles and support bundle export\r\n"));
        sb.Append(Ansi.Line(Ansi.BrightWhite, "  * Local services: NEWS, RANDOM, FEATURED, ONLINE\r\n\r\n"));
        sb.Append(Ansi.Line(Ansi.Yellow, "  Try: ATDT RANDOM, ATDT NEWS, ATDT ONLINE, ATDT FAVORITES\r\n"));
        sb.Append(BuildAnsiPrompt());
        return sb.ToString();
    }

    private string BuildAnsiFeaturedScreen()
    {
        var featured = DialDirectory
            .Where(e => e.IsFavorite && !string.IsNullOrWhiteSpace(e.Host))
            .OrderByDescending(e => e.LastResult.Contains("Online", StringComparison.OrdinalIgnoreCase))
            .ThenBy(e => e.Name)
            .FirstOrDefault()
            ?? DialDirectory.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.Host));

        var sb = new StringBuilder();
        sb.Append(Ansi.Clear);
        sb.Append(Ansi.Home);
        sb.Append(BuildAnsiHeader("RetroModem Bridge", "FEATURED BBS"));

        if (featured is null)
        {
            sb.Append(Ansi.Line(Ansi.Yellow, "  No BBS entries saved yet.\r\n"));
            sb.Append(BuildAnsiPrompt());
            return sb.ToString();
        }

        sb.Append(Ansi.Line(Ansi.BrightWhite, "  " + featured.DisplayName + "\r\n"));
        sb.Append(Ansi.Line(Ansi.Cyan, "  Host: " + featured.Host + ":" + featured.Port + "\r\n"));
        sb.Append(Ansi.Line(Ansi.Cyan, "  Alias: ATDT" + featured.Alias + "\r\n"));
        sb.Append(Ansi.Line(Ansi.Dim, "  Status: " + (string.IsNullOrWhiteSpace(featured.LastResult) ? "Not checked" : featured.LastResult) + "\r\n\r\n"));
        sb.Append(Ansi.Line(Ansi.Yellow, "  Dial it from your terminal with ATDT" + featured.Alias + "\r\n"));
        sb.Append(BuildAnsiPrompt());
        return sb.ToString();
    }

    private string BuildAnsiRandomScreen()
    {
        var candidates = DialDirectory
            .Where(e => e.IsFavorite && !string.IsNullOrWhiteSpace(e.Host))
            .ToList();

        if (candidates.Count == 0)
            candidates = DialDirectory.Where(e => !string.IsNullOrWhiteSpace(e.Host)).ToList();

        var pick = candidates.Count == 0 ? null : candidates[Random.Shared.Next(candidates.Count)];

        var sb = new StringBuilder();
        sb.Append(Ansi.Clear);
        sb.Append(Ansi.Home);
        sb.Append(BuildAnsiHeader("RetroModem Bridge", "RANDOM BBS"));

        if (pick is null)
        {
            sb.Append(Ansi.Line(Ansi.Yellow, "  No BBS entries saved yet.\r\n"));
            sb.Append(BuildAnsiPrompt());
            return sb.ToString();
        }

        sb.Append(Ansi.Line(Ansi.BrightWhite, "  Random pick: " + pick.DisplayName + "\r\n"));
        sb.Append(Ansi.Line(Ansi.Cyan, "  Host: " + pick.Host + ":" + pick.Port + "\r\n"));
        sb.Append(Ansi.Line(Ansi.Cyan, "  Dial: ATDT" + pick.Alias + "\r\n"));
        sb.Append(Ansi.Line(Ansi.Dim, "  Notes: " + Truncate(pick.Notes, 50) + "\r\n\r\n"));
        sb.Append(Ansi.Line(Ansi.Yellow, "  Type ATDT" + pick.Alias + " to dial this board.\r\n"));
        sb.Append(BuildAnsiPrompt());
        return sb.ToString();
    }

    private void RefreshLocalBbsCurrentList()
    {
        IEnumerable<BbsEntry> entries = DialDirectory.Where(e => !string.IsNullOrWhiteSpace(e.Host));

        if (_localBbsMenuMode is LocalBbsMenuMode.Favorites or LocalBbsMenuMode.SearchFavorites)
            entries = entries.Where(e => e.IsFavorite);

        if (!string.IsNullOrWhiteSpace(_localBbsSearchText))
            entries = entries.Where(e => MatchesLocalBbsSearch(e, _localBbsSearchText));

        _localBbsCurrentList = entries
            .OrderByDescending(e => e.IsFavorite)
            .ThenBy(e => e.Name)
            .ThenBy(e => e.Alias)
            .ToList();

        var totalPages = GetLocalBbsTotalPages();
        if (_localBbsPage >= totalPages)
            _localBbsPage = Math.Max(0, totalPages - 1);
    }

    private static bool MatchesLocalBbsSearch(BbsEntry entry, string search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return true;

        return entry.Alias.Contains(search, StringComparison.OrdinalIgnoreCase)
            || entry.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
            || entry.Host.Contains(search, StringComparison.OrdinalIgnoreCase)
            || entry.Category.Contains(search, StringComparison.OrdinalIgnoreCase)
            || entry.SystemType.Contains(search, StringComparison.OrdinalIgnoreCase)
            || entry.Notes.Contains(search, StringComparison.OrdinalIgnoreCase)
            || entry.LastResult.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private int GetLocalBbsTotalPages()
    {
        return Math.Max(1, (int)Math.Ceiling(_localBbsCurrentList.Count / (double)LocalBbsPageSize));
    }

    private List<BbsEntry> GetLocalBbsPageEntries()
    {
        return _localBbsCurrentList
            .Skip(_localBbsPage * LocalBbsPageSize)
            .Take(LocalBbsPageSize)
            .ToList();
    }

    private string BuildAnsiPagedBbsListScreen()
    {
        var title = _localBbsMenuMode == LocalBbsMenuMode.Favorites ? "FAVORITES" : "BBS DIRECTORY";
        var list = GetLocalBbsPageEntries();
        var totalPages = GetLocalBbsTotalPages();

        var sb = new StringBuilder();
        sb.Append(Ansi.Clear);
        sb.Append(Ansi.Home);
        sb.Append(BuildAnsiHeader("RetroModem Bridge", title));

        var searchLabel = string.IsNullOrWhiteSpace(_localBbsSearchText)
            ? "none"
            : Truncate(_localBbsSearchText, 34);

        sb.Append(Ansi.Cyan).Append("  Page ").Append((_localBbsPage + 1).ToString()).Append(" of ").Append(totalPages.ToString());
        sb.Append(Ansi.Dim).Append("   Matches: ").Append(_localBbsCurrentList.Count.ToString());
        sb.Append(Ansi.Dim).Append("   Search: ").Append(searchLabel).Append(Ansi.Reset).Append("\r\n\r\n");

        if (list.Count == 0)
        {
            sb.Append(Ansi.Line(Ansi.Yellow, _localBbsMenuMode == LocalBbsMenuMode.Favorites ? "  No matching favorites found.\r\n" : "  No matching BBS entries found.\r\n"));
        }
        else
        {
            for (var i = 0; i < list.Count; i++)
            {
                var entry = list[i];
                var number = (i + 1).ToString("00");
                var star = entry.IsFavorite ? "*" : " ";
                var name = Truncate(string.IsNullOrWhiteSpace(entry.Name) ? entry.DialTarget : entry.Name.Trim(), 26);
                var alias = Truncate(string.IsNullOrWhiteSpace(entry.Alias) ? entry.DialTarget : entry.Alias.Trim(), 12);
                var ansi = entry.SupportsAnsi ? "ANSI" : "TXT ";
                var status = Truncate(string.IsNullOrWhiteSpace(entry.LastResult) ? "" : entry.LastResult, 10);

                sb.Append(Ansi.Yellow).Append("  [").Append(number).Append("] ");
                sb.Append(entry.IsFavorite ? Ansi.Magenta : Ansi.Dim).Append(star).Append(' ');
                sb.Append(Ansi.BrightWhite).Append(name.PadRight(26));
                sb.Append(Ansi.Cyan).Append(' ').Append(alias.PadRight(12));
                sb.Append(Ansi.Green).Append(' ').Append(ansi);
                if (!string.IsNullOrWhiteSpace(status))
                    sb.Append(Ansi.Dim).Append(' ').Append(status);
                sb.Append(Ansi.Reset).Append("\r\n");
            }
        }

        sb.Append("\r\n");
        sb.Append(Ansi.Line(Ansi.Dim, "  01-15 = dial  N = next  P = prev  S = search  C = clear\r\n"));
        sb.Append(Ansi.Line(Ansi.Dim, "  M = main menu  Q = disconnect\r\n"));
        sb.Append(BuildAnsiPrompt());
        return sb.ToString();
    }

    private string BuildAnsiSearchPromptScreen()
    {
        var title = _localBbsMenuMode == LocalBbsMenuMode.SearchFavorites
            ? "SEARCH FAVORITES"
            : "SEARCH DIRECTORY";

        var sb = new StringBuilder();
        sb.Append(Ansi.Clear);
        sb.Append(Ansi.Home);
        sb.Append(BuildAnsiHeader("RetroModem Bridge", title));
        sb.Append(Ansi.Line(Ansi.BrightWhite, "  Search by name, alias, host, category, system, notes,\r\n"));
        sb.Append(Ansi.Line(Ansi.BrightWhite, "  or last connection result.\r\n\r\n"));
        sb.Append(Ansi.Line(Ansi.Dim, "  Examples: coco, mystic, ansi, games, florida, online\r\n\r\n"));
        sb.Append(Ansi.Line(Ansi.Yellow, "  Search for: "));
        return sb.ToString();
    }

    private void RefreshLocalGuideCurrentList()
    {
        var existing = DialDirectory
            .Where(e => !string.IsNullOrWhiteSpace(e.Host))
            .Select(e => $"{e.Host.Trim().ToLowerInvariant()}:{e.Port}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        IEnumerable<BbsGuideEntry> entries;
        try
        {
            entries = BbsGuideParser.LoadCurrentList()
                .Where(e => !string.IsNullOrWhiteSpace(e.Host))
                .Where(e => !existing.Contains($"{e.Host.Trim().ToLowerInvariant()}:{e.Port}"));
        }
        catch (Exception ex)
        {
            LogMessage("Could not load bundled Telnet BBS Guide: " + ex.Message);
            entries = Array.Empty<BbsGuideEntry>();
        }

        if (!string.IsNullOrWhiteSpace(_localBbsSearchText))
            entries = entries.Where(e => MatchesLocalGuideSearch(e, _localBbsSearchText));

        _localGuideCurrentList = entries
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Host, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var totalPages = GetLocalGuideTotalPages();
        if (_localBbsPage >= totalPages)
            _localBbsPage = Math.Max(0, totalPages - 1);
    }

    private static bool MatchesLocalGuideSearch(BbsGuideEntry entry, string search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return true;

        return entry.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
            || entry.Host.Contains(search, StringComparison.OrdinalIgnoreCase)
            || entry.Software.Contains(search, StringComparison.OrdinalIgnoreCase)
            || entry.Location.Contains(search, StringComparison.OrdinalIgnoreCase)
            || entry.Source.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private int GetLocalGuideTotalPages()
    {
        return Math.Max(1, (int)Math.Ceiling(_localGuideCurrentList.Count / (double)LocalBbsPageSize));
    }

    private List<BbsGuideEntry> GetLocalGuidePageEntries()
    {
        return _localGuideCurrentList
            .Skip(_localBbsPage * LocalBbsPageSize)
            .Take(LocalBbsPageSize)
            .ToList();
    }

    private BbsEntry AddGuideEntryToDirectory(BbsGuideEntry guideEntry)
    {
        var existing = DialDirectory.FirstOrDefault(e =>
            string.Equals(e.Host.Trim(), guideEntry.Host.Trim(), StringComparison.OrdinalIgnoreCase) &&
            e.Port == guideEntry.Port);

        if (existing is not null)
            return existing;

        var alias = NextDirectoryAlias();
        var entry = guideEntry.ToDirectoryEntry(alias);

        if (DialDirectory is IList<BbsEntry> list)
            list.Add(entry);

        DirectoryChanged?.Invoke();
        LogMessage($"Added guide entry from local BBS: {entry.DisplayName} ({entry.Host}:{entry.Port}) as alias {entry.Alias}.");
        return entry;
    }

    private string NextDirectoryAlias()
    {
        var used = DialDirectory
            .Select(e => e.Alias)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var i = 1; i < 10000; i++)
        {
            var alias = i.ToString();
            if (!used.Contains(alias))
                return alias;
        }

        return DateTime.Now.ToString("HHmmss");
    }

    private async Task RunLocalGuideUpdateAsync(GuideDownloadKind kind)
    {
        try
        {
            var progress = new Progress<string>(status =>
            {
                var parts = status.Split('|', 2);
                var percent = 0;
                var message = status;

                if (parts.Length == 2 && int.TryParse(parts[0], out var parsed))
                {
                    percent = parsed;
                    message = parts[1];
                }

                WriteSerialText(BuildAnsiGuideUpdateProgressScreen(kind, percent, message));
            });

            WriteSerialText(BuildAnsiGuideUpdateProgressScreen(kind, 5, "Starting guide update"));
            var result = await TelnetBbsGuideUpdater.DownloadAndInstallAsync(kind, progress).ConfigureAwait(false);

            _localBbsMenuMode = LocalBbsMenuMode.UpdateGuide;
            WriteSerialText(BuildAnsiGuideUpdateCompleteScreen(result));
        }
        catch (Exception ex)
        {
            _localBbsMenuMode = LocalBbsMenuMode.UpdateGuide;
            WriteSerialText(BuildAnsiGuideUpdateErrorScreen(ex.Message));
            LogMessage("Guide update failed: " + ex.Message);
        }
    }

    private (int DirectoryCount, int GuideCount, int NewCount, int AlreadyAddedCount, int FavoritesCount, int OnlineCount) GetGuideStats()
    {
        var directoryEntries = DialDirectory
            .Where(e => !string.IsNullOrWhiteSpace(e.Host))
            .ToList();

        var existing = directoryEntries
            .Select(e => $"{e.Host.Trim().ToLowerInvariant()}:{e.Port}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var guideEntries = new List<BbsGuideEntry>();

        try
        {
            guideEntries = BbsGuideParser.LoadCurrentList()
                .Where(e => !string.IsNullOrWhiteSpace(e.Host))
                .GroupBy(e => $"{e.Host.Trim().ToLowerInvariant()}:{e.Port}")
                .Select(g => g.First())
                .ToList();
        }
        catch
        {
        }

        var alreadyAdded = guideEntries.Count(e => existing.Contains($"{e.Host.Trim().ToLowerInvariant()}:{e.Port}"));
        var newCount = Math.Max(0, guideEntries.Count - alreadyAdded);
        var favorites = directoryEntries.Count(e => e.IsFavorite);
        var online = directoryEntries.Count(e =>
            e.LastResult.Contains("Online", StringComparison.OrdinalIgnoreCase) ||
            e.LastResult.Contains("Connected", StringComparison.OrdinalIgnoreCase));

        return (directoryEntries.Count, guideEntries.Count, newCount, alreadyAdded, favorites, online);
    }

    private string BuildAnsiGuideStatsBlock()
    {
        var stats = GetGuideStats();
        var sb = new StringBuilder();

        sb.Append(Ansi.BrightBlue).Append("  ┌").Append(new string('─', 58)).Append("┐\r\n");
        sb.Append(Ansi.BrightBlue).Append("  │").Append(Ansi.BrightWhite).Append("  GUIDE / DIRECTORY STATS".PadRight(58)).Append(Ansi.BrightBlue).Append("│\r\n");
        sb.Append(Ansi.BrightBlue).Append("  │").Append(Ansi.Cyan).Append("  Current BBS Directory listings : ").Append(Ansi.BrightWhite).Append(stats.DirectoryCount.ToString().PadLeft(5)).Append(Ansi.BrightBlue).Append(new string(' ', 18)).Append("│\r\n");
        sb.Append(Ansi.BrightBlue).Append("  │").Append(Ansi.Cyan).Append("  Current guide listings         : ").Append(Ansi.BrightWhite).Append(stats.GuideCount.ToString().PadLeft(5)).Append(Ansi.BrightBlue).Append(new string(' ', 18)).Append("│\r\n");
        sb.Append(Ansi.BrightBlue).Append("  │").Append(Ansi.Green).Append("  New guide listings to add      : ").Append(Ansi.BrightWhite).Append(stats.NewCount.ToString().PadLeft(5)).Append(Ansi.BrightBlue).Append(new string(' ', 18)).Append("│\r\n");
        sb.Append(Ansi.BrightBlue).Append("  │").Append(Ansi.Dim).Append("  Already in your directory      : ").Append(Ansi.BrightWhite).Append(stats.AlreadyAddedCount.ToString().PadLeft(5)).Append(Ansi.BrightBlue).Append(new string(' ', 18)).Append("│\r\n");
        sb.Append(Ansi.BrightBlue).Append("  │").Append(Ansi.Magenta).Append("  Favorites / online-tested      : ").Append(Ansi.BrightWhite).Append(stats.FavoritesCount.ToString().PadLeft(5)).Append(Ansi.Dim).Append(" / ").Append(Ansi.BrightWhite).Append(stats.OnlineCount.ToString().PadLeft(5)).Append(Ansi.BrightBlue).Append(new string(' ', 10)).Append("│\r\n");
        sb.Append(Ansi.BrightBlue).Append("  └").Append(new string('─', 58)).Append("┘\r\n\r\n");

        return sb.ToString();
    }

    private string BuildAnsiGuideUpdateMenuScreen()
    {
        var sb = new StringBuilder();
        sb.Append(Ansi.Clear);
        sb.Append(Ansi.Home);
        sb.Append(BuildAnsiHeader("RetroModem Bridge", "UPDATE BBS GUIDE"));
        sb.Append(Ansi.Line(Ansi.BrightWhite, "  Download the latest Telnet BBS Guide from the web.\r\n\r\n"));
        sb.Append(BuildAnsiGuideStatsBlock());
        sb.Append(Ansi.Line(Ansi.Yellow, "  [1] Monthly official list\r\n"));
        sb.Append(Ansi.Line(Ansi.Yellow, "  [2] Daily personal-use list\r\n"));
        sb.Append(Ansi.Line(Ansi.Yellow, "  [3] Current guide status\r\n\r\n"));
        sb.Append(Ansi.Line(Ansi.Dim, "  M = main menu, Q = disconnect\r\n\r\n"));
        sb.Append(Ansi.Line(Ansi.Cyan, "  " + TelnetBbsGuideUpdater.GetStatusText() + "\r\n"));
        sb.Append(BuildAnsiPrompt());
        return sb.ToString();
    }

    private string BuildAnsiGuideStatusScreen()
    {
        var sb = new StringBuilder();
        sb.Append(Ansi.Clear);
        sb.Append(Ansi.Home);
        sb.Append(BuildAnsiHeader("RetroModem Bridge", "GUIDE STATUS"));
        sb.Append(Ansi.Line(Ansi.BrightWhite, "  " + TelnetBbsGuideUpdater.GetStatusText() + "\r\n\r\n"));
        sb.Append(BuildAnsiGuideStatsBlock());
        sb.Append(Ansi.Line(Ansi.Dim, "  Guide path:\r\n"));
        sb.Append(Ansi.Line(Ansi.Cyan, "  " + Truncate(TelnetBbsGuideUpdater.GetPreferredCsvPath(), 62) + "\r\n\r\n"));
        sb.Append(Ansi.Line(Ansi.Dim, "  Use ATDT GUIDE to browse entries not already in your directory.\r\n"));
        sb.Append(BuildAnsiPrompt());
        return sb.ToString();
    }

    private string BuildAnsiGuideUpdateProgressScreen(GuideDownloadKind kind, int percent, string message)
    {
        percent = Math.Max(0, Math.Min(100, percent));
        var width = 42;
        var filled = (int)Math.Round(width * (percent / 100.0));
        var empty = Math.Max(0, width - filled);
        var bar = new string('█', filled) + new string('░', empty);

        var sb = new StringBuilder();
        sb.Append(Ansi.Clear);
        sb.Append(Ansi.Home);
        sb.Append(BuildAnsiHeader("RetroModem Bridge", "GUIDE UPDATE"));
        sb.Append(Ansi.Line(Ansi.BrightWhite, "  Source: " + kind + "\r\n\r\n"));
        sb.Append(Ansi.BrightBlue).Append("  ╔").Append(new string('═', width)).Append("╗\r\n");
        sb.Append(Ansi.BrightBlue).Append("  ║").Append(Ansi.Green).Append(bar).Append(Ansi.BrightBlue).Append("║\r\n");
        sb.Append(Ansi.BrightBlue).Append("  ╚").Append(new string('═', width)).Append("╝\r\n");
        sb.Append(Ansi.Yellow).Append("  ").Append(percent.ToString().PadLeft(3)).Append("%  ");
        sb.Append(Ansi.BrightWhite).Append(Truncate(message, 46)).Append(Ansi.Reset).Append("\r\n\r\n");
        sb.Append(Ansi.Dim).Append("  Please wait. Do not hang up while the guide updates.\r\n");
        return sb.ToString();
    }

    private string BuildAnsiGuideUpdateCompleteScreen(GuideUpdateResult result)
    {
        RefreshLocalGuideCurrentList();

        var sb = new StringBuilder();
        sb.Append(Ansi.Clear);
        sb.Append(Ansi.Home);
        sb.Append(BuildAnsiHeader("RetroModem Bridge", "GUIDE UPDATED"));
        var stats = GetGuideStats();

        sb.Append(Ansi.Line(Ansi.Green, "  Update complete!\r\n\r\n"));
        sb.Append(Ansi.Line(Ansi.BrightWhite, "  Source: " + result.Kind + "\r\n"));
        sb.Append(Ansi.Line(Ansi.BrightWhite, "  Downloaded guide entries: " + result.EntryCount + "\r\n"));
        sb.Append(Ansi.Line(Ansi.Cyan, "  Current BBS Directory listings: " + stats.DirectoryCount + "\r\n"));
        sb.Append(Ansi.Line(Ansi.Green, "  New guide listings to add: " + stats.NewCount + "\r\n"));
        sb.Append(Ansi.Line(Ansi.Dim, "  Already in your directory: " + stats.AlreadyAddedCount + "\r\n"));
        sb.Append(Ansi.Line(Ansi.Magenta, "  Favorites / online-tested: " + stats.FavoritesCount + " / " + stats.OnlineCount + "\r\n\r\n"));
        sb.Append(Ansi.Line(Ansi.Yellow, "  Type ATDT GUIDE or choose 9 from the menu to browse.\r\n\r\n"));
        sb.Append(Ansi.Line(Ansi.Dim, "  M = main menu, Q = disconnect\r\n"));
        sb.Append(BuildAnsiPrompt());
        return sb.ToString();
    }

    private string BuildAnsiGuideUpdateErrorScreen(string message)
    {
        var sb = new StringBuilder();
        sb.Append(Ansi.Clear);
        sb.Append(Ansi.Home);
        sb.Append(BuildAnsiHeader("RetroModem Bridge", "UPDATE FAILED"));
        sb.Append(Ansi.Line(Ansi.Red, "  Could not update the Telnet BBS Guide.\r\n\r\n"));
        sb.Append(Ansi.Line(Ansi.BrightWhite, "  " + Truncate(message, 62) + "\r\n\r\n"));
        sb.Append(Ansi.Line(Ansi.Dim, "  The site may be blocking automated downloads.\r\n"));
        sb.Append(Ansi.Line(Ansi.Dim, "  Use the Windows app to load a downloaded ZIP/CSV manually.\r\n\r\n"));
        sb.Append(Ansi.Line(Ansi.Dim, "  M = main menu, Q = disconnect\r\n"));
        sb.Append(BuildAnsiPrompt());
        return sb.ToString();
    }

    private string BuildAnsiGuideListScreen()
    {
        var list = GetLocalGuidePageEntries();
        var totalPages = GetLocalGuideTotalPages();

        var sb = new StringBuilder();
        sb.Append(Ansi.Clear);
        sb.Append(Ansi.Home);
        sb.Append(BuildAnsiHeader("RetroModem Bridge", "IMPORT GUIDE"));

        var searchLabel = string.IsNullOrWhiteSpace(_localBbsSearchText)
            ? "none"
            : Truncate(_localBbsSearchText, 34);

        sb.Append(Ansi.Cyan).Append("  Page ").Append((_localBbsPage + 1).ToString()).Append(" of ").Append(totalPages.ToString());
        sb.Append(Ansi.Dim).Append("   Not in directory: ").Append(_localGuideCurrentList.Count.ToString());
        sb.Append(Ansi.Dim).Append("   Search: ").Append(searchLabel).Append(Ansi.Reset).Append("\r\n\r\n");

        if (list.Count == 0)
        {
            sb.Append(Ansi.Line(Ansi.Yellow, "  No guide entries found that are not already in your directory.\r\n"));
        }
        else
        {
            for (var i = 0; i < list.Count; i++)
            {
                var entry = list[i];
                var number = (i + 1).ToString("00");
                var name = Truncate(entry.Name, 27);
                var host = Truncate(entry.Host + ":" + entry.Port, 25);
                var software = Truncate(entry.Software, 10);

                sb.Append(Ansi.Yellow).Append("  [").Append(number).Append("] ");
                sb.Append(Ansi.BrightWhite).Append(name.PadRight(27));
                sb.Append(Ansi.Cyan).Append(' ').Append(host.PadRight(25));
                if (!string.IsNullOrWhiteSpace(software))
                    sb.Append(Ansi.Dim).Append(' ').Append(software);
                sb.Append(Ansi.Reset).Append("\r\n");
            }
        }

        sb.Append("\r\n");
        sb.Append(Ansi.Line(Ansi.Dim, "  01-15 = view  N = next  P = prev  S = search  C = clear\r\n"));
        sb.Append(Ansi.Line(Ansi.Dim, "  M = main menu  Q = disconnect\r\n"));
        sb.Append(BuildAnsiPrompt());
        return sb.ToString();
    }

    private string BuildAnsiGuideEntryScreen(BbsGuideEntry entry)
    {
        var sb = new StringBuilder();
        sb.Append(Ansi.Clear);
        sb.Append(Ansi.Home);
        sb.Append(BuildAnsiHeader("RetroModem Bridge", "GUIDE ENTRY"));

        sb.Append(Ansi.Line(Ansi.BrightWhite, "  " + Truncate(entry.Name, 58) + "\r\n"));
        sb.Append(Ansi.Line(Ansi.Cyan, "  Host: " + entry.Host + ":" + entry.Port + "\r\n"));
        if (!string.IsNullOrWhiteSpace(entry.Software))
            sb.Append(Ansi.Line(Ansi.Cyan, "  Software: " + Truncate(entry.Software, 48) + "\r\n"));
        if (!string.IsNullOrWhiteSpace(entry.Location))
            sb.Append(Ansi.Line(Ansi.Cyan, "  Location: " + Truncate(entry.Location, 48) + "\r\n"));

        sb.Append("\r\n");
        sb.Append(Ansi.Line(Ansi.Yellow, "  [A] Add to your BBS Directory\r\n"));
        sb.Append(Ansi.Line(Ansi.Yellow, "  [D] Dial once without saving\r\n"));
        sb.Append(Ansi.Line(Ansi.Yellow, "  [G] Back to guide list\r\n"));
        sb.Append(Ansi.Line(Ansi.Dim, "  M = main menu, Q = disconnect\r\n"));
        sb.Append(BuildAnsiPrompt());
        return sb.ToString();
    }

    private string BuildAnsiGuideAddedScreen(BbsEntry entry)
    {
        var sb = new StringBuilder();
        sb.Append(Ansi.Clear);
        sb.Append(Ansi.Home);
        sb.Append(BuildAnsiHeader("RetroModem Bridge", "ADDED TO DIRECTORY"));
        sb.Append(Ansi.Line(Ansi.BrightWhite, "  Added: " + Truncate(entry.DisplayName, 50) + "\r\n"));
        sb.Append(Ansi.Line(Ansi.Cyan, "  Host: " + entry.Host + ":" + entry.Port + "\r\n"));
        sb.Append(Ansi.Line(Ansi.Cyan, "  Alias: " + entry.Alias + "\r\n\r\n"));
        sb.Append(Ansi.Line(Ansi.Yellow, "  Dial anytime with: ATDT" + entry.Alias + "\r\n\r\n"));
        sb.Append(Ansi.Line(Ansi.Dim, "  Returning to the guide list. The added board is now hidden.\r\n\r\n"));
        sb.Append(Ansi.Line(Ansi.Dim, "  N/P browse, S search, M menu, Q quit\r\n"));
        sb.Append(BuildAnsiPrompt());
        return sb.ToString();
    }

    private static string BuildAnsiGuideDialingScreen(BbsGuideEntry entry)
    {
        return Ansi.Clear + Ansi.Home +
               Ansi.Line(Ansi.BrightBlue, "\r\n  Dialing guide entry once...\r\n") +
               Ansi.Line(Ansi.BrightWhite, "  " + entry.Name + "\r\n") +
               Ansi.Line(Ansi.Cyan, "  " + entry.Host + ":" + entry.Port + "\r\n\r\n") +
               Ansi.Reset;
    }

    private string BuildAnsiGuideSearchPromptScreen()
    {
        var sb = new StringBuilder();
        sb.Append(Ansi.Clear);
        sb.Append(Ansi.Home);
        sb.Append(BuildAnsiHeader("RetroModem Bridge", "SEARCH IMPORT GUIDE"));
        sb.Append(Ansi.Line(Ansi.BrightWhite, "  Search the bundled Telnet BBS Guide entries that are\r\n"));
        sb.Append(Ansi.Line(Ansi.BrightWhite, "  not already in your BBS Directory.\r\n\r\n"));
        sb.Append(Ansi.Line(Ansi.Dim, "  Examples: coco, mystic, synchronet, games, florida\r\n\r\n"));
        sb.Append(Ansi.Line(Ansi.Yellow, "  Search for: "));
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
        sb.Append(Ansi.Line(Ansi.Magenta, "  Local services: ATDT MENU, HELP, TIME, BBSLIST, FAVORITES\r\n"));
        sb.Append(Ansi.Line(Ansi.Magenta, "                  ATDT NEWS, RANDOM, FEATURED, ONLINE, GUIDE, UPDATEGUIDE\r\n"));
        sb.Append(Ansi.Line(Ansi.Dim, "  Directory menu: N next, P previous, S search, C clear search\r\n\r\n"));
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
        entry.LastChecked = DateTime.Now;
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
    Favorites,
    SearchDirectory,
    SearchFavorites,
    Guide,
    SearchGuide,
    GuideDetails,
    UpdateGuide
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

