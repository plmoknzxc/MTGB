"""Repair missing level ability blocks using published Japanese printed text."""
import gzip
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
folder = ROOT / 'Assets/StreamingAssets/CardTranslations'
base = {}
for line in (folder / 'ja-JP.txt').read_text(encoding='utf-8-sig').splitlines():
    row = line.split('|', 3)
    if len(row) == 4:
        base[row[0]] = row
local = {}
for line in (ROOT / 'ForgeRuntime/catalog.jsonl').read_text(encoding='utf-8').splitlines():
    card = json.loads(line)
    for face in card['faces']:
        local.setdefault(face['name'], face)
level = re.compile(r'(?:Lv|Ｌｖ|レベル|LEVEL)\s*[０-９0-9]+\s*[-–—~＋+]', re.I)
candidates = {}
with gzip.open(ROOT / '.tools/japanese-card-images/all-cards.jsonl.gz', 'rt', encoding='utf-8') as source:
    for line in source:
        card = json.loads(line)
        name = card['name']
        if card.get('lang') != 'ja' or card.get('layout') != 'leveler' or name not in base or name not in local:
            continue
        if level.search(base[name][3]) or 'LEVEL ' not in local[name]['text']:
            continue
        text = card.get('printed_text') or ''
        if len(level.findall(text)) < 2 or not re.search('[\u3040-\u30ff]', text):
            continue
        # Exact canonical face identity and physical card stats are required.
        face = local[name]
        if any(face.get(key) != card.get(key) for key in ('power', 'toughness')) or face['manaCost'] != card['mana_cost']:
            continue
        if name not in candidates or len(text) > len(candidates[name]['printed_text']):
            candidates[name] = card
correction_path = folder / 'ja-JP.corrections.json'
corrections = json.loads(correction_path.read_text(encoding='utf-8')) if correction_path.exists() else dict(entries=[])
correction_names = {row['name'] for row in corrections['entries']}
rows, evidence = [], []
for name, card in sorted(candidates.items()):
    if name not in correction_names:
        corrections['entries'].append(dict(name=name, expectedRules=base[name][3].replace('\\n', '\n')))
    values = [name, card['printed_name'], card['printed_type_line'], card['printed_text']]
    rows.append('|'.join(value.replace('|', '｜').replace('\n', '\\n') for value in values))
    evidence.append(dict(name=name, source=card['scryfall_uri'], printingID=card['id'],
                         previousRules=base[name][3], sha256=hashlib.sha256(card['printed_text'].encode()).hexdigest()))
(folder / 'ja-JP.printed.txt').write_text('\n'.join(rows) + '\n', encoding='utf-8')
correction_path.write_text(json.dumps(corrections, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
(ROOT / 'Tools/ForgeBridge/Reports/JapaneseLevelerRepairs.json').write_text(json.dumps(dict(
    repaired=len(rows), entries=evidence), ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print('Identity-checked complete Japanese leveler records:', len(rows))
