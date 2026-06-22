PRAGMA foreign_keys = ON;

CREATE TABLE metadata (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

CREATE TABLE cards (
    card_key TEXT PRIMARY KEY,
    card_id INTEGER NOT NULL UNIQUE,
    oracle_id TEXT NOT NULL DEFAULT '',
    set_code TEXT NOT NULL,
    collector_number TEXT NOT NULL,
    name TEXT NOT NULL,
    type_flags INTEGER NOT NULL,
    mana_cost TEXT NOT NULL DEFAULT '',
    rules_text TEXT NOT NULL DEFAULT '',
    power INTEGER NOT NULL DEFAULT 0,
    toughness INTEGER NOT NULL DEFAULT 0,
    script_path TEXT NOT NULL DEFAULT '',
    enabled INTEGER NOT NULL DEFAULT 1,
    UNIQUE (set_code, collector_number)
);

CREATE TABLE card_effects (
    card_key TEXT NOT NULL,
    effect_order INTEGER NOT NULL,
    trigger TEXT NOT NULL,
    effect_key TEXT NOT NULL,
    parameters_json TEXT NOT NULL DEFAULT '{}',
    PRIMARY KEY (card_key, effect_order),
    FOREIGN KEY (card_key) REFERENCES cards(card_key) ON DELETE CASCADE
);

CREATE INDEX cards_name_index ON cards(name);
CREATE INDEX cards_printing_index ON cards(set_code, collector_number);
