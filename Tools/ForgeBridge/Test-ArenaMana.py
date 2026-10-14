"""Actual Arena of Glory exert cost, mana-carried haste, expiry and cancellation."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'.tools/arena-mana-1006/native';OUT.mkdir(parents=True,exist_ok=True);CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURE=ROOT/'Tools/ForgeBridge/tests/RestrictedManaFixture.java';SHA=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),str(FIXTURE)],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES=['haste-expiry','split-haste','cancel-cost','noncreature']
def resource_state(player):
    # Stack grouping eligibility is a derived UI hint, not a paid resource.
    state=json.loads(json.dumps(player))
    for card in state['battlefield']:card.pop('stackablePermanent',None)
    return state
def run(case):
    folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True)
    meta=dict(case=case,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],driverSha256=SHA(Path(__file__)),fixtures=[dict(path=FIXTURE.relative_to(ROOT).as_posix(),sha256=SHA(FIXTURE))])
    def finish(status,message):
        result=dict(meta,status=status,result=status+' '+case+': '+message)
        for p in [folder/'result.json',OUT/(case+'-result.json')]:p.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
        return result
    with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'engine.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
        names=['Bloodlust Inciter','Goblin Arsonist'] if case=='split-haste' else ['Shock'] if case=='noncreature' else ['Goblin Piker']
        start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session=case,fixture=case,hand=names,lands=['Arena of Glory','Mountain'],seed=4,firstPlayerSeat=1,startingLife=20,decks=[DECK,DECK])),encoding='utf8')
        proc=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.RestrictedManaFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
        messages=queue.Queue()
        def reader():
            try:
                for line in proc.stdout:messages.put(json.loads(line))
            finally:messages.put(None)
        threading.Thread(target=reader,daemon=True).start()
        def snap(m,label):
            for seat,v in enumerate(m['views'],1):(folder/(label+'-seat'+str(seat)+'.json')).write_text(json.dumps(dict(v,request=m['request'],awaitingInput=True),ensure_ascii=False,indent=2),encoding='utf8')
        activation=False;baseline=None;cancelled=False;cast_count=0;resolved_count=0;attacked=False;selected=[];paid_cost=False;exerted=False;skipped=False;expired=False
        try:
            for step in range(400):
                m=messages.get(timeout=35);assert m is not None,'Bridge exited';trace.write(json.dumps(m,ensure_ascii=False)+'\n');trace.flush();assert m['type'] not in {'fatal','rejected','ended'},m
                if m['type']!='request':continue
                r=m['request'];v=m['views'][0];one=v['players'][0];kind=r['kind'];opts=r['options'];ids=[o['id'] for o in opts];pick='ok'
                arena=next((c for c in one['battlefield'] if c['name']=='Arena of Glory'),None)
                if kind=='InputPassPriority' and r['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
                    if not activation:
                        baseline=json.loads(json.dumps(one));pick='card:'+str(arena['id']);activation=True;snap(m,'before-activation')
                    elif cancelled and not exerted:
                        assert resource_state(one)==resource_state(baseline), 'Cancelled exert/mana activation changed resources or state';snap(m,'cancel-restored');exerted=True;activation=False;cancelled=False
                        # The cancelled attempt is proved; retry the exact same activation.
                        pick='card:'+str(arena['id']);activation=True
                    elif resolved_count<len(names):
                        if cast_count>resolved_count:
                            if case=='noncreature':
                                assert one['manaPool'][3]==1 and sum(one['manaPool'])==1 and v['players'][1]['life']==18 and 'Shock' in one['graveyard']
                            else:
                                live=next(c for c in one['battlefield'] if c['name']==names[resolved_count]);assert 'HASTE' in live['keywords'] and 'HASTE' in live['grantedKeywords']
                                assert one['manaPool'][3]==(1 if case=='split-haste' and resolved_count==0 else 0)
                            snap(m,'resolved-'+str(resolved_count));resolved_count+=1
                        if resolved_count<len(names):
                            if cast_count==0:
                                assert arena['tapped'] and arena['exertedThisTurn']==1 and arena['exertedBySeats']==[1]
                                assert one['manaPool'][3]==2 and sum(one['manaPool'])==2 and next(c for c in one['battlefield'] if c['name']=='Mountain')['tapped'];snap(m,'produced')
                            card=next(c for c in one['hand'] if c['name']==names[cast_count]);pick='card:'+str(card['id']);cast_count+=1
                        elif case=='noncreature':return finish('PASS','Actual R+tap+exert activation makes RR; spent on Shock without haste/restriction, native damage20->18, one R remains and priority restores.')
                    elif v['turn']==3:
                        assert arena['tapped'] and not arena['exertedBySeats'] and attacked and expired
                        skipped=True;snap(m,'skipped-next-untap')
                    elif v['turn']==5:
                        assert skipped and not arena['tapped'] and not arena['exertedBySeats'] and v['players'][1]['life']==18
                        snap(m,'untap-restored');return finish('PASS','Actual exert mana/creature spending grants haste, exact first-turn attack/damage, EOT expiry, next own untap skipped and following untap restores; native priority continues.')
                elif kind=='dialog':
                    snap(m,'ability-choice');pick=next(o['id'] for o in opts if '{R}{R}' in o['label'].replace(' ',''))
                elif 'PayMana' in kind:
                    snap(m,'payment-'+str(step))
                    if one['manaPool'][3]>0:pick='mana:R'
                    else:pick='card:'+str(next(c for c in one['battlefield'] if c['name']=='Mountain' and not c['tapped'])['id']);paid_cost=True
                elif kind.startswith('InputSelect') and 'Targets' not in kind:
                    snap(m,'exert-cost-'+str(step))
                    if r['message'].startswith('Cleanup Phase'):
                        pick='ok' if 'ok' in ids else next(o['id'] for o in opts if o['id'].startswith('card:') and not o.get('selected'))
                    elif case=='cancel-cost' and not exerted and not cancelled:
                        assert 'cancel' in ids, 'Exert cost has no cancellation contract';pick='cancel';cancelled=True
                    elif 'ok' in ids:pick='ok'
                    else:pick='card:'+str(arena['id'])
                elif kind=='InputSelectTargets':pick='ok' if r.get('selectedCount') else 'player:'+str(v['players'][1]['targetId'])
                elif kind=='InputConfirm' and r['message']=='Exert Arena of Glory?' and case=='cancel-cost' and not exerted:
                    snap(m,'exert-confirm-cancel');pick='cancel';cancelled=True
                elif kind=='InputAttack' and r['seat']==1:
                    if v['turn']==1:
                        snap(m,'haste-attack')
                        attackers={c['id'] for c in one['battlefield'] if c['name'] in names}
                        choices=[o for o in opts if o['id'].startswith('attack:') and o['attackerId'] in attackers and o['attackerId'] not in selected]
                        if choices:pick=choices[0]['id'];selected.append(choices[0]['attackerId'])
                        else:assert len(selected)==len(names), 'Haste did not create native attack permission';pick='ok';attacked=True
                    else:pick='ok'
                elif kind=='InputPassPriority' and v['turn']==2 and not expired:
                    assert all('HASTE' not in c['keywords'] and 'HASTE' not in c['grantedKeywords'] for c in one['battlefield'] if c['name'] in names)
                    assert v['players'][1]['life']==18;expired=True;snap(m,'haste-expired')
                elif kind=='InputMulligan':pick='keep'
                elif kind not in {'InputPassPriority','InputConfirmMulligan','InputConfirm','InputBlock','InputAttack'}:raise AssertionError(('Unhandled Arena flow',r))
                assert pick in ids,(case,kind,pick,r)
                print(case,step,v['turn'],kind,pick,flush=True);proc.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');proc.stdin.flush()
            raise AssertionError('Step limit')
        except Exception as e:finish('FAIL',repr(e));raise
        finally:
            if proc.poll() is None:proc.kill();proc.wait()
if __name__=='__main__':
    for case in sys.argv[1:] or CASES:print(json.dumps(run(case)),flush=True)
