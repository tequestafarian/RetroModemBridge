# RetroModem Bridge v3 Beta

This is an experimental beta version of RetroModem Bridge. It is meant for testing new modem-style features while keeping v1 stable separate.

## What is new in v2 beta
- Fixed the BBS Directory toolbar so the Terminal button is visible next to Dial.

- Vintage modem-style status light panel
- BBS Directory / Dial Aliases
- Dial saved BBS entries from the vintage computer with commands like `ATDT1`
- Copy Dial Command button, which copies the selected alias command such as `ATDT1`
- `ATDL` support to redial the last entry
- `AT&V` support to show bridge settings
- Copy log and save log buttons
- Import and export the BBS directory as JSON
- Basic Telnet negotiation filtering
- Separate settings file: `settings-v3-beta.json`
- Separate EXE name: `RetroModemBridge-v3-beta.exe`

## How the BBS Directory works

The BBS Directory is a phone book for Telnet BBSes.

Example directory entry:

```text
Alias: 1
Name: Dark Realms
Host: darkrealms.ca
Port: 23
```

From the vintage computer terminal, type:

```text
ATDT1
```

RetroModem Bridge translates alias `1` into:

```text
darkrealms.ca:23
```

The Windows app still does not dial by itself. The vintage computer starts the connection. The directory only lets you use short modem-style numbers instead of typing the full host and port every time.

## Build the v2 beta EXE

Install the .NET 8 SDK for Windows, unzip this package, then run:

```powershell
.\publish-exe-v3-beta.ps1
```

Or double-click:

```text
publish-exe-v3-beta.bat
```

The published EXE will be created here:

```text
publish\v3-beta\RetroModemBridge-v3-beta.exe
```

## Basic use

1. Start RetroModem Bridge v3 Beta.
2. Select the COM port connected to your vintage computer.
3. Select the baud rate used by your terminal program.
4. Click Start Bridge.
5. On the vintage computer, type `AT` and press Enter.
6. You should see `OK`.
7. Dial a full address like `ATDT darkrealms.ca:23`, or dial a saved alias like `ATDT1`.

## Useful AT commands

```text
AT      Test command, should return OK
ATI     Show bridge name
AT&V    Show bridge settings
ATDT1   Dial BBS directory alias 1
ATDL    Redial last dialed entry
ATH     Hang up
ATE0    Echo off
ATE1    Echo on
```

## Notes

This is a beta build for testing. Keep v1 stable available if you need the simplest known-good version.

## Telnet BBS Guide import

This v2 beta includes a bundled `bbslist.csv` from The Telnet & Dial-Up BBS Guide.

Source: https://www.telnetbbsguide.com/
Download page: https://www.telnetbbsguide.com/lists/download-list/

Use **Import from Telnet BBS Guide** to browse the included list, search by name, host, software, or location, select BBSes, and add them to your local BBS Directory / Dial Aliases list.

After importing a BBS, dial it from the vintage computer terminal with its assigned alias, for example:

```text
ATDT1
```

The Windows app still acts as the bridge and directory. The vintage computer still initiates the connection.

The **ONLINE** modem light is red/off when disconnected and turns green when a BBS connection is active.

## Resize behavior in this build

The modem status light panel now redraws and scales its LEDs, labels, glow, and modem faceplate when the app window changes size. The ONLINE light uses the green LED style when a BBS connection is active.

## Selecting BBSes from the bundled list

The import window lets you add BBSes in either of these ways:

1. Check the Add box next to each BBS you want.
2. Highlight one or more rows in the list.

Then click **Add Checked or Highlighted to Directory**. The app assigns the next available dial aliases automatically.

## Version 3 Terminal Preview
- Fixed the v3 beta build by restoring the startup sound preference and playback helper methods.

This package includes an experimental built-in ANSI terminal preview.

Use the **Terminal** button in the BBS Directory to open the selected BBS directly inside RetroModem Bridge. This does not replace the serial bridge mode. It is meant as a convenient Windows-side BBS test terminal.

