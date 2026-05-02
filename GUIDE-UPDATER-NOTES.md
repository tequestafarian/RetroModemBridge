# Telnet BBS Guide Updater

This build adds an on-demand Telnet BBS Guide updater.

## Windows app

Open **More > Update / Merge BBS Guide**.

Options:

- Download Monthly
- Download Daily
- Load ZIP/CSV manually

The updater downloads the current ZIP from the Telnet BBS Guide download page, extracts `bbslist.csv`, and saves it locally under the user's AppData folder.

The bundled guide is still kept as a fallback.

## Retro computer side

From the retro terminal:

```text
ATDT UPDATEGUIDE
```

or from the local BBS menu:

```text
U
```

Retro-side update options:

```text
1   Monthly official list
2   Daily personal-use list
3   Current guide status
```

The retro-side updater shows an ANSI progress bar while the update is running.

## Storage

Downloaded guide files are stored here:

```text
%APPDATA%\RetroModemBridge\TelnetBbsGuide\current-bbslist.csv
%APPDATA%\RetroModemBridge\TelnetBbsGuide\last-download.zip
%APPDATA%\RetroModemBridge\TelnetBbsGuide\guide-meta.json
```

## Fallback

If the site blocks automated downloads, use the Windows app's **Load ZIP/CSV** option after downloading the guide manually in your browser.


## Guide stats panel

The retro-side update guide screens now show a stats panel with:

- Current BBS Directory listings
- Current guide listings
- New guide listings available to add
- Entries already in your directory
- Favorites
- Online-tested listings

These stats appear on:

- `ATDT UPDATEGUIDE`
- Update Guide option `3`
- The update complete screen


## New-only guide filter

The Windows Import Guide window now has a Guide filter dropdown:

- New only
- Already in directory
- All guide entries

The default is **New only**, so after updating the monthly/daily guide you can immediately see entries that are not already in your BBS Directory.

The count line also shows:

- total guide entries
- new entries
- already-added entries
