"""Actual cast/payment/resolution regressions for shared player-state and land flows."""
import json
import os
from pathlib import Path
import queue
import re
import subprocess
import sys
import tempfile
import threading
import time

ROOT = Path(__file__).resolve().parents[2]
RUNTIME = ROOT / 'ForgeRuntime'
CFG = json.loads((RUNTIME / 'runtime.json').read_text(encoding='utf-8-sig'))
OUT = ROOT / 'Tools/Validation/Reports/CommonMechanics'
CLASSES = ROOT / '.tools/common-mechanics/classes'
OUT.mkdir(parents=True, exist_ok=True)
CLASSES.mkdir(parents=True, exist_ok=True)
subprocess.run(['javac', '-encoding', 'UTF-8', '-cp', str(RUNTIME / 'classes') + ';' + str(RUNTIME / 'lib/*'),
                '-d', str(CLASSES), str(ROOT / 'Tools/ForgeBridge/tests/CommonMechanicFixture.java')], check=True)
DECK = {'cards': [{'setCode': '10E', 'collectorNumber': '376', 'count': 60}], 'sideboard': []}
CASES = ['story', 'story-unique', 'ascend', 'ascend-source', 'dungeon', 'initiative', 'ring', 'moon-red-blue', 'moon-blue-red']


def run(case):
    with tempfile.TemporaryDirectory(prefix='mtgb-common-') as temp, (OUT / (case + '.log')).open('w', encoding='utf-8') as log, (OUT / (case + '.jsonl')).open('w', encoding='utf-8') as trace:
        start = Path(temp) / 'start.json'
        start.write_text(json.dumps(dict(session=case, fixture=case, seed=4, startingLife=20, firstPlayerSeat=1, decks=[DECK, DECK])), encoding='utf-8')
        process = subprocess.Popen([str(RUNTIME / CFG['java']), '-Djava.awt.headless=true', '-cp', ';'.join(map(str, [CLASSES, RUNTIME / 'classes', RUNTIME / 'lib/*'])),
                                    'mtgb.bridge.CommonMechanicFixture', str(RUNTIME / CFG['assets']), str(start)], cwd=RUNTIME / CFG['assets'],
                                   env=dict(os.environ, APPDATA=temp, LOCALAPPDATA=temp), stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=log, text=True, encoding='utf-8')
        messages = queue.Queue()
        def reader():
            try:
                for line in process.stdout:
                    messages.put(json.loads(line))
            except Exception as error:
                messages.put(error)
            finally:
                messages.put(None)
        threading.Thread(target=reader, daemon=True).start()
        def snapshot(message, label):
            view = dict(message['views'][0])
            view.update(request=message['request'], awaitingInput=True)
            (OUT / (case + '-' + label + '.json')).write_text(json.dumps(view, ensure_ascii=False, indent=2), encoding='utf-8')
        stage = 0
        marker_id = None
        activated_turns = set()
        last_bearer = None
        searched_card = None
        final_draw_hand = None
        deadline = time.monotonic() + 100
        try:
            for step in range(700):
                assert time.monotonic() < deadline, 'Scenario deadline exceeded'
                message = messages.get(timeout=30)
                assert message is not None, 'Bridge exited; inspect scenario log'
                if isinstance(message, Exception): raise message
                trace.write(json.dumps(message) + '\n'); trace.flush()
                assert message['type'] not in ('fatal', 'ended', 'rejected'), message
                if message['type'] != 'request': continue
                request = message['request']; view = message['views'][request['seat'] - 1]
                one, two = message['views'][0]['players']
                field = one['battlefield']; stack = view['stackItems']; options = request['options']
                ids = [option['id'] for option in options]; kind = request['kind']
                pick = 'ok'; casting = None
                def named(name): return next((card for card in field if card['name'] == name), None)
                def markers(name): return [card for card in one.get('effectMarkers', []) if card['name'] == name]
                if kind == 'InputPassPriority' and not stack:
                    if case.startswith(('story', 'ascend')):
                        story = case.startswith('story'); source = 'Óin the Brave' if story else 'Ocelot Pride'
                        marker_name = 'An Enduring Story' if story else "City's Blessing"
                        marker = markers(marker_name)
                        sequence = [source] + (['Ornithopter'] if case in ('story-unique', 'ascend') else []) + ['Murder']
                        if stage == 0:
                            assert not marker, 'Qualification granted without source'
                            snapshot(message, 'before')
                        if stage and named(source):
                            qualified = not (stage == 1 and case in ('story-unique', 'ascend'))
                            assert bool(marker) == qualified, (case, stage, marker)
                            if story:
                                assert named(source)['power'] == (2 if qualified else 1)
                                assert ('HASTE' in named(source)['keywords']) == qualified
                            if qualified:
                                assert len(marker) == 1
                                marker_id = marker[0]['id'] if marker_id is None else marker_id
                                assert marker[0]['id'] == marker_id
                            current = named(source)
                            assert current['qualificationKind'] == ('storied' if story else 'ascend')
                            assert current['qualificationRequired'] == (3 if story else 10)
                            assert current['qualificationEarned'] == qualified
                            assert current['qualificationCount'] == (sum('Legendary' in c['currentType'] or 'Artifact' in c['currentType'] or 'Saga' in c['currentType'] for c in field) if story else len(field))
                            snapshot(message, 'qualified' if qualified else 'below-threshold')
                        if stage == len(sequence) and not named(source):
                            assert len(marker) == 1 and marker[0]['id'] == marker_id, 'Qualification lost with source'
                            assert source in one['graveyard'] and not markers_from(two, marker_name)
                            if case == 'ascend': assert len(field) == 9
                            snapshot(message, 'persistent'); return f'PASS {case}: threshold, source casting/payment, resolution and persistent unique qualification'
                        if request['seat'] == 1 and view['phase'] == 'MAIN1' and stage < len(sequence):
                            casting = sequence[stage]; stage += 1
                    elif case.startswith('moon'):
                        sequence = (['Blood Moon', 'Harbinger of the Seas'] if case == 'moon-red-blue' else ['Harbinger of the Seas', 'Blood Moon']) + ['Murder', 'Disenchant']
                        if stage:
                            color = ('R' if sequence[stage - 1] == 'Blood Moon' else 'U') if stage <= 2 else 'R' if stage == 3 else None
                            for card in [c for c in field + two['battlefield'] if c['land'] and 'Basic' not in c['currentType']]:
                                if color:
                                    assert ('Mountain' if color == 'R' else 'Island') in card['currentType'], (stage, card)
                                    assert '{' + color + '}' in card['currentRules'] and card['landTypesChanged']
                                    assert 'Search your library' not in card['currentRules']
                                else: assert not card['landTypesChanged'], card
                            assert 'Artifact' in named('Darksteel Citadel')['currentType']
                            assert 'Creature' in named('Dryad Arbor')['currentType']
                            assert 'Snow' in named('Scrying Sheets')['currentType']
                            assert 'Legendary' in named("Minamo, School at Water's Edge")['currentType']
                            assert all(not c['landTypesChanged'] for c in field if c['land'] and 'Basic' in c['currentType'])
                            snapshot(message, 'stage-' + str(stage))
                        if stage == 4:
                            assert {'Blood Moon', 'Harbinger of the Seas'} <= set(one['graveyard'])
                            assert 'Search your library' in named('Wooded Foothills')['currentRules']
                            return f'PASS {case}: actual casts, timestamp order, global nonbasic changes, retained supertypes, destruction and restoration'
                        if request['seat'] == 1 and view['phase'] == 'MAIN1': casting = sequence[stage]; stage += 1
                    elif case == 'ring':
                        if stage and len([c for c in field if c['name'] == 'Nazgûl']) == stage and one['ringTemptedCount'] == stage:
                            ring = markers('The Ring')
                            assert len(ring) == 1, ring
                            assert one['ringLevel'] == min(stage, 4)
                            expected_bearer = max(c['id'] for c in field if c['name'] == 'Nazgûl')
                            assert one['ringBearerId'] == expected_bearer
                            if stage > 1: assert last_bearer == expected_bearer
                            for index, card in enumerate(sorted((c for c in field if c['name'] == 'Nazgûl'), key=lambda c: c['id'])):
                                expected_counters = sum(range(index + 1, stage + 1))
                                assert sum(c['count'] for c in card['counters'] if c['kind'] == 'P1P1') == expected_counters
                                assert card['power'] == 1 + expected_counters and card['toughness'] == 2 + expected_counters
                            snapshot(message, 'tempt-' + str(stage))
                            if stage == 5:
                                return 'PASS ring: five real casts, repeated temptation, distinct bearer selections and Wraith counters'
                        if request['seat'] == 1 and view['phase'] == 'MAIN1' and stage < 5: casting = 'Nazgûl'; stage += 1
                    elif case == 'initiative':
                        dungeons = [c for c in one.get('effectMarkers', []) if c['markerKind'] == 'dungeon']
                        if dungeons and named('White Plume Adventurer') and not stack:
                            assert len(dungeons) == 1 and dungeons[0]['name'] == 'Undercity'
                            if searched_card is not None: assert any(c['id'] == searched_card for c in one['hand'])
                            snapshot(message, 'entrance')
                            if any(c['kind'] == 'P1P1' and c['count'] == 2 for c in named('White Plume Adventurer')['counters']):
                                snapshot(message, 'forge'); return 'PASS initiative: actual cast, Undercity entry, basic-land search, upkeep advance and Forge counters'
                        if request['seat'] == 1 and view['phase'] == 'MAIN1' and stage == 0: casting = 'White Plume Adventurer'; stage = 1
                    elif case == 'dungeon':
                        dungeons = [c for c in one.get('effectMarkers', []) if c['markerKind'] == 'dungeon']
                        if stage >= 2:
                            if stage == 3: assert any(c['token'] and c['name'] == 'Treasure Token' for c in field if c.get('token'))
                            if stage >= 4: assert one['life'] == 21 and two['life'] == 19
                            if stage == 5 and one['completedDungeons']:
                                assert not dungeons
                                assert one['completedDungeons'] == ['Lost Mine of Phandelver']
                                assert len(one['hand']) == final_draw_hand + 1, 'Temple did not draw exactly one card'
                                snapshot(message, 'completed'); return 'PASS dungeon: cast Map, four paid activations, legal room choices, Treasure, drain, draw and completion'
                            if dungeons: snapshot(message, 'room-' + str(stage - 1))
                        if request['seat'] == 1 and view['phase'] == 'MAIN1':
                            if stage == 0: casting = 'Dungeon Map'; stage = 1
                            elif named('Dungeon Map') and not named('Dungeon Map')['tapped'] and view['turn'] not in activated_turns:
                                if stage == 4: final_draw_hand = len(one['hand'])
                                activated_turns.add(view['turn']); pick = 'card:' + str(named('Dungeon Map')['id']); stage += 1
                    if casting:
                        card = next(c for c in one['hand'] if c['name'] == casting)
                        pick = 'card:' + str(card['id'])
                elif kind.startswith('InputPayMana'):
                    # Respect Forge's available payment choices instead of guessing the printed cost.
                    cost = request['message'].split('Pay Mana Cost:', 1)[-1]
                    colors = re.findall(r'\{([WUBRG])\}', cost)
                    allowed = colors or list('WUBRGC')
                    pick = next((i for i in ids if i.startswith('mana:') and i.split(':')[1] in allowed), None)
                    if pick is None:
                        basic_names = dict(zip('WUBRG', ['Plains', 'Island', 'Swamp', 'Mountain', 'Forest']))
                        payment = next(c for c in field if not c['tapped'] and c['name'] in [basic_names[x] for x in allowed if x in basic_names] and 'card:' + str(c['id']) in ids)
                        pick = 'card:' + str(payment['id'])
                elif kind == 'InputSelectTargets':
                    target = 'Harbinger of the Seas' if case.startswith('moon') and stage == 3 else 'Blood Moon' if case.startswith('moon') else 'Óin the Brave' if case.startswith('story') else 'Ocelot Pride'
                    pick = 'ok' if request.get('selectedCount') and 'ok' in ids else next(o['id'] for o in options if o.get('card', {}).get('name') == target)
                    snapshot(message, 'targets-' + str(stage))
                elif kind == 'dialog':
                    snapshot(message, 'dialog-' + str(stage))
                    if case == 'dungeon':
                        wanted = 'Venture into the dungeon' if request['message'] == 'getAbilityToPlay' else {2: 'Lost Mine of Phandelver', 3: 'Treasure', 4: 'loses 1 life'}.get(stage)
                        assert wanted is not None, (stage, options)
                        pick = next(o['id'] for o in options if wanted.lower() in o['label'].lower())
                        if request['message'] != 'getAbilityToPlay':
                            if stage == 2:
                                assert request['selectionPurpose'] == 'dungeon' and len(options) == 3
                                assert {o['card']['name'] for o in options} == {'Lost Mine of Phandelver', 'Dungeon of the Mad Mage', 'Tomb of Annihilation'}
                                assert len({o['card']['id'] for o in options}) == 3
                            else:
                                assert request['selectionPurpose'] == 'dungeonRoom' and request['sourceCard']['helperArtName'] == 'Lost Mine of Phandelver'
                    elif case == 'initiative': pick = next((o['id'] for o in options if '+1/+1' in o['label']), ids[0])
                    else: pick = ids[0]
                elif kind.startswith('InputSelect') or kind == 'SelectCards':
                    card_choices = [o for o in options if o.get('card') and not o.get('selected')]
                    if case == 'ring' and 'Ring-bearer' in request['message']:
                        choice = max((o for o in options if o.get('card')), key=lambda o: o['card']['id'])
                        last_bearer = choice['card']['id']
                        pick = 'ok' if choice.get('selected') else choice['id']
                    elif request.get('selectedCount') and 'ok' in ids:
                        pick = 'ok'
                    elif case == 'ring' and card_choices:
                        choice = max(card_choices, key=lambda o: o['card']['id']); pick = choice['id']; last_bearer = choice['card']['id']
                    elif case == 'initiative' and card_choices and not request.get('selectedCount'):
                        pick = card_choices[0]['id']
                        if card_choices[0]['card']['zone'] == 'Library': searched_card = card_choices[0]['card']['id']
                    else: pick = 'ok' if 'ok' in ids else 'confirm-cards' if 'confirm-cards' in ids else card_choices[0]['id']
                    snapshot(message, 'select-' + str(stage))
                elif kind == 'OrderAbilities': pick = 'confirm-order'
                elif kind == 'RevealCards': pick = 'confirm-cards'
                elif kind == 'Scry':
                    snapshot(message, 'scry')
                    pick = 'confirm-scry' if 'confirm-scry' in ids else next(i for i in ids if i.startswith('scry-top:'))
                assert pick in ids, (case, stage, kind, pick, options)
                process.stdin.write(json.dumps(dict(type='respond', session=case, seat=request['seat'], requestId=request['requestId'], option=pick)) + '\n'); process.stdin.flush()
            raise AssertionError('No completion after 700 requests')
        finally:
            if process.poll() is None:
                process.stdin.write('{"type":"shutdown"}\n'); process.stdin.flush()
                try: process.wait(timeout=3)
                except subprocess.TimeoutExpired: process.kill(); process.wait()


def markers_from(player, name):
    return [card for card in player.get('effectMarkers', []) if card['name'] == name]


if __name__ == '__main__':
    results = []
    for case in sys.argv[1:] or CASES:
        try: result = run(case)
        except Exception as error: result = 'FAIL ' + case + ': ' + repr(error)
        results.append(result); print(result, flush=True)
    (OUT / 'result.txt').write_text('\n'.join(results), encoding='utf-8')
    sys.exit(any(result.startswith('FAIL') for result in results))
