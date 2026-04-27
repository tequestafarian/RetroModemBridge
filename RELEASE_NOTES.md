# RetroModem Bridge v2 Beta

RetroModem Bridge v2 Beta is a Windows serial-to-TCP modem bridge for vintage computers. It lets a retro computer with a serial connection dial out to Telnet BBSes through a modern Windows PC.

This version is a major UI and usability update over the first release. The goal was to make the app easier to understand for beginners while keeping the workflow simple: start the bridge on the PC, then dial from the vintage computer.

## What is new in v2 Beta

### New modern UI

The app now has a cleaner Windows interface with a larger RetroModem Bridge logo, rounded white sections, better spacing, and a more polished layout.

The main sections are now organized as:

* Header with logo, modem lights, Start Bridge, Stop, and connection status
* Serial setup
* BBS Directory / Dial Aliases
* Live log

### Start and Stop controls moved to the header

The **Start Bridge** and **Stop** buttons are now placed in the top header area beside the logo. This makes the main bridge controls easier to find and use.

The **Start Bridge** button uses a bright green color so it stands out clearly. The **Stop** button is also larger and easier to see.

### Slim modem light display

The modem light display was redesigned into a slim status strip under the logo.

It shows:

* DTR
* RTS
* CTS
* DSR
* DCD
* RX
* TX
* ONLINE

The lights are now easier to see when the bridge is active. The active lights use brighter colors and stronger contrast so the difference between off and on is more noticeable.

### Beginner-friendly modem light help

The modem lights still have hover help. Move the mouse over the light strip to see a plain-English explanation of each light.

Examples:

* **DTR** means the vintage computer is ready
* **RTS** means the vintage computer wants to send data
* **CTS** means the serial adapter says it is okay to send
* **DCD** usually means a connection is active
* **RX** means data is being received
* **TX** means data is being sent
* **ONLINE** means the bridge is connected to a BBS

### Cleaner Serial setup section

The Serial setup area was simplified and cleaned up.

It includes:

* COM port selector
* Baud rate selector
* Default TCP port field
* Remember COM port checkbox
* DTR option
* RTS option
* Echo option
* Telnet filter option

The dropdowns and input fields were adjusted so they look like normal Windows controls and do not crowd each other.

### Remember COM port

The app can now remember the selected COM port between launches.

When **Remember COM port** is checked, the app saves the selected port and automatically reselects it the next time the app starts. If that COM port is not available, the app falls back safely and lets the user pick another one.

### Option tooltips

The technical options now include beginner-friendly tooltips.

Tooltips were added for:

* DTR
* RTS
* Echo
* Telnet filter

These explain what each option does in simple language.

### BBS Directory / Dial Aliases

v2 Beta adds a built-in BBS directory system. This lets the user save BBS entries inside the app and dial them from the vintage computer using short aliases.

For example, instead of typing:

```text
ATDT darkrealms.ca:23
```

The user can dial:

```text
ATDT1
```

Or use a named alias:

```text
ATDT coco
```

### Default BBS entries

The default directory now includes:

| Alias | Name | Host | Port | Notes |
|---|---|---|---:|---|
| 1 | Dark Realms | darkrealms.ca | 23 | ANSI BBS |
| coco | CoCoNet | coconet.ddns.net | 6809 | CoCoNet BBS |

The previous Particles BBS default entry was removed.

### Import from Telnet BBS Guide

The app now includes an **Import Guide** feature for adding BBS entries from a bundled list based on Telnet BBS Guide data.

This makes it easier for users to browse available BBSes and add selected entries to their local directory.

The bundled BBS list is sourced from:

```text
https://www.telnetbbsguide.com/
```

### BBS Directory actions

The BBS Directory now includes clear action buttons:

* Import Guide
* Add
* Edit
* Delete
* Dial

The **Dial** button copies the correct `ATDT` command for the selected BBS entry. The user can paste or type that command on the vintage computer terminal.

### Live log improvements

The Live log section was cleaned up and now includes timestamped entries.

It includes buttons for:

* Clear
* Copy log
* Save log

The log helps show what the bridge is doing, including COM port refreshes, serial open and close events, and copied dial commands.

### Safer startup behavior

Several startup crash issues were fixed.

The split panel layout is now applied safely after the window has loaded, preventing the app from crashing before the main window appears.

Startup crash logging was also added during testing so silent startup failures could be diagnosed more easily.

### Removed Refresh button from Serial setup

The Refresh button was removed from the Serial setup area to reduce clutter. The COM port list still refreshes when the app starts.

## How to use

1. Connect your vintage computer to the Windows PC using a serial connection.
2. Open RetroModem Bridge.
3. Select the correct COM port.
4. Select the baud rate.
5. Click **Start Bridge**.
6. Open a terminal program on the vintage computer.
7. Type `AT` and confirm the bridge replies with `OK`.
8. Dial a BBS.

Examples:

```text
ATDT darkrealms.ca:23
ATDT1
ATDT coco
```

## Recommended default settings

For many vintage computer setups, these are good starting settings:

| Setting | Value |
|---|---|
| Baud | 19200, if supported |
| Default port | 23 |
| DTR | Enabled |
| RTS | Enabled |
| Echo | Disabled unless the terminal does not show typed commands |
| Telnet filter | Enabled |

## Notes

This is a v2 Beta build. The original stable v1 can still be kept as a known-good version, while v2 Beta introduces the new UI, BBS directory, modem light help, and other usability improvements.

The purpose of this version is to make RetroModem Bridge easier for beginners to understand and more pleasant to use as a daily serial-to-TCP bridge for vintage computers.
