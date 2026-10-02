"""Real Forge: unaffordable Shock is not offered; paid Shock and priority sequencing."""
import json
import os
from pathlib import Path
import queue
import subprocess
import tempfile
import threading
import time

root = Path(__file__).resolve().parents[2]
runtime = root / 'ForgeRuntime'
config = json.loads((runtime / 'runtime.json').read_text(encoding='utf-8-sig'))
out = root / 'Tools/Validation/Reports/CRInteraction'
out.mkdir(parents=True, exist_ok=True)
(out / 'shock-result.txt').write_text('RUNNING', encoding='utf-8')
deck = {'cards': [{'setCode': '10E', 'collectorNumber': n, 'count': 30}
                  for n in ('376', '232')], 'sideboard': []}
messages = queue.Queue()
with tempfile.TemporaryDirectory(prefix='mtgb-shock-') as temp:
    start = Path(temp) / 'start.json'
    start.write_text(json.dumps({'session': 'shock-regression', 'seed': 4, 'startingLife': 20,
                                'firstPlayerSeat': 1, 'decks': [deck, deck]}), encoding='utf-8')
    with (out / 'shock-engine.log').open('w', encoding='utf-8') as errors, (out / 'shock-trace.jsonl').open('w', encoding='utf-8') as trace:
        process = subprocess.Popen([str(runtime / config['java']), '-Djava.awt.headless=true', '-cp',
                                    str(runtime / 'classes') + ';' + str(runtime / 'lib/*'),
                                    'mtgb.bridge.ForgeBridge', str(runtime / config['assets']), str(start)],
                                   cwd=runtime / config['assets'], env=dict(os.environ, APPDATA=temp, LOCALAPPDATA=temp),
                                   stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=errors, text=True, encoding='utf-8')
        threading.Thread(target=lambda: [messages.put(json.loads(line)) for line in process.stdout], daemon=True).start()
        stage = 'unpaid'
        attempted = False
        original_ids = None
        passes = []
        seen = set()
        deadline = time.monotonic() + 90
        try:
            while time.monotonic() < deadline:
                msg = messages.get(timeout=15)
                if msg['type'] == 'rejected' and msg.get('error') == 'stale-request':
                    continue
                assert msg['type'] not in ('fatal', 'rejected', 'ended'), msg
                if msg['type'] != 'request':
                    continue
                req = msg['request']
                if req['requestId'] in seen:
                    continue
                seen.add(req['requestId'])
                view = msg['views'][req['seat'] - 1]
                trace.write(json.dumps({'view': view, 'request': req}, ensure_ascii=False) + '\n'); trace.flush()
                own = view['players'][0]
                options = req['options']
                assert options, ('Empty request', req)
                kind = req['kind']
                pick = next((o for o in options if o['id'] == 'ok'), options[0])
                if view['players'][1]['life'] == 18:
                    assert stage == 'paid' and passes == [1], passes
                    assert '[auto-pass] seat=2' in (out / 'shock-engine.log').read_text(encoding='utf-8')
                    assert any('Shock' in c for c in own['graveyard'])
                    result = 'PASS: zero-land Shock not offered; target/payment remain manual; caster passes, opponent with no actions auto-passes; real resolution 20 -> 18.'
                    (out / 'shock-result.txt').write_text(result, encoding='utf-8'); print(result); break
                assert all(p['life'] == 20 for p in view['players']), 'Unexpected life change'
                if kind == 'InputPassPriority' and view['stackCount']:
                    assert stage == 'paid'
                    passes.append(req['seat'])
                if req['seat'] == 1:
                    if kind == 'InputPassPriority' and view['phase'] == 'MAIN1' and not view['stackCount']:
                        if stage == 'unpaid':
                            assert not own['battlefield'] and own['redMana'] == 0
                            assert any(c['name'] == 'Shock' for c in own['hand'])
                            assert not any(o['label'] == 'Shock [Hand]' for o in options)
                            pick = next(o for o in options if o['label'] == 'Mountain [Hand]')
                            stage = 'paid'
                        else:
                            pick = next(o for o in options if o['label'] == 'Shock [Hand]')
                            attempted = True
                    elif kind == 'InputSelectTargets':
                        assert attempted
                        pick = next(o for o in options if o['id'] == 'player:' + str(view['players'][1]['targetId']))
                    elif kind.startswith('InputPayMana'):
                        available = {c['id'] for c in own['battlefield'] if not c['tapped']}
                        pick = next((o for o in options if o['id'] == 'mana:R' or o['id'] in {'card:' + str(i) for i in available}), pick)
                print(stage, req['seat'], kind, pick['label'], flush=True)
                process.stdin.write(json.dumps({'type': 'respond', 'session': 'shock-regression', 'seat': req['seat'],
                                               'requestId': req['requestId'], 'option': pick['id']}) + '\n')
                process.stdin.flush()
            else:
                raise RuntimeError('Shock interaction timed out')
        except Exception as error:
            (out / 'shock-result.txt').write_text('FAIL: ' + repr(error), encoding='utf-8')
            raise
        finally:
            if process.poll() is None:
                process.stdin.write('{"type":"shutdown"}\n'); process.stdin.flush()
                try:
                    process.wait(timeout=3)
                except subprocess.TimeoutExpired:
                    process.kill(); process.wait()
