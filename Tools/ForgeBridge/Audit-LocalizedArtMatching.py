"""Audit cached source coverage independently of importer name matching.

Only registered, unambiguous local printings and related helper tokens are in
scope. A missing source image is distinct from an available image omitted from
the download plan. Source caches must first be populated by the importers.
"""
import gzip
import json
from pathlib import Path
from urllib.parse import urlparse

ROOT = Path(__file__).resolve().parents[2]


def read(path):
    return json.loads((ROOT / path).read_text(encoding='utf-8'))


def audit():
    catalog = {}
    ambiguous = set()
    for line in (ROOT / 'ForgeRuntime/catalog.jsonl').read_text(encoding='utf-8').splitlines():
        card = json.loads(line)
        if card.get('ambiguous'):
            ambiguous.add(card['key'])
        else:
            catalog[card['key']] = card
    for entry in read('Assets/Resources/RelatedTokenArt.json')['entries']:
        for token in entry['tokens']:
            catalog.setdefault(token['key'], token)
    plans = {lang: {job['relative']: job for job in read('.tools/' + directory + '/plan.json')}
             for lang, directory in [('zh-CN', 'chinese-card-images'), ('ja-JP', 'japanese-card-images')]}
    expected = {'zh-CN': set(), 'ja-JP': set()}
    ambiguous_sources = {'zh-CN': set(), 'ja-JP': set()}
    for code in sorted({key.split('/')[0] for key in catalog}):
        path = ROOT / '.tools/chinese-card-images' / ('set-' + code + '.json')
        if not path.exists():
            raise ValueError('Missing cached set: ' + code)
        data = json.loads(path.read_text(encoding='utf-8'))
        assert len(data['items']) == data['count'] and data.get('total_pages', 1) <= 1
        for item in data['items']:
            key = item['set'].upper() + '/' + item['collector_number']
            faces = [item] + ((item.get('other_faces') or []) if item.get('is_double_faced') else [])
            for index, face in enumerate(faces):
                url = urlparse(face.get('image_url') or '')
                if url.hostname != 'images.mtgch.com' or not url.path.startswith('/zhs/'):
                    continue
                if key in ambiguous and key not in catalog:
                    ambiguous_sources['zh-CN'].add(key)
                if key in catalog:
                    expected['zh-CN'].add(key + ('' if index == 0 else '_' + str(index + 1)) + '.jpg')
    scanned = 0
    with gzip.open(ROOT / '.tools/japanese-card-images/all-cards.jsonl.gz', 'rt', encoding='utf-8') as stream:
        for line in stream:
            card = json.loads(line)
            scanned += 1
            if card.get('lang') != 'ja' or card.get('image_status') == 'placeholder':
                continue
            key = card['set'].upper() + '/' + card['collector_number']
            faces = [card] if card.get('image_uris') else card.get('card_faces', [])
            for index, face in enumerate(faces):
                if urlparse((face.get('image_uris') or {}).get('normal', '')).hostname != 'cards.scryfall.io':
                    continue
                if key in ambiguous and key not in catalog:
                    ambiguous_sources['ja-JP'].add(key)
                if key in catalog:
                    expected['ja-JP'].add(key + ('' if index == 0 else '_' + str(index + 1)) + '.jpg')
    results = {}
    for lang in plans:
        missing = sorted(expected[lang] - plans[lang].keys())
        absent = [relative for relative in plans[lang]
                  if not any((ROOT / 'CardImages' / lang / relative).with_suffix(ext).is_file()
                             for ext in ('.jpg', '.jpeg', '.png'))]
        results[lang] = dict(sourceFaces=len(expected[lang]), plannedFaces=len(plans[lang]),
                             availableButUnmatched=missing, plannedButMissingFiles=absent,
                             ambiguousSourceKeysExcluded=sorted(ambiguous_sources[lang]))
    report = dict(localPrintings=len(catalog), ambiguousLocalKeysExcluded=len(ambiguous),
                  japaneseSourceRecords=scanned, sources=results,
                  scope='Cached Academy Ruins set metadata and Scryfall all_cards snapshot; registered local printings and related tokens. Official Japanese Alchemy gallery imports have a separate report. Does not establish absence of art on other websites or newer source updates.')
    (ROOT / 'Tools/ForgeBridge/Reports/LocalizedArtMatchingAudit.json').write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(report, ensure_ascii=True), flush=True)
    if any(result['availableButUnmatched'] or result['plannedButMissingFiles'] for result in results.values()):
        raise SystemExit(1)


if __name__ == '__main__':
    audit()
