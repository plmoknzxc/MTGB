"""Actual BidLife and forced numeric consumers; native results, no simulated settlement."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime'
RUNTIME=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
OUT=ROOT/'.tools/number-auction-1006/native';OUT.mkdir(parents=True,exist_ok=True)
CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURES=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['StateBranchFixture.java','VariableBranchFixture.java']]
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),*map(str,FIXTURES)],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES=['auction-zero','auction-other','auction-caster','hellion-large','hellion-cancel','bounded-number']

def run(case):
    source='Volcano Hellion' if case.startswith('hellion') else 'Expel the Interlopers' if case=='bounded-number' else 'Illicit Auction'
    own={'Hand':[source],'Library':['Mountain']};other={'Battlefield':['Grizzly Bears'],'Library':['Mountain']}
    folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True)
    sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
    meta={'case':case,'folder':folder.relative_to(ROOT).as_posix(),'runtimeHash':RUNTIME['runtimeHash'],'driverSha256':sha(Path(__file__)),'fixtures':[{'path':p.relative_to(ROOT).as_posix(),'sha256':sha(p)} for p in FIXTURES]}
    def finish(status,message):
        value={**meta,'status':status,'result':status+' '+case+': '+message}
        for p in [folder/'result.json',OUT/(case+'-result.json')]:p.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        return value
    with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as tmp,(folder/'trace.jsonl').open('w',encoding='utf-8') as trace,(folder/'engine.log').open('w',encoding='utf-8') as log:
        temp=Path(tmp);audit=temp/'audit';audit.mkdir();start=temp/'start.json'
        start.write_text(json.dumps(dict(session=case,actor=1,own=own,other=other,auditDir=str(audit),seed=4,firstPlayerSeat=1,startingLife=200 if case.startswith("hellion") else 30,decks=[DECK,DECK])),encoding='utf-8')
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
        stage=0;source_id=None;bear_id=None;raised=set();number_done=False;confirms=0;deadline=time.monotonic()+240
        try:
            for step in range(700):
                assert time.monotonic()<deadline,'Deadline';m=messages.get(timeout=40);assert m is not None,'Native exited'
                trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ['fatal','ended','rejected'],m
                if m['type']!='request':continue
                r=m['request'];opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];view=m['views'][0];one,two=view['players'];pick=None
                snapshot(m,'step-'+str(step));native=observed(r) if stage else None
                if kind=='InputPassPriority':
                    if stage==0 and r['seat']==1 and view['phase']=='MAIN1':
                        source_id=next(c['id'] for c in one['hand'] if c['name']==source);bear_id=next(c['id'] for c in two['battlefield'] if c['name']=='Grizzly Bears');snapshot(m,'before');stage=1;pick='card:'+str(source_id)
                    elif stage and not view['stackItems'] and (source_id in {c['id'] for c in one['graveyardCards']} or case.startswith('hellion') and number_done):
                        if case.startswith('auction'):
                            winner=two if case=='auction-other' else one;loser=one if case=='auction-other' else two;bid=0 if case=='auction-zero' else 6 if case=='auction-caster' else 5
                            bear=next(c for c in winner['battlefield'] if c['id']==bear_id)
                            assert bear_id not in {c['id'] for c in loser['battlefield']} and bear['ownerSeat']==2,bear
                            assert winner['life']==30-bid and loser['life']==30 and confirms>=2,(winner['life'],loser['life'],confirms)
                            snapshot(m,'resolved');return finish('PASS','native life auction '+str(sorted(raised))+' / final bid '+str(bid)+'; only winner loses life, exact targeted creature control changes, owner retained, no cancellation on forced increment, next priority')
                        if case=='bounded-number':
                            assert number_done and one['life']==30 and two['life']==30 and bear_id in {c['id'] for c in two['graveyardCards']}
                            snapshot(m,'resolved');return finish('PASS','explicit Min0/Max10 remains mandatory 11-option numeric choice; actual choose2 destroys targeted 2-power bear, no life loss, next priority')
                        if case=='hellion-cancel':
                            assert one['life']==200 and bear_id in {c['id'] for c in two['battlefield']}
                            assert not any(e['source']==source_id and e['amount']>0 for e in native['damageEvents'])
                            snapshot(m,'resolved');return finish('PASS','native optional integer cancellation falls back to zero; no damage/life loss, exact bear survives, next priority')
                        assert one['life']==77 and bear_id in {c['id'] for c in two['graveyardCards']}
                        events=[e for e in native['damageEvents'] if e['source']==source_id];assert sorted(e['amount'] for e in events)==[123,123],events
                        snapshot(m,'resolved');return finish('PASS','actual nonpreventable 123 damage numeric choice to self and exact target, life 200->77, target dies, next priority')
                    else:pick='ok'
                elif 'SelectTargets' in kind:pick='ok' if r['selectedCount'] else 'card:'+str(bear_id);snapshot(m,'target')
                elif 'PayMana' in kind:
                    ok=next((o for o in opts if o['id']=='ok'),None);pick='ok' if ok and not ok.get('disabled') else next(o['id'] for o in opts if o['id'].startswith('card:'))
                elif kind=='InputConfirm' and case.startswith('auction'):
                    assert 'bid' in r['message'].lower(),r;confirms+=1;seat=r['seat'];raise_it=(case in ['auction-other','auction-caster'] and seat==2 and 2 not in raised) or (case=='auction-caster' and seat==1 and 2 in raised and 1 not in raised)
                    snapshot(m,'bid-confirm-'+str(confirms));pick='ok' if raise_it else 'cancel'
                    if raise_it:raised.add(seat)
                elif kind=='dialog' and case=='bounded-number':
                    assert [int(o['label']) for o in opts]==list(range(11)) and 'cancel' not in ids,r
                    snapshot(m,'bounded-number');pick=next(o['id'] for o in opts if o['label']=='2');number_done=True
                elif kind=='dialog' and case.startswith('auction'):
                    assert all(o['label'].isdigit() for o in opts) and 'cancel' not in ids and [int(o['label']) for o in opts]==list(range(1,10)),r
                    increment=5 if r['seat']==2 else 1;snapshot(m,'bid-increment-'+str(r['seat']));pick=next(o['id'] for o in opts if o['label']==str(increment))
                elif kind=='ChooseNumber' and case.startswith('hellion'):
                    snapshot(m,'number-'+str(r['selectedValue']));assert r['minValue']==0 and r['maxValue']==2147483647,('Printed any amount constrained below 123',r)
                    if case=='hellion-cancel':
                        pick='cancel';number_done=True;snapshot(m,'number-cancel')
                        proc.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');proc.stdin.flush();continue
                    current=r['selectedValue'];pick='confirm-number' if current==123 else 'number-plus-five' if current+5<=123 else 'number-plus'
                    if current==123:number_done=True;snapshot(m,'number-final')
                elif kind=='OrderAbilities':pick='confirm-order'
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
