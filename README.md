# RetroModem Bridge

RetroModem Bridge is a Windows app that helps vintage computers connect to Telnet-accessible BBSes.

If your retro computer has a serial port and terminal software, RetroModem Bridge can act like a modem-style bridge between that computer and the internet. It was originally built for a Tandy / TRS-80 Color Computer 3, but it can also work with other systems such as Commodore, Apple II, Atari, Amiga, DOS PCs, and many other machines that support serial communication.

You connect the retro computer to your Windows PC with a serial cable or USB-to-serial adapter. Then, from the terminal program on the retro computer, you dial a BBS using a command like:

`ATDT bbs.example.com:23`

RetroModem Bridge receives the command, opens a TCP connection from the Windows PC to that host and port, and passes data back and forth between the BBS and your retro computer.

In simple terms, your Windows computer handles the modern network connection, while your retro computer gets the experience of using a modem.

RetroModem Bridge supports basic Hayes-style AT commands. It is not intended to be a full hardware modem emulator, but it provides the core functionality needed for many terminal programs to connect to BBSes over TCP.

## Screenshots

![RetroModem Bridge main window](screenshots/RetroModem-Bridge.png)

![RetroModem Bridge started](screenshots/RetroModem-Bridge-started.png)

![Connected to a BBS](screenshots/CoCo3-connected.jpg)

## Hardware I Use

For my Tandy Color Computer 3 setup, I use a USB to DB25 RS-232 serial adapter connected to a Deluxe RS-232 Pak.

[USB to DB25 RS-232 Serial Adapter](https://amzn.to/4czD1Yg)
Disclosure: As an Amazon Associate, I may earn from qualifying purchases. Using this link does not increase the price you pay.

It lets a retro computer with a serial terminal program dial Telnet BBSes using basic AT commands.

## What is included in this package

- Renamed from Coco Modem Bridge to RetroModem Bridge
- App title changed to RetroModem Bridge
- EXE name changed to RetroModemBridge.exe
- COM dropdown shows friendly USB serial names when Windows exposes them
- Refresh button for rescanning COM ports
- Live serial line status for CTS, DSR, DCD, DTR, and RTS
- One-click publish scripts for creating a self-contained Windows EXE

## Recommended starting settings

- Baud: 19200
- Data: 8-N-1
- Flow control: None
- DTR: On
- RTS: On
- Default TCP port: 23

## Choosing the right COM port

1. Open RetroModem Bridge.
2. Select the COM port for your USB serial adapter.
3. Select the baud rate you plan to use.
4. Click Start Bridge.
5. On the retro computer terminal, type:

```text
AT
```

The app should reply:

```text
OK
```

The COM dropdown may show friendly names like `COM11 - USB Serial Port (FTDI)` when Windows provides that information.

## Basic supported commands

```text
AT
ATZ
ATI
ATE0
ATE1
ATH
ATDT darkrealms.ca:23
ATDT bbs.fozztexx.com:23
```

If no port is included, the app uses the Default TCP port field.

Example:

```text
ATDT darkrealms.ca
```

with Default TCP port set to 23 will connect to:

```text
darkrealms.ca:23
```

## Building in Visual Studio Code

Open the RetroModemBridge folder in VS Code, then run:

```powershell
dotnet restore
dotnet build
dotnet run --project RetroModemBridge
```

## Creating the EXE

Double-click:

```text
publish-exe.bat
```

The finished EXE will be created here:

```text
RetroModemBridge\bin\Release\net8.0-windows\win-x64\publish\RetroModemBridge.exe
```

This publish script creates a self-contained Windows x64 EXE.

## Requirements

To build or publish:

- Windows
- .NET 8 SDK

To run the published self-contained EXE:

- Windows x64
- A serial port or USB-to-serial adapter
