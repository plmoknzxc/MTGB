"""Apply the reviewed Soldier-token erratum, keeping exact previous-text evidence."""
import json
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
folder = ROOT / 'Assets/StreamingAssets/CardTranslations'
contexts = json.loads((ROOT / '.tools/presentation-audit/review-contexts.json').read_text(encoding='utf-8'))
previous = next(x['translation'][3] for x in contexts if x['gap']['name'] == 'Skyknight Vanguard' and x['gap']['language'] == 'zh-CN')
assert previous.count('白色战士衍生生物') == 1
replacement = previous.replace('白色战士衍生生物', '白色士兵衍生生物')
manifest_path = folder / 'zh-CN.corrections.json'
manifest = json.loads(manifest_path.read_text(encoding='utf-8')) if manifest_path.exists() else dict(entries=[])
if not any(x['name'] == 'Skyknight Vanguard' for x in manifest['entries']):
    manifest['entries'].append(dict(name='Skyknight Vanguard', expectedRules=previous))
manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
review_path = folder / 'zh-CN.review.txt'
records = {r.split('|',1)[0]: r for r in review_path.read_text(encoding='utf-8').splitlines()} if review_path.exists() else {}
records['Skyknight Vanguard'] = 'Skyknight Vanguard|||' + replacement.replace('\n', '\\n')
item_previous = next(x['translation'][3] for x in contexts if x['gap']['name'] == 'Item Crate' and x['gap']['language'] == 'zh-CN')
assert item_previous.count('不灭与辟邪') == 1
item_replacement = item_previous.replace('不灭与辟邪', '不灭与死触')
records['Item Crate'] = 'Item Crate|||' + item_replacement.replace('\n', '\\n')
if not any(x['name'] == 'Item Crate' for x in manifest['entries']):
    manifest['entries'].append(dict(name='Item Crate', expectedRules=item_previous))
manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
review_path.write_text('\n'.join(records.values()) + '\n', encoding='utf-8')
(ROOT / 'Tools/ForgeBridge/Reports/ReviewedItemCrateRuleRepair.json').write_text(json.dumps(dict(
    name='Item Crate', previous=item_previous, replacement=item_replacement,
    ruleEvidence='Forge canonical English Star-token clause grants deathtouch and indestructible; Chinese incorrectly said hexproof.'),
    ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
(ROOT / 'Tools/ForgeBridge/Reports/ReviewedChineseRuleRepairs.json').write_text(json.dumps(dict(
    name='Skyknight Vanguard', key='M20/218', previous=previous, replacement=replacement,
    kind='project presentation erratum of the printed Chinese creature type',
    printedImage='CardImages/zh-CN/M20/218.jpg',
    oracle='https://scryfall.com/card/m20/218/skyknight-vanguard',
    ruleEvidence='Current English creates Soldier; the Chinese printed image says Warrior. Engine rules unchanged.'),
    ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print('Reviewed Chinese Soldier-token presentation erratum applied')
