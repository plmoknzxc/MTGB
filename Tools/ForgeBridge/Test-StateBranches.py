"""Real Recruit, Blight and Intensify operations; fixture audit is read-only."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime'
CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
OUT=ROOT/'.tools/state-branches-1006/native';OUT.mkdir(parents=True,exist_ok=True)
CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURE=ROOT/'Tools/ForgeBridge/tests/StateBranchFixture.java'
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),str(FIXTURE)],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES=['recruit-land','recruit-nonland','recruit-seat-two','recruit-no-draw','blight-select','blight-forced','blight-death','blight-ineligible','intensity-all','intensity-self']

def run(case):
    actor=2 if case=='recruit-seat-two' else 1
    source='Esgaroth Garrison' if case.startswith('recruit') else 'Dose of Dawnglow' if case=='blight-ineligible' else 'Sinister Gnarlbark' if case.startswith('blight') else 'Runaway Growth' if case=='intensity-self' else 'Static Discharge'
    own={'Hand':[],'Battlefield':[],'Library':['Mountain']};other={}
    if case.startswith('recruit'):
        own['Hand']=[source]+([] if case=='recruit-no-draw' else ['Shock','Shock','Mountain'])
        if case=='recruit-no-draw':own['Battlefield']=['Maralen of the Mornsong']
    elif case=='blight-ineligible':
        own.update(Hand=[source],Battlefield=['Melira, Sylvok Outcast'],Graveyard=['Grizzly Bears'])
    elif case.startswith('blight'):
        own['Battlefield']=[source]+(['Grizzly Bears','Runeclaw Bear'] if case=='blight-select' else ['Memnite'] if case=='blight-death' else [])
        other['Battlefield']=['Grizzly Bears']
    elif case=='intensity-all':
        own.update(Hand=[source,source],Library=[source],Graveyard=[source],Exile=[source]);other.update(Hand=[source],Library=[source])
    else:own['Hand']=[source,'Shock']
    folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True)
    meta={'case':case,'folder':str(folder.relative_to(ROOT)).replace('\\','/'),'runtimeHash':CFG['runtimeHash'],'fixtureSha256':hashlib.sha256(FIXTURE.read_bytes()).hexdigest(),'driverSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest()}
    def save_result(result):
        record={**meta,**result};(folder/'result.json').write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');(OUT/(case+'-result.json')).write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');return record
    with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'trace.jsonl').open('w',encoding='utf8') as trace,(folder/'engine.log').open('w',encoding='utf8') as log:
        audit=Path(temp)/'audit';audit.mkdir();start=Path(temp)/'start.json'
        start.write_text(json.dumps(dict(session=case,actor=actor,own=own,other=other,auditDir=str(audit),seed=4,firstPlayerSeat=2 if case=='blight-ineligible' else actor,startingLife=30,decks=[DECK,DECK])),encoding='utf8')
        p=subprocess.Popen([str(RT/CFG['java']),'-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.StateBranchFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
        messages=queue.Queue()
        def read():
            try:
                for line in p.stdout:messages.put(json.loads(line))
            finally:messages.put(None)
        threading.Thread(target=read,daemon=True).start()
        def snap(m,label):
            v=dict(m['views'][actor-1]);v.update(request=m['request'],awaitingInput=True)
            (folder/(label+'.json')).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf8')
        def native_audit(r):
            path=audit/(str(r['requestId'])+'.json');deadline=time.monotonic()+5
            while time.monotonic()<deadline:
                try:
                    d=json.loads(path.read_text('utf8'));assert d['requestId']==r['requestId'];break
                except (FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
            else:raise AssertionError('No complete native audit')
            (folder/('audit-'+str(r['requestId'])+'.json')).write_text(json.dumps(d,indent=2),encoding='utf8');return d['cards']
        def check_intensity_projection(m,a):
            expected={c['id']:c for c in a}
            for v in m['views']:
                for player in v['players']:
                    for zone in ['hand','battlefield','graveyardCards','exileCards']:
                        for card in player.get(zone,[]):
                            if card['id'] not in expected or card.get('faceDown'):continue
                            native=expected[card['id']]
                            if native['name'] in ['Static Discharge','Runaway Growth']:
                                assert card.get('hasIntensity') and card['intensity']==native['intensity'],(card,native)
                                assert card['originalIntensity']==(3 if native['name']=='Static Discharge' else 1),(card,native)
                            else:assert not card.get('hasIntensity'),card
                assert not v['players'][2-v['seat']]['hand'],'Intensity projection leaked private hand'
        stage=0;casts=0;choices=0;selected=None;initial=None;start_turn=None;first_intensity=None;first_target=None;first_pool=None;deadline=time.monotonic()+180
        try:
            for step in range(550):
                assert time.monotonic()<deadline,'Deadline';m=messages.get(timeout=40);assert m is not None,'Native exited'
                trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ('fatal','ended','rejected'),m
                if m['type']!='request':continue
                r=m['request'];opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];view=m['views'][actor-1];ownv=view['players'][actor-1];opp=view['players'][2-actor];pick='ok'
                a=native_audit(r) if stage or kind=='InputPassPriority' and view['phase']=='MAIN1' else None
                if case.startswith('intensity') and a is not None:check_intensity_projection(m,a)
                snap(m,'step-'+str(step))
                if kind=='InputPassPriority' and not view['stackItems']:
                    if stage==0 and r['seat']==actor and view['phase']=='MAIN1':
                        initial=ownv;start_turn=view['turn'];first_intensity=a;snap(m,'before');stage=1
                        if not case.startswith('blight') or case=='blight-ineligible':
                            pick='card:'+str(next(c for c in ownv['hand'] if c['name']==source)['id']);casts=1
                            continue_send=True
                        else:continue_send=False
                    else:continue_send=False
                    if not continue_send and stage==1 and case.startswith('recruit') and any(c['name']==source for c in ownv['battlefield']):
                        grave=ownv['graveyardCards'];soldiers=[c for c in ownv['battlefield'] if c.get('token') and 'Soldier' in c.get('currentType','')]
                        if case=='recruit-no-draw':assert ownv['handCount']==0 and not soldiers and choices==0
                        else:
                            assert choices==1 and any(c['id']==selected for c in grave)
                            assert ownv['handCount']==initial['handCount']-1
                            assert len(soldiers)==(0 if case=='recruit-land' else 1)
                            for c in soldiers:assert c['power']==1 and c['toughness']==1 and c['ownerSeat']==actor
                        assert not m['views'][2-actor]['players'][actor-1]['hand'],'Private hand leaked';snap(m,'resolved');return save_result({'status':'PASS','result':'PASS '+case+': actual creature cast/payment/ETB recruit; exact discard identity or native blocked draw; token outcome, private hand and next priority'})
                    elif not continue_send and stage==1 and case.startswith('blight'):
                        if case=='blight-ineligible' and any(c['name']=='Grizzly Bears' for c in ownv['battlefield']) and any(c['name']==source for c in ownv['graveyardCards']):
                            assert choices==0 and all(not c.get('counters') for c in ownv['battlefield']);snap(m,'resolved');return save_result({'status':'PASS','result':'PASS '+case+': actual off-turn reanimation/payment; all own creatures ineligible for -1/-1, no Blight choice or counters, next priority'})
                        if case!='blight-ineligible' and ownv['handCount']>initial['handCount']:
                            field={c['name']:c for c in ownv['battlefield']}
                            if case=='blight-select':assert choices==1 and field['Runeclaw Bear']['power']==1 and field['Runeclaw Bear']['toughness']==1 and field['Grizzly Bears']['power']==2
                            elif case=='blight-forced':assert choices==0 and field[source]['power']==-1 and field[source]['toughness']==3
                            else:assert choices==1 and 'Memnite' not in field and any(c['id']==selected for c in ownv['graveyardCards'])
                            assert opp['battlefield'][0]['power']==2;snap(m,'resolved');return save_result({'status':'PASS','result':'PASS '+case+': actual end-step draw/Blight trigger; exact own candidate or forced singleton, counter/body/death result, opponent untouched and next priority'})
                    elif not continue_send and stage==1 and case=='intensity-all' and any(c['name']==source for c in ownv['graveyardCards']):
                        originals={c['id']:c for c in first_intensity if c['name']==source};now={c['id']:c for c in a if c['name']==source}
                        assert set(now)==set(originals)
                        assert all(c['intensity']==(3+casts if c['seat']==actor else 3) for c in now.values())
                        assert opp['life']==30-(3 if casts==1 else 7)
                        snap(m,'intensified-'+str(casts))
                        if casts==1:pick='card:'+str(next(c for c in ownv['hand'] if c['name']==source)['id']);casts=2
                        else:snap(m,'resolved');return save_result({'status':'PASS','result':'PASS '+case+': two actual targeted/pay spells deal 3 then 4; all exact owned hand/library/grave/exile IDs intensify 3->4->5, opponent copies unchanged, next priority'})
                    elif not continue_send and stage==1 and case=='intensity-self' and any(c['name']==source for c in ownv['battlefield']):
                        if first_target is None:first_target=next(c['id'] for c in ownv['battlefield'] if c['name']=='Forest' and not c['tapped']);first_pool=ownv['manaPool'][4]
                        target=next(c for c in ownv['battlefield'] if c['id']==first_target)
                        if not target['tapped']:pick='card:'+str(first_target);stage=2;snap(m,'mana-before')
                    elif stage==2 and case=='intensity-self':
                        aura=next(c for c in a if c['name']==source);assert aura['intensity']==2
                        assert ownv['manaPool'][4]==first_pool+2;snap(m,'resolved');return save_result({'status':'PASS','result':'PASS '+case+': actual aura cast/target/pay, native enchanted Forest tap yields extra green and self intensity 1->2, next priority'})
                elif kind.startswith('InputSelectTargets'):
                    if r.get('selectedCount') and 'ok' in ids:pick='ok'
                    elif case=='intensity-all':pick=next(o['id'] for o in opts if o['label']=='Player '+str(3-actor))
                    elif case=='intensity-self':
                        target=[o for o in opts if o.get('card',{}).get('name')=='Forest'][-1];first_target=target['card']['id'];first_pool=0;pick=target['id']
                    else:pick=next(o['id'] for o in opts if o.get('card',{}).get('name')=='Grizzly Bears')
                elif case.startswith('recruit') and ('SelectCards' in kind or kind=='SelectCards'):
                    if r.get('selectedCount') and 'ok' in ids:pick='ok'
                    elif r.get('selectedCount') and 'confirm-cards' in ids:pick='confirm-cards'
                    else:
                        choices+=1;snap(m,'choice');cards=[o for o in opts if o.get('card',{}).get('name')];chosen=next(o for o in cards if o['card']['name']==('Mountain' if case=='recruit-land' else 'Shock'));selected=chosen['card']['id'];pick=chosen['id']
                elif case.startswith('blight') and (kind=='SelectCards' or 'SelectEntities' in kind):
                    if r.get('selectedCount') and 'ok' in ids:pick='ok'
                    elif r.get('selectedCount') and 'confirm-cards' in ids:pick='confirm-cards'
                    else:
                        choices+=1;snap(m,'choice');name='Memnite' if case=='blight-death' else 'Runeclaw Bear';chosen=next(o for o in opts if o.get('card',{}).get('name')==name);selected=chosen['card']['id'];pick=chosen['id']
                elif 'PayMana' in kind:pick='ok' if 'ok' in ids and not next(o for o in opts if o['id']=='ok').get('disabled') else next(o['id'] for o in opts if o['id'].startswith('card:'))
                elif kind=='InputMulligan':pick='keep'
                elif kind=='OrderAbilities':pick='confirm-order'
                elif kind=='dialog':pick=ids[0]
                elif 'ok' not in ids:pick=ids[0]
                assert pick in ids,(case,stage,kind,pick,opts)
                print(case,step,kind,pick,flush=True);p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
            raise AssertionError('Step limit')
        except Exception as error:save_result({'status':'FAIL','result':'FAIL '+case,'error':repr(error)});raise
        finally:
            if p.poll() is None:p.kill();p.wait()

if __name__=='__main__':
    results=[]
    for case in sys.argv[1:] or CASES:
        try:result=run(case);print(result['result'],flush=True);results.append(result)
        except Exception as error:print('FAIL '+case+': '+repr(error),flush=True);results.append(json.loads((OUT/(case+'-result.json')).read_text('utf-8')))
    (OUT/'results.json').write_text(json.dumps(dict(status='PASS' if all(x['status']=='PASS' for x in results) else 'FAIL',results=results),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    sys.exit(any(x['status']!='PASS' for x in results))
