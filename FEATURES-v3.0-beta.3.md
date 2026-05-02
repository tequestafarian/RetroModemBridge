# RetroModem Bridge v3 Beta 3 Feature Pass

This source package adds a large usability pass on top of v3 Beta 2.

## Added

- Test All Favorites
- BBS health tracking columns:
  - Status
  - Last checked
  - Response time in ms
- Export Support Bundle
- Retro Computer Profiles
- Search and filter controls for the BBS Directory
- Random Favorite selector
- Duplicate-aware BBS Guide import remains in place
- Updated first-time setup checklist
- Local BBS services:
  - `ATDT NEWS`
  - `ATDT RANDOM`
  - `ATDT FEATURED`
  - `ATDT ONLINE`
- Local BBS menu entries for Featured/Random and News
- Session Mirror fixed-cell / manual box-drawing renderer from the prior build

## Notes

This is a source package. I could not compile the Windows EXE in this environment.
Please build locally with:

```powershell
dotnet build
.\publish-exe-v3-beta.ps1
```

Recommended tag if you release this as a follow-up beta:

```text
v3.0-beta.3
```


## Local BBS search and paging

Added retro-side browsing improvements for the local BBS directory.

From `ATDT MENU`, choose `1` for BBS Directory or `2` for Favorites.

Commands inside the directory/favorites list:

```text
N       Next page
P       Previous page
S       Search
C       Clear search
M       Main menu
Q       Disconnect
01-15   Dial listed BBS on the current page
```

Search matches name, alias, host, category, system type, notes, and last result.


## Retro-side Import Guide browser

Added a local BBS Guide browser that can be used directly from the retro computer.

Commands:

```text
ATDT GUIDE
ATDT IMPORT
```

or choose option `9` from `ATDT MENU`.

The guide browser shows bundled Telnet BBS Guide entries that are not already in your BBS Directory. You can browse, page, search, view details, add entries to your directory, or dial a guide entry once without saving.


## Telnet BBS Guide updater

Added an on-demand guide updater.

Windows app:

- Download Monthly
- Download Daily
- Load ZIP/CSV manually

Retro side:

```text
ATDT UPDATEGUIDE
```

The retro update screen includes an ANSI progress bar and status messages while the guide downloads and installs.

The downloaded guide is stored locally in AppData and used by the Windows import tool and the retro-side `ATDT GUIDE` browser. The bundled guide remains as a fallback.
