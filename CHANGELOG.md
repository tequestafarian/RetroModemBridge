# Changelog

## v3.4

### Added

- Added experimental Local Door Game Mode.
- Added Door Games tab separate from the Telnet BBS Directory.
- Added a separate Door Games listing in the retro-side `ATDT MENU`.
- Added direct retro-side `ATDT DOORS` command.
- Added Local Door editor for adding and editing door entries.
- Added `DOOR32.SYS` generation for local door sessions.
- Added configurable door launch arguments with `{door32}` placeholder support.
- Added Usurper Reborn-friendly default arguments: `--door32 {door32} --stdio`.
- Added single-key input assist for prompts such as `M/F`, `Y/N`, and `Press any key`.
- Added optional output paging for long door text.
- Added back-page review at the More prompt with `B`.

### Changed

- BBS Directory and Door Games now have separate listings/tabs instead of being mixed together in both the Windows UI and retro-side menu.
- Startup sound now defaults to off for new v3.4 settings.
- Checking **Play startup sound** immediately plays a preview.
- App name, assembly name, settings filename, publish output, and support bundle version were updated to v3.4.

### Notes

- Local door support is still experimental and may need per-door tuning.
- Some door games work better with output paging disabled because they draw full-screen ANSI screens.
