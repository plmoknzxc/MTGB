"""Recheck saved native outcomes without rerunning unrelated rule scenarios."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
HERE = ROOT / '.tools/payment-dock-1005'
NATIVE = HERE / 'native'

def read(name):
    return json.loads((NATIVE / (name + '.json')).read_text(encoding='utf-8-sig'))

results = []
for case in ('shock-yes', 'shock-no', 'tax-auto', 'tax-cancel', 'tax-insufficient', 'tax-manual'):
    before, after = read(case + '-before'), read(case + '-resolved')
    player = after['players'][0]
    if case.startswith('shock'):
        source = next(c for c in before['players'][0]['hand'] if c['name'] == 'Hallowed Fountain')
        card = next(c for c in player['battlefield'] if c['id'] == source['id'])
        assert player['life'] == (18 if case == 'shock-yes' else 20)
        assert card['tapped'] == (case == 'shock-no')
        result = dict(case=case, life=player['life'], tapped=card['tapped'])
    else:
        request = read(case + '-payment')['request']
        ids = [o['id'] for o in request['options']]
        assert 'cancel' in ids
        assert ('ok' in ids) == (case != 'tax-insufficient')
        paid = case in ('tax-auto', 'tax-manual')
        source = next(c for c in before['players'][0]['hand'] if c['name'] == 'Opt')
        assert any(c['id'] == source['id'] for c in player['graveyardCards'])
        assert player['libraryCount'] == before['players'][0]['libraryCount'] - int(paid)
        tapped = sum(c['tapped'] for c in player['battlefield'] if c['name'] == 'Island')
        assert tapped == (4 if paid else 1)
        assert player['life'] == 20 and after['players'][1]['life'] == 20
        result = dict(case=case, paid=paid, tappedIslands=tapped, libraryCount=player['libraryCount'])
    results.append(dict(result, PASS=True, evidence=case + '-resolved.json'))

files = list(NATIVE.glob('*-before.json')) + list(NATIVE.glob('*-resolved.json')) + list(NATIVE.glob('*-payment.json'))
files += [HERE/'ui-result.txt', ROOT/'.tools/mana-glyph-review-1004/result.txt']
files += [ROOT/'Assets/Scripts/UI'/name for name in (
    'ForgeBattlefieldController.Payment.cs', 'ForgeBattlefieldController.ControlMode.cs',
    'ForgeBattlefieldController.Integrated.cs', 'ForgeBattlefieldController.Targeting.cs',
    'ForgeActionButtonGraphic.cs')]
files += [ROOT/'Assets/Scripts/Editor/PaymentDockPresentationValidation.cs', ROOT/'Tools/ForgeBridge/PaymentDockNativeValidation.py', ROOT/'Tools/ForgeBridge/PaymentReviewFixture.java', HERE/'manual-land-ui.txt']
assert all(p.exists() for p in files)
assert (HERE/'ui-result.txt').read_text().startswith('PASS:')
assert (ROOT/'.tools/mana-glyph-review-1004/result.txt').read_text().startswith('PASS:')
proof = dict(schema=1, scope='Six actual Forge outcomes; three-language request replay, raycast and automatic-control invocation; affected pool replay. No live-network/all-card claim.',
             native=results, hashes={p.relative_to(ROOT).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in files})
(HERE/'proof.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2), encoding='utf-8')
print('PASS: six saved native outcomes, focused UI and affected pool evidence; proof.json written.')
