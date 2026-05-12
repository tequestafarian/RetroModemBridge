# RetroModem Bridge v3 Beta 3

## The biggest RetroModem Bridge release yet

RetroModem Bridge v3 Beta 3 transforms the app from a simple Windows serial-to-Telnet modem bridge into a full retro-computing connection center.

This release brings a BBS-style experience directly to your vintage computer. From a TRS-80 Color Computer, Commodore, Apple II, Atari, Amiga, DOS machine, or any retro system with a serial terminal, you can now browse, search, update, and launch Telnet BBSes through a local ANSI menu powered by RetroModem Bridge.

The core idea is still simple:

```text
Vintage computer → Serial / USB adapter → Windows PC → Telnet BBS
```

But v3 Beta 3 makes that bridge feel like its own mini online service.

---

## Headline features

### Local BBS menu from the retro computer

Type:

```text
ATDT MENU
```

and RetroModem Bridge opens a built-in local BBS-style ANSI menu on your retro terminal.

From there, you can browse your BBS directory, view favorites, check dial history, use text services, browse the import guide, and update the guide.

### Searchable and paged BBS directory

The local BBS directory is now usable from the retro computer side.

Inside the directory:

```text
N       Next page
P       Previous page
S       Search
C       Clear search
M       Main menu
Q       Disconnect
01-15   Dial the listed BBS on the current page
```

Search checks:

- BBS name
- alias
- host
- category
- system type
- notes
- last connection result

### Retro-side Import Guide browser

Type:

```text
ATDT GUIDE
```

or choose option `9` from the local menu.

You can browse the bundled or updated Telnet BBS Guide directly from the retro computer. It only shows entries that are not already in your BBS directory, making it easy to discover new boards without touching the Windows PC.

Inside the guide browser:

```text
N       Next page
P       Previous page
S       Search guide
C       Clear search
01-15   View guide entry
A       Add selected entry to your directory
D       Dial once without saving
G       Back to guide list
```

### Monthly and Daily Telnet BBS Guide updates

Type:

```text
ATDT UPDATEGUIDE
```

or choose `U` from the local BBS menu.

RetroModem Bridge can now update the BBS Guide directly from the app or from the retro computer.

Monthly and Daily updates are handled separately:

```text
Monthly = bbslist.csv
Daily   = dialdirectory.xml
```

That keeps both formats stable and prevents one update type from breaking the other.

The retro-side update screen includes an ANSI progress display and guide stats.

### Guide stats

The update screen shows useful stats:

- Current BBS Directory listings
- Current guide listings
- New guide listings available to add
- Entries already in your directory
- Favorites
- Online-tested listings

### Session Mirror

The Windows app now includes a Session Mirror so you can see what the retro computer is seeing.

Useful for:

- troubleshooting
- screenshots
- testing local menu screens
- typing into a session from the PC when input is enabled

### Favorites, dial history, and connection testing

v3 Beta 3 adds a more complete BBS management workflow:

- mark favorite BBSes
- view dial history
- test one BBS
- test all favorites
- track connection status
- track last checked time
- track response time in ms

### Profiles for different retro systems

Save serial settings for different setups, such as:

- CoCo 3 / NetMate / 19200
- Generic 9600 8-N-1
- other retro machines or terminal programs

### Export Support Bundle

Create a troubleshooting ZIP with:

- system info
- COM port list
- live log
- BBS directory
- dial history
- settings summary

This makes it much easier to help someone debug a setup.

### New RMB app icon

v3 Beta 3 includes the new RMB modem/bridge icon.

---

## Local BBS commands

From the retro terminal:

```text
AT
ATI
AT&V
ATDT MENU
ATDT GUIDE
ATDT UPDATEGUIDE
ATDT HELP
ATDT TIME
ATDT NEWS
ATDT RANDOM
ATDT ONLINE
ATDT FAVORITES
ATDT BBSLIST
ATH
```

---

## Recommended first test

After starting the bridge in Windows, try this from your retro terminal:

```text
AT
ATDT MENU
```

Then test:

```text
1
S
coco
C
M
9
U
3
```

---

## Notes for Monthly and Daily Guide updates

Monthly updates use:

```text
bbslist.csv
```

Daily updates use:

```text
dialdirectory.xml
```

The app intentionally treats these as separate update paths because the ZIP formats are different.

---

## Beta status

This is still labeled beta, but this is the recommended v3 build.

Please report issues with:

- serial adapters
- ANSI rendering
- Session Mirror rendering
- Telnet negotiation
- Monthly/Daily guide updates
- local BBS menu behavior
- retro terminal compatibility

Tested around the original use case:

- TRS-80 Color Computer 3
- Deluxe RS-232 Pak
- DB25-to-USB serial adapter
- Windows PC
- NetMate terminal software
