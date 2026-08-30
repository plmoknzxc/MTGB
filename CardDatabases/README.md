# MTGB card databases

`core.mtgbdb` and every other `*.mtgbdb` file in this folder are SQLite databases.
The custom extension prevents players from confusing them with unrelated SQLite files.

Load order:

1. `core.mtgbdb`
2. Other `*.mtgbdb` files in ordinal file-name order

Card IDs and `(set_code, collector_number)` printings must be unique across all loaded databases.
The core database wins when a later database contains a duplicate.

Card images and Lua scripts remain separate:

```text
CardImages/<set_code>/<collector_number>.png
CardScripts/<set_code>/m<collector_number>.lua
```

The runtime requires schema V7. It retains every face and feature, structured rule entries,
power/toughness expressions, feature roles, and role-specific values. Gameplay continues to
use face 0, feature 0 as the primary characteristic while retaining all feature data for
multipart-card and structured-card support. `script_path` is a fallback for editor-created
paths; the preferred script convention is still the printing-key path above.
The runtime also recognizes the DataEditor resource folders `卡图` and `脚本` for compatibility.
