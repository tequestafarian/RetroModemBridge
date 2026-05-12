# RetroModem Bridge IRC Chat Bridge

This package adds experimental IRC support to RetroModem Bridge.

## Retro-side usage

From the retro computer terminal, you can use the direct dial command:

```text
ATDT IRC
```

You can also open the local BBS companion menu and choose IRC from there:

```text
ATDT MENU
```

Then choose:

```text
12. IRC Chat Bridge
```

RMB shows a short instruction screen and then connects to the default IRC preset.

## Default preset

```text
Alias: irc
Server: irc.libera.chat
Port: 6697
TLS: yes
Channel: #retromodem
Nickname: RMBUser
```

## IRC commands

```text
/help
/join #channel
/nick NewName
/me waves
/msg nick hello
/quit
```

Type normal messages and press ENTER to chat in the active channel.

## What RMB handles

RMB handles TLS, IRC login, PING/PONG, nickname fallback, and IRC formatting cleanup on the Windows side. The retro computer only needs a plain serial terminal.
