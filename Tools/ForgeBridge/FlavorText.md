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
