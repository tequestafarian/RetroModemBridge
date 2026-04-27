# RetroModem Bridge 2.0 Features

RetroModem Bridge 2.0 is a Windows serial-to-TCP modem bridge for vintage computers. It lets a retro computer use a serial connection to “dial” Telnet BBSes through a modern Windows PC.

This build is a major update from the first release. The focus is a cleaner interface, easier setup, better feedback, and a built-in BBS directory.

![RetroModem Bridge startup screen](screenshots/retromodem-bridge-startup.png)

## Main highlights

### Modernized user interface

The interface has been redesigned with a cleaner Windows layout. The main sections are easier to understand and are grouped into logical areas:

- Header with logo, Start Bridge, Stop, status, and modem lights
- Serial setup
- BBS Directory / Dial Aliases
- Live log

The app now feels more like a polished control panel instead of a basic utility window.

### Header with main controls

The main **Start Bridge** and **Stop** buttons are now in the header area next to the logo.

This makes the most important actions easier to find:

- **Start Bridge** starts the serial-to-TCP bridge
- **Stop** closes the active bridge connection

### Slim modem light strip

The large modem-light display was replaced with a slimmer light strip under the logo.

The light strip shows:

| Light | Meaning |
|---|---|
| DTR | The vintage computer is ready |
| RTS | The vintage computer wants to send data |
| CTS | The serial adapter says it is okay to send |
| DSR | The serial device says it is ready |
| DCD | Carrier detected, usually meaning connected |
| RX | Data is being received |
| TX | Data is being sent |
| ONLINE | The bridge is connected to a BBS |

The lights now use stronger contrast so active lights are easier to notice.

### Hover help for modem lights

The modem light strip includes hover help that explains each light in plain English. This helps users who are not familiar with old Hayes modem light labels.

### Cleaner serial setup

The serial setup section includes the basic settings needed to start the bridge:

- COM port selector
- Baud rate selector
- Default TCP port
- Remember COM port
- DTR option
- RTS option
- Echo option
- Telnet filter option

The COM port and baud dropdowns use normal Windows-style controls, making them easier to recognize and use.

### Remember COM port

The app can remember the selected COM port between launches.

When **Remember COM port** is enabled, the app will automatically reselect the previous COM port the next time it starts. If the port is not available, the app safely falls back and lets the user pick another one.

### Beginner-friendly option tooltips

The technical serial options now include simple hover explanations:

| Option | Plain-English purpose |
|---|---|
| DTR | Tells the serial adapter that the vintage computer is ready |
| RTS | Tells the serial adapter that the vintage computer wants to send data |
| Echo | Repeats typed AT commands back to the terminal |
| Telnet filter | Hides Telnet control codes that can show up as garbage characters |

### BBS Directory / Dial Aliases

The built-in BBS Directory works like a phone book for Telnet BBSes.

Instead of typing a full host and port every time, users can save aliases and dial them from the vintage computer.

Example:

```text
ATDT1
```

or:

```text
ATDT coco
```

The bridge translates the alias into the saved BBS host and port.

### Default BBS entries

This build includes these default BBS entries:

| Alias | Name | Host | Port | Notes |
|---|---|---|---:|---|
| 1 | Dark Realms | darkrealms.ca | 23 | ANSI BBS |
| coco | CoCoNet | coconet.ddns.net | 6809 | CoCoNet BBS |

### BBS Directory actions

The BBS Directory includes these actions:

- **Import Guide**
- **Add**
- **Edit**
- **Delete**
- **Dial**

The **Dial** button copies the correct `ATDT` command for the selected BBS entry.

### Telnet BBS Guide import

The app includes an **Import Guide** feature for adding BBS entries from a bundled Telnet BBS Guide list.

The bundled BBS list is sourced from:

```text
https://www.telnetbbsguide.com/
```

This makes it easier for users to find and add BBSes without manually typing each host and port.

### Live log

The Live log shows what the bridge is doing.

It includes:

- Timestamped log entries
- Clear log
- Copy log
- Save log

The log is useful for troubleshooting connection issues and seeing bridge activity.

### AT command support

The bridge supports common modem-style commands, including:

```text
AT
ATI
AT&V
ATDT host:port
ATDT1
ATDT coco
ATDL
ATH
ATE0
ATE1
```

### Telnet filtering

Telnet filtering helps remove Telnet negotiation codes that can otherwise appear as garbage characters or cause display issues in vintage terminal software.

This should usually stay enabled.

### Safer startup behavior

The app layout was updated to avoid startup crashes related to split panel sizing. The interface now applies split positions safely after the window has loaded.

### Screenshot included

The project includes a startup screenshot here:

```text
screenshots/retromodem-bridge-startup.png
```

This can be used in the GitHub README to show what the app looks like.

## Recommended starting settings

| Setting | Recommended value |
|---|---|
| Baud | 19200 if supported by the vintage computer and terminal |
| Default port | 23 |
| DTR | Enabled |
| RTS | Enabled |
| Echo | Disabled unless typed commands do not appear |
| Telnet filter | Enabled |

## Basic usage

1. Connect the vintage computer to the Windows PC using a serial connection.
2. Start RetroModem Bridge.
3. Select the correct COM port.
4. Select the baud rate.
5. Click **Start Bridge**.
6. Open a terminal program on the vintage computer.
7. Type `AT` and press Enter.
8. Confirm the bridge replies with `OK`.
9. Dial a BBS.

Examples:

```text
ATDT darkrealms.ca:23
ATDT1
ATDT coco
```

## Notes

RetroModem Bridge 2.0 is designed to be easier for beginners while still keeping the retro modem workflow intact. The Windows app starts the bridge, but the vintage computer still initiates the connection using AT commands.
