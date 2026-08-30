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
    supertype_flags INTEGER NOT NULL,
    subtypes TEXT,
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

CREATE TABLE card_strings (
    card_key TEXT NOT NULL,
    string_index INTEGER NOT NULL,
    text TEXT NOT NULL DEFAULT '',
    PRIMARY KEY (card_key, string_index),
    FOREIGN KEY (card_key) REFERENCES cards(card_key) ON DELETE CASCADE
);

CREATE TABLE card_characteristics (
    card_key TEXT PRIMARY KEY,
    loyalty INTEGER NOT NULL DEFAULT 0,
    defense INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY (card_key) REFERENCES cards(card_key) ON DELETE CASCADE
);

CREATE TABLE card_features (
    card_key TEXT NOT NULL,
    face_index INTEGER NOT NULL,
    feature_index INTEGER NOT NULL,
    feature_role INTEGER NOT NULL DEFAULT 0,
    role_value TEXT NOT NULL DEFAULT '',
    name TEXT NOT NULL,
    type_flags INTEGER NOT NULL,
    mana_cost TEXT NOT NULL DEFAULT '',
    rules_text TEXT NOT NULL DEFAULT '',
    power INTEGER NOT NULL DEFAULT 0,
    toughness INTEGER NOT NULL DEFAULT 0,
    loyalty INTEGER NOT NULL DEFAULT 0,
    defense INTEGER NOT NULL DEFAULT 0,
    supertype_flags INTEGER NOT NULL DEFAULT 0,
    subtypes TEXT NOT NULL DEFAULT '',
    power_expression TEXT NOT NULL DEFAULT '',
    toughness_expression TEXT NOT NULL DEFAULT '',
    PRIMARY KEY (card_key, face_index, feature_index),
    FOREIGN KEY (card_key) REFERENCES cards(card_key) ON DELETE CASCADE
);

CREATE TABLE feature_effects (
    card_key TEXT NOT NULL,
    face_index INTEGER NOT NULL,
    feature_index INTEGER NOT NULL,
    effect_order INTEGER NOT NULL,
    trigger TEXT NOT NULL,
    effect_key TEXT NOT NULL,
    parameters_json TEXT NOT NULL DEFAULT '{}',
    PRIMARY KEY (card_key, face_index, feature_index, effect_order),
    FOREIGN KEY (card_key, face_index, feature_index)
        REFERENCES card_features(card_key, face_index, feature_index) ON DELETE CASCADE
);

CREATE TABLE feature_strings (
    card_key TEXT NOT NULL,
    face_index INTEGER NOT NULL,
    feature_index INTEGER NOT NULL,
    string_index INTEGER NOT NULL,
    text TEXT NOT NULL DEFAULT '',
    PRIMARY KEY (card_key, face_index, feature_index, string_index),
    FOREIGN KEY (card_key, face_index, feature_index)
        REFERENCES card_features(card_key, face_index, feature_index) ON DELETE CASCADE
);

CREATE TABLE feature_rule_entries (
    card_key TEXT NOT NULL,
    face_index INTEGER NOT NULL,
    feature_index INTEGER NOT NULL,
    entry_order INTEGER NOT NULL,
    entry_kind INTEGER NOT NULL,
    chapter_numbers TEXT NOT NULL DEFAULT '',
    text TEXT NOT NULL DEFAULT '',
    PRIMARY KEY (card_key, face_index, feature_index, entry_order),
    FOREIGN KEY (card_key, face_index, feature_index)
        REFERENCES card_features(card_key, face_index, feature_index) ON DELETE CASCADE
);

CREATE INDEX cards_name_index ON cards(name);
CREATE INDEX cards_printing_index ON cards(set_code, collector_number);
CREATE INDEX card_features_name_index ON card_features(name);
