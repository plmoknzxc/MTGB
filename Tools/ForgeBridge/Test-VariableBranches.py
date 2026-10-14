"""Actual StoreSVar consumers: seven calculation forms, choices and downstream results."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime'
RUNTIME=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
OUT=ROOT/'.tools/variable-branches-1006/native';OUT.mkdir(parents=True,exist_ok=True)
CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURES=[ROOT/'Tools/ForgeBridge/tests'/name for name in ['StateBranchFixture.java','VariableBranchFixture.java']]
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),*map(str,FIXTURES)],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES=['slaughter-single','slaughter-delirium','drain-creature','drain-player','bell-trigger-reset','doodle-match','doodle-mismatch','join-forces','vermin-repeat']

def run(case):
    own={'Hand':[],'Battlefield':[],'Library':['Opt']};other={'Battlefield':[],'Library':['Mountain']}
    if case.startswith('slaughter'):
        source='To the Slaughter';own['Hand']=[source];other['Battlefield']=['Grizzly Bears','Jace Beleren']
        if case=='slaughter-delirium':own['Graveyard']=['Shock','Grizzly Bears','Forest','Ornithopter']
    elif case.startswith('drain'):
        source='Drain Life';own['Hand']=[source];other['Battlefield']=['Grizzly Bears']
    elif case=='bell-trigger-reset':
        source='Bell Borca, Spectral Sergeant';own['Battlefield']=[source];own['Hand']=['Swords to Plowshares','Swords to Plowshares'];other['Battlefield']=['Grizzly Bears','Memnite']
    elif case.startswith('doodle'):source='Buzzing Whack-a-Doodle';own['Hand']=[source]
    elif case=='join-forces':source='Alliance of Arms';own['Hand']=[source]
    else:source='Plague of Vermin';own['Hand']=[source]
    folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True)
    hash_file=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
    meta={'case':case,'folder':folder.relative_to(ROOT).as_posix(),'runtimeHash':RUNTIME['runtimeHash'],'driverSha256':hash_file(Path(__file__)),'fixtures':[{'path':p.relative_to(ROOT).as_posix(),'sha256':hash_file(p)} for p in FIXTURES]}
    def finish(status,message):
        result={**meta,'status':status,'result':status+' '+case+': '+message}
        for p in [folder/'result.json',OUT/(case+'-result.json')]:p.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        return result
    with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as tmp,(folder/'trace.jsonl').open('w',encoding='utf-8') as trace,(folder/'engine.log').open('w',encoding='utf-8') as log:
        temp=Path(tmp);audit=temp/'audit';audit.mkdir();start=temp/'start.json'
        start.write_text(json.dumps(dict(session=case,actor=1,own=own,other=other,auditDir=str(audit),seed=4,firstPlayerSeat=1,startingLife=30,decks=[DECK,DECK])),encoding='utf-8')
        process=subprocess.Popen([str(RT/RUNTIME['java']),'-Djava.awt.headless=true','-Djava.io.tmpdir='+tmp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.VariableBranchFixture',str(RT/RUNTIME['assets']),str(start)],cwd=RT/RUNTIME['assets'],env=dict(os.environ,APPDATA=tmp,LOCALAPPDATA=tmp,TEMP=tmp,TMP=tmp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf-8')
        messages=queue.Queue()
        def reader():
            try:
                for line in process.stdout:messages.put(json.loads(line))
            finally:messages.put(None)
        threading.Thread(target=reader,daemon=True).start()
        def snapshot(m,label):
            for seat,v in enumerate(m['views'],1):
                v=dict(v);v.update(request=m['request'],awaitingInput=True)
                (folder/(label+('-other' if seat==2 else '')+'.json')).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
        def observed(request):
            path=audit/(str(request['requestId'])+'.json');deadline=time.monotonic()+5
            while time.monotonic()<deadline:
                try:
                    value=json.loads(path.read_text('utf-8'));assert value['requestId']==request['requestId'];break
                except (FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
            else:raise AssertionError('Missing native audit')
            (folder/('audit-'+str(request['requestId'])+'.json')).write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding='utf-8');return value
        stage=0;casts=0;source_id=None;targets=[];choice_seats=[];number_seats=[];number_values=[];initial=None;deadline=time.monotonic()+240
        activated=False;selected_sacrifices=[];initial_opponent_ids=[];sacrifice_variable_seen=False;drain_payment_seen=False;join_intermediate_seen=False;vermin_totals_seen=False
        try:
            for step in range(1000):
                assert time.monotonic()<deadline,'Deadline';m=messages.get(timeout=40);assert m is not None,'Native exited'
                trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ['fatal','ended','rejected'],m
                if m['type']!='request':continue
                r=m['request'];opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];view=m['views'][0];one,two=view['players'];pick=None
                snapshot(m,'step-'+str(step));audit_value=observed(r) if stage or kind=='InputPassPriority' and view['phase']=='MAIN1' else {'cards':[],'stack':[],'damageEvents':[]};cards=audit_value['cards']
                if case.startswith('drain'):
                    for paid in audit_value['stack']:
                        if paid['id']==source_id:
                            assert paid['x']==4 and len(paid['mana'])==6 and paid['mana'].count('B')>=5,paid
                            drain_payment_seen=True
                for v in m['views']:
                    assert not v['players'][2-v['seat']]['hand'],'Private hand leaked'
                if kind=='InputPassPriority' and not view['stackItems']:
                    if stage==0 and r['seat']==1 and view['phase']=='MAIN1':
                        initial=one;snapshot(m,'before');stage=1
                        source_id=next(c['id'] for c in one['battlefield' if case=='bell-trigger-reset' else 'hand'] if c['name']==source)
                        initial_opponent_ids=[c['id'] for c in two['battlefield'] if c['name'] in ['Grizzly Bears','Jace Beleren']]
                        castname='Swords to Plowshares' if case=='bell-trigger-reset' else source
                        pick='card:'+str(next(c['id'] for c in one['hand'] if c['name']==castname));casts=1
                    elif stage:
                        native=next((c for c in cards if c['id']==source_id),None)
                        resolved=any(c['id']==source_id for c in one['graveyardCards']) or case.startswith('doodle') and any(c['id']==source_id for c in one['battlefield'])
                        if case.startswith('slaughter') and resolved:
                            assert sacrifice_variable_seen,'No native in-resolution Count value'
                            assert native['vars']['XDelirium']=='Number$0','Moved card should return to printed variable state'
                            names={c['name'] for c in two['battlefield']};grave={c['id'] for c in two['graveyardCards']}
                            assert set(selected_sacrifices)<=grave
                            assert (set(initial_opponent_ids)<=grave)==(case=='slaughter-delirium')
                            assert 'Grizzly Bears' not in names and ('Jace Beleren' not in names)==(case=='slaughter-delirium')
                            snapshot(m,'resolved');return finish('PASS','actual cast/payment/target/sacrifice; Count delirium stored and one versus creature+planeswalker result, exact IDs, next priority')
                        if case.startswith('drain') and resolved:
                            assert casts==1 and len(targets)==1 and drain_payment_seen and one['life']==(32 if case=='drain-creature' else 34)
                            damages=[e for e in audit_value['damageEvents'] if e['source']==source_id];assert len(damages)==1 and damages[0]['amount']==4,damages
                            if case=='drain-creature':assert damages[0]['vars']['Limit']=='Number$2' and damages[0]['target']==targets[0] and targets[0] in {c['id'] for c in two['graveyardCards']} and two['life']==30
                            else:assert damages[0]['vars']['Limit']=='Number$30' and two['life']==26 and any(c['name']=='Grizzly Bears' for c in two['battlefield'])
                            snapshot(m,'resolved');return finish('PASS','actual X=4 black-only cast/payment; native pre-damage limit and damage/life result, target identity, next priority')
                        if case=='bell-trigger-reset' and casts:
                            bell=next(c for c in one['battlefield'] if c['id']==source_id)
                            if len(two['exileCards'])==casts:
                                if casts==1:
                                    assert bell['power']==2 and native['vars']['ExiledCMC']=='Number$2';snapshot(m,'noted-two');pick='card:'+str(next(c['id'] for c in one['hand'] if c['name']=='Swords to Plowshares'));casts=2
                                elif view['turn']==1:
                                    assert bell['power']==2 and native['vars']['ExiledCMC']=='Number$2';snapshot(m,'lower-not-remembered')
                                elif view['turn']>1:
                                    assert bell['power']==0 and native['vars']['ExiledCMC']=='Number$0' and set(targets)=={c['id'] for c in two['exileCards']};snapshot(m,'resolved');return finish('PASS','two actual exile spells remember greatest mana value, lower value does not replace it; native turn-begin Number reset and power zero, exact exile IDs, next priority')
                        if case.startswith('doodle') and resolved:
                            card=next(c for c in one['battlefield'] if c['id']==source_id)
                            assert choice_seats==[1,2],choice_seats
                            assert native['vars']['YouChose']==('Number$1' if case=='doodle-match' else 'Number$0'),native
                            if not activated:
                                snapshot(m,'abilities');initial=one;pick='card:'+str(source_id);activated=True
                            elif card['tapped']:
                                if case=='doodle-match':assert one['life']==33 and two['life']==30 and one['handCount']==initial['handCount']
                                else:assert one['life']==30 and two['life']==30 and one['handCount']==initial['handCount']+1
                                snapshot(m,'resolved');return finish('PASS','actual enter choices in both seats; Number/CountSVar secret selection yields correct granted ability; real tap/payment activation and life/draw result, next priority')
                        if case=='join-forces' and resolved:
                            assert number_values==[2,3] and number_seats==[1,2],(number_values,number_seats)
                            assert join_intermediate_seen,'No native intermediate payment sum'
                            assert native['vars']['JoinForcesAmount']=='Number$0','Moved card resets printed variable'
                            for player in [one,two]:
                                soldiers=[c for c in player['battlefield'] if c.get('token') and 'Soldier' in c.get('currentType','')];assert len(soldiers)==5 and all(c['power']==1 and c['toughness']==1 and c['ownerSeat']==player['seat'] for c in soldiers)
                            snapshot(m,'resolved');return finish('PASS','actual spell and both seats choose/pay 2+3 mana; CountSVar dynamic addition survives repeat/subchain, five correct-owner Soldiers per seat, next priority')
                        if case=='vermin-repeat' and resolved:
                            assert number_values==[2,1,3,0,0,0] and number_seats==[1,2,1,2,1,2],(number_values,number_seats)
                            assert one['life']==25 and two['life']==29 and vermin_totals_seen
                            for player,count in [(one,5),(two,1)]:
                                rats=[c for c in player['battlefield'] if c.get('token') and 'Rat' in c.get('currentType','')];assert len(rats)==count and all(c['power']==1 and c['toughness']==1 and c['ownerSeat']==player['seat'] for c in rats)
                            assert native['vars']['NumPlayerGiveup']=='Number$0' and all(native['vars'].get('Player '+str(s)) in [None,'Number$0'] for s in [1,2]),native
                            snapshot(m,'resolved');return finish('PASS','actual repeat life choices/payment, per-player additive totals 5/1, stop only all decline, correct Rat owners and final accumulator reset, next priority')
                    if pick is None:pick='ok'
                elif 'SelectTargets' in kind:
                    if r.get('selectedCount') and 'ok' in ids:pick='ok'
                    elif case.startswith('slaughter') or case=='drain-player':
                        pick=next(o['id'] for o in opts if o['label']=='Player 2')
                        if case=='drain-player':targets.append(pick)
                    else:
                        name='Grizzly Bears' if case.startswith('drain') or casts==1 else 'Memnite';chosen=next(o for o in opts if o.get('card',{}).get('name')==name);targets.append(chosen['card']['id']);pick=chosen['id']
                elif 'PayMana' in kind:
                    ok=next((o for o in opts if o['id']=='ok'),None);pick='ok' if ok and not ok.get('disabled') else next(o['id'] for o in opts if o['id'].startswith('card:'))
                elif kind=='ChooseNumber' or kind.startswith('InputChooseX') or case=='vermin-repeat' and kind=='dialog' and all(o['label'].isdigit() for o in opts):
                    if case.startswith('drain'):value=4
                    elif case=='join-forces':value=[2,3][len(number_values)]
                    elif case=='vermin-repeat':value=[2,1,3,0,0,0][len(number_values)]
                    else:raise AssertionError(('Unexpected number',kind,r))
                    if case=='join-forces' and len(number_values)==1:
                        variable=next(c for c in cards if c['id']==source_id)['vars'];assert variable['JoinForcesAmount']=='Number$2',variable;join_intermediate_seen=True
                    if case=='vermin-repeat' and len(number_values)==4:
                        variable=next(c for c in cards if c['id']==source_id)['vars'];assert variable.get('Player 1')=='Number$5' and variable.get('Player 2')=='Number$1',variable;vermin_totals_seen=True
                    if 'confirm-number' in ids:
                        current=r['selectedValue'];pick='confirm-number' if current==value else 'number-plus' if current<value else 'number-minus'
                    else:pick=next(o['id'] for o in opts if o['label']==str(value) or o['id']=='number:'+str(value))
                    if pick=='confirm-number' or 'confirm-number' not in ids:
                        if case in ['join-forces','vermin-repeat']:number_values.append(value);number_seats.append(r['seat'])
                        snapshot(m,'number-'+str(len(number_values)))
                elif case.startswith('slaughter') and ('SelectCards' in kind or 'SelectEntities' in kind):
                    in_resolution=next(c for c in cards if c['id']==source_id);assert in_resolution['vars']['XDelirium']==('Number$1' if case=='slaughter-delirium' else 'Number$0'),in_resolution;sacrifice_variable_seen=True
                    if r.get('selectedCount',0)>=r.get('minCount',1) and ('ok' in ids or 'confirm-cards' in ids):pick='ok' if 'ok' in ids else 'confirm-cards'
                    else:
                        chosen=next(o for o in opts if o.get('card',{}).get('name') in ['Grizzly Bears','Jace Beleren'] and not o.get('selected'))
                        pick=chosen['id'];selected_sacrifices.append(chosen['card']['id']);snapshot(m,'sacrifice-'+str(len(selected_sacrifices)))
                elif case.startswith('doodle') and kind=='ChooseOptions' and any(o['label'].rstrip().endswith((' — Whack',' — Doodle')) for o in opts):
                    if 'confirm-cards' in ids:pick='confirm-cards'
                    else:
                        label='Doodle' if case=='doodle-match' or r['seat']==2 else 'Whack';pick=next(o['id'] for o in opts if o['label'].rstrip().endswith(' — '+label));choice_seats.append(r['seat']);snapshot(m,'secret-'+str(r['seat']))
                elif kind=='InputMulligan':pick='keep'
                elif kind=='OrderAbilities':pick='confirm-order'
                elif kind=='dialog':pick=ids[0]
                elif 'ok' in ids:pick='ok'
                else:pick=ids[0]
                assert pick in ids,(case,stage,kind,pick,opts)
                print(case,step,kind,pick,flush=True);process.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');process.stdin.flush()
            raise AssertionError('Step limit')
        except Exception as error:finish('FAIL',traceback.format_exc());raise
        finally:
            if process.poll() is None:process.kill();process.wait()

if __name__=='__main__':
    results=[]
    for case in sys.argv[1:] or CASES:
        try:r=run(case);print(r['result'],flush=True);results.append(r)
        except Exception as error:print('FAIL '+case+': '+repr(error),flush=True);results.append(json.loads((OUT/(case+'-result.json')).read_text('utf-8')))
    (OUT/'results.json').write_text(json.dumps(dict(status='PASS' if all(r['status']=='PASS' for r in results) else 'FAIL',results=results),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    sys.exit(any(r['status']!='PASS' for r in results))
