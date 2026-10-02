"""Export optional front-face flavor text from the existing Scryfall bulk cache.

No network requests or card/rules mutations. Match exact printing AND card name.
Run: python Tools/ForgeBridge/Export-FlavorText.py
"""
import collections
import gzip
import json
from pathlib import Path
import unicodedata

ROOT = Path(__file__).resolve().parents[2]


def normal(value):
    return unicodedata.normalize('NFKC', value or '').replace('’', "'").casefold().strip()


def main():
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
            names = {normal(part.get('name')) for part in [card, *card.get('card_faces', [])]}
            if normal(candidates[0]['name']) not in names:
                continue
            front = (card.get('card_faces') or [card])[0]
            matches[key].append({'key': key, 'name': candidates[0]['name'],
                                 'text': front.get('flavor_text', ''), 'sourceId': card['id']})
    output = ROOT / 'Assets/StreamingAssets/CardFlavor.jsonl'
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = output.with_suffix('.tmp')
    count = 0
    with temporary.open('w', encoding='utf8', newline='\n') as target:
        for key in sorted(matches):
            rows = matches[key]
            if len(rows) != 1 or not rows[0]['text'].strip():
                continue
            target.write(json.dumps(rows[0], ensure_ascii=False) + '\n')
            count += 1
    temporary.replace(output)
    print(f'Exported {count} exact-printing front-face flavor texts to {output}')


if __name__ == '__main__':
    main()
