# Local BBS Import Guide Browser

This build adds a retro-side browser for the bundled Telnet BBS Guide.

## Direct command

From the retro terminal:

```text
ATDT GUIDE
```

or:

```text
ATDT IMPORT
```

## Local BBS menu

From `ATDT MENU`, choose:

```text
9  Browse Import Guide
```

## Guide commands

Inside the guide list:

```text
N       Next page
P       Previous page
S       Search guide
C       Clear search
M       Main menu
Q       Disconnect
01-15   View guide entry on the current page
```

Inside a guide entry:

```text
A       Add to your BBS Directory
D       Dial once without saving
G       Back to guide list
M       Main menu
Q       Disconnect
```

## Duplicate handling

The guide browser hides entries that are already in the BBS Directory by matching:

```text
host + port
```

When you add a guide entry, RetroModem Bridge assigns the next available numeric alias and removes it from the guide browser list.
