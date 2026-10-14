"""Native restricted spending, reflection without inherited restrictions, and replacement."""
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
OUT = ROOT / '.tools/restricted-mana-1006/native'
OUT.mkdir(parents=True, exist_ok=True)
CFG = json.loads((RT / 'runtime.json').read_text(encoding='utf-8-sig'))
FIXTURE = ROOT / 'Tools/ForgeBridge/tests/RestrictedManaFixture.java'
CLASSES = OUT / 'classes'
CLASSES.mkdir(exist_ok=True)
SHA = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
subprocess.run(['javac', '-encoding', 'UTF-8', '-cp', str(RT/'classes')+';'+str(RT/'lib/*'),
                '-d', str(CLASSES), str(FIXTURE)], check=True)
DECK = {'cards': [{'setCode': '10E', 'collectorNumber': '376', 'count': 60}], 'sideboard': []}
CASES = {
    'cavern-chosen': dict(hand=['Cavern of Souls','Bloodlust Inciter','Goblin Arsonist','Shock'],
                          lands=[], source='Cavern of Souls', spell='Bloodlust Inciter', color=3,
                          legal=['Bloodlust Inciter'], forbidden=['Goblin Arsonist','Shock']),
    'ziggurat-creature': dict(hand=['Bloodlust Inciter','Goblin Arsonist','Shock'],
                             lands=['Ancient Ziggurat'], source='Ancient Ziggurat', spell='Bloodlust Inciter', color=3,
                             legal=['Bloodlust Inciter','Goblin Arsonist'], forbidden=['Shock']),
    'reflect-restriction': dict(hand=['Shock','Bloodlust Inciter'],
                               lands=['Reflecting Pool','Ancient Ziggurat'], source='Reflecting Pool', spell='Shock', color=3,
                               legal=['Shock','Bloodlust Inciter'], forbidden=[]),
    'reflect-empty': dict(hand=['Llanowar Elves'], lands=['Reflecting Pool'], source='Reflecting Pool', spell=None,
                         legal=[], forbidden=['Llanowar Elves']),
    'replacement-black': dict(hand=['Dark Ritual','Llanowar Elves'], lands=['Forest'], source='Forest', spell='Dark Ritual', color=2,
                             legal=['Dark Ritual'], forbidden=['Llanowar Elves']),
}
CASES['cavern-counter'] = dict(CASES['cavern-chosen'], counter=True)
CASES['reflect-counter'] = dict(CASES['reflect-restriction'], spell='Bloodlust Inciter', counter=True)

