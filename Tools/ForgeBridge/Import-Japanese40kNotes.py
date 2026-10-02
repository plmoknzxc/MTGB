"""Import identity-mapped card blocks from the official Japanese 40K release notes.

The cached PDF is authoritative Japanese text even though the product was only
printed in English. Rulings and power/toughness lines are excluded from rules.
"""
import hashlib
import json
import re
from pathlib import Path
import fitz
import requests

ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / '.tools/japanese-rule-supplements'
URL = 'https://media.wizards.com/2022/downloads/40K_Release_Notes/JP_40K%20Commander%20Release%20Notes_20220912.pdf'
path = WORK / 'JP_40K_ReleaseNotes.pdf'
if not path.exists():
    response = requests.get(URL, timeout=45)
    response.raise_for_status()
    path.write_bytes(response.content)
aliases = {}
name_cache = WORK / '40k-official-names.json'
report_cache = ROOT / 'Tools/ForgeBridge/Reports/Japanese40kNotes.json'
if name_cache.exists():
    official_names = json.loads(name_cache.read_text(encoding='utf-8'))
else:
    official_names = json.loads(report_cache.read_text(encoding='utf-8'))['officialNameEvidence']
for row in official_names:
    aliases[row['japanese']] = row['name']
for file in (ROOT / 'Assets/StreamingAssets/CardTranslations').glob('ja-JP*.txt'):
    for line in file.read_text(encoding='utf-8-sig').splitlines():
        row = line.split('|', 3)
        if len(row) == 4:
            aliases[row[1]] = row[0]
catalog = {}
for line in (ROOT / 'ForgeRuntime/catalog.jsonl').read_text(encoding='utf-8').splitlines():
    card = json.loads(line)
    if card['forgeSet'] == '40K':
        catalog[card['name']] = card
with fitz.open(path) as document:
    text = '\n'.join(page.get_text() for page in document)
matches = list(re.finditer(r'^《([^》]+)》\s*$', text, re.MULTILINE))
records = []
evidence = []
skipped = []
for index, match in enumerate(matches):
    japanese = match[1]
    english = aliases.get(japanese)
    card = catalog.get(english)
    if not card:
        continue
    end = matches[index+1].start() if index+1 < len(matches) else len(text)
    lines = [line.strip() for line in text[match.end():end].splitlines() if line.strip()]
    if not lines or not lines[0].startswith('{'):
        continue
    mana = lines.pop(0).replace(' ', '')
    if mana != card['faces'][0]['manaCost']:
        raise ValueError('Mana identity mismatch: ' + english)
    type_line = lines.pop(0)
    if not re.search('クリーチャー|アーティファクト|エンチャント|インスタント|ソーサリー|土地', type_line):
        skipped.append(dict(name=english, reason='Official PDF card block omits type line; retained existing text'))
        continue
    if lines and re.fullmatch(r'[０-９0-9＊*]+/[０-９0-9＊*]+', lines[0]):
        printed = lines.pop(0).translate(str.maketrans('０１２３４５６７８９', '0123456789'))
        face = card['faces'][0]
        if printed != face['power'] + '/' + face['toughness']:
            skipped.append(dict(name=english, reason='PDF stats differ from current catalog; requires manual version review'))
            continue
    rules = []
    for line in lines:
        if line.startswith('•'):
            break
        rules.append(line)
    if not rules:
        continue
    rule_text = ''.join(rules)
    records.append('|'.join((english, japanese, type_line, rule_text)))
    evidence.append(dict(name=english, japanese=japanese, source=URL,
                         validation='Exact existing Japanese name, 40K catalog membership, mana cost and creature stats'))
destination = ROOT / 'Assets/StreamingAssets/CardTranslations/ja-JP.notes.txt'
destination.write_text('\n'.join(records) + '\n', encoding='utf-8')
(ROOT / 'Tools/ForgeBridge/Reports/Japanese40kNotes.json').write_text(json.dumps(dict(
    records=len(records), source=URL, sourceSHA256=hashlib.sha256(path.read_bytes()).hexdigest(), entries=evidence, skipped=skipped,
    officialNameEvidence=official_names),
    ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print('Validated official Japanese card blocks:', len(records))
