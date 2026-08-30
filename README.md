# MTGB

MTGB is a Unity 6 prototype for a Magic-style card game. The current scene focuses on an Arena-like battlefield, UI hands, mana, phases, card play, and Lua-driven card behavior.

## Requirements

- Unity `6000.3.11f1`
- Windows for the current native SQLite prototype

## Project layout

- `Assets/Scenes/MTGAPrototype.unity` - current prototype scene
- `Assets/Scripts` - rules, UI, database loading, and scripting integration
- `Assets/Plugins/LuaCSharp` - Lua-CSharp runtime binaries used to execute card scripts
- `CardDatabases/*.mtgbdb` - SQLite card databases and schema
- `CardImages/<set>/<collector>.png` - external card image convention
- `CardScripts/<set>/m<collector>.lua` - external card script convention
- `Decks/*.mdk` - deck files

## Card data

The game loads `CardDatabases/core.mtgbdb`, then other `*.mtgbdb` files directly under `CardDatabases`. A printing is identified by `(set_code, collector_number)`. Runtime state such as zones, damage, tapping, and ownership is not stored in the database.

Card images and scripts use the same printing key:

- Images: `CardImages/<set>/<collector>.png`
- Scripts: `CardScripts/<set>/m<collector>.lua`

The runtime accepts schema V7 databases only. The standalone data editor is maintained separately in [MTGB-DataEditor](https://github.com/plmoknzxc/MTGB-DataEditor). It does not require Unity.

## Development tooling

Editor automation uses MCP For Unity when available. Card scripts run through Lua-CSharp.