def run(case):
    spec = CASES[case]
    folder = OUT / case / str(time.time_ns())
    folder.mkdir(parents=True)
    identity = dict(case=case, folder=folder.relative_to(ROOT).as_posix(), runtimeHash=CFG['runtimeHash'],
                    driverSha256=SHA(Path(__file__)), fixtures=[dict(path=FIXTURE.relative_to(ROOT).as_posix(), sha256=SHA(FIXTURE))])
    def finish(status, detail):
        result = dict(identity, status=status, result=status+' '+case+': '+detail)
        for p in [folder/'result.json', OUT/(case+'-result.json')]:
            p.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
        return result
    with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp, (folder/'engine.log').open('w',encoding='utf8') as log, (folder/'trace.jsonl').open('w',encoding='utf8') as trace:
        start = Path(temp)/'start.json'
        start.write_text(json.dumps(dict(session=case,fixture=case,hand=spec['hand'],lands=spec['lands'],counter=spec.get('counter',False),seed=4,
                                        firstPlayerSeat=1,startingLife=20,decks=[DECK,DECK])),encoding='utf8')
        process = subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true',
            '-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),
            'mtgb.bridge.RestrictedManaFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],
            env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),
            stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
        messages = queue.Queue()
        def reader():
            try:
                for line in process.stdout: messages.put(json.loads(line))
            finally: messages.put(None)
        threading.Thread(target=reader,daemon=True).start()
        def snap(m,label):
            for seat,view in enumerate(m['views'],1):
                (folder/(label+'-seat'+str(seat)+'.json')).write_text(json.dumps(dict(view,request=m['request'],awaitingInput=True),ensure_ascii=False,indent=2),encoding='utf8')
        floated = False
        cast = False
        seen_stack = False
        played = False
        counter_cast = False
        try:
            for step in range(100):
                m = messages.get(timeout=35)
                assert m is not None, 'Bridge exited'
                trace.write(json.dumps(m,ensure_ascii=False)+'\n');trace.flush()
                assert m['type'] not in {'fatal','rejected','ended'}, m
                if m['type'] != 'request': continue
                r=m['request'];v=m['views'][0];one=v['players'][0];opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];pick='ok'
                if any(s['kind']=='spell' and s['source']['name']==spec['spell'] for s in v['stackItems']):
                    seen_stack=True;snap(m,'stack')
                if kind=='InputPassPriority' and r['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
                    if spec['source']=='Cavern of Souls' and not played:
                        land=next(c for c in one['hand'] if c['name']==spec['source']);pick='card:'+str(land['id']);played=True
                    elif not floated and spec['spell']:
                        source=next(c for c in one['battlefield'] if c['name']==spec['source']);pick='card:'+str(source['id']);floated=True
                    elif not cast:
                        for name in spec['legal']+spec['forbidden']:
                            card=next(c for c in one['hand'] if c['name']==name)
                            assert ('card:'+str(card['id']) in ids)==(name in spec['legal']), ('Native spend restriction',name,r)
                        if not spec['spell']:
                            assert sum(one['manaPool'])==0 and not one['battlefield'][0]['tapped'];snap(m,'empty-reflection')
                            return finish('PASS','No producible color: creature not offered; land and hand retained; next native priority.')
                        assert one['manaPool'][spec['color']]==1 and sum(one['manaPool'])==1
                        snap(m,'floated-legality');card=next(c for c in one['hand'] if c['name']==spec['spell']);pick='card:'+str(card['id']);cast=True
                    elif seen_stack:
                        assert spec['spell'] not in [c['name'] for c in one['hand']]
                        assert sum(c['land'] and c['tapped'] for c in one['battlefield'])==1
                        if spec.get('counter'):
                            other=v['players'][1]
                            assert counter_cast and 'Counterspell' in other['graveyard'] and sum(other['manaPool'])==0
                            assert sum(c['name']=='Island' and c['tapped'] for c in other['battlefield'])==2
                            creature=[c for c in one['battlefield'] if c['name']=='Bloodlust Inciter']
                            assert bool(creature)==(case=='cavern-counter'), 'Counterability modifier inherited/lost'
                            assert ('Bloodlust Inciter' in one['graveyard'])==(case=='reflect-counter')
                            assert sum(one['manaPool'])==0 and other['life']==20
                        elif spec['spell']=='Bloodlust Inciter':
                            assert sum(one['manaPool'])==0 and any(c['name']==spec['spell'] and c['power']==1 and c['toughness']==1 for c in one['battlefield'])
                        elif spec['spell']=='Shock':
                            assert v['players'][1]['life']==18 and sum(one['manaPool'])==0 and 'Shock' in one['graveyard']
                            assert not next(c for c in one['battlefield'] if c['name']=='Ancient Ziggurat')['tapped']
                        else:
                            assert one['manaPool'][2]==3 and sum(one['manaPool'])==3 and 'Dark Ritual' in one['graveyard']
                        assert one['life']==20;snap(m,'resolved')
                        return finish('PASS','Real land production, native legal/forbidden spells, exact resource and resolved effect; priority restored.')
                elif kind=='InputPassPriority' and r['seat']==2 and spec.get('counter') and cast and not counter_cast and v['stackItems']:
                    counter=next(c for c in m['views'][1]['players'][1]['hand'] if c['name']=='Counterspell')
                    pick='card:'+str(counter['id']);counter_cast=True;snap(m,'before-counter')
                elif kind in {'dialog','ChooseType','Option'}:
                    snap(m,'choice-'+str(step))
                    if any(o['label']=='Human' for o in opts):pick=next(o['id'] for o in opts if o['label']=='Human')
                    elif any(o['label']=='RED' for o in opts):pick=next(o['id'] for o in opts if o['label']=='RED')
                    else:
                        options=[o for o in opts if 'one mana of any color' in o['label'].lower()]
                        assert len(options)==1,('Unexpected choice',r);pick=options[0]['id']
                elif 'PayMana' in kind:
                    snap(m,'payment')
                    if r['seat']==2 and spec.get('counter'):
                        island=next(c for c in v['players'][1]['battlefield'] if c['name']=='Island' and not c['tapped'] and 'card:'+str(c['id']) in ids)
                        pick='card:'+str(island['id'])
                    else:
                        base='mana:'+('B' if spec['color']==2 else 'R')
                        pick=next(i for i in ids if i==base or i.startswith(base+':restricted:'))
                    assert pick in ids,(case,r)
                elif kind=='SelectStackTarget':
                    snap(m,'counter-target');pick='ok' if r.get('selectedCount')==1 else next(i for i in ids if i.startswith('stack:'))
                elif kind=='InputSelectTargets':
                    snap(m,'target');pick='ok' if 'ok' in ids else 'player:'+str(v['players'][1]['targetId'])
                elif kind=='InputMulligan':pick='keep'
                elif kind not in {'InputPassPriority','InputConfirmMulligan','InputConfirm'}:
                    raise AssertionError(('Unhandled native request',r))
                assert pick in ids,(case,kind,pick,r)
                print(case,step,kind,pick,flush=True)
                process.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');process.stdin.flush()
            raise AssertionError('Step limit')
        except Exception as error:
            finish('FAIL',repr(error));raise
        finally:
            if process.poll() is None:process.kill();process.wait()

if __name__=='__main__':
    for case in sys.argv[1:] or CASES:
        print(json.dumps(run(case)),flush=True)
