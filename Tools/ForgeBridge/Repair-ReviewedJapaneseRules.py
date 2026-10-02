"""Replace individually reviewed incomplete rules with paired published sources."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
folder = ROOT / 'Assets/StreamingAssets/CardTranslations'
def rows(path):
    return {r[0]: r for line in path.read_text(encoding='utf-8-sig').splitlines() if len(r := line.split('|', 3)) == 4}
arena = rows(folder / 'ja-JP.arena.txt')
foreign = json.loads((ROOT / '.tools/keyword-glossary/mtgjson-foreign-texts.json').read_text(encoding='utf-8'))
contexts = json.loads((ROOT / '.tools/presentation-audit/review-contexts.json').read_text(encoding='utf-8'))
repairs = {}
for name in ("Pyretic Prankster", "Heliod, the Radiant Dawn", "Second Little Pig", "Hunter's Axe", "Medic's Kitesail", "Etali's Favor", "Ninja Teen"):
    repairs[name] = (arena[name][3], 'official Arena card database')
repairs['Kros, Defense Contractor'] = (foreign['Kros, Defense Contractor']['Japanese'][0]['text'].replace('\n', '\\n'), 'https://mtgjson.com/api/v5/AtomicCards.json.xz')
repairs['Tivit, Seller of Secrets'] = (foreign['Tivit, Seller of Secrets']['Japanese'][0]['text'].replace('\n', '\\n'), 'https://mtgjson.com/api/v5/AtomicCards.json.xz')
expected = {c['gap']['name']: c['translation'][3].replace('\n', '\\n') for c in contexts if c['gap']['language'] == 'ja-JP' and c['translation']}
assert expected['Arena'].count('それらの各クリーチャー') == 1
repairs['Arena'] = (expected['Arena'].replace('それらの各クリーチャー', 'それらをタップする。それらの各クリーチャー'), 'project presentation erratum: Japanese source omitted tapping the two targets; canonical Forge English explicitly taps both, then they fight')
for name, boundary in (("Steelburr Champion", '\\n警戒'), ("Swashbuckler\'s Whip", '\\n装備{1}')):
    old = expected[name]
    assert old.count('（') == old.count('）') + 1
    repairs[name] = (old.replace(boundary, '）' + boundary, 1), 'paired English reminder boundary; Japanese text otherwise unchanged')
report_path = ROOT / 'Tools/ForgeBridge/Reports/ReviewedJapaneseRuleRepairs.json'
evidence = json.loads(report_path.read_text(encoding='utf-8'))['entries'] if report_path.exists() else []
for path in (folder / 'ja-JP.txt', folder / 'ja-JP.supplement.txt', folder / 'ja-JP.notes.txt'):
    if not path.exists():
        continue
    records = rows(path)
    changed = False
    for name, (value, source) in repairs.items():
        if name in records and records[name][3] == expected[name] and records[name][3] != value:
            evidence.append(dict(name=name, file=path.name, previous=records[name][3], replacement=value, source=source))
            records[name][3] = value
            changed = True
    if changed:
        path.write_text('\n'.join('|'.join(row) for row in records.values()) + '\n', encoding='utf-8')
report_path.write_text(
    json.dumps(dict(repaired=len(evidence), entries=evidence), ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print('Reviewed Japanese rule repairs:', len(evidence))
