"""Native replacement order, quantities, ownership and zero-target resolution."""
import json,os,queue,shutil,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'Tools/Validation/Reports/Replacements';CLASSES=ROOT/'.tools/replacements/classes'
if (OUT/'result.txt').exists():shutil.copytree(OUT,ROOT/'.tools/replacements/attempts'/str(time.time_ns()))
OUT.mkdir(parents=True,exist_ok=True);CLASSES.mkdir(parents=True,exist_ok=True)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),*[str(ROOT/'Tools/ForgeBridge/tests'/n) for n in ('LibraryCostFixture.java','ReplacementFixture.java')]],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES=['counter-none','counter-zero-target','counter-scales-first','counter-double-first','counter-single-choice','counter-seat-two','counter-other-player','counter-repeat','token-double','token-manufactor','token-other-player','zone-rest-first','zone-leyline-first']

def run(case):
    actor=2 if case=='counter-seat-two' else 1
    target=3-actor if case=='counter-other-player' or case.startswith('zone-') else actor
    spell='Travel Preparations' if case.startswith('counter-') else {'token-double':'Raise the Alarm','token-manufactor':'Gilded Goose','token-other-player':'Thran Spider'}.get(case,'Lightning Bolt')
    with tempfile.TemporaryDirectory(prefix='mtgb-replacement-') as temp,(OUT/(case+'.log')).open('w',encoding='utf8') as log,(OUT/(case+'.jsonl')).open('w',encoding='utf8') as trace:
        audit=Path(temp)/'audit';audit.mkdir();start=Path(temp)/'start.json'
        start.write_text(json.dumps(dict(session=case,fixture=case,auditDir=str(audit),seed=4,startingLife=20,firstPlayerSeat=actor,decks=[DECK,DECK])),encoding='utf8')
        p=subprocess.Popen([str(RT/CFG['java']),'-Djava.awt.headless=true','-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.ReplacementFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
        messages=queue.Queue()
        def read():
            try:
                for line in p.stdout:messages.put(json.loads(line))
            except Exception as e:messages.put(e)
            finally:messages.put(None)
        threading.Thread(target=read,daemon=True).start()
        def snapshot(m,label):
            r=m['request'];seat=r['seat'];v=dict(m['views'][seat-1]);v.update(request=r,awaitingInput=True)
            (OUT/(case+'-'+label+'.json')).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf8')
        def proof(r,label):
            path=audit/(str(r['requestId'])+'.json');end=time.monotonic()+5
            while not path.exists() and time.monotonic()<end:time.sleep(.01)
            assert path.exists(),'Missing library proof';d=json.loads(path.read_text());assert d['requestId']==r['requestId']
            (OUT/(case+'-'+label+'-audit.json')).write_text(json.dumps(d,indent=2),encoding='utf8');return [x['library'] for x in d['players']]
        started=False;source=None;initial=None;library=None;target_id=None;paid=0;round_no=0;replacement_requests=0;replacement_steps=0;selected=set();sources=[];cached_reset=False;deadline=time.monotonic()+160
        try:
            for step in range(700):
                assert time.monotonic()<deadline,'Deadline';m=messages.get(timeout=35);assert m is not None,'Bridge exited'
                if isinstance(m,Exception):raise m
                trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ('fatal','ended','rejected'),m
                if m['type']!='request':continue
                r=m['request'];v=m['views'][actor-1];players=v['players'];one=players[actor-1];ids=[o['id'] for o in r['options']];kind=r['kind'];pick='ok'
                assert not m['views'][2-actor]['players'][actor-1]['hand'],'Private hand leak'
                if kind=='InputPassPriority' and not v['stackItems']:
                    if not started and r['seat']==actor and v['phase']=='MAIN1':
                        source=next(c['id'] for c in one['hand'] if c['name']==spell);sources.append(source)
                        initial=[{c['id'] for c in x['battlefield']} for x in players];library=proof(r,'before-'+str(round_no));paid=0
                        if case.startswith('counter-') or case.startswith('zone-'):target_id=next(c['id'] for c in players[target-1]['battlefield'] if c['name']=='Grizzly Bears')
                        snapshot(m,'before-'+str(round_no));pick='card:'+str(source);started=True
                    elif started and source not in {c['id'] for c in one['hand']}:
                        assert paid==({'Travel Preparations':2,'Raise the Alarm':2,'Gilded Goose':1,'Thran Spider':3,'Lightning Bolt':1}[spell]),('wrong cost',paid)
                        assert sum(one['manaPool'])==0;assert all(x['life']==20 for x in players)
                        assert proof(r,'resolved-'+str(round_no))==library,'Unrelated library movement'
                        if case.startswith('counter-'):
                            bear=next(c for c in players[target-1]['battlefield'] if c['id']==target_id)
                            count=next((x['count'] for x in bear['counters'] if x['kind']=='P1P1'),0)
                            expected=0 if case=='counter-zero-target' else 1 if case=='counter-none' else 4 if case=='counter-scales-first' else 7 if case=='counter-repeat' and round_no==1 else 3
                            assert count==expected and bear['power']==bear['toughness']==2+expected,('wrong order/count',count,expected)
                            assert source in {c['id'] for c in one['graveyardCards']}
                            assert [{c['id'] for c in x['battlefield']} for x in players]==initial
                        elif case.startswith('token-'):
                            new=[[c for c in x['battlefield'] if c['id'] not in initial[i]] for i,x in enumerate(players)]
                            if case=='token-double':
                                assert len(new[actor-1])==8 and not new[2-actor]
                                assert all(c['token'] and c['power']==c['toughness']==1 for c in new[actor-1])
                            elif case=='token-manufactor':
                                assert len(new[actor-1])==7 and not new[2-actor]
                                tokens=[c for c in new[actor-1] if c.get('token')];assert len(tokens)==6
                                assert sorted(c['name'] for c in tokens)==['Clue Token','Clue Token','Food Token','Food Token','Treasure Token','Treasure Token']
                                assert next(c for c in new[actor-1] if c['id']==source)['name']=='Gilded Goose'
                            else:
                                assert len(new[actor-1])==3 and len(new[2-actor])==1
                                assert sum(bool(c.get('token')) for c in new[actor-1])==2 and all(c.get('token') and c['tapped'] and c['name']=='Powerstone Token' for c in new[2-actor])
                                assert all(c['tapped'] and c['name']=='Powerstone Token' for c in new[actor-1] if c.get('token'))
                            tokens=[c for group in new for c in group if c.get('token')]
                            assert len({c['id'] for c in tokens})==len(tokens)
                            # Each battlefield list is the native controller's zone;
                            # ownerSeat is independently serialized on the card.
                            assert all(c['ownerSeat']==i+1 for i,group in enumerate(new) for c in group)
                        else:
                            assert source in {c['id'] for c in one['exileCards']}
                            assert target_id in {c['id'] for c in players[target-1]['exileCards']}
                            assert all(source not in {c['id'] for c in x['graveyardCards']} and target_id not in {c['id'] for c in x['graveyardCards']} for x in players)
                            assert [{c['id'] for c in x['battlefield']} for x in players]==[initial[i]-({target_id} if i==target-1 else set()) for i in range(2)]
                        snapshot(m,'resolved-'+str(round_no))
                        if case=='counter-repeat' and round_no==0:
                            round_no=1;selected=set();paid=0
                            source=next(c['id'] for c in one['hand'] if c['name']==spell);sources.append(source)
                            library=proof(r,'before-'+str(round_no));snapshot(m,'before-'+str(round_no));pick='card:'+str(source)
                        else:
                            if case in ('counter-none','counter-zero-target','token-other-player'):assert replacement_requests==0
                            else:assert replacement_requests>=1,'No real replacement choice'
                            return 'native cast/target/mana; exact source and affected seat, replacement order/counter or token quantities, IDs/ownership/zones, unchanged full libraries and continued priority; '+case
                elif 'PayMana' in kind:
                    pick=next((i for i in ids if i.startswith('mana:')),None)
                    if pick is None:
                        legal=[c for c in one['battlefield'] if 'card:'+str(c['id']) in ids and c.get('land')]
                        preferred='Plains' if spell=='Raise the Alarm' and not paid else 'Forest' if spell=='Travel Preparations' and not paid else None
                        c=next((c for c in legal if c['name']==preferred),legal[0]);pick='card:'+str(c['id'])
                    paid+=1
                elif kind=='InputSelectTargets':
                    snapshot(m,'targets-'+str(round_no)+'-'+str(r.get('selectedCount',0)))
                    if case=='counter-zero-target':pick='ok'
                    elif 'ok' in ids and target_id is not None and 'card:'+str(target_id) not in ids:pick='ok'
                    elif 'ok' in ids and r.get('selectedCount'):pick='ok'
                    elif case=='token-other-player':pick=next(i for i in ids if i.startswith('player:') and i=='player:'+str(players[2-actor]['targetId']))
                    else:pick='card:'+str(target_id)
                elif kind in ('OrderCards','OrderAbilities'):
                    if not r.get('selectedCount') and not r.get('orderedAbilities'):replacement_requests+=1;selected=set()
                    assert r['seat']==target and r['playerSeat']==target,(case,'wrong replacement actor',r['seat'],target)
                    snapshot(m,'replacement-'+str(round_no)+'-'+str(replacement_steps));replacement_steps+=1
                    options=[o for o in r['options'] if o['id'].startswith(('select:','order:')) and not o.get('selected')]
                    first='Hardened Scales' if case=='counter-scales-first' or case=='counter-repeat' and round_no==1 else 'Leyline of the Void' if case=='zone-leyline-first' else 'Rest in Peace' if case.startswith('zone-') else 'Academy Manufactor' if case=='token-manufactor' else 'Doubling Season'
                    if kind=='OrderAbilities':
                        assert r['selectionPurpose']=='replacementOrder'
                        entries=[o['ability'] for o in options]+r['orderedAbilities']
                        assert all(e['kind']=='replacement' and e['source']['id'] in {c['id'] for pview in players for c in pview['battlefield']} for e in entries),'Missing/wrong replacement source'
                        assert len({e['id'] for e in entries})==len(entries),'Replacement identity collision'
                    if case=='counter-repeat' and round_no==1 and r.get('selectedCount') and not cached_reset:
                        assert len(r['orderedAbilities'])==2,'Saved native order lost';pick='reset-order';cached_reset=True
                    elif case=='counter-single-choice' and r.get('selectedCount')==1:
                        assert len(options)==1 and 'confirm-order' in ids,'Cannot confirm first replacement alone';pick='confirm-order'
                    elif not options:pick='confirm-cards' if kind=='OrderCards' else 'confirm-order'
                    else:
                        option=next((o for o in options if first in o['label'] and not selected),options[0]);pick=option['id'];selected.add(option['label'])
                elif kind=='InputConfirm':pick='ok'
                elif kind=='RevealCards':pick='confirm-cards'
                assert pick in ids,(case,kind,pick,ids,r['message'])
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
