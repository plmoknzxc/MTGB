# MTGB

MTGB is a Unity 6 prototype for a Magic-style card game. The current scene focuses on an Arena-like battlefield, UI hands, mana, phases, card play, and Lua-driven card behavior.

## Requirements

- Unity `6000.3.11f1`
- Windows for the current native SQLite prototype
- .NET 8 Desktop Runtime for the standalone data editor

## Project layout

- `Assets/Scenes/MTGAPrototype.unity` - current prototype scene
- `Assets/Scripts` - rules, UI, database loading, and scripting integration
- `Assets/XLua` - XLua runtime used by the game to execute card scripts
- `CardDatabases` - SQLite card databases and schema
- `CardImages/<set>/<collector>.png` - external card image convention
- `Tools/CardDatabaseEditor` - standalone `.mtgbdb` editor source

## Card data

The game loads `CardDatabases/core.mtgbdb`, then databases under `CardDatabases/Expansions`. A printing is identified by `(set_code, collector_number)`. Runtime state such as zones, damage, tapping, and ownership is not stored in the database.

The standalone data editor does not require Unity or XLua. It edits card data and Lua script paths. XLua is required only by the Unity game when a card script is executed.

## Development tooling

PuerTS MCP and related embedded packages are optional local editor tooling and are intentionally not committed because their native binaries are close to 1 GB. Install the Unity MCP package separately when editor automation is needed.

## Validation

Build the standalone editor:

```powershell
dotnet build Tools/CardDatabaseEditor/MTGB.CardDatabaseEditor.csproj -c Release
```

Run its database round-trip test:

```powershell
Tools/CardDatabaseEditor/bin/Release/net8.0-windows/MTGB.CardDatabaseEditor.exe --self-test
```

XLua is distributed under its upstream MIT license; see `LICENSE.TXT` and the files under `Assets/XLua`.
