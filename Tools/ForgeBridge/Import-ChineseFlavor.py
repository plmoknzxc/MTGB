"""Import exact-printing Simplified Chinese flavor text from MTGJSON CSV exports.

Usage: python Tools/ForgeBridge/Import-ChineseFlavor.py \
    --cards .tools/mtgjson-flavor/cards.csv.gz \
    --foreign .tools/mtgjson-flavor/cardForeignData.csv.gz

The CSV exports are available from https://mtgjson.com/api/v5/csv/ .
"""

import argparse
import collections
import csv
import gzip
import json
from pathlib import Path
import unicodedata


ROOT = Path(__file__).resolve().parents[2]
CATALOG = ROOT / "ForgeRuntime/catalog.jsonl"
OUTPUT = ROOT / "Assets/StreamingAssets/CardFlavor.zh-CN.jsonl"


def normal(value):
    return unicodedata.normalize("NFKC", value or "").replace("’", "'").casefold().strip()


def rows(path):
    opener = gzip.open if path.suffix == ".gz" else open
    with opener(path, "rt", encoding="utf-8-sig", newline="") as source:
        yield from csv.DictReader(source)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--cards", type=Path, required=True, help="MTGJSON cards.csv[.gz]")
    parser.add_argument("--foreign", type=Path, required=True, help="MTGJSON cardForeignData.csv[.gz]")
    parser.add_argument("--output", type=Path, default=OUTPUT)
    args = parser.parse_args()

    catalog = collections.defaultdict(list)
    with CATALOG.open(encoding="utf-8") as source:
        for line in source:
            card = json.loads(line)
            catalog[card["key"]].append(card)

    by_uuid = {}
    for card in rows(args.cards):
        if card["language"] != "English":
            continue
        key = card["setCode"].upper() + "/" + card["number"]
        candidates = catalog.get(key, ())
        if len(candidates) != 1 or candidates[0].get("ambiguous"):
            continue
        if normal(candidates[0]["name"]) != normal(card["name"]):
            continue
        by_uuid[card["uuid"]] = (key, candidates[0]["name"])

    matches = collections.defaultdict(list)
    for foreign in rows(args.foreign):
        text = foreign["flavorText"].strip()
        if (foreign["language"] != "Chinese Simplified" or not text
                or not any("\u3400" <= character <= "\u9fff" for character in text)):
            continue
        identity = by_uuid.get(foreign["uuid"])
        if identity is None:
            continue
        key, name = identity
        matches[key].append((name, text, foreign["uuid"]))

    output = args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = output.with_suffix(output.suffix + ".tmp")
    conflicts = []
    written = 0
    with temporary.open("w", encoding="utf-8", newline="\n") as target:
        for key in sorted(matches):
            entries = matches[key]
            if len({(name, text) for name, text, _ in entries}) != 1:
                conflicts.append(key)
                continue
            name, text, source_id = entries[0]
            target.write(json.dumps({"key": key, "name": name, "text": text,
                                     "sourceId": source_id}, ensure_ascii=False) + "\n")
            written += 1
    if conflicts:
        temporary.unlink()
        raise ValueError(f"Conflicting flavor text for {len(conflicts)} printings: {conflicts[:10]}")
    temporary.replace(output)
    print(f"Matched {len(by_uuid)} Forge printings; wrote {written} Chinese flavor texts to {output}")


if __name__ == "__main__":
    main()
