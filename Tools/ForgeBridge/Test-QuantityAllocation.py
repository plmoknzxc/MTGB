"""Actual native quantity allocation consumers; native results, no simulated settlement."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime'
RUNTIME=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
OUT=ROOT/'.tools/quantity-allocation-1006/native';OUT.mkdir(parents=True,exist_ok=True)
CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURES=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['StateBranchFixture.java','VariableBranchFixture.java']]
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),*map(str,FIXTURES)],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES=['counter-division','shield-division','shield-on-damage','mana-combo','mana-different']

def run(case):
    source={'counter-division':'Blessings of Nature','shield-division':'Embolden','shield-on-damage':'Divine Deflection','mana-combo':'Cascading Cataracts','mana-different':'Firemind Vessel'}[case]
    mana=case.startswith('mana')
    own={'Library':['Mountain'],'Battlefield':[source] if mana else ['Grizzly Bears','Memnite'],'Hand':[] if mana else [source]}
    if case.startswith('shield'):own['Hand'].append('Pyroclasm')
    other={'Library':['Mountain']}
    folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True)
    sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
    meta={'case':case,'folder':folder.relative_to(ROOT).as_posix(),'runtimeHash':RUNTIME['runtimeHash'],'driverSha256':sha(Path(__file__)),'fixtures':[{'path':p.relative_to(ROOT).as_posix(),'sha256':sha(p)} for p in FIXTURES]}
    def finish(status,message):
        value={**meta,'status':status,'result':status+' '+case+': '+message}
        for p in [folder/'result.json',OUT/(case+'-result.json')]:p.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        return value
    with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as tmp,(folder/'trace.jsonl').open('w',encoding='utf-8') as trace,(folder/'engine.log').open('w',encoding='utf-8') as log:
        temp=Path(tmp);audit=temp/'audit';audit.mkdir();start=temp/'start.json'
        start.write_text(json.dumps(dict(session=case,actor=1,own=own,other=other,auditDir=str(audit),seed=4,firstPlayerSeat=1,startingLife=30,decks=[DECK,DECK])),encoding='utf-8')
        proc=subprocess.Popen([str(RT/RUNTIME['java']),'-Djava.awt.headless=true','-Djava.io.tmpdir='+tmp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.VariableBranchFixture',str(RT/RUNTIME['assets']),str(start)],cwd=RT/RUNTIME['assets'],env=dict(os.environ,APPDATA=tmp,LOCALAPPDATA=tmp,TEMP=tmp,TMP=tmp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf-8')
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
        stage=0;source_id=None;follow_id=None;creatures=[];selected=[];reset=False;subtracted=False;allocation_done=False;deadline=time.monotonic()+240
        try:
            for step in range(700):
                assert time.monotonic()<deadline,'Deadline';m=messages.get(timeout=40);assert m is not None,'Native exited'
                trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ['fatal','ended','rejected'],m
                if m['type']!='request':continue
                r=m['request'];opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];view=m['views'][0];one,two=view['players'];pick=None
                snapshot(m,'step-'+str(step));native=observed(r) if stage else None
                if kind=='InputPassPriority':
                    if stage==0 and r['seat']==1 and view['phase']=='MAIN1':
                        source_id=next(c['id'] for c in one['battlefield' if mana else 'hand'] if c['name']==source)
                        creatures=[c['id'] for c in one['battlefield'] if c['name'] in ['Grizzly Bears','Memnite']]
                        if case.startswith('shield'):follow_id=next(c['id'] for c in one['hand'] if c['name']=='Pyroclasm')
                        snapshot(m,'before');stage=1;pick='card:'+str(source_id)
                    elif case=='shield-on-damage' and stage==1 and not view['stackItems'] and source_id in {c['id'] for c in one['graveyardCards']}:
                        if r['seat']==1:stage=2;snapshot(m,'shield-established');pick='card:'+str(follow_id)
                    elif stage and allocation_done and not view['stackItems']:
                        if case=='shield-division' and stage==1:
                            if r['seat']==1:stage=2;snapshot(m,'shield-established');pick='card:'+str(follow_id)
                        else:
                            assert reset and subtracted and one['life']==30 and two['life']==(27 if case=='shield-on-damage' else 30)
                            if case=='counter-division':
                                cards=[c for c in one['battlefield'] if c['id'] in creatures]
                                assert len(cards)==2 and sorted((c['power'],c['toughness']) for c in cards)==[(3,3),(4,4)],cards
                                assert all(any(k.get('kind')=='P1P1' and k.get('count')==2 for k in c.get('counters',[])) for c in cards),cards
                                message='real divided four +1/+1 counters, two exact original targets receive 2 each; mandatory one-per-target minimum/reset/decrement/confirm, next priority'
                            elif case.startswith('shield'):
                                assert creatures[0] in {c['id'] for c in one['battlefield']} and creatures[1] in {c['id'] for c in one['graveyardCards']}
                                events=[e for e in native['damageEvents'] if e['source']==follow_id]
                                assert [(e['target'],e['amount']) for e in events]==[(creatures[1],1)],events
                                message='real divided preventive 3/1 shield then actual Pyroclasm: bear survives with all 2 prevented, Memnite receives 1 and dies; exact damage event, next priority'
                                if case=='shield-on-damage':
                                    reflected=[e for e in native['damageEvents'] if e['source']==source_id]
                                    assert sum(e['amount'] for e in reflected)==3 and all(e['target']==two['targetId'] for e in reflected),reflected
                                    message='actual Divine Deflection X3 followed by Pyroclasm invokes native divideShield on damage: minimum0, recipient maximum2, 2/1 prevented; Memnite takes1 and dies, bear survives, exactly3 reflected to opponent; reset/subtract/confirm and next priority'
                            else:
                                pool=one['manaPool'];assert pool[3]==(2 if case=='mana-combo' else 1) and pool[4]==(3 if case=='mana-combo' else 1) and sum(pool)==(5 if case=='mana-combo' else 2),pool
                                assert next(c for c in one['battlefield'] if c['id']==source_id)['tapped']
                                message='actual activated mana combination '+('2R/3G after pay5' if case=='mana-combo' else '1R/1G with native per-color maximum1')+'; zero minimum/reset/decrement and exact pool, next priority'
                            snapshot(m,'resolved');return finish('PASS',message)
                    if pick is None:pick='ok'
                elif 'SelectTargets' in kind:
                    assert case in ['counter-division','shield-division','shield-on-damage'];count=r['selectedCount']
                    if case=='shield-on-damage':pick='ok' if count else 'player:'+str(two['targetId'])
                    elif count==2:pick='ok'
                    else:chosen=creatures[count];pick='card:'+str(chosen);selected.append(chosen)
                elif 'PayMana' in kind:
                    ok=next((o for o in opts if o['id']=='ok'),None);pick='ok' if ok and not ok.get('disabled') else next(o['id'] for o in opts if o['id'].startswith('card:'))
                elif kind=='AssignAmount':
                    assert r['selectionPurpose']=='genericAmount' and not any(i in ids for i in ['cancel','skip-amount'])
                    assert r['sourceCardId']==source_id if case!='shield-on-damage' else r['sourceCard']['name'].startswith(source+' ('+str(source_id)+')'),r
                    values=r['allocations'];minimum=0 if mana or case=='shield-on-damage' else 1;total=5 if case=='mana-combo' else 2 if mana else 3 if case=='shield-on-damage' else 4
                    assert r['maxCount']==total and ('confirm-amount' in ids)==(r['remainingCount']==0) and all(v['amount']>=minimum for v in values)
                    desired={v['id']:(2 if v['label'].upper()=='RED' else 3 if v['label'].upper()=='GREEN' else 0) for v in values} if case=='mana-combo' else {v['id']:(1 if v['label'].upper() in ['RED','GREEN'] else 0) for v in values} if mana else {v['id']:(2 if case=='counter-division' else 3 if v['targetId']==creatures[0] else 1) for v in values}
                    if case=='shield-on-damage':desired={v['id']:(2 if v['targetId']==creatures[0] else 1) for v in values}
                    if not mana:assert {v['targetId'] for v in values}==set(creatures)
                    for v in values:
                        assert ('amount-sub:'+str(v['id']) in ids)==(v['amount']>minimum)
                        if case=='mana-different' and v['amount']==1:assert 'amount-add:'+str(v['id']) not in ids
                        if case=='shield-on-damage' and v['amount']==2:assert 'amount-add:'+str(v['id']) not in ids
                    if not subtracted and r['selectedCount']>len(values)*minimum:
                        v=next(v for v in values if v['amount']>minimum);snapshot(m,'allocation-before-subtract');pick='amount-sub:'+str(v['id']);subtracted=True
                    elif r['remainingCount']==0 and not reset:reset=True;snapshot(m,'allocation-before-reset');pick='reset-amount'
                    elif r['remainingCount']==0:
                        assert all(v['amount']==desired[v['id']] for v in values),values
                        snapshot(m,'allocation-confirm');pick='confirm-amount';allocation_done=True
                    else:
                        snapshot(m,'allocation-draft-'+str(r['selectedCount']));v=next(v for v in values if v['amount']<desired[v['id']]);pick='amount-add:'+str(v['id'])
                elif kind=='dialog' and mana:
                    pick=next((o['id'] for o in opts if 'five mana' in o['label'].lower()),None) if case=='mana-combo' else ids[0]
                    assert pick is not None,r
                elif kind=='dialog' and 'targets' in r['message'].lower():pick=next(o['id'] for o in opts if o['label']=='2')
                elif kind=='OrderAbilities':pick='confirm-order' if 'confirm-order' in ids else next(i for i in ids if i.startswith('order:'))
                elif kind=='ChooseNumber' or kind.startswith('InputChooseX'):
                    assert case=='shield-on-damage';pick='confirm-number' if r['selectedValue']==3 else 'number-plus'
                elif 'Mulligan' in kind:pick='keep' if 'keep' in ids else 'ok'
                elif 'ok' in ids:pick='ok'
                else:raise AssertionError(('Unhandled request',r))
                assert pick in ids,(case,stage,kind,pick,r)
                print(case,step,kind,repr(r.get('message','')[:100]),pick,flush=True)
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
