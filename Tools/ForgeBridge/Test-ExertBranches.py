"""Actual optional attack and mana exert costs across two subsequent untap steps."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime'
RUNTIME=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
OUT=ROOT/'.tools/exert-branches-1006/native';OUT.mkdir(parents=True,exist_ok=True)
CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURES=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['StateBranchFixture.java','ExertBranchFixture.java']]
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),*map(str,FIXTURES)],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES=['attack-exert','attack-decline','attack-two','mana-exert','mana-ordinary','mana-cancel']

def run(case):
    source='Glory-Bound Initiate' if case.startswith('attack') else 'Oasis Ritualist'
    own={'Battlefield':[source]*(2 if case=='attack-two' else 1),'Library':['Mountain']};other={'Library':['Mountain']}
    folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True)
    sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
    meta={'case':case,'folder':folder.relative_to(ROOT).as_posix(),'runtimeHash':RUNTIME['runtimeHash'],'driverSha256':sha(Path(__file__)),'fixtures':[{'path':p.relative_to(ROOT).as_posix(),'sha256':sha(p)} for p in FIXTURES]}
    def finish(status,message):
        value={**meta,'status':status,'result':status+' '+case+': '+message}
        for p in [folder/'result.json',OUT/(case+'-result.json')]:p.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        return value
    with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as tmp,(folder/'trace.jsonl').open('w',encoding='utf-8') as trace,(folder/'engine.log').open('w',encoding='utf-8') as log:
        temp=Path(tmp);audit=temp/'audit';audit.mkdir();start=temp/'start.json'
        start.write_text(json.dumps(dict(session=case,own=own,other=other,auditDir=str(audit),seed=4,firstPlayerSeat=1,startingLife=30,decks=[DECK,DECK])),encoding='utf-8')
        proc=subprocess.Popen([str(RT/RUNTIME['java']),'-Djava.awt.headless=true','-Djava.io.tmpdir='+tmp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.ExertBranchFixture',str(RT/RUNTIME['assets']),str(start)],cwd=RT/RUNTIME['assets'],env=dict(os.environ,APPDATA=tmp,LOCALAPPDATA=tmp,TEMP=tmp,TMP=tmp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf-8')
        messages=queue.Queue()
        def reader():
            try:
                for line in proc.stdout:messages.put(json.loads(line))
            finally:messages.put(None)
        threading.Thread(target=reader,daemon=True).start()
        def snapshot(m,label):
            for seat,v in enumerate(m['views'],1):
                v=dict(v);v.update(request=m['request'],awaitingInput=True)
                (folder/(label+('-other' if seat==2 else '')+'.json')).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
        def observed(r):
            path=audit/(str(r['requestId'])+'.json');deadline=time.monotonic()+5
            while time.monotonic()<deadline:
                try:value=json.loads(path.read_text('utf-8'));break
                except (FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
            else:raise AssertionError('Missing native audit')
            (folder/('audit-'+str(r['requestId'])+'.json')).write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding='utf-8');return value
        source_id=None;declared=False;declared_ids=set();activated=False;decision=False;checked=False;next_seen=False;reset_exert=False;after_eot=False;deadline=time.monotonic()+240
        exert=case.endswith('exert') or case=='attack-two'
        try:
            for step in range(700):
                assert time.monotonic()<deadline,'Deadline';m=messages.get(timeout=40);assert m is not None,'Native exited'
                trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ['fatal','ended','rejected'],m
                if m['type']!='request':continue
                r=m['request'];opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];view=m['views'][0];one,two=view['players'];pick=None
                snapshot(m,'step-'+str(step))
                if source_id is None and kind=='InputPassPriority' and r['seat']==1 and view['phase']=='MAIN1':
                    source_id=next(c['id'] for c in one['battlefield'] if c['name']==source);snapshot(m,'before')
                native=observed(r) if source_id is not None else None
                card=next((c for c in one['battlefield'] if c['id']==source_id),None)
                state=next((c for c in native['cards'] if c['id']==source_id),None) if native else None
                if state:assert card['exertedBySeats']==state['waitingUntapSeats'] and card['exertedThisTurn']==state['exertedThisTurn'],'Typed exert projection differs from native'
                if case=='attack-two' and native:
                    for item in native['cards']:
                        projected=next(c for c in one['battlefield'] if c['id']==item['id'])
                        assert projected['exertedBySeats']==item['waitingUntapSeats'] and projected['exertedThisTurn']==item['exertedThisTurn']
                if kind=='InputPassPriority':
                    if case=='attack-two' and checked and not after_eot and view['turn']==2 and view['phase']=='MAIN1':
                        assert len(native['cards'])==2 and all(c['tapped'] and c['waitingUntapSeats']==[1] and c['exertedThisTurn']==0 for c in native['cards'])
                        assert all((c['power'],c['toughness'])==(3,1) and 'LIFELINK' not in c['keywords'] for c in one['battlefield']);snapshot(m,'pending-after-eot');after_eot=True
                    if case=='mana-cancel' and decision:
                        assert not card['tapped'] and not state['waitingUntapSeats'] and state['exertedThisTurn']==0 and not sum(one['manaPool']);snapshot(m,'resolved')
                        return finish('PASS','native mana ability menu cancel leaves card untapped, no exert flag/no mana, next priority')
                    if case.startswith('mana') and source_id and not activated:
                        assert 'card:'+str(source_id) in ids;pick='card:'+str(source_id);activated=True
                    if card and not checked and (declared and view['phase']=='MAIN2' or activated and sum(one['manaPool'])>0):
                        assert card['tapped'] and state['waitingUntapSeats']==([1] if exert else []) and state['exertedThisTurn']==(1 if exert else 0),state
                        if case.startswith('attack'):
                            amount=2 if case=='attack-two' else 1
                            assert one['life']==(30+4*amount if exert else 30) and two['life']==(30-4*amount if exert else 27),(one['life'],two['life'])
                            assert (card['power'],card['toughness'])==((4,4) if exert else (3,1))
                            assert ('LIFELINK' in card['keywords'])==exert
                        else:assert sum(one['manaPool'])==(2 if exert else 1),one['manaPool']
                        snapshot(m,'effect');checked=True
                        if case=='attack-two':assert len(native['cards'])==2 and all(c['tapped'] and c['waitingUntapSeats']==[1] and c['exertedThisTurn']==1 for c in native['cards'])
                    if checked and view['turnSeat']==1 and view['turn']==3 and view['phase']=='MAIN1' and not next_seen:
                        assert card['tapped']==exert and not state['waitingUntapSeats'] and state['exertedThisTurn']==0,state
                        if case.startswith('attack'):assert (card['power'],card['toughness'])==(3,1) and 'LIFELINK' not in card['keywords']
                        snapshot(m,'next-untap');next_seen=True
                        if case=='attack-two':assert all(c['tapped'] and not c['waitingUntapSeats'] and c['exertedThisTurn']==0 for c in native['cards'])
                    if next_seen and view['turnSeat']==1 and view['turn']==5 and view['phase']=='MAIN1':
                        assert not card['tapped'] and not state['waitingUntapSeats'];snapshot(m,'following-untap')
                        if case=='attack-two':assert all(not c['tapped'] and not c['waitingUntapSeats'] for c in native['cards'])
                        return finish('PASS','actual '+('attack optional cost and damage/lifelink' if case.startswith('attack') else 'mana activation/cost and pool')+'; '+('skip exactly next own untap then recover' if exert else 'decline/no exert cost untaps normally')+' with native flags and next priority')
                    if pick is None:pick='ok'
                elif kind=='InputAttack':
                    if r['seat']==1 and view['turn']==1 and any(o['id'].startswith('attack:') and o['attackerId'] not in declared_ids for o in opts):
                        choice=next(o for o in opts if o['id'].startswith('attack:') and o['attackerId'] not in declared_ids and o['defenderKind']=='player');pick=choice['id'];declared_ids.add(choice['attackerId']);declared=True
                    else:pick='ok'
                elif kind=='InputBlock':pick='ok'
                elif kind=='InputMulligan':pick='keep'
                elif kind=='OrderCards' and 'exert' in r['message'].lower():
                    assert r['selectionPurpose']=='exertAttack'
                    assert r['minCount']==0 and r['maxCount']==(2 if case=='attack-two' else 1) and {o['card']['id'] for o in opts if o.get('card')}==declared_ids
                    snapshot(m,'exert-choice-'+str(r['selectedCount']));decision=True
                    if exert and r['selectedCount']==r['maxCount'] and not reset_exert:pick='reset-cards';reset_exert=True
                    else:pick=next(o['id'] for o in opts if o.get('card') and not o['selected']) if exert and r['selectedCount']<r['maxCount'] else 'confirm-cards'
                elif kind=='dialog' and case.startswith('mana'):
                    if any('exert' in o['label'].lower() for o in opts):
                        pick='cancel' if case=='mana-cancel' else next(o['id'] for o in opts if o['id'].startswith('choice:') and ('exert' in o['label'].lower())==exert);snapshot(m,'mana-ability');decision=True
                    else:pick=ids[0];snapshot(m,'mana-color')
                elif kind=='dialog' or kind=='InputConfirm':
                    assert 'exert' in (r.get('message','')+' '.join(o['label'] for o in opts)).lower(),r
                    snapshot(m,'exert-choice');decision=True
                    pick=('ok' if exert else 'cancel') if kind=='InputConfirm' else ids[0 if exert else -1]
                elif kind=='OrderAbilities':pick='confirm-order'
                elif 'ok' in ids:pick='ok'
                else:raise AssertionError(('Unhandled request',r))
                assert pick in ids,(case,kind,pick,r)
                print(case,step,view['turn'],view['phase'],kind,repr(r.get('message','')[:85]),pick,flush=True)
                proc.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');proc.stdin.flush()
            raise AssertionError('Step limit')
        except Exception:finish('FAIL',traceback.format_exc());raise
        finally:
            if proc.poll() is None:proc.kill();proc.wait()

if __name__=='__main__':
    results=[]
    for case in sys.argv[1:] or CASES:
        try:r=run(case);print(r['result'],flush=True);results.append(r)
        except Exception as e:print('FAIL '+case+': '+repr(e),flush=True);results.append(json.loads((OUT/(case+'-result.json')).read_text('utf-8')))
    (OUT/'results.json').write_text(json.dumps(dict(status='PASS' if all(r['status']=='PASS' for r in results) else 'FAIL',results=results),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    sys.exit(any(r['status']!='PASS' for r in results))
