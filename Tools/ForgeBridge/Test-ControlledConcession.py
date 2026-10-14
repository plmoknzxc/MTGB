"""Actual controlled native waiting input -> controller concession -> terminal probes."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'.tools/controlled-concession-1009';OUT.mkdir(parents=True,exist_ok=True)
CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURES=[ROOT/'Tools/ForgeBridge/tests/RoyalStateFixture.java',ROOT/'Tools/ForgeBridge/tests/ControlledConcessionFixture.java']
SHA=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),*[str(p) for p in FIXTURES]],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}

def run(case):
    folder=OUT/'native'/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir()
    meta={'case':case,'runtimeHash':CFG['runtimeHash'],'driver':SHA(Path(__file__)),'fixtures':[{'path':p.relative_to(ROOT).as_posix(),'sha256':SHA(p)} for p in FIXTURES],'folder':folder.relative_to(ROOT).as_posix()}
    def finish(status,note):
        value={**meta,'status':status,'note':note}
        (folder/'result.json').write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        (OUT/(case+'-result.json')).write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        return value
    with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'trace.jsonl').open('w',encoding='utf-8') as trace,(folder/'engine.log').open('w',encoding='utf-8') as log:
        other={'Hand':['Divination','Opt'],'Battlefield':['Island']*3}
        if case.startswith('replacement-'):other['Battlefield']+=['Alhammarret\'s Archive','Thought Reflection']
        config={'session':case,'seed':4,'startingLife':30,'firstPlayerSeat':1,'decks':[DECK,DECK], 'own':{'Hand':['Worst Fears','Shock']},'other':other,'auditDir':str(audit)}
        start=Path(temp)/'start.json';start.write_text(json.dumps(config),encoding='utf-8')
        proc=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.ControlledConcessionFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf-8')
        inbox=queue.Queue()
        def reader():
            try:
                for line in proc.stdout:inbox.put(json.loads(line))
            finally:inbox.put(None)
        threading.Thread(target=reader,daemon=True).start()
        def send(value):proc.stdin.write(json.dumps(value)+'\n');proc.stdin.flush()
        def reply(r,option,seat=None):send({'type':'respond','session':case,'seat':seat or r['seat'],'requestId':r['requestId'],'option':option})
        def read(timeout=30):
            value=inbox.get(timeout=timeout);assert value is not None,'Native process exited';trace.write(json.dumps(value)+'\n');trace.flush();return value
        def snap(m,name):
            for index,v in enumerate(m['views'],1):
                value={**v,'request':m.get('request'),'awaitingInput':m.get('request',{}).get('seat')==index}
                (folder/(name+'-seat'+str(index)+'.json')).write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding='utf-8')
        fear_cast=False;fear_paid=False;controlled_cast=False;partial_click=False;initial_remaining=None;known_ids=set();deadline=time.monotonic()+240
        before_draw_hand=set();before_draw_library=None;normal_orders=0
        def terminal(m):
            r=m['request'];snap(m,'waiting')
            reply(r,r['options'][0]['id'],2);wrong=read(10)
            assert wrong['type']=='rejected' and wrong['error']=='wrong-seat',wrong
            send({'type':'concede','session':case,'seat':1})
            ended=None;after=[];limit=time.monotonic()+15
            while time.monotonic()<limit:
                value=read(10);after.append(value)
                assert value['type']!='fatal',value
                if value['type']=='ended':ended=value;break
            assert ended and ended['winnerSeat']==2,ended
            snap(ended,'ended')
            for seat in [1,2]:
                reply(r,r['options'][0]['id'],seat);rejected=read(10);after.append(rejected)
                assert rejected['type']=='rejected' and rejected['error']=='stale-request',rejected
            send({'type':'concede','session':case,'seat':1})
            limit=time.monotonic()+3
            while time.monotonic()<limit:
                try:value=read(.15)
                except queue.Empty:continue
                after.append(value)
                assert value['type'] not in ('fatal','request','accepted'),value
            assert sum(v['type']=='ended' for v in after)==1,after
            state=json.loads((audit/'terminal.json').read_text())
            assert state['gameOver'] and state['pendingAbsent'] and state['finished'],state
            assert state['activeGameWorkers']==0 and state['blockedNativeChoices']==0,('Native worker not released',state)
            return finish('PASS','Actual Worst Fears paid/resolved control; '+case+' waiting routed physical1/player2; actual controller concession -> winner2 once; wrong slave and both old replies rejected; pending cleared, native worker/callback released. UI and production session privacy clearing separate; no network/four-player claim.')
        try:
            for step in range(650):
                assert time.monotonic()<deadline,'Scenario deadline'
                m=read();assert m['type'] not in ('fatal','ended','rejected'),m
                if m['type']!='request':continue
                r=m['request'];kind=r['kind'];opts=r['options'];ids=[o['id'] for o in opts];v=m['views'][0];players=v['players'];pick=None
                if players[1].get('controllingSeat')==1:
                    known_ids.update(c['id'] for c in players[1]['hand'])
                    assert not m['views'][1]['players'][0]['hand'],'Slave sees unseen master hand'
                controlled=r.get('playerSeat')==2 and r['seat']==1
                if kind=='InputPassPriority' and not v['stackItems']:
                    if case=='replacement-complete' and controlled_cast and normal_orders:
                        assert players[1]['handCount']==len(before_draw_hand)+7,(players[1]['handCount'],len(before_draw_hand))
                        assert players[1]['libraryCount']==before_draw_library-8,(players[1]['libraryCount'],before_draw_library)
                        assert any(c['name']=='Divination' for c in players[1]['graveyardCards'])
                        snap(m,'normal-resolved');return finish('PASS','Actual controlled Divination, both mandatory native draw replacements chosen by original order IDs/confirmed; eight actual cards drawn, exact hand/library counts and Divination graveyard; same controller1/player2 priority restored. Normal integer callback completion regression, not another concession or new mechanic.')
                    if not fear_cast and r.get('playerSeat')==1 and v['phase']=='MAIN1':
                        assert not players[1]['hand'],'Unseen opponent hand before control'
                        card=next(c for c in players[0]['hand'] if c['name']=='Worst Fears');pick='card:'+str(card['id']);fear_cast=True
                    elif controlled and v['phase']=='MAIN1' and not controlled_cast:
                        assert fear_paid and any(c['name']=='Worst Fears' for c in players[0]['exileCards'])
                        before_draw_hand={c['id'] for c in players[1]['hand']};before_draw_library=players[1]['libraryCount'];snap(m,'before-controlled-cast')
                        card=next(c for c in players[1]['hand'] if c['name']=='Divination');pick='card:'+str(card['id']);controlled_cast=True
                    else:pick='ok'
                elif 'SelectTargets' in kind:
                    pick='ok' if 'ok' in ids and not next(o for o in opts if o['id']=='ok').get('disabled') else 'player:'+str(players[1]['targetId'])
                elif 'PayMana' in kind:
                    if controlled and controlled_cast and case=='partial-payment':
                        if not partial_click:
                            initial_remaining=r.get('remainingManaCost');snap(m,'before-partial');partial_click=True
                            islands={c['id'] for c in players[1]['battlefield'] if c['name']=='Island'}
                            pick=next(o['id'] for o in opts if o['id'].startswith('card:') and int(o['id'][5:]) in islands)
                        else:
                            assert r.get('remainingManaCost')!=initial_remaining,('No payment progress',r)
                            assert sum(c.get('tapped',False) for c in players[1]['battlefield'])==1,players[1]['battlefield']
                            assert r.get('remainingManaCost') not in ('','0','{0}',None),'Cost unexpectedly fully paid'
                            return terminal(m)
                    else:
                        pick='ok' if 'ok' in ids and not next(o for o in opts if o['id']=='ok').get('disabled') else next(o['id'] for o in opts if o['id'].startswith('card:'))
                        if pick=='ok':fear_paid=True
                elif case=='replacement-modal' and controlled and r.get('selectionPurpose')=='replacementOrder':
                    assert fear_cast and fear_paid and len([o for o in opts if o['id'].startswith('order:')])>=2,r
                    return terminal(m)
                elif case=='replacement-complete' and controlled and r.get('selectionPurpose')=='replacementOrder':
                    normal_orders+=1;pick='confirm-order' if 'confirm-order' in ids else next(i for i in ids if i.startswith('order:'))
                elif kind=='dialog' and controlled and controlled_cast:
                    # Land's single mana ability is usually auto-selected; only choose a native option.
                    pick=next(o['id'] for o in opts if not o.get('disabled'))
                elif 'ok' in ids:pick='ok'
                elif kind=='RevealCards':pick='confirm-cards'
                elif kind=='OrderAbilities':pick=next((i for i in ids if i.startswith('order:')),'confirm-order')
                else:raise AssertionError(('Unhandled',kind,r['message'],ids))
                assert pick in ids,(kind,pick,ids)
                print(case,step,kind,r['seat'],r.get('playerSeat'),pick,flush=True);reply(r,pick)
            raise AssertionError('Step limit')
        except Exception:
            finish('FAIL',traceback.format_exc());raise
        finally:
            if proc.poll() is None:proc.kill();proc.wait()

    return finish('FAIL','Unreachable')

if __name__=='__main__':
    for case in sys.argv[1:] or ['partial-payment','replacement-modal']:
        result=run(case);print(result['status'],result['note'],flush=True)
