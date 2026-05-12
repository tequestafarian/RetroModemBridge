# RetroModem Bridge SSH Proxy Integration

This add-on adds the core SSH proxy classes for RetroModem Bridge.

The goal is direct SSH dialing from the retro computer:

```text
ATDT ssh://username@example.com:22
```

Example:

```text
ATDT ssh://pi@192.168.1.50:22
```

The CoCo, Commodore, Apple II, DOS PC, etc. does not need to understand SSH. RetroModem Bridge runs on Windows, connects to SSH, and sends plain terminal bytes back over serial.

## What this package installs

```text
RetroModemBridge/SshProxy/SshDialProfile.cs
RetroModemBridge/SshProxy/SshProxySession.cs
RetroModemBridge/SshProxy/SshPasswordPrompt.cs
```

It also runs:

```powershell
dotnet add package SSH.NET
```

## What still needs to be wired

MainForm.cs needs one hook where ATDT commands are processed.

The important logic is:

```csharp
if (dialTarget.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase))
{
    await ConnectSshAsync(dialTarget);
    return;
}
```

Then add a helper method named `ConnectSshAsync` that:

1. Parses the SSH URL.
2. Prompts for the password.
3. Connects using `SshProxySession`.
4. Sends SSH output back to the serial port.
5. Sends serial input to SSH while connected.

See `MAINFORM-SSH-PATCH-SNIPPETS.txt` for the exact snippets.

## First-pass limits

This first version supports:

```text
Password-based SSH login
Interactive shell stream
ANSI passthrough
Direct ssh:// dialing
```

Not included yet:

```text
Saved SSH passwords
Private key login
Known-host fingerprint database
Directory UI protocol dropdown
SSH aliases in the BBS directory
```

Those can be added after direct SSH dialing works.
