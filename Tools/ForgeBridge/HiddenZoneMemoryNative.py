"""Real Thoughtseize -> priority -> Brainstorm; seat-local memory and ambiguity."""
import json,os,queue,subprocess,tempfile,threading,time,traceback,shutil
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';OUT=ROOT/'.tools/resolution-dock-1005/native';OUT.mkdir(parents=True,exist_ok=True)
cfg=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
classes=OUT/'classes';classes.mkdir(exist_ok=True)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),str(ROOT/'Tools/ForgeBridge/ResolutionReviewFixture.java'),str(ROOT/'Tools/ForgeBridge/HiddenZoneMemoryFixture.java')],check=True)
deck={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
def run():
    if (OUT/'hand-memory.jsonl').exists():
        archive=OUT/'attempts'/str(time.time_ns());archive.mkdir(parents=True)
        for old in OUT.glob('hand-*'):
            if old.is_file():shutil.copy2(old,archive/old.name)
    with tempfile.TemporaryDirectory(prefix='temp-',dir=OUT) as temp,(OUT/'hand-memory.log').open('w',encoding='utf-8') as log,(OUT/'hand-memory.jsonl').open('w',encoding='utf-8') as trace:
        start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session='hand-memory',seed=4,startingLife=20,firstPlayerSeat=1,decks=[deck,deck])),encoding='utf-8')
        process=subprocess.Popen([str(RT/cfg['java']),'-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.HiddenZoneMemoryFixture',str(RT/cfg['assets']),str(start)],cwd=RT/cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf-8')
        messages=queue.Queue()
        def read():
            try:
                for line in process.stdout:messages.put(json.loads(line))
            finally:messages.put(None)
        threading.Thread(target=read,daemon=True).start()
        def save(m,label,seat=1):
            v=dict(m['views'][seat-1]);v['awaitingInput']=m['request']['seat']==seat
            if v['awaitingInput']:v['request']=m['request']
            (OUT/(label+'.json')).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
        thought=False;brain=False;known=None;discarded=None;selected=[];revealed=[];persist=0;returned=False;deadline=time.monotonic()+120
        try:
            for step in range(450):
                assert time.monotonic()<deadline,'deadline';m=messages.get(timeout=35);assert m is not None,'bridge exited'
                trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ('fatal','ended','rejected'),m
                if m['type']!='request':continue
                r=m['request'];kind=r['kind'];ids=[o['id'] for o in r['options']];v=m['views'][0];p=v['players'][1];pick='ok'
                if kind=='InputPassPriority':
                    if not thought and r['seat']==1 and v['phase']=='MAIN1':
                        save(m,'hand-private-before');assert not p['hand'],'initial hand leaked'
                        pick='card:'+str(next(c['id'] for c in v['players'][0]['hand'] if c['name']=='Thoughtseize'));thought=True
                    elif thought and not brain and not v['stackItems'] and discarded is not None:
                        visible={c['id'] for c in p['hand']};actual={c['id'] for c in m['views'][1]['players'][1]['hand']}
                        assert visible==actual==known-{discarded},('known hand lost',visible,actual,known)
                        persist+=1;save(m,'hand-known-priority')
                        if r['seat']==2:
                            pick='card:'+str(next(c['id'] for c in p['hand'] if c['name']=='Brainstorm'));brain=True
                    elif brain and not v['stackItems'] and returned:
                        assert not p['hand'],'old hand stays visible after hidden return'
                        assert not p.get('libraryTop'),'hidden return top leaked to former viewer'
                        owner=m['views'][1]['players'][1]
                        assert len(owner['hand'])==owner['handCount'],'owner hand hidden'
                        assert owner.get('libraryTop',{}).get('id') in selected,'owner known top lost'
                        save(m,'hand-mixed-priority');save(m,'hand-mixed-owner',2)
                        assert v['players'][0]['life']==18,'Thoughtseize life not paid'
                        return dict(PASS=True,case='Thoughtseize then Brainstorm',knownCount=len(known),discarded=discarded,returned=selected,prioritySnapshots=persist,ownerCount=owner['handCount'],limits='Seat-view protocol and native effects, not live networking.')
                elif 'PayMana' in kind:pick='ok'
                elif kind=='InputSelectTargets':pick='ok' if 'ok' in ids else 'player:'+str(p['targetId'])
                elif kind=='RevealCards':
                    if not brain:
                        shown={c['id'] for c in r['previewCards'] if c['zone']=='Hand' and c['ownerSeat']==2};known=(known or set())|shown;assert known
                        save(m,'hand-reveal');revealed.append(len(known))
                    pick='confirm-cards'
                elif kind.startswith('InputSelect') or kind in ('ChooseCards','InputSelectEntitiesFromList'):
                    options=[o for o in r['options'] if o.get('card') and o['id'].startswith(('card:','select:','value:'))]
                    if not brain:
                        if discarded is not None and 'ok' in ids:pick='ok'
                        else:
                            option=next(o for o in options if o['card']['name']=='Negate');discarded=option['card']['id'];pick=option['id'];save(m,'hand-discard-choice')
                    else:
                        save(m,'hand-return-'+str(len(selected)),2)
                        if len(selected)==2:
                            pick='ok' if 'ok' in ids else 'confirm-cards';returned=True
                        else:
                            option=next(o for o in options if o['card']['id'] not in selected and o['card']['id'] in known and o['card']['name'] in ('Duress','Grizzly Bears'))
                            selected.append(option['card']['id']);pick=option['id']
                elif kind=='OrderCards':
                    options=[o for o in r['options'] if o.get('card') and not o.get('selected')]
                    pick=options[0]['id'] if options else 'confirm-cards';returned=not options
                assert pick in ids,(kind,pick,ids,r.get('message'))
                process.stdin.write(json.dumps(dict(type='respond',session='hand-memory',seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');process.stdin.flush()
            raise AssertionError('step budget')
        finally:process.kill();process.wait(timeout=5)
try:result=run()
except Exception:result=dict(PASS=False,error=traceback.format_exc())
(OUT/'hand-memory-result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(result,ensure_ascii=False),flush=True)
raise SystemExit(0 if result['PASS'] else 1)
