# RetroModem Bridge v3 Beta 2

**Version:** `v3.0-beta.2`  
**Release type:** Beta / pre-release  
**Recommended GitHub tag:** `v3.0-beta.2`

RetroModem Bridge v3 Beta 2 expands the app from a simple Windows serial-to-Telnet bridge into a more complete control center for connecting vintage computers to Telnet BBSes.

This release keeps the original purpose intact:

```text
Vintage computer → Serial / USB adapter → Windows PC → Telnet BBS
```

But it adds a local BBS-style menu, session mirroring, favorites, dial history, connection testing, and a proper app icon.

---

## Screenshots

### RetroModem Bridge running

![RetroModem Bridge running](screenshots/retromodem-bridge-v3beta.png)

The main RetroModem Bridge window with the serial bridge controls, BBS directory, connection tools, modem lights, and live log.

### Local BBS menu on a CoCo 3

![Local BBS running on CoCo 3 powered by RMB](screenshots/CoCo3-v3.b2-coco3-localbbs.jpg)

`ATDT MENU` opens a local BBS-style menu served by RetroModem Bridge and displayed on the CoCo 3.

### Local BBS directory listing on a CoCo 3

![Local BBS directory listing running on CoCo 3 powered by RMB](screenshots/CoCo3-v3.b2-coco3-localbbs-directory.jpg)

The local BBS directory lets you browse saved BBS entries directly from the retro terminal and dial a selected entry by number.

### Session Mirror

![RetroModem Bridge Session Mirror](screenshots/retromodem-bridge-v3beta-session-mirror.png)

The Session Mirror shows what the retro computer is seeing from inside the Windows app. Input can be enabled so you can type into the active session from the PC.

### Session Mirror connected to a Telnet BBS

![RetroModem Bridge Session Mirror connected to Telnet BBS](screenshots/retromodem-bridge-v3beta-session-mirror-connect-from-pc.png)

The Session Mirror can also be used while connected to a Telnet BBS, making it easier to test, troubleshoot, and capture screenshots.

---

## Highlights

### Session Mirror

Added a new **Session Mirror** window.

The Session Mirror lets you:

- See what the retro computer is seeing from inside the Windows app
- Watch ANSI output from the active session
- Optionally type into the active session from the app
- Use **Local Echo** so typed characters appear immediately in the mirror window

This is useful for troubleshooting, screenshots, testing BBSes, and controlling a session without sitting directly at the retro computer.

---

### Local BBS Menu

`ATDT MENU` now opens a local BBS-style menu served directly by RetroModem Bridge.

The local menu includes:

- BBS Directory
- Favorites
- Dial History
- Time Service
- Help / Commands
- About This Bridge
- Disconnect

From the local menu, you can list BBSes and dial entries by number.

Example:

```text
ATDT MENU
1
03
```

That opens the local menu, shows the directory, and dials entry 03.

---

### BBS Companion Features

This release improves the BBS directory and dialing workflow.

Added or improved:

- Better BBS directory fields
- Favorites
- Dial history
- Connection testing
- Last connection result
- ANSI support field
- Notes field
- Local text services

Supported local service aliases:

```text
ATDT HELP
ATDT TIME
ATDT MENU
ATDT BBSLIST
ATDT FAVORITES
```

---

### Cleaner BBS Directory Toolbar

The BBS directory toolbar has been cleaned up so it does not wrap as easily.

Visible actions:

- Import Guide
- Mirror
- Add
- Edit
- Delete
- Favorite
- Test
- More

Less-used tools are under **More**.

---

### New App Icon

Added a new RetroModem Bridge application icon.

The icon is included as:

```text
RetroModemBridge/Assets/retromodem-bridge.ico
```

The project file now uses it as the Windows application icon.

---

## Notes for CoCo Users

This version was built around the original use case:

- TRS-80 Color Computer 3
- Deluxe RS-232 Pak
- DB25-to-USB serial adapter
- Windows PC
- NetMate terminal software
- Telnet BBS connections

Other retro systems with serial terminal software may also work.

---

## Known Beta Notes

This is still a beta release.

Please report issues with:

- Serial adapter compatibility
- ANSI rendering
- Local BBS menu behavior
- Session Mirror typing/local echo
- BBS disconnects
- Telnet negotiation quirks
- COM port detection

If characters appear doubled in the Session Mirror, turn off **Local Echo**.

---

## Suggested Test Commands

From your retro terminal:

```text
AT
ATI
AT&V
ATDT HELP
ATDT TIME
ATDT MENU
ATDT BBSLIST
ATDT FAVORITES
ATDT <your saved alias>
ATH
```

From the Windows app:

1. Start the bridge.
2. Open **Mirror**.
3. Dial `ATDT MENU` from the retro computer.
4. Confirm the local menu appears in the mirror.
5. Enable input in the mirror and type a menu number.
6. Test a BBS entry with the **Test** button.
7. Mark a BBS as favorite.
8. Open `ATDT FAVORITES` from the retro computer.

---

## Suggested GitHub Release Title

```text
RetroModem Bridge v3 Beta 2 - Session Mirror, Local BBS Menu, and App Icon
```

## Suggested GitHub Tag

```text
v3.0-beta.2
```

## Suggested GitHub Release Summary

```text
RetroModem Bridge v3 Beta 2 adds a live Session Mirror, local BBS-style ANSI menu, favorites, dial history, connection testing, cleaner directory tools, and a new app icon.
```

## Suggested Commit Message

```text
Add v3 beta 2 session mirror, local BBS menu, toolbar cleanup, and app icon
```

---

## Recommended Release Asset Name

```text
RetroModemBridge-v3.0-beta.2-win-source.zip
```

If you publish a compiled Windows release, a good asset name would be:

```text
RetroModemBridge-v3.0-beta.2-win64.zip
```
