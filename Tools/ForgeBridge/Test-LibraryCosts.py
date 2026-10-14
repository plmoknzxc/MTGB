"""Actual cumulative upkeep from same graveyard: source actor, exact IDs, cancellation and continuation."""
import json,os,queue,re,shutil,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'Tools/Validation/Reports/LibraryCosts';CLASSES=ROOT/'.tools/library-costs/classes'
if (OUT/'result.txt').exists():shutil.copytree(OUT,ROOT/'.tools/library-costs/attempts'/str(time.time_ns()))
OUT.mkdir(parents=True,exist_ok=True);CLASSES.mkdir(parents=True,exist_ok=True)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),str(ROOT/'Tools/ForgeBridge/tests/LibraryCostFixture.java')],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES=['own','opponent','seat-two','cancel-player','cancel-card','insufficient','second-age']

def run(case):
    actor=2 if case=='seat-two' else 1;target=3-actor if case=='opponent' else actor
    with tempfile.TemporaryDirectory(prefix='mtgb-library-cost-') as temp,(OUT/(case+'.log')).open('w',encoding='utf8') as log,(OUT/(case+'.jsonl')).open('w',encoding='utf8') as trace:
        audit=Path(temp)/'audit';audit.mkdir();start=Path(temp)/'start.json'
        start.write_text(json.dumps(dict(session=case,fixture=case,auditDir=str(audit),seed=4,startingLife=20,firstPlayerSeat=actor,decks=[DECK,DECK])),encoding='utf8')
        p=subprocess.Popen([str(RT/CFG['java']),'-Djava.awt.headless=true','-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.LibraryCostFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
        messages=queue.Queue()
        def read():
            try:
                for line in p.stdout:messages.put(json.loads(line))
            except Exception as e:messages.put(e)
            finally:messages.put(None)
        threading.Thread(target=read,daemon=True).start()
        def snapshot(m,label):
            v=dict(m['views'][actor-1]);v.update(request=m['request'] if m['request']['seat']==actor else None,awaitingInput=m['request']['seat']==actor)
            (OUT/(case+'-'+label+'.json')).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf8')
        def proof(r,label):
            path=audit/(str(r['requestId'])+'.json');end=time.monotonic()+4
            # Existence can precede the observer closing its file on Windows.
            # Wait for a complete, readable record; never replace missing proof.
            d=None
            while d is None and time.monotonic()<end:
                try:d=json.loads(path.read_text(encoding='utf8'))
                except (OSError,json.JSONDecodeError):time.sleep(.01)
            assert d is not None,'Missing or unreadable library proof';assert d['requestId']==r['requestId']
            (OUT/(case+'-'+label+'-audit.json')).write_text(json.dumps(d,indent=2),encoding='utf8');return [x['library'] for x in d['players']]
        cast=False;source=None;paid=0;age=0;player_requests=0;chosen=[];initial_graves=None;before=None;cancelled=False;ordered=[];bottom=[];deadline=time.monotonic()+150
        try:
            for step in range(1000):
                assert time.monotonic()<deadline,'Deadline';m=messages.get(timeout=35);assert m is not None,'Bridge exited; inspect log'
                if isinstance(m,Exception):raise m
                trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ('fatal','ended','rejected'),m
                if m['type']!='request':continue
                r=m['request'];v=m['views'][actor-1];players=v['players'];one=players[actor-1];ids=[o['id'] for o in r['options']];kind=r['kind'];pick='ok'
                # The nonacting player never receives the other player's hand.
                assert not m['views'][2-actor]['players'][actor-1]['hand'],'Private hand leak'
                if kind=='InputPassPriority':
                    if not cast and r['seat']==actor and v['phase']=='MAIN1' and not v['stackItems']:
                        source=next(c['id'] for c in one['hand'] if c['name']=='Jötun Grunt');pick='card:'+str(source);cast=True
                        initial_graves=[{c['id'] for c in x['graveyardCards']} for x in players]
                    elif cast and not age and not v['stackItems'] and any(c['id']==source for c in one['battlefield']):
                        assert paid==2 and sum(one['manaPool'])==0;age=1;snapshot(m,'cast')
                    elif age and v['phase']=='UPKEEP' and v['turnSeat']==actor:
                        if v['stackItems'] and before is None:
                            before=proof(r,'before-'+str(age));initial_graves=[{c['id'] for c in x['graveyardCards']} for x in players];ordered=[];bottom=[];snapshot(m,'before-'+str(age))
                        elif not v['stackItems'] and before is not None:
                            after=proof(r,'resolved-'+str(age));grave=[{c['id'] for c in x['graveyardCards']} for x in players]
                            failed=case in ('cancel-player','cancel-card','insufficient')
                            card=next((c for c in one['battlefield'] if c['id']==source),None)
                            assert bool(card)==(not failed),'Upkeep sacrifice incorrect'
                            if failed:
                                assert source in grave[actor-1];assert not chosen or case=='cancel-card'
                                assert after==before,'Cancelled/unpayable cost moved library cards'
                                assert grave[actor-1]-{source}==initial_graves[actor-1] and grave[2-actor]==initial_graves[2-actor]
                                assert player_requests==(0 if case=='insufficient' else 1)
                            else:
                                assert next(c['count'] for c in card['counters'] if c['kind']=='AGE')==age
                                assert len(chosen)==(2 if age==1 else 6)
                                count=2*age;current=chosen[-count:]
                                assert len(set(current))==count and set(current)<=initial_graves[target-1]
                                assert after[target-1][:-count]==before[target-1] and set(after[target-1][-count:])==set(current)
                                assert after[target-1][-count:]==bottom,'Owner-selected bottom order lost'
                                assert player_requests==(1 if age==1 else 3),'Age counter cost repetitions wrong'
                                assert after[2-target]==before[2-target] and grave[target-1]==initial_graves[target-1]-set(current)
                                assert grave[2-target]==initial_graves[2-target] and card['power']==card['toughness']==4
                            assert all(x['life']==20 for x in players);snapshot(m,'resolved-'+str(age))
                            if case=='second-age' and age==1:age=2;before=None
                            else:return 'native cast/two mana; actual upkeep age '+str(age)+'; original player/card identities, graveyard legality, exact library prefix and bottom ID set, cancellation/unpayable sacrifice or paid survivor; continued priority; '+case
                elif 'PayMana' in kind:
                    pick=next((i for i in ids if i.startswith('mana:')),None)
                    if pick is None:pick=next('card:'+str(c['id']) for c in one['battlefield'] if c['name']=='Plains' and 'card:'+str(c['id']) in ids)
                    paid+=1
                elif kind=='ChooseOptions':
                    assert r['seat']==actor and r['playerSeat']==actor,'Legacy helper wrong operator'
                    assert r['minCount']==0 and r['maxCount']==1
                    if not r['selectedCount']:
                        player_requests+=1;snapshot(m,'players-'+str(age))
                        options=[o for o in r['options'] if o['id'].startswith('select:')]
                        expected=[x['name'] for x in players if len({c['id'] for c in x['graveyardCards']})>=2*age]
                        assert [o['label'] for o in options]==expected,'Ineligible graveyard offered'
                        if case=='cancel-player':pick='confirm-cards';cancelled=True
                        else:pick=next(o['id'] for o in options if o['label']==players[target-1]['name'])
                    else:pick='confirm-cards';snapshot(m,'players-selected-'+str(age))
                elif kind=='InputSelectEntitiesFromList' and any(i.startswith('player:') for i in ids):
                    assert r['seat']==actor and r['playerSeat']==actor
                    if r.get('selectedCount') and 'ok' in ids:pick='ok';snapshot(m,'players-selected-'+str(age))
                    else:
                        player_requests+=1;snapshot(m,'players-'+str(age))
                        offered={i for i in ids if i.startswith('player:')}
                        # Forge repeats the two-card cost once per age counter;
                        # each repetition chooses a graveyard independently.
                        expected={'player:'+str(x['targetId']) for x in players if len(x['graveyardCards'])>=2}
                        assert offered==expected,'Ineligible graveyard offered'
                        if case=='cancel-player':pick='cancel';cancelled=True
                        else:pick='player:'+str(players[target-1]['targetId'])
                elif kind=='dialog':
                    cards=[o for o in r['options'] if o.get('card')]
                    if cards:
                        assert r['seat']==actor and r['playerSeat']==actor
                        offered={o['card']['id'] for o in cards};assert offered==initial_graves[target-1]-set(chosen),'Other graveyard or duplicate offered'
                        snapshot(m,'cards-'+str(age)+'-'+str(len(chosen)))
                        if case=='cancel-card' and chosen:pick='cancel';cancelled=True
                        else:
                            option=cards[0];pick=option['id'];chosen.append(option['card']['id'])
                    else:pick=ids[0]
                elif kind.startswith('InputSelectCards'):
                    if v['phase']=='UPKEEP' and v['turnSeat']==actor:
                        assert r['seat']==actor and r['playerSeat']==actor
                        snapshot(m,'cards-'+str(age)+'-'+str(r.get('selectedCount',0)))
                        offered={int(i[5:]) for i in ids if i.startswith('card:')}
                        assert offered<=initial_graves[target-1],'Cards from other graveyard offered'
                        if case=='cancel-card' and r.get('selectedCount'):pick='cancel';cancelled=True
                        elif 'ok' in ids:pick='ok'
                        else:
                            pick=next(i for i in ids if i.startswith('card:') and int(i[5:]) not in chosen);chosen.append(int(pick[5:]))
                    elif 'ok' in ids:pick='ok'
                    else:pick=next(i for i in ids if i.startswith('card:'))
                elif kind=='OrderAbilities':pick='confirm-order'
                elif kind=='OrderCards':
                    assert r['seat']==target and r['playerSeat']==target,'Owner does not order own library cards'
                    snapshot(m,'order-'+str(age)+'-'+str(r['selectedCount']))
                    if r['selectedCount']==0:ordered=[]
                    if r['remainingCount']==0:pick='confirm-cards';bottom.extend(reversed(ordered))
                    else:
                        option=next(o for o in r['options'] if o['id'].startswith('select:') and not o.get('selected'));pick=option['id'];ordered.append(option['card']['id'])
                elif kind=='RevealCards':pick='confirm-cards'
                assert pick in ids,(case,age,kind,pick,ids,r['message'])
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
        except Exception as e:
            result='FAIL '+case+': '+repr(e);(OUT/(case+'-error.txt')).write_text(traceback.format_exc(),encoding='utf8')
        print(result,flush=True);results.append(result)
    (OUT/'result.txt').write_text('\n'.join(results),encoding='utf8');sys.exit(any(x.startswith('FAIL') for x in results))
