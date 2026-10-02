"""Review every missing field against the complete cached foreign-print source.

No inferred translation and no runtime network lookup. Exact canonical face
names are required; source absence is bounded to the indexed source snapshot.
"""
import gzip
import json
import re
from pathlib import Path
from JapanesePrintedText import face_rules

ROOT = Path(__file__).resolve().parents[2]
FOLDER = ROOT / 'Assets/StreamingAssets/CardTranslations'
contexts = json.loads((ROOT / '.tools/presentation-audit/review-contexts.json').read_text(encoding='utf-8'))
wanted = {x['gap']['name'] for x in contexts}
source = {}
japanese = re.compile(r'[\u3040-\u30ff\u3400-\u9fff]')
with gzip.open(ROOT / '.tools/japanese-card-images/all-cards.jsonl.gz', 'rt', encoding='utf-8') as stream:
    for line in stream:
        card = json.loads(line)
        if card.get('lang') != 'ja':
            continue
        for face in card.get('card_faces') or [card]:
            if face['name'] not in wanted:
                continue
            candidate = dict(name=face.get('printed_name', ''), type=face.get('printed_type_line', ''),
                             rules=face_rules(card, face), url=card['scryfall_uri'], released=card['released_at'])
            source.setdefault(face['name'], []).append(candidate)
foreign_path = ROOT / '.tools/keyword-glossary/mtgjson-foreign-texts.json'
for name, languages in json.loads(foreign_path.read_text(encoding='utf-8')).items():
    if name not in wanted:
        continue
    for row in languages.get('Japanese', []):
        source.setdefault(name, []).append(dict(name=row.get('faceName') or row.get('name', ''),
            type=row.get('type', ''), rules=row.get('text', ''),
            url='https://mtgjson.com/api/v5/AtomicCards.json.xz', released=''))
rows, review = {}, []
for context in contexts:
    gap = context['gap']
    if gap['field'] not in ('name', 'type', 'rules'):
        continue
    candidates = source.get(gap['name'], []) if gap['language'] == 'ja-JP' else []
    available = [x for x in candidates if japanese.search(x[gap['field']])]
    result = dict(**gap, checkedSource='cached Scryfall all_cards Japanese face records and MTGJSON Japanese foreign text; existing Arena/PDF supplements already applied before candidate generation')
    if available:
        best = max(available, key=lambda x: x['released'])
        row = rows.setdefault(gap['name'], [gap['name'], '', '', ''])
        row[{'name': 1, 'type': 2, 'rules': 3}[gap['field']]] = best[gap['field']]
        result.update(status='published-field-imported', source=best['url'], value=best[gap['field']])
    elif candidates and gap['field'] == 'name' and any(x['name'] == gap['detail'] and x['name'] for x in candidates):
        result.update(status='published-Latin-name-retained', source=candidates[0]['url'])
    elif gap['language'] == 'zh-CN' and gap['name'] in ('C.A.M.P.', 'V.A.T.S.'):
        result.update(status='published-Latin-name-retained', source='existing identity-checked Chinese metadata')
    else:
        result.update(status='translation-unavailable-in-checked-sources', japanesePrints=len(candidates))
    review.append(result)
(FOLDER / 'ja-JP.review.txt').write_text('\n'.join('|'.join(value.replace('|', '｜').replace('\n', '\\n') for value in row)
                                                   for _, row in sorted(rows.items())) + '\n', encoding='utf-8')
(ROOT / 'Tools/ForgeBridge/Reports/LocalizedMissingFieldReview.json').write_text(
    json.dumps(dict(complete=True, reviewed=len(review), importedFaces=len(rows), entries=review,
                    limitation='Absence is limited to cached source snapshot, not proof that no translation exists elsewhere.'), ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print('Reviewed missing fields:', len(review), '; published faces imported:', len(rows))
