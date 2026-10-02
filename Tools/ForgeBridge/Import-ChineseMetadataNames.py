"""Fill missing Chinese names/types independently of rules-text coverage.

An already translated effect or a vanilla card must not prevent its name/type
from being imported. Existing translated fields remain owned by the base data.
"""
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
names = set()
for line in (ROOT / 'ForgeRuntime/catalog.jsonl').read_text(encoding='utf-8').splitlines():
    card = json.loads(line)
    if card.get('ambiguous'):
        continue
    names.add(card['name'])
    names.update(face['name'] for face in card['faces'])
rows, evidence = {}, {}
for path in sorted((ROOT / '.tools/chinese-card-images').glob('set-*.json')):
    for item in json.loads(path.read_text(encoding='utf-8'))['items']:
        for face in [item] + (item.get('other_faces') or []):
            name = face['display_name']
            chinese = face.get('display_name_zh') or ''
            if name not in names or not re.search('[\u3400-\u9fff]', chinese):
                continue
            type_line = face.get('display_type_line') or ''
            if not re.search('[\u3400-\u9fff]', type_line):
                type_line = ''
            rows.setdefault(name, [name, chinese, type_line, ''])
            evidence.setdefault(name, dict(name=name, printing=item['set']+'/'+item['collector_number'],
                                         source='https://mtgch.com'+item['card_detail_url']))
destination = ROOT / 'Assets/StreamingAssets/CardTranslations/zh-CN.notes.txt'
destination.write_text('\n'.join('|'.join(value.replace('|', '｜').replace('\n', '\\n') for value in row)
                                   for name, row in sorted(rows.items()))+'\n', encoding='utf-8')
(ROOT / 'Tools/ForgeBridge/Reports/ChineseMetadataNames.json').write_text(json.dumps(dict(
    publishedRecords=len(rows), fields='name/type only; runtime fills missing fields, preserves rules',
    entries=list(evidence.values())), ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
print('Identity-matched Chinese name/type records:', len(rows))
