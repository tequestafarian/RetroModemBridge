# RetroModem Bridge v2 Beta

This is an experimental beta version of RetroModem Bridge. It is meant for testing new modem-style features while keeping v1 stable separate.


## Screenshot

![RetroModem Bridge startup screen](screenshots/retromodem-bridge-startup.png)

## What is new in v2 beta

- Vintage modem-style status light panel
- BBS Directory / Dial Aliases
- Dial saved BBS entries from the vintage computer with commands like `ATDT1`
- Copy Dial Command button, which copies the selected alias command such as `ATDT1`
- `ATDL` support to redial the last entry
- `AT&V` support to show bridge settings
- Copy log and save log buttons
- Import and export the BBS directory as JSON
- Basic Telnet negotiation filtering
- Separate settings file: `settings-v2-beta.json`
- Separate EXE name: `RetroModemBridge-v2-beta.exe`

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
.\publish-exe-v2-beta.ps1
```

Or double-click:

```text
publish-exe-v2-beta.bat
```

The published EXE will be created here:

```text
publish\v2-beta\RetroModemBridge-v2-beta.exe
```

## Basic use

1. Start RetroModem Bridge v2 Beta.
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
