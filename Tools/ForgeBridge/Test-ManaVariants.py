"""Real snow/hybrid/two-brid payment. Unique attempts; no fixture-produced outcomes."""
import hashlib
import json
import os
from pathlib import Path
import queue
import subprocess
import sys
import tempfile
import threading
import time

ROOT = Path(__file__).resolve().parents[2]
RT = ROOT / 'ForgeRuntime'
OUT = ROOT / '.tools/mana-variants-1006/native'
OUT.mkdir(parents=True, exist_ok=True)
CFG = json.loads((RT / 'runtime.json').read_text(encoding='utf-8-sig'))
CLASSES = OUT / 'classes'
CLASSES.mkdir(exist_ok=True)
subprocess.run(['javac', '-encoding', 'UTF-8', '-cp', str(RT/'classes')+';'+str(RT/'lib/*'),
                '-d', str(CLASSES), str(ROOT/'Tools/ForgeBridge/tests/ManaVariantFixture.java')], check=True)
DECK = {'cards': [{'setCode': '10E', 'collectorNumber': '376', 'count': 60}], 'sideboard': []}
CASES = ['snow-paid', 'snow-insufficient', 'hybrid-green', 'hybrid-red', 'hybrid-mixed',
         'hybrid-insufficient', 'hybrid-retry', 'twobrid-white', 'twobrid-generic',
         'twobrid-mixed', 'twobrid-insufficient']
CASES += ['phyrexian-mana','phyrexian-life','phyrexian-mixed','phyrexian-insufficient',
          'phyrexian-prohibited','phyrexian-prohibited-mana','phyrexian-cancel','phyrexian-black']

