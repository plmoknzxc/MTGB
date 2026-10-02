"""Exercise both seats through real bridge London mulligan rounds."""
import json, os, queue, subprocess, tempfile, threading
from pathlib import Path

root = Path(__file__).resolve().parents[2]
runtime = root / 'ForgeRuntime'
cfg = json.loads((runtime / 'runtime.json').read_text(encoding='utf-8-sig'))
bridge = Path(os.environ.get('MTGB_BRIDGE_CLASSES', str(root / '.tools/selection-tests/bridge')))
out = root / 'Tools/Validation/Reports/MulliganRounds'
out.mkdir(parents=True, exist_ok=True)
deck = {'cards': [{'setCode': '10E', 'collectorNumber': '376', 'count': 60}], 'sideboard': []}

def run(first, desired):
    session = f'rounds-{first}-{desired[0]}-{desired[1]}'
    expected = [seat for n in range(max(desired)+1) for seat in (first, 3-first) if n <= desired[seat-1] and n < 7]
    decisions = []; counts = [0, 0]
    with tempfile.TemporaryDirectory(prefix='mtgb-mulligan-') as temp, (out / (session+'.log')).open('w', encoding='utf-8') as log:
        start = Path(temp) / 'start.json'
        start.write_text(json.dumps(dict(session=session, seed=4, startingLife=20, firstPlayerSeat=first, decks=[deck,deck])), encoding='utf-8')
        p = subprocess.Popen([str(runtime/cfg['java']), '-Djava.awt.headless=true', '-cp', str(bridge)+';'+str(runtime/'lib/*'), 'mtgb.bridge.ForgeBridge', str(runtime/cfg['assets']), str(start)], cwd=runtime/cfg['assets'], env=dict(os.environ, APPDATA=temp, LOCALAPPDATA=temp), stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=log, text=True, encoding='utf-8')
        messages = queue.Queue()
        threading.Thread(target=lambda: [messages.put(json.loads(line)) for line in p.stdout], daemon=True).start()
        try:
            for _ in range(100):
                m = messages.get(timeout=30)
                assert m['type'] not in ('fatal','rejected','ended'), m
                if m['type'] != 'request': continue
                r = m['request']; seat = r['seat']; kind = r['kind']; options = r['options']; pick = 'ok'
                print(session, seat, kind, flush=True)
                if kind in ('InputMulligan', 'InputConfirmMulligan'):
                    decisions.append(seat)
                    assert decisions == expected[:len(decisions)], (decisions, expected)
                    pick = 'cancel' if counts[seat-1] < desired[seat-1] else 'ok'
                    counts[seat-1] += 1
                elif kind == 'InputLondonMulligan':
                    if not any(o['id']=='ok' for o in options):
                        pick = next(o['id'] for o in options if o['id'].startswith('card:') and not o['label'].startswith('[已选'))
                elif kind == 'InputPassPriority':
                    assert decisions == expected, (decisions, expected)
                    for s in (1,2):
                        player = m['views'][s-1]['players'][s-1]
                        assert len(player['hand']) == 7-desired[s-1], player
                    return f'PASS {session}: {decisions}'
                else: raise AssertionError(kind)
                p.stdin.write(json.dumps(dict(type='respond',session=session,seat=seat,requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
            raise AssertionError('Mulligan did not finish')
        finally:
            if p.poll() is None:
                p.stdin.write('{"type":"shutdown"}\n');p.stdin.flush()
                try: p.wait(timeout=3)
                except subprocess.TimeoutExpired: p.kill();p.wait()

if __name__ == '__main__':
    results = [run(first, desired) for first, desired in [(1,(2,1)),(2,(2,2)),(1,(0,3)),(1,(7,0))]]
    (out/'result.txt').write_text('\n'.join(results), encoding='utf-8')
    print('\n'.join(results))

