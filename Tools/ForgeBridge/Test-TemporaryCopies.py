"""Native temporary/permanent copies and expiry-trigger countering, bounded regression."""
import json,os,queue,subprocess,tempfile,threading,sys,time,hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';OUT=ROOT/'.tools/temporary-copy-1006/native';OUT.mkdir(parents=True,exist_ok=True)
CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'));CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),str(ROOT/'Tools/ForgeBridge/tests/TemporaryCopyFixture.java')],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
def run(case):
    folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True)
    with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'trace.jsonl').open('w',encoding='utf8') as trace,(folder/'engine.log').open('w',encoding='utf8') as log:
        start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session=case,fixture=case,seed=4,firstPlayerSeat=1,startingLife=20,decks=[DECK,DECK])))
        p=subprocess.Popen([str(RT/CFG['java']),'-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.TemporaryCopyFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
        q=queue.Queue()
        def read():
            try:
                for line in p.stdout:q.put(json.loads(line))
            finally:q.put(None)
        threading.Thread(target=read,daemon=True).start()
        activated=set();cast=False;quasi=False;counter=False;seen=False;trigger_seen=False
        def snap(m,label):
            v=dict(m['views'][0]);v.update(request=m['request'],awaitingInput=m['request']['seat']==1)
            (folder/(label+'.json')).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf8')
        try:
            for step in range(220):
                m=q.get(timeout=40);assert m is not None,'Engine exited';trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ('fatal','rejected','ended'),m
                if m['type']!='request':continue
                r=m['request'];v=m['views'][0];one,two=v['players'];ids=[o['id'] for o in r['options']];kind=r['kind'];pick='ok';field=one['battlefield'];stack=v['stackItems']
                tokens=[c for c in field if c.get('token') and c['name']=='Grizzly Bears'];timed=[c for c in tokens if c.get('pendingDepartures')];permanent=[c for c in tokens if not c.get('pendingDepartures')]
                for c in timed:
                    if case=='reflection-twice':assert 'HASTE' in c['grantedKeywords'], 'Reflection copy exception must remain granted'
                    d=c['pendingDepartures'][0];assert d['action']==('exile' if case=='exile' else 'sacrifice') and d['phase'].strip().lower()=='end of turn',d
                    other=next(x for x in m['views'][1]['players'][0]['battlefield'] if x['id']==c['id']);assert other['pendingDepartures']==c['pendingDepartures']
                assert all(not c.get('pendingDepartures') for c in field if not c.get('token')),field
                expiry=[s for s in stack if s['kind']=='trigger' and ('sacrifice' in s['description'].lower() or 'exile' in s['description'].lower())]
                if expiry:trigger_seen=True;assert timed,'Expiry must remain attached while its trigger is on stack';snap(m,'expiry-stack')
                desired=2 if case=='reflection-twice' else 1
                if not seen and len(timed)==desired and (case!='reflection-twice' or quasi and permanent):
                    seen=True;snap(m,'copies')
                    if case=='reflection-twice':
                        markers=[e for e in one['effectMarkers'] if e.get('markerKind')=='delayed'];assert len(markers)==2 and len({e['markerGroupKey'] for e in markers})==1,markers
                        assert len(permanent)==1,'A copy of a timed token must not inherit the delayed instruction'
                if seen and not timed and not stack and (v['phase']=='END_OF_TURN' or v['turn']>=2):
                    assert trigger_seen,'No native expiry trigger observed'
                    if case=='reflection-twice':assert len(tokens)==1 and not one['effectMarkers']
                    elif case=='exile':assert not tokens
                    else:assert counter and len(tokens)==1 and not one['effectMarkers']
                    snap(m,'after-expiry');result=dict(case=case,status='PASS',folder=str(folder.relative_to(ROOT)),triggerStackKeepsLink=True,copyOfCopyDoesNotInherit=case=='reflection-twice',counteredExpirySurvives=counter)
                    (OUT/(case+'-result.json')).write_text(json.dumps(result,indent=2),encoding='utf8');return result
                if kind=='InputPassPriority':
                    if r['seat']==1 and v['phase']=='MAIN1' and not stack:
                        if case=='exile' and not cast:pick='card:'+str(next(c['id'] for c in one['hand'] if c['name']=='Heat Shimmer'));cast=True
                        elif case!='exile' and len(activated)<desired:
                            source=next(c for c in field if c['name']=='Reflection of Kiki-Jiki' and c['id'] not in activated);activated.add(source['id']);pick='card:'+str(source['id'])
                        elif case=='reflection-twice' and len(timed)==2 and not quasi:pick='card:'+str(next(c['id'] for c in one['hand'] if c['name']=='Quasiduplicate'));quasi=True
                    elif r['seat']==2 and case=='counter-trigger' and expiry and not counter:
                        pick='card:'+str(next(c['id'] for c in m['views'][1]['players'][1]['hand'] if c['name']=='Stifle'));counter=True
                elif kind=='SelectStackTarget':pick='ok' if 'ok' in ids else next(i for i in ids if i.startswith('stack:'))
                elif kind in ('InputSelectTargets','InputSelectCardsFromList') or kind.startswith('InputSelect'):
                    if 'ok' in ids:pick='ok'
                    else:
                        target=next((c for c in tokens if quasi and not seen),None) or next(c for c in field if c['name']=='Grizzly Bears' and not c.get('token'))
                        pick='card:'+str(target['id'])
                elif 'PayMana' in kind:
                    if 'ok' in ids:pick='ok'
                    elif any(i.startswith('mana:') for i in ids):pick=next(i for i in ids if i.startswith('mana:'))
                    else:pick=next(o['id'] for o in r['options'] if o['label'].startswith(('Mountain [','Island [')))
                elif kind=='OrderAbilities':pick='confirm-order'
                elif kind=='Option':pick=next(i for i in ids if i not in ('cancel','no'))
                elif kind=='InputMulligan':pick='keep'
                if pick not in ids:pick=next((i for i in ids if i not in ('cancel','no')),ids[0])
                print(case,step,kind,v['phase'],len(tokens),len(timed),pick,flush=True)
                p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
            raise AssertionError('Step limit')
        finally:
            if p.poll() is None:p.kill();p.wait()
for case in sys.argv[1:] or ['reflection-twice','exile','counter-trigger']:
    print(json.dumps(run(case)),flush=True)
