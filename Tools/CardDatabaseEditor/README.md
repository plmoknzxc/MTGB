# MTGB Data Editor

Windows desktop editor for MTGB `.mtgbdb` SQLite card databases.

## Features

- Open and create `.mtgbdb` databases.
- Search cards by name, ID, set code, collector number, or type.
- Edit multiple MTG card types, mana cost, rules text, stats, and script path.
- Edit multiple ordered effects with JSON parameters.
- Validate schema version and card/effect data.

## Requirements

- Windows 10 or later.
- .NET 8 Desktop Runtime.

The data editor does **not** depend on Unity or XLua. It only writes the Lua script path into the database. MTGB's Unity runtime needs XLua when it loads and executes those scripts.

## Run from source

```powershell
dotnet run --project MTGB.CardDatabaseEditor.csproj
```

## Validate without opening the UI

```powershell
dotnet run --project MTGB.CardDatabaseEditor.csproj -- --validate path/to/cards.mtgbdb
```

## Database layout

The included `schema.sql` defines the current schema. A card printing is identified by `(set_code, collector_number)`, while `oracle_id` identifies the shared rules object. `card_effects` supports multiple ordered effects per card.
