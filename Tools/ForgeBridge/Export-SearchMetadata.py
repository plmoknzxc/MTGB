"""Build local advanced-search metadata from the existing bulk cache; never downloads cards."""
import collections
import gzip
import json
import unicodedata
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
def normalize(value):
    return unicodedata.normalize('NFKC', value or '').replace('’', "'").casefold().strip()
def colors(values):
    return sum(1 << 'WUBRG'.index(v) for v in set(values) if v in 'WUBRG')

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
        faces = card.get('card_faces') or [card]
        if normalize(candidates[0]['name']) not in {normalize(c.get('name')) for c in [card, *faces]}:
            continue
        matches[key].append(dict(key=key, name=candidates[0]['name'],
            colors=colors(card.get('colors', [c for f in faces for c in f.get('colors', [])])),
            identity=colors(card.get('color_identity', [])), manaValue=card.get('cmc', 0),
            games=sum({'paper':1,'arena':2,'mtgo':4}.get(g,0) for g in set(card.get('games',[]))),
            legalityMask=sum(bit for state,bit in [('legal',1),('restricted',2),('banned',4)] if state in card.get('legalities',{}).values()),
            rarity=card.get('rarity', ''), setName=card.get('set_name', ''),
            artist=' / '.join(sorted({f.get('artist', '') for f in faces} - {''})),
            released=int(card.get('released_at', '0000-00-00').replace('-', '')),
            layout=card.get('layout', '')))
output = ROOT / 'Assets/StreamingAssets/CardSearch.jsonl'
temporary = output.with_suffix('.tmp')
with temporary.open('w', encoding='utf8', newline='\n') as target:
    for key in sorted(matches):
        if len(matches[key]) == 1:
            target.write(json.dumps(matches[key][0], ensure_ascii=False, separators=(',', ':')) + '\n')
temporary.replace(output)
print(f'Exported {sum(len(v) == 1 for v in matches.values())} exact-printing search records')
