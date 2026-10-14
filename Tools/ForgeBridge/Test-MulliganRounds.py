"""Exercise both seats through real bridge London mulligan rounds."""
import json, os, queue, shutil, subprocess, sys, tempfile, threading, time
from pathlib import Path

root = Path(__file__).resolve().parents[2]
runtime = root / 'ForgeRuntime'
cfg = json.loads((runtime / 'runtime.json').read_text(encoding='utf-8-sig'))
bridge = runtime / 'classes'
out = root / 'Tools/Validation/Reports/MulliganRounds'
out.mkdir(parents=True, exist_ok=True)
if (out/'result.txt').exists():
    shutil.copytree(out,root/'.tools/mulligan-audit/attempts'/str(time.time_ns()))
(out/'result.txt').write_text('RUNNING',encoding='utf8')
deck = {'cards': [{'setCode': '10E', 'collectorNumber': '376', 'count': 60}], 'sideboard': []}

def run(first, desired):
    session = f'rounds-{first}-{desired[0]}-{desired[1]}'
    expected = [seat for n in range(max(desired)+1) for seat in (first, 3-first) if n <= desired[seat-1] and n < 7]
    decisions = []; counts = [0, 0]; london_hands={}; returned={}
    with tempfile.TemporaryDirectory(prefix='mtgb-mulligan-') as temp, (out / (session+'.log')).open('w', encoding='utf-8') as log, (out/(session+'.jsonl')).open('w',encoding='utf8') as trace:
        start = Path(temp) / 'start.json'
        start.write_text(json.dumps(dict(session=session, seed=4, startingLife=20, firstPlayerSeat=first, decks=[deck,deck])), encoding='utf-8')
        p = subprocess.Popen([str(runtime/cfg['java']), '-Djava.awt.headless=true', '-cp', str(bridge)+';'+str(runtime/'lib/*'), 'mtgb.bridge.ForgeBridge', str(runtime/cfg['assets']), str(start)], cwd=runtime/cfg['assets'], env=dict(os.environ, APPDATA=temp, LOCALAPPDATA=temp), stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=log, text=True, encoding='utf-8')
        messages = queue.Queue()
        threading.Thread(target=lambda: [messages.put(json.loads(line)) for line in p.stdout], daemon=True).start()
        try:
            for _ in range(100):
                m = messages.get(timeout=30)
                trace.write(json.dumps(m)+'\n');trace.flush()
                assert m['type'] not in ('fatal','rejected','ended'), m
                if m['type'] != 'request': continue
                r = m['request']; seat = r['seat']; kind = r['kind']; options = r['options']; pick = 'ok'
                for viewer in (1,2):
                    assert not m['views'][viewer-1]['players'][2-viewer]['hand'],'Opponent mulligan hand exposed'
                print(session, seat, kind, flush=True)
                if kind in ('InputMulligan', 'InputConfirmMulligan'):
                    decisions.append(seat)
                    assert decisions == expected[:len(decisions)], (decisions, expected)
                    pick = 'cancel' if counts[seat-1] < desired[seat-1] else 'ok'
                    counts[seat-1] += 1
                elif kind == 'InputLondonMulligan':
                    hand={c['id'] for c in m['views'][seat-1]['players'][seat-1]['hand']}
                    london_hands.setdefault(seat,hand)
                    returned[seat]={int(o['id'][5:]) for o in options if o['id'].startswith('card:') and o['label'].startswith('[已选')}
                    assert r['selectedCount']==len(returned[seat]) and r['remainingCount']==desired[seat-1]-len(returned[seat]),r
                    snapshot=dict(m['views'][seat-1],request=r,awaitingInput=True)
                    (out/(session+f'-seat-{seat}-selected-{r["selectedCount"]}.json')).write_text(json.dumps(snapshot,ensure_ascii=False,indent=2),encoding='utf8')
                    if not any(o['id']=='ok' for o in options):
                        pick = next(o['id'] for o in options if o['id'].startswith('card:') and not o['label'].startswith('[已选'))
                elif kind == 'InputPassPriority':
                    assert decisions == expected, (decisions, expected)
                    for s in (1,2):
                        player = m['views'][s-1]['players'][s-1]
                        assert len(player['hand']) == 7-desired[s-1], player
                        assert player['libraryCount']==53+desired[s-1],player
                        if s in london_hands:
                            assert {c['id'] for c in player['hand']}==london_hands[s]-returned[s],'Wrong selected cards returned'
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
    results=[]
    for first,desired in [(1,(2,1)),(2,(2,2)),(1,(0,3)),(1,(7,0))]:
        try:result=run(first,desired)
        except Exception as error:result=f'FAIL rounds-{first}-{desired[0]}-{desired[1]}: {error!r}'
        print(result,flush=True);results.append(result)
    (out/'result.txt').write_text('\n'.join(results), encoding='utf-8')
    print('\n'.join(results))
    sys.exit(any(r.startswith('FAIL') for r in results))

