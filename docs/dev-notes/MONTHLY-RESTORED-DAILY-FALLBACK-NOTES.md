# Monthly Restored + Daily Fallback

This build restores the original Monthly ZIP behavior.

Monthly ZIP behavior:

- uses `bbslist.csv`
- extracts it directly to RMB's local `current-bbslist.csv`
- does not use the newer daily XML conversion path

Daily ZIP fallback:

- only used if `bbslist.csv` is missing
- looks for `dialdirectory.xml`
- sanitizes loose XML ampersands
- converts the XML entries into RMB's local `current-bbslist.csv`

Why:

The Daily ZIP support accidentally changed too much of the guide updater path and broke the Monthly update that previously worked. This build keeps the original Monthly path intact and only adds Daily handling as a fallback.
