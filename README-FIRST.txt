RetroModem Bridge v3.5 IRC feature package

Run install-irc-feature-v3.5.bat from the root of your RetroModemBridge repo.

This updated package adds IRC support and also adds IRC to the retro-side local BBS menu.

From the retro terminal:
  ATDT MENU
  choose 12 for IRC Chat Bridge

Or direct dial:
  ATDT IRC

IRC commands after connect:
  /help
  /join #channel
  /nick NewName
  /me waves
  /msg nick hello
  /quit

The installer creates .irc-backup copies before patching.
