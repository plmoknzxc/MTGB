"""Native casts, opposing separation, whole-pile selection and exact zone/privacy assertions."""
import json,os,queue,re,shutil,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime'
CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'Tools/Validation/Reports/Piles';CLASSES=ROOT/'.tools/piles/classes'
if (OUT/'result.txt').exists():shutil.copytree(OUT,ROOT/'.tools/piles/attempts'/str(time.time_ns()))
OUT.mkdir(parents=True,exist_ok=True);CLASSES.mkdir(parents=True,exist_ok=True)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),str(ROOT/'Tools/ForgeBridge/tests/PileFixture.java')],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES=['fof-left','fof-right','fof-empty','fof-all','fof-seat2','favor-hidden','favor-visible','favor-empty','parley-left','parley-right','parley-zero','portal-left','portal-right','portal-empty']
BASELINE='--baseline' in sys.argv

def run(case):
    actor=2 if case.endswith('seat2') else 1;separator=3-actor
    portal=case.startswith('portal')
    source='Phyrexian Portal' if portal else 'Boneyard Parley' if case.startswith('parley') else "Fortune's Favor" if case.startswith('favor') else 'Fact or Fiction'
    size=10 if portal else 4 if case.startswith('favor') else 5
    with tempfile.TemporaryDirectory(prefix='mtgb-piles-') as temp,(OUT/(case+'.log')).open('w',encoding='utf8') as log,(OUT/(case+'.jsonl')).open('w',encoding='utf8') as trace:
        audit=Path(temp)/'audit';audit.mkdir()
        start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session=case,fixture=case,auditDir=str(audit),seed=4,startingLife=20,firstPlayerSeat=actor,decks=[DECK,DECK])),encoding='utf8')
        p=subprocess.Popen([str(RT/CFG['java']),'-Djava.awt.headless=true','-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.PileFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
        messages=queue.Queue()
        def read():
            try:
                for line in p.stdout:messages.put(json.loads(line))
            except Exception as error:messages.put(error)
            finally:messages.put(None)
        threading.Thread(target=read,daemon=True).start()
        def snapshot(m,label):
            view=dict(m['views'][m['request']['seat']-1]);view.update(request=m['request'],awaitingInput=True)
            (OUT/(case+'-'+label+'.json')).write_text(json.dumps(view,ensure_ascii=False,indent=2),encoding='utf8')
        def library_proof(r,label):
            path=audit/(str(r['requestId'])+'.json');limit=time.monotonic()+5
            while not path.exists() and time.monotonic()<limit:time.sleep(.01)
            assert path.exists(),'Missing read-only library proof'
            data=json.loads(path.read_text());assert data['requestId']==r['requestId']
            (OUT/(case+'-'+label+'-audit.json')).write_text(json.dumps(data,indent=2),encoding='utf8')
            return data['players'][actor-1]['library']
        stage=0;pool={};first=set();chosen=set();initial=None;initial_library=0;pile_count=0;paid=0;searched=None;target_ids=set();deadline=time.monotonic()+150
        try:
            for step in range(400):
                assert time.monotonic()<deadline,'Deadline'
                m=messages.get(timeout=40);assert m is not None,'Bridge exited'
                if isinstance(m,Exception):raise m
                trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ('fatal','ended','rejected'),m
                if m['type']!='request':continue
                r=m['request'];v=m['views'][r['seat']-1];players=v['players'];player=players[actor-1]
                ids=[o['id'] for o in r['options']];kind=r['kind'];pick='ok'
                if kind=='InputPassPriority' and not v['stackItems']:
                    if stage==0 and r['seat']==actor and v['phase']=='MAIN1':
                        initial={c['id'] for c in player['hand']};initial_library=player['libraryCount'];before_library=library_proof(r,'before');snapshot(m,'before')
                        pick='card:'+str(next(c for c in (player['battlefield'] if portal else player['hand']) if c['name']==source)['id']);stage=1
                    elif stage==1 and (portal and pile_count==1 or not portal and any(c['name']==source for c in player['graveyardCards'])):
                        assert paid== (3 if portal else 7 if case.startswith('parley') else 4),('Wrong mana payments',paid)
                        assert sum(c.get('tapped',False) for c in player['battlefield'] if c['name'] in ('Island','Swamp'))==paid
                        assert sum(player['manaPool'])==0,'Unspent mana after exact payment'
                        assert not m['views'][separator-1]['players'][actor-1]['hand'],'Private final hand leaked'
                        if case=='parley-zero':
                            assert pile_count==0 and not pool and sum(len(x['graveyardCards']) for x in players)==6
                        else:
                            assert pile_count==1 and len(pool)==size
                            if portal:
                                assert any(c['name']==source for c in player['battlefield'])
                                hand={c['id'] for c in player['hand']};exile={c['id'] for c in player['exileCards']}
                                assert hand-initial==({searched} if chosen else set()) and (searched is None or searched in chosen)
                                assert exile==set(pool)-chosen and not chosen&exile
                                assert player['libraryCount']==initial_library-len(exile)-(1 if chosen else 0)
                                assert set(library_proof(r,'resolved'))==set(before_library)-exile-({searched} if searched is not None else set()),'Portal shuffled wrong identities into library'
                            elif case.startswith('parley'):
                                battlefield={c['id']:c for c in player['battlefield']}
                                assert chosen<=battlefield.keys() and not (set(pool)-chosen)&battlefield.keys()
                                for card_id,card in pool.items():
                                    owner=card['ownerSeat']
                                    if card_id in chosen:assert battlefield[card_id]['ownerSeat']==owner
                                    else:assert any(c['id']==card_id for c in players[owner-1]['graveyardCards'])
                                assert all(not any(c['id'] in pool for c in x['exileCards']) for x in players)
                            else:
                                hand={c['id'] for c in player['hand']};grave={c['id'] for c in player['graveyardCards']}
                                assert hand-initial==chosen and set(pool)-chosen<=grave and not chosen&grave
                                assert len(hand)==len(initial)-1+len(chosen) and player['libraryCount']==initial_library-size
                                assert library_proof(r,'resolved')==[i for i in before_library if i not in pool],'Unrevealed library reordered or wrong IDs removed'
                        snapshot(m,'resolved');return 'native cast/targets/payment; opposing separator; whole-pile exact IDs and owner zones; hidden payload/privacy; empty boundary; priority resumes'
                elif kind=='InputSelectTargets':
                    if case=='parley-zero':pick='ok'
                    elif case.startswith('parley'):
                        if r.get('selectedCount',0)>=5:pick='ok'
                        else:
                            option=next(o for o in r['options'] if o['id'].startswith('card:') and int(o['id'][5:]) not in target_ids)
                            pick=option['id'];target_ids.add(int(pick[5:]))
                    else:pick=next(i for i in ids if i.startswith('player:') and i.endswith(str(separator)))
                elif kind in ('SelectCards','InputSelectCardsFromList') and r['seat']==separator:
                    cards=[o['card'] for o in r['options'] if o.get('card')];assert len(cards)==size
                    if not pool:pool={c['id']:c for c in cards};snapshot(m,'separate')
                    count=0 if case.endswith('empty') else size if case.endswith('all') else 2
                    selected={o['card']['id'] for o in r['options'] if o.get('selected') and o.get('card')}
                    if len(selected)<count:pick=next(o['id'] for o in r['options'] if o.get('card') and o['card']['id'] not in selected)
                    else:first=selected;pick='confirm-cards' if kind=='SelectCards' else 'ok';snapshot(m,'separated')
                elif kind=='ChoosePile' or BASELINE and kind=='dialog' and any(o.get('card',{}).get('id')==-2147483648 for o in r['options']):
                    assert r['seat']==actor;pile_count+=1
                    left=not case.endswith(('right','visible'));chosen=first if left else set(pool)-first
                    if not BASELINE:
                        assert [o['id'] for o in r['options']]==['pile:1','pile:2'] and r.get('selectionPurpose')=='pile'
                        piles=r['piles'];assert len(piles)==2
                        for index,pile in enumerate(piles):
                            expected=first if index==0 else set(pool)-first
                            hidden=portal or case.startswith('favor') and index==0
                            assert pile['pileId']==index+1 and pile['cardCount']==len(expected) and pile['concealed']==hidden
                            if hidden:
                                assert not pile['cards'] and set(pile)=={'pileId','cardCount','concealed','cards'},'Hidden pile identities serialized'
                                def visible_ids(value):
                                    if isinstance(value,dict):
                                        return ({value['id']} if 'id' in value and 'name' in value else set()).union(*(visible_ids(x) for x in value.values()))
                                    if isinstance(value,list):return set().union(*(visible_ids(x) for x in value))
                                    return set()
                                assert not expected&visible_ids(dict(v,request=r)),'Hidden card leaked through another actor-view field'
                            else:
                                assert {c['id'] for c in pile['cards']}==expected
                                assert all(c.get('printingKey') and c.get('currentType') and 'currentRules' in c for c in pile['cards']),'Visible pile lacks face metadata'
                        for other in m['views']:
                            if other['seat']!=actor:assert not other.get('request'),'Pile request leaked to another seat'
                    snapshot(m,'choice');pick=('choice:0' if left else next(o['id'] for o in r['options'] if o.get('card',{}).get('id')==-2147483647)) if BASELINE else ('pile:1' if left else 'pile:2')
                elif portal and pile_count and kind in ('SelectCards','InputSelectCardsFromList','InputSelectEntitiesFromList'):
                    candidates=[o for o in r['options'] if o.get('card')];assert {o['card']['id'] for o in candidates}==chosen,'Search escaped the chosen pile'
                    if r.get('selectedCount'):
                        assert r['selectedCount']==1 and next(o['card']['id'] for o in candidates if o.get('selected'))==searched
                        snapshot(m,'search-selected');pick='confirm-cards' if kind=='SelectCards' else 'ok'
                    else:snapshot(m,'search');searched=candidates[0]['card']['id'];pick=candidates[0]['id']
                elif 'PayMana' in kind:
                    cost=r.get('remainingManaCost') or r['message'].split('Pay Mana Cost:',1)[-1];colors=re.findall(r'\{([WUBRG])\}',cost);allowed=colors or ['U','B']
                    pick=next((i for i in ids if i.startswith('mana:') and i.split(':')[1] in allowed),None)
                    if pick is None:pick=next('card:'+str(c['id']) for c in player['battlefield'] if c['name'] in [('Island' if x=='U' else 'Swamp') for x in allowed] and 'card:'+str(c['id']) in ids)
                    paid+=1
                elif kind=='RevealCards':pick='confirm-cards'
                elif kind=='dialog':pick=ids[0]
                elif kind=='OrderAbilities':pick='confirm-order'
                assert pick in ids,(case,stage,kind,pick,r['message'],ids[:12])
                p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
            raise AssertionError('No completion')
        finally:
            if p.poll() is None:
                p.stdin.write('{"type":"shutdown"}\n');p.stdin.flush()
                try:p.wait(timeout=3)
                except subprocess.TimeoutExpired:p.kill();p.wait()
if __name__=='__main__':
    results=[]
    for case in [x for x in sys.argv[1:] if x!='--baseline'] or CASES:
        try:result='PASS '+case+': '+run(case)
        except Exception as error:
            result='FAIL '+case+': '+repr(error)
            (OUT/(case+'-error.txt')).write_text(traceback.format_exc(),encoding='utf8')
        print(result,flush=True);results.append(result)
    (OUT/'result.txt').write_text('\n'.join(results),encoding='utf8');sys.exit(any(x.startswith('FAIL') for x in results))
