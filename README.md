# RetroModem Bridge v3.4

RetroModem Bridge is a Windows serial-to-TCP modem bridge for vintage computers. It lets a machine such as a TRS-80 Color Computer, Commodore, Apple II, Atari, Amiga, or DOS PC use a serial connection to dial Telnet BBSes with Hayes-style `ATDT` commands.

Version 3.4 also adds an experimental **Local Door Game Mode**. This lets the retro computer dial a local door game alias, such as `ATDT USURPER`, and RetroModem Bridge launches the configured door game on the Windows PC.

## Main features

- Windows GUI for choosing COM port, baud rate, and modem-style options.
- Telnet BBS dialing with aliases like `ATDT DARK` or `ATDT coco`.
- BBS Directory with import support for the bundled Telnet BBS Guide list.
- Separate **BBS Directory** and **Door Games** tabs.
- Retro-side `ATDT MENU` also separates BBS listings from Door Game listings. Use `ATDT DOORS` to jump straight to Door Games.
- Local Door Game Mode with `DOOR32.SYS` generation.
- Door game input assist for single-key prompts.
- Optional door output paging with a `More` prompt.
- Built-in ANSI terminal preview and Session Mirror.
- Dial history, favorites, connection testing, profiles, and support bundle export.
- Startup sound is off by default. Checking **Play startup sound** previews the sound immediately.

## Local Door Game Mode

Door games are configured in the **Door Games** tab. A typical Usurper Reborn setup uses:

```text
Arguments: --door32 {door32} --stdio
```

RetroModem Bridge creates the `DOOR32.SYS` file automatically and replaces `{door32}` with the generated file path.

From the retro computer, dial the door alias just like a BBS. You can also use `ATDT MENU` and choose **Door Games**, or type `ATDT DOORS` to see only local door entries:

```text
ATDT USURPER
```

Recommended first settings for Usurper Reborn:

```text
[x] Auto-Enter after single keys
[x] Pause long output with More prompt
Lines per page: 23
More prompt: -- More -- Space/Enter=next, B=back --
```

If the More prompt interferes with ANSI screens, disable output paging for that door entry.

## Build

Install the .NET 8 SDK for Windows, then run:

```powershell
.\publish-exe-v3.4.ps1
```

Or double-click:

```text
publish-exe-v3.4.bat
```

The published executable should appear in:

```text
publish\v3.4\RetroModemBridge-v3.4.exe
```

## Basic setup

1. Connect your retro computer serial interface to the Windows PC with a USB serial adapter.
2. Start RetroModem Bridge.
3. Pick the COM port and baud rate.
4. Click **Start Bridge**.
5. In your retro terminal, type a dial command such as:

```text
ATDT coco
```

or for a local door game:

```text
ATDT USURPER
```

## Notes

This project is inspired by TCPSer-style modem bridges, with a Windows GUI and extra retro-computing conveniences added on top.

The bundled Telnet BBS list is provided as a convenience. BBS availability can change at any time.
