"""Export exact-printing image URLs from the existing local Scryfall bulk cache."""
import collections
import gzip
import json
from pathlib import Path
import unicodedata

ROOT = Path(__file__).resolve().parents[2]
def normal(s):
    return unicodedata.normalize('NFKC', s or '').replace('’', "'").casefold().strip()

catalog = collections.defaultdict(list)
with (ROOT / 'ForgeRuntime/catalog.jsonl').open(encoding='utf8') as source:
    for line in source:
        card = json.loads(line)
        catalog[card['key']].append(card)
matches = collections.defaultdict(list)
with gzip.open(ROOT / '.tools/card-images/default-cards.jsonl.gz', 'rt', encoding='utf8') as source:
    for line in source:
        card = json.loads(line)
        key = card.get('set', '').upper() + '/' + card.get('collector_number', '')
        candidates = catalog.get(key, [])
        if len(candidates) != 1 or candidates[0].get('ambiguous'):
            continue
        if normal(candidates[0]['name']) not in {normal(c.get('name')) for c in [card, *card.get('card_faces', [])]}:
            continue
        if card.get('image_status') == 'placeholder':
            continue
        faces = [card] if card.get('image_uris') else card.get('card_faces', [])
        images = []
        for face in faces:
            uris = face.get('image_uris', {})
            images.append({k: uris.get(k, '') for k in ('small', 'normal', 'large')})
        if images and all(i['normal'].startswith('https://cards.scryfall.io/') for i in images):
            matches[key].append({'key': key, 'name': candidates[0]['name'], 'language': card.get('lang', ''), 'faces': images})
output = ROOT / 'Assets/StreamingAssets/CardArtSources.jsonl'
output.parent.mkdir(parents=True, exist_ok=True)
temporary = output.with_suffix('.tmp')
with temporary.open('w', encoding='utf8', newline='\n') as target:
    for key in sorted(matches):
        if len(matches[key]) == 1:
            target.write(json.dumps(matches[key][0], ensure_ascii=False) + '\n')
temporary.replace(output)
print(f'Exported {sum(len(v)==1 for v in matches.values())} exact-printing image sources')
