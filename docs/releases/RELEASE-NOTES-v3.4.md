# RetroModem Bridge v3.4

RetroModem Bridge v3.4 adds the first experimental Local Door Game Mode. The goal is simple: let a retro computer dial a local door game from the terminal the same way it dials a Telnet BBS.

Example:

```text
ATDT USURPER
```

RetroModem Bridge then launches the configured local door game, creates `DOOR32.SYS`, and routes the serial session between the retro computer and the game process.

## Highlights

- Local Door Game Mode.
- Door Games tab separate from the BBS Directory.
- Retro-side `ATDT MENU` now has a separate Door Games listing.
- `ATDT DOORS` jumps directly to the local door game list.
- Add/Edit Door dialog with executable, working folder, arguments, node, user, input assist, and paging options.
- `DOOR32.SYS` generator.
- Usurper Reborn-friendly launch arguments.
- Single-key prompt assist.
- Optional More prompt for long door output.
- Startup sound defaults to off.

## Suggested Usurper Reborn arguments

```text
--door32 {door32} --stdio
```

## Known notes

Door output paging is optional. If a door game draws full-screen ANSI screens and the More prompt gets in the way, turn paging off for that door entry.

This package contains source files. Build on Windows with the included v3.4 publish script.
