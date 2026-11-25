# Keyword icons

The artwork in `ForgeAbilities.png` is drawn for MTGB's battlefield footer.
`Index.json` maps Forge identities to 80 × 80 cells using top-left coordinates.
The 494 × 740 atlas uses its native size and lossless import. Protection and
hexproof variants use the relevant mana colors. Gained abilities have a blue backing.

Rebuild the atlas with `python Tools/CardRendering/DrawKeywordIcons.py` using
Pillow and resvg-py. The drawing source defines the silhouettes and their ink outlines.
