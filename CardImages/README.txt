Forge / Unity card images

Layout: CardImages/<UPPERCASE SET>/<COLLECTOR NUMBER>.jpg
Second face: CardImages/<UPPERCASE SET>/<COLLECTOR NUMBER>_2.jpg
Unity also supports PNG and JPEG. Missing images use a text card fallback.

Official images come from Scryfall's default-cards bulk dataset, using exact
printing keys and matching card names. Normal images are generally 488 x 680.
Existing local images are preserved. No Unity texture import is required.
For standalone games place CardImages beside the executable.

Resume/import: python Tools/ForgeBridge/Import-CardImages.py
Dependencies: requests, Pillow. Requires ForgeRuntime/catalog.jsonl.
Source URLs, hashes, download results and unmatched entries: .tools/card-images/
Downloaded JPG files are excluded from Git and can be recreated.

Image source: https://scryfall.com/
Card artwork and game content belong to their respective rights holders.
The former CardPacks/.mtgbpack and Lua instructions no longer apply here.
