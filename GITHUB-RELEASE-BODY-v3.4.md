# RetroModem Bridge v3.4

This release adds experimental **Local Door Game Mode**.

Now RetroModem Bridge can do more than dial Telnet BBSes. You can also add a local door game, give it an alias, and dial it from the retro computer.

Example:

```text
ATDT USURPER
```

## New in v3.4

- Local Door Game Mode.
- Separate **BBS Directory** and **Door Games** tabs.
- Separate Door Games listing inside the retro-side `ATDT MENU`.
- Direct `ATDT DOORS` command for local door games.
- Add/Edit Door Game window.
- `DOOR32.SYS` generation.
- Configurable door arguments with `{door32}` placeholder.
- Usurper Reborn-friendly default: `--door32 {door32} --stdio`.
- Single-key prompt helper for prompts like `M/F`, `Y/N`, and `Press any key`.
- Optional More prompt paging for long door text.
- Startup sound now defaults to off.
- Checking **Play startup sound** previews the sound immediately.

## Build

Run:

```powershell
.\publish-exe-v3.4.ps1
```

The EXE will be created here:

```text
publish\v3.4\RetroModemBridge-v3.4.exe
```

## Note

Local door mode is experimental. Some doors may need their own settings. If the More prompt interferes with ANSI drawing, disable output paging for that door entry.
