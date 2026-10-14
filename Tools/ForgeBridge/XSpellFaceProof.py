"""Check the bounded X-face review; engine results and Unity replay stay separate."""
import hashlib
import json
import struct
from pathlib import Path

root = Path(__file__).resolve().parents[2]
out = root / '.tools/x-stack-unified-1005'

def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def main():
    assert (out / 'result.txt').read_text(encoding='utf-8').startswith('PASS:'), 'Unity review incomplete'
    cases = []
    for value, folder in [(0, 'native-zero'), (2, 'native-two'), (7, 'native-seven-fixed')]:
        base = out / folder
        assert (base / 'result-x.txt').read_text(encoding='utf-8').startswith('PASS XX:')
        stack = read(base / 'x-stack.json')['stackItems'][0]
        assert stack['source']['name'] == 'Walking Ballista'
        assert stack['hasXValue'] and stack['xValue'] == value
        assert stack['source']['manaCost'] == ('{' + str(value) + '}') * 2
        player = read(base / 'x-resolved.json')['players'][0]
        assert sum(c['tapped'] for c in player['battlefield'] if c['land']) == value * 2
        if value:
            card = next(c for c in player['battlefield'] if c['name'] == 'Walking Ballista')
            assert card['power'] == card['toughness'] == value
            assert any(c['kind'] == 'P1P1' and c['count'] == value for c in card['counters'])
        else:
            assert 'Walking Ballista' in player['graveyard']
        cases.append({'x': value, 'cost': stack['source']['manaCost'], 'paid': value * 2,
                      'traceSha256': sha(base / 'x.jsonl'), 'stackSha256': sha(base / 'x-stack.json'),
                      'resolvedSha256': sha(base / 'x-resolved.json')})
    screenshots = {}
    for path in out.glob('*.png'):
        data = path.read_bytes()
        assert data[:8] == b'\x89PNG\r\n\x1a\n'
        width, height = struct.unpack('>II', data[16:24])
        assert width > 1000 and height > 500
        screenshots[path.name] = {'sha256': sha(path), 'width': width, 'height': height}
    sources = ['Assets/Scripts/UI/ForgeBattlefieldController.Prepared.cs',
               'Assets/Scripts/UI/ForgeBattlefieldController.ChosenSpell.cs',
               'Assets/Scripts/UI/ForgeBattlefieldController.Integrated.cs',
               'Assets/Scripts/UI/ForgeBattlefieldController.StackList.cs',
               'Assets/Scripts/Editor/XSpellPresentationValidation.cs',
               'Tools/ForgeBridge/Test-AdvancedInteractions.py',
               'Tools/ForgeBridge/tests/AdvancedInteractionFixture.java']
    proof = {'engine': cases, 'unity': (out / 'result.txt').read_text(encoding='utf-8'),
             'screenshots': screenshots, 'sources': {p: sha(root / p) for p in sources},
             'runtimeManifestSha256': sha(root / 'ForgeRuntime/runtime.json'),
             'bridgeClassSha256': sha(root / 'ForgeRuntime/classes/mtgb/bridge/ForgeBridge.class'),
             'replayInputs': {p: sha(root / p) for p in [
                 '.tools/x-stack-comparison-1005/omen-x4.json',
                 '.tools/x-stack-comparison-1005/wrath-x3.json',
                 '.tools/x-stack-unified-1005/cast-adventure-stack.json',
                 '.tools/x-stack-unified-1005/prepare-stack.json']},
             'limitations': ['Omen/Wrath/Adventure/Prepare use historical native snapshots.',
                             'Crowded list expansion is a layout adapter.',
                             'No full card pool or live network verification.']}
    (out / 'proof.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2), encoding='utf-8')
    print('PASS: XX zero/two/seven native assertions and bounded Unity evidence')

if __name__ == '__main__':
    main()
