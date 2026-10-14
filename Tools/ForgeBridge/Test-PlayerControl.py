"""Actual whole-turn / combat control: operator, private hand, mana, spells, attack and release."""
import json,os,queue,re,shutil,subprocess,sys,tempfile,threading,time
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2];RUNTIME=ROOT/'ForgeRuntime'
CFG=json.loads((RUNTIME/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'Tools/Validation/Reports/PlayerControl';CLASSES=ROOT/'.tools/player-control/classes'
if OUT.exists() and (OUT/'result.txt').exists():
    shutil.copytree(OUT,ROOT/'.tools/player-control/attempts'/str(time.time_ns()))
OUT.mkdir(parents=True,exist_ok=True);CLASSES.mkdir(parents=True,exist_ok=True)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RUNTIME/'classes')+';'+str(RUNTIME/'lib/*'),'-d',str(CLASSES),str(ROOT/'Tools/ForgeBridge/tests/PlayerControlFixture.java')],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES=['turn-one','turn-two','combat']

def run(case, observe=None):
    master=2 if case=='turn-two' else 1;slave=3-master;combat=case=='combat'
    with tempfile.TemporaryDirectory(prefix='mtgb-control-') as temp,(OUT/(case+'.log')).open('w',encoding='utf8') as log,(OUT/(case+'.jsonl')).open('w',encoding='utf8') as trace:
        start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session=case,fixture=case,seed=4,startingLife=20,firstPlayerSeat=master,decks=[DECK,DECK])),encoding='utf8')
        process=subprocess.Popen([str(RUNTIME/CFG['java']),'-Djava.awt.headless=true','-cp',';'.join(map(str,[CLASSES,RUNTIME/'classes',RUNTIME/'lib/*'])),'mtgb.bridge.PlayerControlFixture',str(RUNTIME/CFG['assets']),str(start)],cwd=RUNTIME/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
        messages=queue.Queue()
        def read():
            try:
                for line in process.stdout:messages.put(json.loads(line))
            except Exception as error:messages.put(error)
            finally:messages.put(None)
        threading.Thread(target=read,daemon=True).start()
        def write(r,option,seat=None):
            process.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'] if seat is None else seat,requestId=r['requestId'],option=option))+'\n');process.stdin.flush()
        def snapshot(m,label,seat=master):
            view=dict(m['views'][seat-1]);view.update(request=m['request'] if m['request']['seat']==seat else None,awaitingInput=m['request']['seat']==seat)
            (OUT/(case+'-'+label+'.json')).write_text(json.dumps(view,ensure_ascii=False,indent=2),encoding='utf8')
        cast=False;controlled=False;cast_count=0;floated=0;wrong_checked=False;attack=False;known_slave_hand=set();deadline=time.monotonic()+120
        try:
            for step in range(700):
                assert time.monotonic()<deadline,'Scenario deadline'
                m=messages.get(timeout=30);assert m is not None,'Bridge exited'
                if isinstance(m,Exception):raise m
                trace.write(json.dumps(m)+'\n');trace.flush()
                assert m['type'] not in ('fatal','ended','rejected'),m
                if m['type']!='request':continue
                r=m['request'];view=m['views'][master-1];p=view['players'][master-1];q=view['players'][slave-1]
                actual=r['playerSeat'];options=r['options'];ids=[o['id'] for o in options];kind=r['kind'];pick='ok'
                is_control=q.get('controllingSeat')==master
                if is_control:
                    known_slave_hand.update(c['id'] for c in q['hand'])
                    controlled=True
                    assert q['hand'],'Master cannot view controlled player hand'
                    assert not m['views'][slave-1]['players'][master-1]['hand'],'Slave sees master private hand'
                    assert r['seat']==master,'Controlled decision routed to slave'
                    if not wrong_checked:
                        snapshot(m,'controlled-hand');snapshot(m,'slave-view',slave)
                        write(r,ids[0],slave)
                        rejected=messages.get(timeout=10);trace.write(json.dumps(rejected)+'\n');trace.flush()
                        assert rejected['type']=='rejected' and rejected['error']=='wrong-seat',rejected
                        wrong_checked=True
                if controlled and not is_control and kind=='InputPassPriority' and (combat and view['phase']=='MAIN2' or not combat and view['turn']>=3):
                    assert wrong_checked and cast_count==2 and attack
                    assert q['life']==18 and p['life']==18,(q['life'],p['life'])
                    # Already seen identities stay known until a hand/library
                    # randomization clears them. Release removes control, not
                    # the player's memory; never disclose previously unseen IDs.
                    assert all(c['id'] in known_slave_hand for c in q['hand']),'Unseen hand exposed after release'
                    assert actual==r['seat'],'Physical seat did not regain ordinary decisions'
                    assert any(c['name']=='Opt' for c in q['graveyardCards']) and any(c['name']=='Shock' for c in q['graveyardCards'])
                    assert any(c['name']==('Secret of Bloodbending' if combat else 'Worst Fears') for c in p['exileCards'])
                    snapshot(m,'released');snapshot(m,'released-slave',slave)
                    return 'PASS '+case+': actual control spell/target/payment/exile; operator seat and private sight; wrong-seat rejection; controlled Opt/scry/draw and Shock/mana/target; attack and exact damage; expiry restores ordinary seat; retained hand identities limited to cards already seen'
                if kind=='InputPassPriority' and not view['stackItems']:
                    if not cast and actual==master and view['phase']=='MAIN1':
                        assert not q['hand'],'Opponent hand exposed before control'
                        snapshot(m,'before')
                        name='Secret of Bloodbending' if combat else 'Worst Fears'
                        pick='card:'+str(next(c for c in p['hand'] if c['name']==name)['id']);cast=True
                    elif is_control and actual==slave and cast_count<2 and (combat or view['phase']=='MAIN1'):
                        if floated<2:
                            island=next(c for c in q['battlefield'] if c['name']=='Island' and not c['tapped'] and 'card:'+str(c['id']) in ids)
                            pick='card:'+str(island['id']);floated+=1
                        else:
                            assert q['manaPool'][1]==(2 if cast_count==0 else 1),'Controlled mana was applied to wrong player'
                            name=['Opt','Shock'][cast_count]
                            pick='card:'+str(next(c for c in q['hand'] if c['name']==name)['id']);cast_count+=1
                            snapshot(m,'casting-'+name)
                elif 'PayMana' in kind:
                    paying=view['players'][actual-1]
                    cost=r['message'].split('Pay Mana Cost:',1)[-1];colors=re.findall(r'\{([WUBRG])\}',cost);allowed=colors or list('WUBRGC')
                    pick=next((i for i in ids if i.startswith('mana:') and i.split(':')[1] in allowed),None)
                    if pick is None:
                        basics=dict(zip('WUBRG',['Plains','Island','Swamp','Mountain','Forest']))
                        pick=next('card:'+str(c['id']) for c in paying['battlefield'] if c['name'] in [basics[x] for x in allowed if x in basics] and 'card:'+str(c['id']) in ids)
                    if is_control:
                        expected_blue=2 if cast_count==1 else 1
                        assert actual==slave and q['manaPool'][1]==expected_blue and p['manaPool'][1]==0,('Controlled payment mana owner',actual,cast_count,p['manaPool'],q['manaPool'])
                        if cast_count==2:snapshot(m,'controlled-payment')
                elif kind=='InputSelectTargets':
                    if r.get('selectedCount') and 'ok' in ids:pick='ok'
                    else:pick='player:'+str(q['targetId'])
                    if is_control:snapshot(m,'controlled-target')
                elif kind=='Scry':
                    assert is_control and actual==slave
                    pick=next((i for i in ids if i.startswith('scry-top:')),'confirm-scry');snapshot(m,'controlled-scry')
                elif kind=='InputAttack':
                    if is_control:
                        assert actual==slave
                        if not attack:
                            bear=next(c for c in q['battlefield'] if c['name']=='Grizzly Bears')
                            pick='attack:'+str(bear['id'])+':player:'+str(p['targetId']);attack=True;snapshot(m,'controlled-attack')
                elif kind=='ChooseOptions':
                    # Decline the optional waterbend cost, exercising combat-only duration.
                    assert combat and not controlled and 'confirm-cards' in ids
                    pick='confirm-cards'
                elif kind.startswith('InputSelectCards') or kind=='InputSelectEntitiesFromList':
                    # Cleanup discard is also a decision by the current controller.
                    pick='ok' if 'ok' in ids else next(o['id'] for o in options if o['id'].startswith('card:') and not o.get('selected'))
                elif kind=='dialog':pick=ids[0]
                elif kind=='RevealCards':pick='confirm-cards'
                elif kind=='OrderAbilities':pick='confirm-order'
                if observe is not None:
                    override=observe(m,pick)
                    if override is not None:pick=override
                assert pick in ids,(case,actual,kind,pick,[(o['id'],o['label']) for o in options])
                write(r,pick)
            raise AssertionError('No completion after 700 decisions')
        finally:
            if process.poll() is None:
                process.stdin.write('{"type":"shutdown"}\n');process.stdin.flush()
                try:process.wait(timeout=3)
                except subprocess.TimeoutExpired:process.kill();process.wait()

if __name__=='__main__':
    results=[]
    for case in sys.argv[1:] or CASES:
        try:result=run(case)
        except Exception as error:result='FAIL '+case+': '+repr(error)
        print(result,flush=True);results.append(result)
    (OUT/'result.txt').write_text('\n'.join(results),encoding='utf8')
    sys.exit(any(r.startswith('FAIL') for r in results))
