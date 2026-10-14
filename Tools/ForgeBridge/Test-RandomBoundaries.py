"""Real native costs, turn limits and zone/duration boundaries; no forced outcomes."""
import json,os,queue,re,shutil,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'Tools/Validation/Reports/RandomBoundaries';CLASSES=ROOT/'.tools/random-boundaries/classes'
if (OUT/'result.txt').exists():shutil.copytree(OUT,ROOT/'.tools/random-boundaries/attempts'/str(time.time_ns()))
OUT.mkdir(parents=True,exist_ok=True);CLASSES.mkdir(parents=True,exist_ok=True)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),*[str(ROOT/'Tools/ForgeBridge/tests'/f) for f in ('RandomMechanicFixture.java','RandomBoundaryFixture.java')]],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES=['monitor-no-mana','monitor-cancel-retry','monitor-limit','night-limit','night-no-pay','xeno-last','swap-duration']
def run(case):
    with tempfile.TemporaryDirectory(prefix='mtgb-random-boundary-') as temp,(OUT/(case+'.log')).open('w',encoding='utf8') as log,(OUT/(case+'.jsonl')).open('w',encoding='utf8') as trace:
        audit=Path(temp)/'audit';audit.mkdir();start=Path(temp)/'start.json'
        start.write_text(json.dumps(dict(session=case,fixture=case,auditDir=str(audit),seed=4,startingLife=20,firstPlayerSeat=1,decks=[DECK,DECK])),encoding='utf8')
        p=subprocess.Popen([str(RT/CFG['java']),'-Djava.awt.headless=true','-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.RandomBoundaryFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
        messages=queue.Queue()
        def read():
            try:
                for line in p.stdout:messages.put(json.loads(line))
            except Exception as e:messages.put(e)
            finally:messages.put(None)
        threading.Thread(target=read,daemon=True).start()
        def snapshot(m,label):
            v=dict(m['views'][0]);v.update(request=m['request'] if m['request']['seat']==1 else None,awaitingInput=m['request']['seat']==1)
            (OUT/(case+'-'+label+'.json')).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf8')
        def proof(r,label):
            data=[]
            for prefix in ('','limits-'):
                path=audit/(prefix+str(r['requestId'])+'.json');limit=time.monotonic()+5
                while not path.exists() and time.monotonic()<limit:time.sleep(.01)
                assert path.exists(),'Missing read-only proof'
                d=json.loads(path.read_text());assert d['requestId']==r['requestId'];data.append(d)
                (OUT/(case+'-'+label+'-'+('limits' if prefix else 'audit')+'.json')).write_text(json.dumps(d,indent=2),encoding='utf8')
            return data[0]['players'],data[1]['cards']
        stage=0;spell_no=0;source_id=None;hand_before=None;library_before=None;paid=0;roll_count=0;roll_values=[];increment_count=0;reroll_rounds=0;cancelled=0;original_roll=None;scry_count=0;whacker_id=None;fly_id=None;deadline=time.monotonic()+230
        def start_spell(m,one,r):
            nonlocal spell_no,source_id,hand_before,library_before,paid,roll_count,roll_values,scry_count
            card=next(c for c in one['hand'] if c['name']=='Contact Other Plane')
            source_id=card['id'];hand_before={c['id'] for c in one['hand']};state,limits=proof(r,'before-'+str(spell_no+1));library_before=state[0]['library'];roll_count=state[0]['rolled'];roll_values=state[0]['rolls'];paid=0;scry_count=0;spell_no+=1
            snapshot(m,'before-'+str(spell_no));return 'card:'+str(source_id)
        try:
            for step in range(850):
                assert time.monotonic()<deadline,'Deadline';m=messages.get(timeout=40);assert m is not None,'Bridge exited'
                if isinstance(m,Exception):raise m
                trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ('fatal','ended','rejected'),m
                if m['type']!='request':continue
                r=m['request'];v=m['views'][0];one=v['players'][0];ids=[o['id'] for o in r['options']];kind=r['kind'];pick='ok';field={c['name']:c for c in one['battlefield']}
                assert not m['views'][1]['players'][0]['hand'],'Private hand leak'
                if kind=='InputPassPriority' and not v['stackItems']:
                    if stage==0 and r['seat']==1 and v['phase']=='MAIN1':
                        if case=='swap-duration':
                            whacker_id=field['Vedalken Squirrel-Whacker']['id'];fly_id=field['Ebony Fly']['id'];source_id=fly_id;pick='card:'+str(fly_id)
                        else:pick=start_spell(m,one,r)
                        stage=1
                    elif case=='swap-duration':
                        if stage==1 and field['Ebony Fly'].get('creature'):
                            state,_=proof(r,'animated');assert paid==4 and state[0]['rolled']==1 and state[0]['rolls']==[4]
                            assert field['Ebony Fly']['id']==fly_id and field['Ebony Fly']['power']==field['Ebony Fly']['toughness']==4
                            assert field['Vedalken Squirrel-Whacker']['power']==original_roll and field['Vedalken Squirrel-Whacker']['toughness']==5
                            snapshot(m,'animated');stage=2
                        elif stage==2 and v['turnSeat']==2 and v['phase']=='MAIN1':
                            assert field['Ebony Fly']['id']==fly_id and not field['Ebony Fly']['creature'] and 'FLYING' not in field['Ebony Fly']['keywords'] and 'FLYING' not in field['Ebony Fly']['grantedKeywords']
                            assert field['Vedalken Squirrel-Whacker']['power']==original_roll and field['Vedalken Squirrel-Whacker']['toughness']==5
                            snapshot(m,'expired');proof(r,'expired');stage=3
                        elif stage==3 and r['seat']==1 and v['turnSeat']==1 and v['phase']=='MAIN1':
                            pick='card:'+str(next(c['id'] for c in one['hand'] if c['name']=='Unsummon'));paid=0;stage=4
                        elif stage==4 and 'Vedalken Squirrel-Whacker' not in field and any(c['name']=='Unsummon' for c in one['graveyardCards']):
                            card=next(c for c in one['hand'] if c['name']=='Vedalken Squirrel-Whacker');assert card['id']==whacker_id and paid==1
                            snapshot(m,'returned');proof(r,'returned');pick='card:'+str(card['id']);paid=0;stage=5
                        elif stage==5 and 'Vedalken Squirrel-Whacker' in field:
                            state,_=proof(r,'recast');c=field['Vedalken Squirrel-Whacker'];assert paid==4 and c['id']==whacker_id and state[0]['rolled']==2
                            assert len(state[0]['rolls'])==2 and (c['power'],c['toughness'])==tuple(state[0]['rolls'])
                            assert not field['Ebony Fly']['creature'];snapshot(m,'recast');return 'actual Fly activation/payment/swap; Fly EOT restores noncreature, exchanged Whacker PT survives turn; real Unsummon and recast re-roll both ETB base stats, exact ID and costs; priority'
                    elif stage==1 and any(c['id']==source_id for c in one['graveyardCards']):
                        state,limits=proof(r,'resolved-'+str(spell_no));result=state[0]['rolls'][-1]
                        expected_physical=2 if case=='monitor-cancel-retry' or case=='monitor-limit' and spell_no!=2 else 1
                        assert state[0]['rolled']==roll_count+expected_physical and len(state[0]['rolls'])==len(roll_values)+1
                        assert paid==4+(1 if expected_physical==2 else 0) and sum(one['manaPool'])==0
                        draw=3 if result==20 else 2 if 1<=result<=19 else 0;expected_scry=3 if result==20 else 2 if 10<=result<=19 else 0
                        assert scry_count==expected_scry and state[0]['library']==library_before[draw:]
                        assert {c['id'] for c in one['hand']}-hand_before==set(library_before[:draw]) and len(one['hand'])==len(hand_before)-1+draw
                        if case.startswith('night'):
                            assert one['life']==20-((2 if spell_no==3 else 1) if case=='night-limit' else 0)
                            assert increment_count==((2 if spell_no==3 else 1) if case=='night-limit' else 0)
                            if case=='night-limit' and spell_no!=2:assert result==original_roll+1
                        if case=='xeno-last':
                            assert increment_count==1 and 'Xenosquirrels' not in field and any(c['name']=='Xenosquirrels' for c in one['graveyardCards'])
                            assert result==original_roll+1 and not next(c for c in one['graveyardCards'] if c['name']=='Xenosquirrels')['counters']
                        if case.startswith('monitor'):
                            expected_rounds=2 if case in ('monitor-no-mana','monitor-cancel-retry') else 2 if spell_no==3 else 1
                            assert reroll_rounds==expected_rounds
                            if case in ('monitor-no-mana','monitor-cancel-retry'):assert cancelled==1
                        modifier='Monitor Monitor' if case.startswith('monitor') else 'Night Shift of the Living Dead'
                        if case!='xeno-last':assert next(c['mods'] for c in limits if c['name']==modifier)==('Number$0' if case in ('monitor-no-mana','night-no-pay') else 'Number$1')
                        snapshot(m,'resolved-'+str(spell_no))
                        if case.endswith('limit') and spell_no==1 and r['seat']==1:
                            pick=start_spell(m,one,r)
                        elif case.endswith('limit') and spell_no==2:stage=2
                        elif not case.endswith('limit') or spell_no==3:return 'real costs/optional cancellation or resource prohibition; exact physical rolls, final draw/scry/top-ID order, native modification limit, life/counters, priority; '+case
                    elif stage==2 and v['turnSeat']==2 and v['phase']=='MAIN1':
                        state,limits=proof(r,'reset');assert all(c['mods']=='Number$0' for c in limits)
                        snapshot(m,'reset');stage=3
                    elif stage==3 and r['seat']==1 and v['turnSeat']==1 and v['phase']=='MAIN1':pick=start_spell(m,one,r);stage=1
                elif 'PayMana' in kind:
                    cost=r.get('remainingManaCost') or r['message'].split('Pay Mana Cost:',1)[-1]
                    optional=case.startswith('monitor') and kind=='InputPayManaOfCostPayment' and reroll_rounds>0 and r['message'].startswith('Monitor Monitor') and '{1}' in cost
                    if optional and case in ('monitor-no-mana','monitor-cancel-retry') and cancelled==0:
                        snapshot(m,'cancel-mana');assert 'cancel' in ids
                        if case=='monitor-no-mana':assert not any(i.startswith(('card:','mana:')) for i in ids),ids
                        pick='cancel';cancelled+=1
                    else:
                        colors=re.findall(r'\{([WUBRG])\}',cost);allowed=colors or list('WUBRGC')
                        pick=next((i for i in ids if i.startswith('mana:') and i.split(':')[1] in allowed),None)
                        if pick is None:
                            basics=dict(zip('WUBRG',['Plains','Island','Swamp','Mountain','Forest']))
                            lands=sorted(one['battlefield'],key=lambda c:c['name']=='Island' and not colors)
                            pick=next('card:'+str(c['id']) for c in lands if c['name'] in [basics[x] for x in allowed if x in basics] and 'card:'+str(c['id']) in ids)
                        paid+=1
                elif kind=='Scry':
                    if not scry_count:scry_count=r['selectedCount']+r['remainingCount']
                    pick='confirm-scry' if 'confirm-scry' in ids else next(i for i in ids if i.startswith('scry-top:'))
                elif kind=='ChooseOptions' and 'reroll' in r['message'].lower():
                    if not r.get('selectedCount'):reroll_rounds+=1;snapshot(m,'reroll-'+str(reroll_rounds))
                    pick='confirm-cards' if r.get('selectedCount') or case=='monitor-no-mana' and reroll_rounds==2 else next(i for i in ids if i.startswith('select:'))
                elif kind=='InputConfirm' and 'increase or decrease' in r['message'].lower():increment_count+=1;snapshot(m,'increment-'+str(increment_count));pick='ok'
                elif kind=='InputSelectTargets':pick='ok' if r.get('selectedCount') and 'ok' in ids else 'card:'+str(whacker_id)
                elif kind=='dialog':
                    text=r['message'].lower()
                    if 'choose a roll to modify' in text:
                        original_roll=int(r['options'][0]['label']);snapshot(m,'modify-'+str(increment_count+1));pick=ids[0]
                    elif 'choose a roll to swap' in text:original_roll=int(r['options'][0]['label']);pick=ids[0]
                    elif 'power or toughness' in text:pick=next(o['id'] for o in r['options'] if o['label'].lower()=='power')
                    elif 'getabilitytoplay' in text:pick=next(o['id'] for o in r['options'] if 'roll a d6' in o['label'].lower())
                    else:pick=ids[0]
                elif kind in ('InputSelectCardsFromList','InputSelectEntitiesFromList','SelectCards'):
                    required=int(re.search(r'Select (\d+) card',r['message'])[1]) if 'to discard' in r['message'] else 1
                    if r.get('selectedCount',0)>=required and ('ok' in ids or 'confirm-cards' in ids):pick='ok' if 'ok' in ids else 'confirm-cards'
                    else:
                        candidates=[o['id'] for o in r['options'] if o['id'].startswith(('card:','select:')) and not o.get('selected')]
                        pick=next((i for i in candidates if any(c['name']=='Mountain' and i.endswith(':'+str(c['id'])) for c in one['hand'])),None) or candidates[0]
                elif kind=='RevealCards':pick='confirm-cards'
                elif kind=='OrderAbilities':pick='confirm-order'
                assert pick in ids,(case,stage,kind,pick,r['message'],ids[:15])
                p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
            raise AssertionError('No completion')
        finally:
            if p.poll() is None:
                p.stdin.write('{"type":"shutdown"}\n');p.stdin.flush()
                try:p.wait(timeout=3)
                except subprocess.TimeoutExpired:p.kill();p.wait()
if __name__=='__main__':
    results=[]
    for case in sys.argv[1:] or CASES:
        try:result='PASS '+case+': '+run(case)
        except Exception as e:result='FAIL '+case+': '+repr(e);(OUT/(case+'-error.txt')).write_text(traceback.format_exc(),encoding='utf8')
        print(result,flush=True);results.append(result)
    (OUT/'result.txt').write_text('\n'.join(results),encoding='utf8');sys.exit(any(r.startswith('FAIL') for r in results))
