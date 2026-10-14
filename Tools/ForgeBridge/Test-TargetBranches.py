"""Actual targeting, cancellation and resolution legality; native read-only damage audit."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime'
RUNTIME=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
OUT=ROOT/'.tools/target-branches-1006/native';OUT.mkdir(parents=True,exist_ok=True)
CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURES=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['StateBranchFixture.java','VariableBranchFixture.java']]
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),*map(str,FIXTURES)],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES=['finale-zero','finale-ten-zero','hex-six','hex-five','shock-reset-cancel','arc-partial','arc-all-invalid','redirect-change','redirect-decline']

def run(case):
    source='Finale of Eternity' if case.startswith('finale') else 'Hex' if case.startswith('hex') else 'Shock' if case.startswith(('shock','redirect')) else 'Arc Lightning'
    own={'Hand':[source],'Library':['Opt']};other={'Battlefield':['Grizzly Bears']*(6 if case=='hex-six' else 5 if case=='hex-five' else 2),'Library':['Mountain']}
    if case=='finale-ten-zero':own['Graveyard']=['Memnite']
    if case.startswith('arc'):own['Hand'].append('Shock')
    if case.startswith('redirect'):own['Hand'].append('Redirect')
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
        stage=0;source_id=None;shock_id=None;bear_ids=[];selected=[];toggle=0;reset=0;allocated=[];allocation_reset=False;target_owner=None;redirect_choice=False;deadline=time.monotonic()+240
        try:
            for step in range(700):
                assert time.monotonic()<deadline,'Deadline';m=messages.get(timeout=40);assert m is not None,'Native exited'
                trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ['fatal','ended','rejected'],m
                if m['type']!='request':continue
                r=m['request'];opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];view=m['views'][0];one,two=view['players'];pick=None
                snapshot(m,'step-'+str(step));native=observed(r) if stage or kind=='InputPassPriority' and view['phase']=='MAIN1' else {'damageEvents':[]}
                for v in m['views']:assert not v['players'][2-v['seat']]['hand'],'Private hand leaked'
                if kind=='InputPassPriority':
                    if stage==0 and r['seat']==1 and view['phase']=='MAIN1':
                        source_id=next(c['id'] for c in one['hand'] if c['name']==source);bear_ids=[c['id'] for c in two['battlefield'] if c['name']=='Grizzly Bears'];snapshot(m,'before')
                        if case=='hex-five' and 'card:'+str(source_id) not in ids:
                            assert len(bear_ids)==5 and not view['stackItems'];return finish('PASS','native cast availability excludes six-target spell with only five legal candidates; hand and priority preserved')
                        if case.startswith('arc'):shock_id=next(c['id'] for c in one['hand'] if c['name']=='Shock')
                        if case.startswith('redirect'):shock_id=next(c['id'] for c in one['hand'] if c['name']=='Redirect')
                        stage=1;pick='card:'+str(source_id)
                    elif stage and not view['stackItems']:
                        if case=='shock-reset-cancel' and stage==3:
                            assert source_id in {c['id'] for c in one['hand']} and reset==2 and one['life']==two['life']==30 and all(not c['tapped'] for c in one['battlefield'])
                            snapshot(m,'resolved');return finish('PASS','single target native draft, reset, toggle, cancel restores hand/untapped resources/empty stack and next priority')
                        if case=='hex-five' and stage==3:
                            assert source_id in {c['id'] for c in one['hand']} and len(bear_ids)==5;return finish('PASS','insufficient six-target selection cannot confirm; cancel restores hand and next priority')
                        if source_id in {c['id'] for c in one['graveyardCards']}:
                            if case.startswith('finale'):
                                assert not selected and all(b in {c['id'] for c in two['battlefield']} for b in bear_ids)
                                assert (any(c['name']=='Memnite' for c in one['battlefield']))==(case=='finale-ten-zero')
                                snapshot(m,'resolved');return finish('PASS','actual X cast/payment with zero targets; optional target confirmation and X-dependent non-targeted subeffect, next priority')
                            if case=='hex-six':
                                assert set(selected)==set(bear_ids) and toggle==2 and set(bear_ids)<={c['id'] for c in two['graveyardCards']}
                                snapshot(m,'resolved');return finish('PASS','six same-name distinct IDs, exact minimum/maximum confirm gate, deselect/reselect and all six destroyed, next priority')
                            if case.startswith('arc'):
                                events=[e for e in native['damageEvents'] if e['source']==source_id];shock=[e for e in native['damageEvents'] if e['source']==shock_id]
                                assert len(shock)==1 and shock[0]['target']==bear_ids[0] and shock[0]['amount']==2
                                if case=='arc-partial':assert len(events)==1 and events[0]['target']==bear_ids[1] and events[0]['amount']==2 and set(bear_ids)<={c['id'] for c in two['graveyardCards']},events
                                else:assert not events and bear_ids[1] in {c['id'] for c in two['battlefield']},events
                                snapshot(m,'resolved');return finish('PASS','actual divided spell '+str(allocated)+' then responding Shock; exact damage IDs prove '+('remaining legal target receives its original allocation' if case=='arc-partial' else 'all invalid targets stop damage')+'; next priority')
                            if case.startswith('redirect'):
                                assert shock_id in {c['id'] for c in one['graveyardCards']} and redirect_choice
                                events=[e for e in native['damageEvents'] if e['source']==source_id];assert len(events)==1 and events[0]['amount']==2
                                expected=one if case=='redirect-change' else two;assert events[0]['target']==expected['targetId'] and expected['life']==28 and (two if case=='redirect-change' else one)['life']==30
                                snapshot(m,'resolved');return finish('PASS','actual Shock and Redirect stack-target draft/confirm; '+('retarget to original caster' if case=='redirect-change' else 'decline keeps original target')+' with exact player damage/life and next priority')
                    elif case.startswith(('arc','redirect')) and stage==1 and r['seat']==1 and any(s.get('source',{}).get('id')==source_id for s in view['stackItems']):
                        stage=2;snapshot(m,'respond-before-invalidation');pick='card:'+str(shock_id)
                    if pick is None:pick='ok'
                elif 'SelectTargets' in kind:
                    target_owner=r['sourceCardId'];count=r.get('selectedCount',0);snapshot(m,'target-'+str(target_owner)+'-'+str(count))
                    if case.startswith('finale'):
                        assert r['minCount']==0 and r['maxCount']==3 and count==0 and 'ok' in ids;pick='ok'
                    elif case.startswith('hex'):
                        assert r['minCount']==6 and r['maxCount']==6 and ('ok' in ids)==(count>=6),r
                        candidates=[o for o in opts if o.get('card',{}).get('id') in bear_ids];assert len(candidates)==len(bear_ids)
                        if case=='hex-five':stage=3;pick='cancel'
                        elif count==1 and toggle==0:pick='card:'+str(selected.pop());toggle=1
                        elif count==0 and toggle==1:toggle=2;pick='card:'+str(bear_ids[0]);selected.append(bear_ids[0])
                        elif count==6:pick='ok'
                        else:chosen=next(b for b in bear_ids if b not in selected);selected.append(chosen);pick='card:'+str(chosen)
                    elif case=='shock-reset-cancel':
                        if count==0 and reset==0:pick='card:'+str(bear_ids[0]);reset=1
                        elif count==1 and reset==1:assert 'reset-targets' in ids;pick='reset-targets';reset=2
                        elif count==0 and reset==2:pick='card:'+str(bear_ids[1]);reset=3
                        else:assert count==1;pick='cancel';stage=3;reset=2
                    elif target_owner==shock_id:
                        pick='ok' if count else 'card:'+str(bear_ids[0])
                    elif case.startswith('redirect'):
                        pick='ok' if count else 'player:'+str((two if stage==1 else one)['targetId'])
                    elif count==r['maxCount'] or 'ok' in ids and len(selected)==(2 if case=='arc-partial' else 1):pick='ok'
                    else:
                        chosen=bear_ids[len(selected)];pick='card:'+str(chosen);selected.append(chosen)
                elif 'PayMana' in kind:
                    ok=next((o for o in opts if o['id']=='ok'),None);pick='ok' if ok and not ok.get('disabled') else next(o['id'] for o in opts if o['id'].startswith('card:'))
                elif kind=='AssignAmount':
                    assert case=='arc-partial' and r['selectionPurpose']=='genericAmount' and r['sourceCardId']==source_id and r['maxCount']==3
                    assert [a['targetId'] for a in r['allocations']]==bear_ids and all(a['amount']>=1 for a in r['allocations'])
                    assert ('confirm-amount' in ids)==(r['remainingCount']==0) and not any(i.startswith('amount-sub:') for i in ids if r['selectedCount']==2)
                    if r['remainingCount']==0 and not allocation_reset:allocation_reset=True;pick='reset-amount';snapshot(m,'division-before-reset')
                    elif r['remainingCount']:pick='amount-add:1';snapshot(m,'division-minimum')
                    else:allocated=[a['amount'] for a in r['allocations']];assert allocated==[1,2] and allocation_reset;pick='confirm-amount';snapshot(m,'division-confirm')
                elif kind=='SelectStackTarget' and case.startswith('redirect'):
                    target=next(s for s in view['stackItems'] if s['source']['id']==source_id);pick='ok' if r['selectedCount'] else 'stack:'+str(target['id']);snapshot(m,'stack-target-'+str(r['selectedCount']))
                elif kind=='InputConfirm' and case.startswith('redirect'):
                    assert 'target' in r['message'].lower(),r;redirect_choice=True;pick='ok' if case=='redirect-change' else 'cancel';snapshot(m,'retarget-optional')
                elif kind=='ChooseNumber' or kind.startswith('InputChooseX'):
                    value=10 if case=='finale-ten-zero' else 0
                    current=r.get('selectedValue',0);pick='confirm-number' if current==value else 'number-plus' if current<value else 'number-minus'
                elif kind=='dialog' and any('damage to deal' in r['message'] for _ in [0]):
                    value=1 if case=='arc-partial' and len(allocated)==0 else 2 if case=='arc-partial' else 3
                    pick=next(o['id'] for o in opts if o['label']==str(value));allocated.append(value);snapshot(m,'division-'+str(len(allocated)))
                elif kind=='dialog' and 'targets' in r['message'].lower():
                    value=2 if case=='arc-partial' else 1;pick=next(o['id'] for o in opts if o['label']==str(value))
                elif kind=='InputMulligan':pick='keep'
                elif kind=='OrderAbilities':pick='confirm-order'
                elif kind=='dialog':pick=ids[0]
                elif 'ok' in ids:pick='ok'
                else:raise AssertionError(('Unhandled request',r))
                assert pick in ids,(case,stage,kind,pick,r)
                print(case,step,kind,repr(r.get('message','')[:90]),pick,flush=True)
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
