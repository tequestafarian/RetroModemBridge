# Session Mirror renderer update

This build changes the ANSI terminal renderer used by Session Mirror and the built-in terminal.

Changes:
- Uses a 24-row retro terminal viewport instead of 25 rows.
- Draws text one fixed cell at a time on an 80-column grid.
- This should make ANSI boxes, line art, and local BBS screens align more closely with the CoCo/NetMate display.
- The byte stream sent to the real retro computer is unchanged.

Why:
The previous renderer drew normal text in longer Windows font runs. That looked smoother on Windows, but it could make the Mirror window differ from the retro computer because retro terminals render fixed-width cells.


## Gap fix

This build slightly overlaps CP437 box/shade graphics when rendering in the Session Mirror. This reduces tiny visual gaps between adjacent ANSI cells while keeping the actual serial output unchanged.


## Manual box drawing fix

This build stops relying on the Windows font to draw CP437 box-drawing characters in the Session Mirror. Box characters such as `═`, `║`, `╔`, and `╝` are now drawn as continuous line strokes inside the fixed 80-column grid. This should remove the small visual gaps between ANSI border characters.
