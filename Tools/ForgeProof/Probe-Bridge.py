"""Diagnostic wire driver; Unity Play Mode and Player tests remain the acceptance tests."""
import json, pathlib, subprocess, threading, queue, time
root = pathlib.Path(__file__).resolve().parents[2]
work = root / '.tools/forge-proof'
cfg = json.loads((work / 'runtime/bridge-config.json').read_text(encoding='utf-8'))
messages = queue.Queue()
with (work / 'java-stderr.log').open('w', encoding='utf-8') as err:
    p = subprocess.Popen([cfg['java'], '-Djava.awt.headless=true', '-Dfile.encoding=UTF-8', '-Xmx1024m', '-cp', cfg['classpath'], 'mtgb.proof.ForgeProof', cfg['assets']], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=err, text=True, encoding='utf-8')
    def read():
        for line in p.stdout:
            messages.put(line)
        messages.put(None)
    threading.Thread(target=read, daemon=True).start()
    began=time.monotonic()
    land=False; mana=False; cast=False
    try:
        for step in range(150):
            line=messages.get(timeout=90)
            if line is None: raise RuntimeError('Java exited; inspect java-stderr.log')
            m=json.loads(line)
            if m['type']=='fatal': raise RuntimeError(m['error'])
            if m['type']!='request': print(m,flush=True); continue
            r=m['request']; v=m['views'][0]; players=v['players']
            print(r['requestId'],r['seat'],r['kind'],v['phase'],r['message'][:80], [(o['id'],o['label']) for o in r['options']], 'life',players[1]['life'],flush=True)
            if players[1]['life']==17:
                print('FORGE RULES PROBE PASS',time.monotonic()-began,flush=True); break
            opts=r['options']; chosen=None
            if v['fixtureReady'] and r['seat']==1 and r['kind']=='InputPassPriority' and v['phase']=='MAIN1':
                name='Mountain [Hand]' if not land else 'Mountain [Battlefield]' if not mana else 'Lightning Bolt [Hand]' if not cast else None
                chosen=next((o for o in opts if o['label']==name),None)
                if chosen:
                    if not land: land=True
                    elif not mana: mana=True
                    else: cast=True
            if r['kind']=='InputSelectTargets': chosen=next(o for o in opts if o['label']=='Player 2')
            if chosen is None: chosen=next((o for o in opts if o['id']=='ok'),opts[0])
            p.stdin.write(json.dumps(dict(type='respond',session=m['session'],seat=r['seat'],requestId=r['requestId'],option=chosen['id']))+'\n'); p.stdin.flush()
            time.sleep(.15)
        else: raise RuntimeError('Step budget exhausted')
    finally:
        if p.poll() is None:
            try:
                p.stdin.write('{"type":"shutdown"}\n'); p.stdin.flush(); p.wait(5)
            except Exception: p.kill(); p.wait()
