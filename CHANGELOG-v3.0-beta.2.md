# Changelog

## v3.0-beta.2 - RetroModem Bridge v3 Beta 2

### Added

- Added Session Mirror window
- Added optional input from Session Mirror
- Added Local Echo option for Session Mirror
- Added local BBS-style menu through `ATDT MENU`
- Added BBS favorites
- Added dial history
- Added connection testing
- Added local text services:
  - `ATDT HELP`
  - `ATDT TIME`
  - `ATDT MENU`
  - `ATDT BBSLIST`
  - `ATDT FAVORITES`
- Added improved BBS directory fields:
  - Category
  - System type
  - ANSI support
  - Favorite
  - Last result
  - Notes
- Added new app icon
- Added cleaner BBS directory toolbar
- Added More dropdown for less-used directory actions

### Changed

- Moved Import Guide back to the visible toolbar
- Made Mirror a prominent toolbar action
- Moved Dial/Copy Dial Command into the More menu
- Improved local BBS menu layout
- Improved ANSI local menu styling
- Updated main forms to use the new app icon

### Fixed / Improved

- Fixed Session Mirror typing visibility with Local Echo
- Reduced BBS directory toolbar wrapping
- Improved local menu spacing and alignment
- Improved app polish for beta release

### Beta notes

- Session Mirror input may duplicate characters if the remote BBS also echoes. Turn off Local Echo in that case.
- ANSI rendering may vary by retro terminal program.
- This is still a pre-release beta.
