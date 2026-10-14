"""Native combat declarations, assignment gates, two damage steps and exact outcomes."""
import json,os,queue,shutil,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'Tools/Validation/Reports/CombatAllocation';CLASSES=ROOT/'.tools/combat-allocation/classes'
if (OUT/'result.txt').exists():shutil.copytree(OUT,ROOT/'.tools/combat-allocation/attempts'/str(time.time_ns()))
OUT.mkdir(parents=True,exist_ok=True);CLASSES.mkdir(parents=True,exist_ok=True)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),*[str(ROOT/'Tools/ForgeBridge/tests'/n) for n in ('LibraryCostFixture.java','CombatAllocationFixture.java')]],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES=['trample-reset-release','trample-overassign','trample-save-blocker','trample-seat-two','damaged-blocker','deathtouch-trample','deathtouch','first-strike','double-strike-blocked','double-strike-open','menace','block-multiple','as-unblocked-accept','as-unblocked-decline','planeswalker','many-blockers']

def run(case):
    actor=2 if case.endswith('seat-two') else 1;other=3-actor
    spell='Rancor' if case in ('many-blockers','deathtouch-trample','first-strike','double-strike-blocked','double-strike-open') else 'Shock' if case=='damaged-blocker' else None
    with tempfile.TemporaryDirectory(prefix='mtgb-combat-allocation-') as temp,(OUT/(case+'.log')).open('w',encoding='utf8') as log,(OUT/(case+'.jsonl')).open('w',encoding='utf8') as trace:
        audit=Path(temp)/'audit';audit.mkdir();start=Path(temp)/'start.json'
        start.write_text(json.dumps(dict(session=case,fixture=case,auditDir=str(audit),seed=4,startingLife=20,firstPlayerSeat=actor,decks=[DECK,DECK])),encoding='utf8')
        p=subprocess.Popen([str(RT/CFG['java']),'-Djava.awt.headless=true','-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.CombatAllocationFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
        messages=queue.Queue()
        def reader():
            try:
                for line in p.stdout:messages.put(json.loads(line))
            except Exception as e:messages.put(e)
            finally:messages.put(None)
        threading.Thread(target=reader,daemon=True).start()
        def snapshot(m,label):
            r=m['request'];v=dict(m['views'][r['seat']-1]);v.update(request=r,awaitingInput=True)
            (OUT/(case+'-'+label+'.json')).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf8')
        def proof(r,label):
            path=audit/(str(r['requestId'])+'.json');end=time.monotonic()+5
            while not path.exists() and time.monotonic()<end:time.sleep(.01)
            assert path.exists(),'Missing private library proof';d=json.loads(path.read_text());assert d['requestId']==r['requestId']
            (OUT/(case+'-'+label+'-audit.json')).write_text(json.dumps(d,indent=2),encoding='utf8');return [x['library'] for x in d['players']]
        initial=None;library=None;initial_hands=None;attacked=set();block_pairs=set();contexts={};allocations=[];source_ids=[];spell_id=None;cast=False;resolved=not spell;paid=0;menace_attempt=False;menace_recovered=False;menace_request=None;optional=0;probe=0;step_no=0;deadline=time.monotonic()+180
        try:
            for step in range(900):
                assert time.monotonic()<deadline,'Deadline';m=messages.get(timeout=5 if case=='menace' and menace_attempt and not menace_recovered else 35);assert m is not None,'Bridge exited'
                if isinstance(m,Exception):raise m
                trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ('fatal','ended','rejected'),m
                if m['type']!='request':continue
                r=m['request'];v=m['views'][actor-1];players=v['players'];one,two=players[actor-1],players[other-1];kind=r['kind'];ids=[o['id'] for o in r['options']];pick='ok'
                assert not m['views'][other-1]['players'][actor-1]['hand'] and not m['views'][actor-1]['players'][other-1]['hand'],'Private hand leak'
                if kind=='InputPassPriority':
                    if initial is None and r['seat']==actor and v['phase']=='MAIN1':
                        initial=players;initial_hands=[{c['id'] for c in m['views'][s]['players'][s]['hand']} for s in range(2)];library=proof(r,'before');snapshot(m,'before');source_ids=[c['id'] for c in one['battlefield'] if c['creature']]
                    if initial is not None and v['phase']=='MAIN1' and r['seat']==actor and not v['stackItems']:
                        if spell and not cast:
                            spell_id=next(c['id'] for c in one['hand'] if c['name']==spell);pick='card:'+str(spell_id);cast=True
                        elif spell and cast and not resolved and spell_id not in {c['id'] for c in one['hand']}:
                            assert paid==1 and sum(one['manaPool'])==0,'Wrong native payment'
                            if spell=='Rancor':assert next(c for c in one['battlefield'] if c['id']==source_ids[0])['power']==(12 if case=='many-blockers' else 4 if case in ('deathtouch-trample','first-strike') else 3)
                            else:assert next(c for c in two['battlefield'] if c['name']=='Wall of Air')['damage']==2
                            resolved=True;snapshot(m,'spell-resolved')
                    if initial is not None and attacked and v['phase']=='MAIN2' and not v['stackItems']:
                        assert resolved and proof(r,'resolved')==library,'Combat moved library cards';snapshot(m,'resolved')
                        assert sum(one['manaPool'])==0 and paid==(1 if spell else 0)
                        expected_life=(24,18) if case=='deathtouch-trample' else (22,20) if case=='deathtouch' else (20,14) if case=='double-strike-open' else (20,19) if case=='damaged-blocker' else (20,13) if case=='as-unblocked-accept' else (20,18) if case in ('trample-reset-release','trample-seat-two','double-strike-blocked') else (20,20)
                        assert (one['life'],two['life'])==expected_life,('Wrong native life',case,one['life'],two['life'],expected_life)
                        dead_actor=set(source_ids) if case in ('many-blockers','double-strike-blocked','menace','block-multiple') else set()
                        all_defenders={c['id'] for c in initial[other-1]['battlefield'] if c['creature'] or c['planeswalker']}
                        survivor=set()
                        if case in ('trample-save-blocker','menace','many-blockers'):
                            survivor={card for card,n in allocations[-1]['amounts'].items() if card is not None and n==(0 if case in ('trample-save-blocker','many-blockers') else 1)}
                        dead_other=all_defenders-survivor
                        if case=='as-unblocked-accept':dead_other=set()
                        expected_actor_field={c['id'] for c in initial[actor-1]['battlefield']}-dead_actor
                        if spell=='Rancor' and case not in ('double-strike-blocked','many-blockers'):expected_actor_field.add(spell_id)
                        assert {c['id'] for c in one['battlefield']}==expected_actor_field,'Actor field IDs'
                        assert {c['id'] for c in two['battlefield']}=={c['id'] for c in initial[other-1]['battlefield']}-dead_other,'Defender field IDs'
                        assert {c['id'] for c in one['graveyardCards']}=={c['id'] for c in initial[actor-1]['graveyardCards']}|dead_actor|({spell_id} if spell=='Shock' else set()),'Actor grave IDs'
                        assert {c['id'] for c in two['graveyardCards']}=={c['id'] for c in initial[other-1]['graveyardCards']}|dead_other,'Defender grave IDs'
                        expected_hand=initial_hands[actor-1]-({spell_id} if spell and case not in ('double-strike-blocked','many-blockers') else set())
                        assert {c['id'] for c in one['hand']}==expected_hand,'Cast aura returning or spell hand IDs'
                        assert {c['id'] for c in m['views'][other-1]['players'][other-1]['hand']}==initial_hands[other-1]
                        for seat in range(2):assert {c['id'] for c in players[seat]['exileCards']}=={c['id'] for c in initial[seat]['exileCards']},'Unexpected exile'
                        expected_contexts=0 if case in ('double-strike-open','as-unblocked-accept') else 2 if case=='double-strike-blocked' else 1
                        assert len(allocations)==expected_contexts,('Damage steps',case,allocations)
                        if case=='first-strike':assert allocations[0]['phase']=='COMBAT_FIRST_STRIKE_DAMAGE'
                        if case=='double-strike-blocked':assert [a['phase'] for a in allocations]==['COMBAT_FIRST_STRIKE_DAMAGE','COMBAT_DAMAGE']
                        if case=='menace':assert menace_attempt and menace_recovered,'Illegal one-blocker declaration did not retry'
                        if case=='many-blockers':assert len(survivor)==4 and len(dead_other)==6,'Wide native assignment outcomes'
                        if case.startswith('as-unblocked'):assert optional==1
                        if case=='trample-reset-release':assert probe==7,'Missing release/reset probe'
                        if not dead_actor:
                            c=next(c for c in one['battlefield'] if c['id']==source_ids[0]);expected_damage=0 if case in ('first-strike','double-strike-open') else 2 if case.startswith('deathtouch') or case=='planeswalker' else 3 if case=='damaged-blocker' else 4
                            assert c['damage']==expected_damage,('Marked source damage',c['damage'],expected_damage)
                        if survivor:
                            assert all(c['damage']==(1 if case=='menace' else 0) for c in two['battlefield'] if c['id'] in survivor)
                        return 'native cast/payment when required, attack/block declarations and legal assignment/reset/phase gates; exact lives, deaths/survivors, aura return, IDs/regions, private hands/full libraries and resumed priority'
                elif kind=='InputAttack':
                    assert resolved and r['seat']==actor
                    defender=next(c['id'] for c in two['battlefield'] if c['planeswalker']) if case=='planeswalker' else two['targetId'];def_kind='card' if case=='planeswalker' else 'player'
                    candidate=next((o for o in r['options'] if o['id'].startswith('attack:') and o['attackerId'] in source_ids and o['attackerId'] not in attacked and o['defenderKind']==def_kind and o['defenderId']==defender),None)
                    if candidate:pick=candidate['id'];attacked.add(candidate['attackerId'])
                    else:assert attacked==set(source_ids);snapshot(m,'attack-confirm')
                elif kind=='InputBlock':
                    assert r['seat']==other;snapshot(m,'block-'+str(step_no))
                    candidate=next((o for o in r['options'] if o['id'].startswith('block:') and (o['attackerId'],o['defenderId']) not in block_pairs),None)
                    if case=='menace' and len(block_pairs)==1 and not menace_attempt:menace_attempt=True;menace_request=r;pick='ok'
                    else:
                        if case=='menace' and menace_attempt and len(block_pairs)==1:
                            assert r['requestId']>menace_request['requestId'] and r['contextId']==menace_request['contextId'],'Rejected declaration did not get a fresh request in same native input'
                            assert [o['id'] for o in r['options']]==[o['id'] for o in menace_request['options']],'Rejected declaration changed choices'
                            assert len(m['views'][other-1]['notices'])==1 and not m['views'][actor-1]['notices'],'Private validation error missing/leaked'
                            assert 'cannot be blocked with 1' in m['views'][other-1]['notices'][0]['message']
                            menace_recovered=True
                        if candidate:pick=candidate['id'];block_pairs.add((candidate['attackerId'],candidate['defenderId']))
                elif kind=='AssignAmount':
                    ctx=r['contextId'];rows=r['allocations'];assert r['sourceCard']['id']==r['sourceCardId']>0
                    assert r['selectionPurpose']=='combatDamage','Assignment lacks typed purpose'
                    for row in rows:
                        if row.get('card'):assert row['targetKind']=='card' and row['targetId']==row['card']['id']
                        else:assert row['targetKind']=='player' and row['targetId']==two['targetId'] and row['playerSeat']==other
                        if row.get('hasLethal'):
                            expected=max(0,row['card']['toughness']-row['card']['damage']);expected=min(expected,1) if 'DEATHTOUCH' in r['sourceCard']['keywords'] else expected
                            assert row['lethal']==expected,'Native lethal metadata wrong'
                    assert r['seat']==(other if case=='block-multiple' else actor),'Wrong native damage decider'
                    if ctx not in contexts:
                        if case in ('trample-reset-release','trample-seat-two'):goal=[2,2,2]
                        elif case=='trample-overassign':goal=[3,3,0]
                        elif case=='trample-save-blocker':goal=[6,0,0]
                        elif case=='damaged-blocker':goal=[3 if a.get('card',{}).get('name')=='Wall of Air' else 2 if a.get('card') else 1 for a in rows]
                        elif case=='deathtouch-trample':goal=[1,1,2]
                        elif case=='deathtouch':goal=[1,1]
                        elif case=='first-strike':goal=[2,2,0]
                        elif case=='double-strike-blocked':goal=[2,1,0] if v['phase']=='COMBAT_FIRST_STRIKE_DAMAGE' else [1,2]
                        elif case=='menace':goal=[2,1]
                        elif case=='block-multiple':goal=[2,2]
                        elif case=='as-unblocked-decline':goal=[3,4]
                        elif case=='planeswalker':goal=[4 if a['card']['planeswalker'] else 2 for a in rows]
                        elif case=='many-blockers':goal=[2]*6+[0]*5
                        else:raise AssertionError(('Unexpected assignment',case))
                        assert len(goal)==len(rows) and sum(goal)==r['maxCount'],(case,goal,r)
                        contexts[ctx]=goal
                    goal=contexts[ctx];spent=sum(a['amount'] for a in rows);assert spent==r['selectedCount'] and spent+r['remainingCount']==r['maxCount']
                    assert ('confirm-amount' in ids)==(spent==r['maxCount']),'Early/missing confirm'
                    defender_row=next((i for i,a in enumerate(rows) if not a.get('card') or a['card']['planeswalker']),None)
                    if defender_row is not None:
                        lethal=[min(max(0,a['card']['toughness']-a['card']['damage']),1) if 'DEATHTOUCH' in r['sourceCard']['keywords'] else max(0,a['card']['toughness']-a['card']['damage']) for a in rows[:defender_row]]
                        assert ('amount-add:'+str(rows[defender_row]['id']) in ids)==(spent<r['maxCount'] and all(a['amount']>=n for a,n in zip(rows,lethal))),'Trample lethal gate'
                    if case=='trample-reset-release' and probe<7:
                        if probe==6:
                            assert [a['amount'] for a in rows]==[1,2,0],'Decreasing lethal did not release defender damage';assert 'amount-add:2' not in ids
                        pick=['amount-add:0','amount-add:0','amount-add:1','amount-add:1','amount-add:2','amount-sub:0','reset-amount'][probe];probe+=1
                    else:
                        target=next((i for i,a in enumerate(rows) if a['amount']<goal[i]),None)
                        if target is None:
                            pick='confirm-amount';allocations.append(dict(phase=v['phase'],source=r['sourceCardId'],amounts={a.get('card',{}).get('id'):a['amount'] for a in rows}));snapshot(m,'assigned-'+str(len(allocations)))
                        else:pick='amount-add:'+str(rows[target]['id'])
                    snapshot(m,'allocation-'+str(step_no))
                elif kind.startswith('InputPayMana'):
                    land='Mountain' if spell=='Shock' else 'Forest';color='R' if spell=='Shock' else 'G'
                    if 'mana:'+color in ids:pick='mana:'+color
                    else:pick='card:'+str(next(c['id'] for c in one['battlefield'] if c['name']==land and 'card:'+str(c['id']) in ids));paid+=1
                elif kind=='InputSelectTargets':
                    target=next(c['id'] for c in two['battlefield'] if c['name']=='Wall of Air') if spell=='Shock' else source_ids[0]
                    pick='ok' if r.get('selectedCount') and 'ok' in ids else 'card:'+str(target)
                elif kind=='InputConfirm':
                    assert case.startswith('as-unblocked');assert r['seat']==actor;optional+=1;snapshot(m,'as-unblocked');pick='ok' if case.endswith('accept') else 'cancel'
                elif kind=='dialog' and case=='menace':pick=ids[0]
                elif kind=='RevealCards':pick='confirm-cards'
                elif 'Mulligan' in kind or kind=='InputChooseStartingHand':pick='ok'
                else:raise AssertionError(('Unadapted test input',case,kind,ids,r['message']))
                assert pick in ids,(case,kind,pick,ids,r['message']);step_no+=1
                p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
            raise AssertionError('No final priority')
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
    (OUT/'result.txt').write_text('\n'.join(results),encoding='utf8');sys.exit(any(x.startswith('FAIL') for x in results))
