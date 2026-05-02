# RetroModem Bridge v3 Beta 3

## A major upgrade for vintage computers and Telnet BBS access

RetroModem Bridge v3 Beta 3 is the biggest update so far. What started as a Windows serial-to-Telnet bridge now feels like a full retro-side BBS launcher and connection center.

Your vintage computer can now open a local BBS-style menu, browse and search BBS listings, update the Telnet BBS Guide, add new BBSes, launch favorites, and connect to Telnet boards through a Windows PC.

## Highlights

- **Local BBS menu** with `ATDT MENU`
- **Paged and searchable BBS directory** from the retro computer
- **Retro-side Import Guide browser** with `ATDT GUIDE`
- **Monthly and Daily Telnet BBS Guide updater** with `ATDT UPDATEGUIDE`
- **ANSI guide update screen** with progress and stats
- **Session Mirror** in the Windows app
- **Favorites and dial history**
- **Connection testing**
- **Test All Favorites**
- **BBS health/status columns**
- **Retro computer profiles**
- **Export Support Bundle**
- **New RMB app icon**

## Local BBS commands

```text
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
```

## Monthly and Daily update support

Monthly and Daily guide downloads are handled separately because they use different file formats:

```text
Monthly = bbslist.csv
Daily   = dialdirectory.xml
```

This keeps both update paths stable.

## Recommended test

From your retro terminal:

```text
AT
ATDT MENU
```

Then try:

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

## Release status

This is still marked as a beta/pre-release, but it is the recommended v3 build.