Current terminal preview features:

- Connect to a selected BBS from the directory
- Basic ANSI color support
- Basic cursor movement support
- Clear screen support
- CP437/DOS character decoding
- Keyboard input back to the BBS
- Basic Telnet negotiation handling

The terminal preview is intended for testing and will continue to improve in Version 3.


## v3 Beta ANSI terminal improvements

This build improves the built-in terminal preview with better ANSI/BBS handling:

- Better ANSI cursor movement support
- ANSI scrolling region support
- Clear screen and clear line mode support
- Insert/delete line handling
- Insert/delete/erase character handling
- OSC/title sequence filtering
- ESC save/restore cursor support
- Better Telnet negotiation filtering, including sub-negotiation skipping
- Page Up, Page Down, Insert, Delete, and F1-F4 key sequences

The terminal is still a preview, but it should handle more BBS ANSI screens correctly than the first v3 beta.

## v3 beta terminal rendering fix

- Fixed right-side character clipping in the built-in ANSI terminal renderer.

- Improved terminal rendering so ANSI/CP437 glyphs are drawn after backgrounds, preventing adjacent cells from clipping character edges.

- Adjusted ANSI terminal font sizing and row-run rendering to reduce right-edge character clipping.

- Improved ANSI art rendering by drawing CP437 block and shade characters as flush cell graphics.

- Tightened the BBS Directory toolbar and list spacing so all action buttons fit on one row and more entries are visible at startup.

## New in this v3 beta build

- Added a confirmation popup before deleting BBS Directory entries.
- Added a startup modem sound using the included `Assets/startup-modem.mp3` file.
- Added a **Play startup sound** checkbox at the bottom of the main window so users can turn the sound off.

## ANSI auto-detect improvements

- Built-in terminal now replies to ANSI auto-detect prompts and ANSI cursor-position queries.

## Latest v3 beta UI update

- Enlarged the modem lights and moved the labels below each light for easier reading.

## V3 BBS Companion additions in this build

This build adds the first pass of the BBS Companion update:

- Expanded BBS Directory fields: category, system type, ANSI support, favorite, last result, and notes.
- Favorites: select a BBS row and click **Favorite** to star or unstar it.
- Dial History: click **History** to view recent dial attempts and text-service calls.
- Connection Testing: select a BBS row and click **Test** to check whether the host and port are reachable from Windows.
- Beginner Setup: click **Setup** for a simple first-run checklist.
- Built-in text services from the retro terminal:
  - `ATDT HELP`
  - `ATDT TIME`
  - `ATDT MENU`
  - `ATDT BBSLIST`
  - `ATDT FAVORITES`

The existing ANSI terminal form is still included. The bridge still supports normal Hayes-style commands such as `AT`, `ATI`, `AT&V`, `ATDT alias`, `ATDT host:port`, `ATDL`, and `ATH`.

## V3 local BBS menu with ANSI

This build upgrades `ATDT MENU` into a small local BBS-style menu served directly by RetroModem Bridge.

From the vintage computer terminal:

```text
ATDT MENU
```

The bridge returns `CONNECT`, clears the screen, and displays an ANSI-colored menu.

Menu options:

```text
1  BBS Directory
2  Favorites
3  Dial History
4  Time Service
5  Help / Commands
6  About This Bridge
Q  Disconnect
```

When viewing the BBS Directory or Favorites screen, enter the listed number to dial that BBS directly from the menu.

Examples:

```text
ATDT MENU
1
03
```

That opens the directory, then dials entry 03.

Other menu controls:

```text
M   Return to main menu
Q   Disconnect from the local menu
ATH Also exits the local menu
```

The menu uses ANSI colors, CP437 box drawing, and an ACiD-inspired BBS layout. It looks best in ANSI terminals that support IBM/CP437 characters, such as many classic BBS terminal setups.



## v3 Beta 2 Release Notes

See `RELEASE-NOTES-v3.0-beta.2.md` and `CHANGELOG-v3.0-beta.2.md`.
