"""Real Thoughtseize cast/payment/reveal/discard, using existing fixture setup."""
import json,os,queue,subprocess,tempfile,threading,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime'
CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'Tools/Validation/Reports/Thoughtseize';OUT.mkdir(parents=True,exist_ok=True)
CLASSES=ROOT/'.tools/thoughtseize/classes';CLASSES.mkdir(parents=True,exist_ok=True)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),str(ROOT/'Tools/ForgeBridge/tests/CardSelectionFixture.java')],check=True)
def run():
    deck={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
    with tempfile.TemporaryDirectory(prefix='mtgb-thoughtseize-') as temp,(OUT/'native.log').open('w',encoding='utf8') as log,(OUT/'native.jsonl').open('w',encoding='utf8') as trace:
        start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session='thoughtseize',spell='Thoughtseize',seed=4,startingLife=20,firstPlayerSeat=1,decks=[deck,deck])),encoding='utf8')
        p=subprocess.Popen([str(RT/CFG['java']),'-Djava.awt.headless=true','-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.CardSelectionFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
        messages=queue.Queue()
        def reader():
            for line in p.stdout:messages.put(json.loads(line))
            messages.put(None)
        threading.Thread(target=reader,daemon=True).start()
        def shot(m,name):
            r=m['request'];v=dict(m['views'][0]);v.update(request=r if r['seat']==1 else None,awaitingInput=r['seat']==1)
            (OUT/(name+'.json')).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf8')
        cast=False;selected=False;paid=False;seen_reveal=False;before=None;discard_id=None
        deadline=time.monotonic()+120
        try:
            for step in range(180):
                assert time.monotonic()<deadline,'Deadline'
                m=messages.get(timeout=30);assert m is not None,'Bridge exited';trace.write(json.dumps(m)+'\n');trace.flush()
                assert m['type'] not in ('fatal','ended','rejected'),m
                if m['type']!='request':continue
                r=m['request'];v=m['views'][0];ids=[o['id'] for o in r['options']];kind=r['kind'];pick='ok'
                assert all('request' not in view for view in m['views']),'Private request leaked into shared state'
                if kind=='InputPassPriority':
                    if not cast and r['seat']==1 and v['phase']=='MAIN1':
                        before=v;assert not v['players'][1]['hand'],'Opponent hidden hand leaked'
                        shot(m,'before');c=next(c for c in v['players'][0]['hand'] if c['name']=='Thoughtseize');pick='card:'+str(c['id']);cast=True
                    elif cast and not v['stackItems'] and 'Thoughtseize' in v['players'][0]['graveyard']:
                        me,other=v['players'];assert selected and paid and seen_reveal,'Missing real decisions'
                        assert me['life']==before['players'][0]['life']-2,'Life loss wrong'
                        assert other['handCount']==before['players'][1]['handCount']-1,'Hand count wrong'
                        assert any(c['id']==discard_id and c['name']=='Negate' for c in other['graveyardCards']),'Wrong card discarded'
                        assert not other['hand'],'Private hand remains exposed after resolution'
                        assert me['libraryCount']==before['players'][0]['libraryCount'] and other['libraryCount']==before['players'][1]['libraryCount'],'Library changed'
                        shot(m,'resolved');return 'PASS thoughtseize: actual cast/target/black payment; reveal to caster only; select exact Negate ID, opponent hand -1 and matching graveyard ID; caster life -2; private hand hidden and priority restored'
                elif 'PayMana' in kind:
                    c=next(c for c in v['players'][r['playerSeat']-1]['battlefield'] if c['name']=='Swamp' and 'card:'+str(c['id']) in ids);pick='card:'+str(c['id']);paid=True;shot(m,'payment')
                elif kind=='InputSelectTargets':pick='ok' if r.get('selectedCount') and 'ok' in ids else 'player:'+str(v['players'][1]['targetId']);shot(m,'target')
                elif kind=='RevealCards':seen_reveal=True;shot(m,'revealed' if r['seat']==1 else 'revealed-other');pick='confirm-cards'
                elif kind=='SelectCards' or kind.startswith('InputSelect'):
                    shot(m,'selected' if selected else 'choice');seen_reveal=True
                    options=[o for o in r['options'] if o.get('card')]
                    assert all('Land' not in o['card']['currentType'].split(' - ')[0] for o in options),'Land selectable'
                    assert any(o['card']['name']=='Duress' for o in options),'Other nonland omitted'
                    if selected:pick='confirm-cards' if 'confirm-cards' in ids else 'ok'
                    else:
                        option=next(o for o in options if o['card']['name']=='Negate');discard_id=option['card']['id'];pick=option['id'];selected=True
                assert pick in ids,(kind,pick,ids)
                p.stdin.write(json.dumps(dict(type='respond',session='thoughtseize',seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
            raise AssertionError('Unresolved')
        finally:
            if p.poll() is None:
                p.stdin.write('{"type":"shutdown"}\n');p.stdin.flush()
                try:p.wait(timeout=3)
                except subprocess.TimeoutExpired:p.kill();p.wait()
try:result=run()
except Exception as error:result='FAIL thoughtseize: '+repr(error)
(OUT/'result.txt').write_text(result,encoding='utf8');print(result,flush=True)
raise SystemExit(result.startswith('FAIL'))
