# MTGB card databases

`core.mtgbdb` and every file under `Expansions/*.mtgbdb` are SQLite databases.
The custom extension prevents players from confusing them with unrelated SQLite files.

Load order:

1. `core.mtgbdb`
2. Expansion databases in ordinal file-name order

Card IDs and `(set_code, collector_number)` printings must be unique across all loaded databases.
The core database wins when a later database contains a duplicate.

Card images remain separate:

```text
CardImages/<set_code>/<collector_number>.png
```

Lua scripts are paths relative to their database. Script paths outside that folder are rejected.
The current runtime supports the first `on_play` effect through `CardEffectRegistry`; the schema already allows multiple ordered effects for later rule-engine work.