def run(case):
    folder = OUT / case / str(time.time_ns())
    folder.mkdir(parents=True)
    seat = 2 if case in {'hybrid-red', 'twobrid-generic','phyrexian-mixed'} else 1
    phyrexian=case.startswith('phyrexian')
    life=5 if case in {'phyrexian-life','phyrexian-cancel','phyrexian-black'} else 3 if case in {'phyrexian-mixed','phyrexian-insufficient'} else 20
    name = 'Grave Titan' if case=='phyrexian-black' else 'Moltensteel Dragon' if phyrexian else "Arcum's Astrolabe" if case.startswith('snow') else 'Burning-Tree Emissary' if case.startswith('hybrid') else 'Spectral Procession'
    with tempfile.TemporaryDirectory(dir=OUT, prefix='temp-') as temp, (folder/'engine.log').open('w', encoding='utf8') as log, (folder/'trace.jsonl').open('w', encoding='utf8') as trace:
        config = dict(session=case, fixture=case, seed=4, firstPlayerSeat=seat, startingLife=life, decks=[DECK, DECK])
        start = Path(temp)/'start.json';start.write_text(json.dumps(config))
        process = subprocess.Popen([str(RT/CFG['java']), '-Djava.awt.headless=true', '-Djava.io.tmpdir='+temp,
            '-cp', ';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])), 'mtgb.bridge.ManaVariantFixture',
            str(RT/CFG['assets']), str(start)], cwd=RT/CFG['assets'],
            env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),
            stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
        messages=queue.Queue()
        def read():
            try:
                for line in process.stdout: messages.put(json.loads(line))
            finally: messages.put(None)
        threading.Thread(target=read,daemon=True).start()
        def receive():
            m=messages.get(timeout=35);assert m is not None,'Bridge exited'
            trace.write(json.dumps(m)+'\n');trace.flush()
            assert m['type'] not in {'fatal','ended'},m
            return m
        def send(request, option, actor=None):
            process.stdin.write(json.dumps(dict(type='respond',session=case,seat=actor or request['seat'],
                requestId=request['requestId'],option=option))+'\n');process.stdin.flush()
        def snap(m,label):
            v=dict(m['views'][seat-1]);v.update(request=m['request'],awaitingInput=True)
            (folder/(label+'.json')).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf8')
        cast=False;seen_stack=False;floated=False;cancelled=False;wrong=False;pending=None;before=None;guarded=False;spell_id=None;life_reserved=0
        try:
            for step in range(160):
                m=receive();assert m['type']!='rejected',m
                if m['type']!='request':continue
                r=m['request'];v=m['views'][seat-1];player=v['players'][seat-1];ids=[o['id'] for o in r['options']];kind=r['kind'];pick='ok'
                if pending is not None:
                    assert r['kind']==pending['request']['kind'] and r['contextId']==pending['request']['contextId']
                    assert r['requestId']!=pending['request']['requestId']
                    assert player==pending['views'][seat-1]['players'][seat-1], 'Unpayable pool resource consumed'
                    assert r['message']==pending['request']['message'] and ids==[o['id'] for o in pending['request']['options']]
                    snap(m,'wrong-pool-retry');pending=None
                entries=[s for s in v['stackItems'] if s['kind']=='spell' and s['source']['name']==name]
                if entries:seen_stack=True;snap(m,'stack')
                if kind=='InputPassPriority' and r['seat']==seat and v['phase']=='MAIN1' and not v['stackItems']:
                    card=next((c for c in player['hand'] if c['name']==name),None)
                    if not floated and case in {'snow-paid','snow-insufficient','hybrid-retry'}:
                        land=next(c for c in player['battlefield'] if c['name']==('Island' if case=='hybrid-retry' else 'Forest') and not c['tapped'])
                        pick='card:'+str(land['id']);floated=True
                    elif case.endswith('insufficient') or case=='phyrexian-prohibited':
                        assert card and 'card:'+str(card['id']) not in ids, 'Unpayable spell offered'
                        assert not cast and not seen_stack
                        snap(m,'insufficient');return dict(case=case,status='PASS',spell=name,seat=seat,insufficientNotOffered=True,folder=str(folder.relative_to(ROOT)))
                    elif seen_stack and not card:
                        if phyrexian:
                            dragon=next(c for c in player['battlefield'] if c['name']==name)
                            stats=6 if case=='phyrexian-black' else 5 if 'prohibited' in case else 4
                            assert dragon['power']==stats and dragon['toughness']==stats
                            if case=='phyrexian-black':
                                assert sum(c.get('token',False) and 'Zombie' in c['currentType'] for c in player['battlefield'])==2
                                krrik=next(c for c in player['battlefield'] if c['name']=="K'rrik, Son of Yawgmoth")
                                assert any(c['kind']=='P1P1' and c['count']==1 for c in krrik['counters'])
                            expected=6 if case in {'phyrexian-mana','phyrexian-prohibited-mana'} else 5 if case=='phyrexian-mixed' else 4
                            assert player['life']==life-(0 if expected==6 else 2 if expected==5 else 4)
                            assert sum(player['manaPool'])==0
                        elif case.startswith('snow'):
                            assert any(c['name']==name for c in player['battlefield'])
                            assert next(c for c in player['battlefield'] if c['name']=='Snow-Covered Forest')['tapped']
                            assert player['manaPool'][4]==1 and wrong and cancelled
                            assert len(player['hand'])==len(before['hand']), 'Astrolabe entry draw missing'
                            expected=2 # one ordinary floated resource plus one snow payment
                        elif case.startswith('hybrid'):
                            creature=next(c for c in player['battlefield'] if c['name']==name)
                            assert creature['power']==2 and creature['toughness']==2
                            assert player['manaPool'][3]==1 and player['manaPool'][4]==1, 'Emissary entry mana missing'
                            assert player['manaPool'][1]==(1 if case=='hybrid-retry' else 0)
                            expected=3 if case=='hybrid-retry' else 2
                        else:
                            tokens=[c for c in player['battlefield'] if c.get('token') and 'Spirit' in c['currentType']]
                            assert len(tokens)==3 and all(c['power']==1 and c['toughness']==1 for c in tokens)
                            assert sum(player['manaPool'])==0
                            expected=3 if case=='twobrid-white' else 6 if case=='twobrid-generic' else 5
                        assert sum(c['tapped'] for c in player['battlefield'] if c['land'])==expected
                        if not phyrexian:assert player['life']==20
                        snap(m,'resolved');return dict(case=case,status='PASS',spell=name,seat=seat,tappedSources=expected,
                            cancelled=cancelled,wrongPoolRetry=wrong,wrongSeatGuard=guarded,lifeBefore=life,lifeAfter=player['life'],folder=str(folder.relative_to(ROOT)))
                    else:
                        if cancelled and cast is False:
                            assert player==before, 'Cancel changed cards, life or pool'
                        assert card and 'card:'+str(card['id']) in ids
                        before=json.loads(json.dumps(player));spell_id=card['id'];pick='card:'+str(spell_id);cast=True;snap(m,'before-cast')
                elif 'PayMana' in kind:
                    snap(m,'payment')
                    if not guarded:
                        send(r,ids[0],3-seat);rejection=receive();assert rejection['type']=='rejected' and rejection['error']=='wrong-seat';guarded=True
                    if phyrexian:
                        assert r.get("remainingManaCost") and r.get("reservedLifePayment")==life_reserved*2,(case,r)
                        reserve=0 if case in {'phyrexian-mana','phyrexian-prohibited-mana'} else 1 if case=='phyrexian-mixed' else 2
                        if case=='phyrexian-cancel' and life_reserved==1 and not cancelled:
                            assert player['life']==life,'Reserved life spent before completion';pick='cancel';cancelled=True;cast=False;seen_stack=False;life_reserved=0;snap(m,'cancel-payment')
                        elif life_reserved<reserve:
                            pick='player:'+str(player['targetId'])
                            assert pick in ids,('Native Phyrexian life option missing',case,r)
                            snap(m,'life-payment-'+str(life_reserved));life_reserved+=1
                        else:
                            lands=[c for c in player['battlefield'] if c['land'] and not c['tapped'] and 'card:'+str(c['id']) in ids]
                            assert lands,(case,r);pick='card:'+str(lands[0]['id'])
                    elif case in {'snow-paid','hybrid-retry'} and not wrong:
                        pick='mana:'+('G' if case=='snow-paid' else 'U');assert pick in ids
                        pending=json.loads(json.dumps(m));wrong=True;snap(m,'wrong-pool-before')
                    elif case in {'snow-paid','hybrid-retry'} and not cancelled:
                        assert 'cancel' in ids;pick='cancel';cancelled=True;cast=False;seen_stack=False;snap(m,'cancel-payment')
                    elif 'ok' in ids and case in {'hybrid-red','twobrid-white','twobrid-generic'}:pick='ok'
                    else:
                        lands=[c for c in player['battlefield'] if c['land'] and not c['tapped'] and 'card:'+str(c['id']) in ids]
                        if case=='snow-paid':lands=[c for c in lands if c['name']=='Snow-Covered Forest']
                        assert lands,(case,r,player);pick='card:'+str(lands[0]['id'])
                elif kind=='Option':pick=next(i for i in ids if i not in {'cancel','no'})
                elif kind=='InputMulligan':pick='keep'
                assert pick in ids,(case,kind,pick,ids)
                print(case,step,kind,pick,flush=True);send(r,pick)
            raise AssertionError('Step limit')
        finally:
            if process.poll() is None:process.kill();process.wait()

for case in sys.argv[1:] or CASES:
    assert case in CASES,case
    try:
        result=run(case);result['runtimeHash']=CFG['runtimeHash']
        result['sourceHash']=hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
        (OUT/(case+'-result.json')).write_text(json.dumps(result,indent=2),encoding='utf8')
        print('PASS '+case+': '+json.dumps(result),flush=True)
    except Exception:
        print('FAIL '+case,flush=True);raise
