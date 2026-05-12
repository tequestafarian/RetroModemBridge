# RetroModem Bridge SSH Proxy Integration

This add-on adds the core SSH bridge pieces for RetroModem Bridge.

The vintage computer does **not** need to understand SSH. The Windows app connects to SSH, then passes plain terminal bytes back and forth over serial.

## What this adds

- `SshProxySession.cs`
- `SshDialProfile.cs`
- `SshPasswordPrompt.cs`
- SSH.NET NuGet package
- Patch snippets for MainForm/AppSettings integration

## Recommended UI wording

Add SSH as a connection mode beside the current modes:

```text
Connection Mode:
[ Telnet BBS ]
[ SSH ]
[ Local Door ]
```

A beginner-friendly label:

```text
SSH mode lets your retro computer use normal ATDT commands while Windows handles the encrypted SSH connection.
```

## AppSettings additions

Add something like this to `AppSettings.cs`:

```csharp
public bool EnableSshMode { get; set; } = true;
public string DefaultSshHost { get; set; } = "";
public int DefaultSshPort { get; set; } = 22;
public string DefaultSshUsername { get; set; } = "";
public string DefaultSshTerminal { get; set; } = "ansi";
```

Do **not** save SSH passwords in plain text.

## Directory model additions

If your BBS directory entries currently only know host/port/alias, add a connection type:

```csharp
public enum DialConnectionType
{
    Telnet,
    Ssh,
    LocalDoor
}
```

Then add this to the directory entry model:

```csharp
public DialConnectionType ConnectionType { get; set; } = DialConnectionType.Telnet;
public string SshUsername { get; set; } = "";
public string SshTerminal { get; set; } = "ansi";
```

For existing entries, default to `Telnet`.

## MainForm fields

Add:

```csharp
using RetroModemBridge.SshProxy;

private SshProxySession? _sshSession;
```

## When ATDT resolves an SSH alias

Where your current ATDT handler resolves an alias and starts a Telnet connection, branch on the connection type:

```csharp
if (entry.ConnectionType == DialConnectionType.Ssh)
{
    await StartSshConnectionFromEntryAsync(entry);
    return;
}
```

## Start SSH connection

Adapt names to match your existing serial write/log methods.

```csharp
private async Task StartSshConnectionFromEntryAsync(DialEntry entry)
{
    string password;

    using (var prompt = new SshPasswordPrompt(entry.Host, entry.SshUsername))
    {
        if (prompt.ShowDialog(this) != DialogResult.OK)
        {
            Log("SSH login cancelled.");
            SendToSerial("NO CARRIER\r\n");
            return;
        }

        password = prompt.Password;
    }

    _sshSession?.Dispose();
    _sshSession = new SshProxySession();

    _sshSession.Log += msg => BeginInvoke(new Action(() => Log(msg)));

    _sshSession.DataReceived += data =>
    {
        // Replace SerialPort with the name of your serial port field.
        if (SerialPort != null && SerialPort.IsOpen)
            SerialPort.Write(data, 0, data.Length);
    };

    _sshSession.Disconnected += () =>
    {
        BeginInvoke(new Action(() =>
        {
            SetOnlineLight(false);
            Log("SSH disconnected.");
        }));
    };

    try
    {
        await _sshSession.ConnectAsync(
            entry.Host,
            entry.Port <= 0 ? 22 : entry.Port,
            entry.SshUsername,
            password,
            string.IsNullOrWhiteSpace(entry.SshTerminal) ? "ansi" : entry.SshTerminal);

        SetOnlineLight(true);
        SendToSerial("CONNECT 19200\r\n");
    }
    catch (Exception ex)
    {
        Log("SSH connect failed: " + ex.Message);
        SendToSerial("NO CARRIER\r\n");
    }
}
```

## Serial input forwarding

Where you currently forward serial bytes to Telnet, add an SSH branch:

```csharp
if (_sshSession?.IsConnected == true)
{
    _sshSession.SendFromSerial(buffer, 0, bytesRead);
    return;
}
```

## Hangup handling

Where `ATH`, DTR drop, Stop Bridge, or disconnect cleanup happens:

```csharp
_sshSession?.Disconnect();
_sshSession = null;
```

## Suggested dial examples

```text
ATDT mypi
ATDT myserver
ATDT sshdemo
```

## Suggested Reddit feature wording

```text
I am also adding SSH dialing support. The vintage computer still uses normal ATDT commands, but RetroModem Bridge connects to the remote system over SSH from Windows and passes the plain terminal session back over serial.
```

## First version limits

- Password login only
- Host key is displayed but auto-trusted
- No private key login yet
- No saved passwords
- Best for shells, Linux boxes, Raspberry Pi setups, and private SSH-only systems

## Later improvements

- Known-host fingerprint storage
- Private key login
- Per-entry SSH username
- Per-entry terminal type
- SSH entries inside the BBS directory
- Optional local echo override per SSH profile
