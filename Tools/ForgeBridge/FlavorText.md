# Card flavor display metadata

`Export-FlavorText.py` reads the existing `.tools/card-images/default-cards.jsonl.gz`
Scryfall bulk cache and the local Forge catalog. It makes no network requests.
Run `python Tools/ForgeBridge/Export-FlavorText.py` after updating either source.

Output: `Assets/StreamingAssets/CardFlavor.jsonl` (included in Unity builds).
Each row retains the Scryfall source ID. Rows require a unique Forge printing,
matching set/collector number and card name, and a unique source match.
Only front-face flavor is exported; absent/ambiguous matches are omitted.

The Unity loader additionally checks printing key, name and ambiguity. This is
optional display metadata, separate from the authoritative Forge rules catalog.
It does not modify official effects, rules, runtime checksums, or DIY packages.

## Simplified Chinese

`Import-ChineseFlavor.py` reads MTGJSON's `cards.csv.gz` and
`cardForeignData.csv.gz` exports. Download both from
`https://mtgjson.com/api/v5/csv/`, then run:

```sh
python Tools/ForgeBridge/Import-ChineseFlavor.py --cards cards.csv.gz --foreign cardForeignData.csv.gz
```

The output is `Assets/StreamingAssets/CardFlavor.zh-CN.jsonl`. An entry is used
only when the MTGJSON English card matches one unambiguous Forge printing by
set code, collector number, and original name. Conflicting Chinese entries
abort the import. Entries without Chinese characters are omitted. In the Chinese UI, a matched entry replaces that printing's
English flavor text; otherwise the existing English text remains. No rules or
card images are changed.
