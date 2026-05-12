using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Text.RegularExpressions;

namespace RetroModemBridge;

public sealed class IrcBridgeSession : IDisposable
{
    private readonly IrcPreset _preset;
    private readonly Action<string> _writeSerialText;
    private readonly Action<string> _log;
    private readonly Action _trafficChanged;
    private readonly Encoding _encoding;
    private TcpClient? _client;
    private Stream? _stream;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private readonly StringBuilder _inputBuffer = new();
    private readonly CancellationTokenSource _cts = new();
    private string _nick;
    private string _channel;
    private bool _joined;
    private bool _disposed;

    public IrcBridgeSession(IrcPreset preset, Encoding? serialEncoding, Action<string> writeSerialText, Action<string> log, Action trafficChanged)
    {
        _preset = preset;
        _writeSerialText = writeSerialText;
        _log = log;
        _trafficChanged = trafficChanged;
        _encoding = serialEncoding ?? Encoding.GetEncoding(437);
        _nick = SafeNick(string.IsNullOrWhiteSpace(preset.Nickname) ? "RMBUser" : preset.Nickname);
        _channel = string.IsNullOrWhiteSpace(preset.Channel) ? "#retromodem" : preset.Channel.Trim();
    }

    public bool IsConnected => _client?.Connected == true && _stream is not null;
    public string CurrentConnection => $"IRC {_preset.Server}:{_preset.Port} {_channel}";

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_preset.Server))
            throw new InvalidOperationException("IRC server is blank.");

        _client = new TcpClient();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(20));
        await _client.ConnectAsync(_preset.Server.Trim(), _preset.Port, timeoutCts.Token).ConfigureAwait(false);

        Stream stream = _client.GetStream();
        if (_preset.UseTls)
        {
            var ssl = new SslStream(stream, false, (sender, certificate, chain, errors) => errors == System.Net.Security.SslPolicyErrors.None);
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = _preset.Server.Trim(),
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            }, timeoutCts.Token).ConfigureAwait(false);
            stream = ssl;
        }

        _stream = stream;
        _reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true);
        _writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true)
        {
            NewLine = "\r\n",
            AutoFlush = true
        };

        await SendRawAsync($"NICK {_nick}").ConfigureAwait(false);
        await SendRawAsync($"USER {_nick} 0 * :{Sanitize(_preset.RealName)}").ConfigureAwait(false);
        _ = Task.Run(ReadLoopAsync);
    }

    public void HandleSerialBytes(byte[] bytes, int count)
    {
        if (bytes.Length == 0 || count <= 0)
            return;

        for (var i = 0; i < count; i++)
        {
            var value = bytes[i];
            var ch = (char)value;
            if (ch == '\r' || ch == '\n')
            {
                var line = _inputBuffer.ToString();
                _inputBuffer.Clear();
                if (!string.IsNullOrWhiteSpace(line))
                    _ = Task.Run(() => HandleUserLineAsync(line.Trim()));
                continue;
            }

            if (ch == '\b' || value == 127)
            {
                if (_inputBuffer.Length > 0)
                    _inputBuffer.Length--;
                continue;
            }

            if (!char.IsControl(ch))
                _inputBuffer.Append(ch);
        }
    }

    public async Task HandleUserLineAsync(string line)
    {
        if (_writer is null || string.IsNullOrWhiteSpace(line))
            return;

        try
        {
            if (line.StartsWith("/", StringComparison.Ordinal))
            {
                await HandleSlashCommandAsync(line).ConfigureAwait(false);
                return;
            }

            await SendRawAsync($"PRIVMSG {_channel} :{Sanitize(line)}").ConfigureAwait(false);
            WriteLocal($"<{_nick}> {line}");
        }
        catch (Exception ex)
        {
            WriteSystem("IRC input error: " + ex.Message);
            _log("IRC input error: " + ex.Message);
        }
    }

    private async Task HandleSlashCommandAsync(string line)
    {
        var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var command = parts[0].ToLowerInvariant();
        var arg = parts.Length > 1 ? parts[1].Trim() : string.Empty;

        switch (command)
        {
            case "/help":
                WriteSystem("IRC commands: /join #channel, /nick name, /me action, /msg nick text, /raw command, /quit");
                break;
            case "/join":
                if (string.IsNullOrWhiteSpace(arg)) { WriteSystem("Usage: /join #channel"); break; }
                _channel = arg.StartsWith("#", StringComparison.Ordinal) ? arg : "#" + arg;
                _joined = false;
                await SendRawAsync($"JOIN {_channel}").ConfigureAwait(false);
                break;
            case "/nick":
                if (string.IsNullOrWhiteSpace(arg)) { WriteSystem("Usage: /nick nickname"); break; }
                _nick = SafeNick(arg);
                await SendRawAsync($"NICK {_nick}").ConfigureAwait(false);
                break;
            case "/me":
                if (string.IsNullOrWhiteSpace(arg)) { WriteSystem("Usage: /me waves"); break; }
                await SendRawAsync($"PRIVMSG {_channel} :\u0001ACTION {Sanitize(arg)}\u0001").ConfigureAwait(false);
                WriteLocal($"* {_nick} {arg}");
                break;
            case "/msg":
                var msgParts = arg.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (msgParts.Length < 2) { WriteSystem("Usage: /msg nick message"); break; }
                await SendRawAsync($"PRIVMSG {SafeNick(msgParts[0])} :{Sanitize(msgParts[1])}").ConfigureAwait(false);
                WriteLocal($"-> {msgParts[0]}: {msgParts[1]}");
                break;
            case "/raw":
                if (string.IsNullOrWhiteSpace(arg)) { WriteSystem("Usage: /raw IRC COMMAND"); break; }
                await SendRawAsync(arg).ConfigureAwait(false);
                break;
            case "/quit":
                await SendRawAsync("QUIT :RetroModem Bridge").ConfigureAwait(false);
                Dispose();
                break;
            default:
                WriteSystem("Unknown IRC command. Type /help.");
                break;
        }
    }

    private async Task ReadLoopAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested && _reader is not null)
            {
                var line = await _reader.ReadLineAsync(_cts.Token).ConfigureAwait(false);
                if (line is null)
                    break;
                await HandleServerLineAsync(line).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!_disposed)
            {
                WriteSystem("IRC disconnected: " + ex.Message);
                _log("IRC disconnected: " + ex.Message);
            }
        }
    }

    private async Task HandleServerLineAsync(string line)
    {
        _trafficChanged();
        _log("IRC < " + line);

        if (line.StartsWith("PING ", StringComparison.OrdinalIgnoreCase))
        {
            await SendRawAsync("PONG " + line[5..]).ConfigureAwait(false);
            return;
        }

        var parsed = ParseIrcLine(line);
        var code = parsed.Command;

        if (code == "001" && !_joined)
        {
            WriteSystem($"Connected to {_preset.Server}. Joining {_channel}...");
            await SendRawAsync($"JOIN {_channel}").ConfigureAwait(false);
            return;
        }

        if (code == "433")
        {
            _nick = MakeAlternateNick(_nick);
            WriteSystem($"Nickname in use. Trying {_nick}...");
            await SendRawAsync($"NICK {_nick}").ConfigureAwait(false);
            return;
        }

        if (code == "JOIN")
        {
            var who = NickFromPrefix(parsed.Prefix);
            var channel = parsed.Parameters.FirstOrDefault() ?? _channel;
            if (string.Equals(who, _nick, StringComparison.OrdinalIgnoreCase))
            {
                _joined = true;
                _channel = channel;
                WriteSystem($"Joined {_channel}. Type /help for IRC commands.");
            }
            else if (_preset.ShowJoinPartNoise)
            {
                WriteSystem($"{who} joined {channel}");
            }
            return;
        }

        if (code == "PART" || code == "QUIT")
        {
            if (_preset.ShowJoinPartNoise)
                WriteSystem($"{NickFromPrefix(parsed.Prefix)} left");
            return;
        }

        if (code == "PRIVMSG" || code == "NOTICE")
        {
            if (parsed.Parameters.Count < 2)
                return;

            var from = NickFromPrefix(parsed.Prefix);
            var target = parsed.Parameters[0];
            var message = DecodeMessage(parsed.Parameters[1]);
            if (message.StartsWith("\u0001ACTION ", StringComparison.Ordinal) && message.EndsWith("\u0001", StringComparison.Ordinal))
                WriteLocal($"* {from} {message[8..^1]}");
            else if (string.Equals(target, _nick, StringComparison.OrdinalIgnoreCase))
                WriteLocal($"[PM {from}] {message}");
            else
                WriteLocal($"<{from}> {message}");
            return;
        }

        if (code == "332" && parsed.Parameters.Count >= 3)
        {
            WriteSystem("Topic: " + DecodeMessage(parsed.Parameters[2]));
            return;
        }

        if (code.StartsWith("4", StringComparison.Ordinal) || code.StartsWith("5", StringComparison.Ordinal))
        {
            WriteSystem(DecodeMessage(string.Join(' ', parsed.Parameters.Skip(1))));
        }
    }

    private async Task SendRawAsync(string text)
    {
        if (_writer is null)
            return;
        _log("IRC > " + text);
        await _writer.WriteLineAsync(text).ConfigureAwait(false);
        _trafficChanged();
    }

    private void WriteSystem(string text) => _writeSerialText("\r\n*** " + ToRetroText(text) + "\r\n");
    private void WriteLocal(string text) => _writeSerialText("\r\n" + ToRetroText(text) + "\r\n");

    private string ToRetroText(string text)
    {
        text = _preset.StripFormatting ? StripIrcFormatting(text) : text;
        return text.Replace('\t', ' ');
    }

    private static string DecodeMessage(string text) => text.Replace("\u0001", "\x01");

    private static string StripIrcFormatting(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        value = Regex.Replace(value, "\x03(\d{1,2})?(,\d{1,2})?", string.Empty);
        value = value.Replace("\x02", string.Empty)
                     .Replace("\x0F", string.Empty)
                     .Replace("\x16", string.Empty)
                     .Replace("\x1D", string.Empty)
                     .Replace("\x1F", string.Empty);
        return value;
    }

    private static string Sanitize(string text) => text.Replace("\r", " ").Replace("\n", " ").Trim();

    private static string SafeNick(string nick)
    {
        var cleaned = Regex.Replace(nick.Trim(), "[^A-Za-z0-9_`\\[\\]{}|^-]", "");
        if (string.IsNullOrWhiteSpace(cleaned))
            cleaned = "RMBUser";
        if (char.IsDigit(cleaned[0]))
            cleaned = "RMB" + cleaned;
        return cleaned.Length > 24 ? cleaned[..24] : cleaned;
    }

    private static string MakeAlternateNick(string nick)
    {
        var suffix = DateTime.Now.Second.ToString("00");
        var baseNick = nick.Length > 20 ? nick[..20] : nick;
        return SafeNick(baseNick + suffix);
    }

    private static string NickFromPrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return "server";
        var bang = prefix.IndexOf('!');
        return bang > 0 ? prefix[..bang] : prefix;
    }

    private static ParsedIrcLine ParseIrcLine(string line)
    {
        var prefix = string.Empty;
        if (line.StartsWith(':'))
        {
            var space = line.IndexOf(' ');
            if (space > 0)
            {
                prefix = line[1..space];
                line = line[(space + 1)..];
            }
        }

        var parameters = new List<string>();
        var trailingIndex = line.IndexOf(" :", StringComparison.Ordinal);
        string? trailing = null;
        if (trailingIndex >= 0)
        {
            trailing = line[(trailingIndex + 2)..];
            line = line[..trailingIndex];
        }

        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var command = tokens.Count > 0 ? tokens[0].ToUpperInvariant() : string.Empty;
        if (tokens.Count > 1)
            parameters.AddRange(tokens.Skip(1));
        if (trailing is not null)
            parameters.Add(trailing);

        return new ParsedIrcLine(prefix, command, parameters);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        try { _cts.Cancel(); } catch { }
        try { _writer?.Dispose(); } catch { }
        try { _reader?.Dispose(); } catch { }
        try { _stream?.Dispose(); } catch { }
        try { _client?.Close(); } catch { }
        _cts.Dispose();
    }

    private sealed record ParsedIrcLine(string Prefix, string Command, List<string> Parameters);
}
