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
    private bool _disposed;

    public event Action<string>? Log;
    public event Action<string>? StatusChanged;

    public bool IsSerialOpen => _serialPort?.IsOpen == true;
    public bool IsConnected => _tcpClient?.Connected == true;
    public bool EchoEnabled { get; set; }
    public int DefaultTcpPort { get; set; } = 23;

    public void OpenSerial(string portName, int baudRate, bool dtrEnable, bool rtsEnable)
    {
        ThrowIfDisposed();

        CloseSerial();

        _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            DtrEnable = dtrEnable,
            RtsEnable = rtsEnable,
            NewLine = "\r",
            ReadTimeout = 250,
            WriteTimeout = 1000,
            Encoding = Encoding.ASCII
        };

        _serialPort.DataReceived += SerialPortOnDataReceived;
        _serialPort.Open();

        LogMessage($"Opened {portName} at {baudRate} 8-N-1. DTR={(dtrEnable ? "on" : "off")}, RTS={(rtsEnable ? "on" : "off")}. ");
        SetStatus("Serial open. Waiting for AT commands.");
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
    }

    public string GetLineStatusText()
    {
        var port = _serialPort;
        if (port is null || !port.IsOpen)
            return "Line status: serial closed";

        try
        {
            return $"Line status: CTS={(port.CtsHolding ? "On" : "Off")}  DSR={(port.DsrHolding ? "On" : "Off")}  DCD={(port.CDHolding ? "On" : "Off")}  DTR={(port.DtrEnable ? "On" : "Off")}  RTS={(port.RtsEnable ? "On" : "Off")}";
        }
        catch (Exception ex)
        {
            return "Line status unavailable: " + ex.Message;
        }
    }

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

            if (IsConnected)
            {
                var stream = _networkStream;
                if (stream is not null && stream.CanWrite)
                {
                    stream.Write(buffer, 0, read);
                    stream.Flush();
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

        LogMessage("Terminal > " + command);

        if (upper == "A/" || upper == "AT" || upper == "ATZ" || upper == "AT&F")
        {
            SendResponse("OK");
            return;
        }

        if (upper == "ATI" || upper == "ATI0")
        {
            SendResponse("RetroModem Bridge 1.0");
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
            HangUp("ATH received");
            SendResponse("OK");
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

        HangUp("Preparing new dial command", silent: true);

        var parsed = ParseDialString(dialString, DefaultTcpPort);
        if (parsed is null)
        {
            SendResponse("ERROR");
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

            SendResponse("CONNECT");
            LogMessage($"Connected to {host}:{port}.");
            SetStatus($"Connected to {host}:{port}");

            _ = Task.Run(() => PumpNetworkToSerialAsync(_networkCts.Token));
        }
        catch (Exception ex)
        {
            LogMessage("Connect failed: " + ex.Message);
            SetStatus("Connection failed.");
            SendResponse("NO CARRIER");
            HangUp("Connect failed", silent: true);
        }
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

                var port = _serialPort;
                if (port is not null && port.IsOpen)
                    port.Write(buffer, 0, read);
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

    public void HangUp(string reason, bool silent = false)
    {
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
            var port = _serialPort;
            if (port is not null && port.IsOpen)
                port.Write(text);
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
            var port = _serialPort;
            if (port is not null && port.IsOpen)
                port.Write(new[] { value }, 0, 1);
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
