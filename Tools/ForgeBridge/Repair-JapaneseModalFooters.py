"""Repair exact cached-source matches; never guess at arbitrary translated prose."""
import json
from pathlib import Path
from JapanesePrintedText import face_rules

ROOT = Path(__file__).resolve().parents[2]
folder = ROOT / 'Assets/StreamingAssets/CardTranslations'
repairs = {}
for path in sorted((ROOT / '.tools/japanese-rule-supplements').glob('page-*.json')):
    for card in json.loads(path.read_text(encoding='utf-8')).get('data', []):
        for face in card.get('card_faces', []):
            old = face.get('printed_text', '')
            clean = face_rules(card, face)
            if old != clean:
                repairs[(face['name'], old)] = (clean, card['scryfall_uri'])
evidence = []
for path in (folder / 'ja-JP.txt', folder / 'ja-JP.supplement.txt'):
    lines = path.read_text(encoding='utf-8-sig').splitlines()
    for index, line in enumerate(lines):
        row = line.split('|', 3)
        if len(row) != 4:
            continue
        found = repairs.get((row[0], row[3].replace('\\n', '\n')))
        if found:
            row[3] = found[0].replace('\n', '\\n')
            lines[index] = '|'.join(row)
            evidence.append(dict(name=row[0], file=path.name, source=found[1]))
    if any(item['file'] == path.name for item in evidence):
        path.write_text('\n'.join(lines) + '\n', encoding='utf-8')
(ROOT / 'Tools/ForgeBridge/Reports/JapaneseModalFooterRepairs.json').write_text(
    json.dumps(dict(repaired=len(evidence), entries=evidence), ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print('Identity-checked modal footer repairs:', len(evidence))
