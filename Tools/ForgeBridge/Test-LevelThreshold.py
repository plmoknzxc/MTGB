"""One native level-up lifetime with real payment, response timing, removal and expiry."""
import json,os,queue,subprocess,tempfile,threading,time,traceback,shutil,sys
from pathlib import Path
R=Path(__file__).resolve().parents[2];B=R/'.tools/level-threshold-1010';B.mkdir(exist_ok=True);RT=R/'ForgeRuntime'
cfg=json.loads((RT/'runtime.json').read_text('utf-8-sig'));C=B/'classes';C.mkdir(exist_ok=True)
fix=[R/'Tools/ForgeBridge/tests/LevelThresholdFixture.java',R/'Tools/ForgeBridge/tests/StateBranchFixture.java']
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(C),*map(str,fix)],check=True)
remove_tail='--remove-tail'in sys.argv
expiry_tail='--expiry-tail'in sys.argv
tail='--tail'in sys.argv or remove_tail or expiry_tail
F=B/'native'/str(time.time_ns());F.mkdir(parents=True);audit=F/'audit';audit.mkdir();checks=[];paid=[];seen=set();stage='start';ids={};base=None;stamp=None
def wr(p,z):p.write_text(json.dumps(z,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def ck(v,label):
    assert v,label
    checks.append(label)
def obs(rid):
    end=time.monotonic()+5
    while time.monotonic()<end:
        try:return json.loads((audit/(str(rid)+'.json')).read_text('utf8'))
        except(FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
    raise AssertionError('Missing read-only native snapshot')
def card(n,id):return next(c for p in n['players']for c in p['cards']if c['id']==id)
def snap(m,n,label):wr(F/(label+'.json'),m);wr(F/(label+'-native.json'),n)
def stats(m,n,level,power,toughness,boost=0):
    c=card(n,ids['commander']);ck(c['zone']=='Battlefield'and c['timestamp']==stamp,'Same original commander and battlefield timestamp')
    ck(c['level']==level,'Exact native LEVEL count '+str(level));ck((c['power'],c['toughness'])==(power,toughness),'Exact final commander dimensions '+str((power,toughness)))
    natural=2 if level<2 else 3 if level<4 else 4
    ck((c['basePower'],c['baseToughness'])==(2,2),'Printed baseline2/2 retained');ck((c['currentPower'],c['currentToughness'])==(natural,natural),'Actual threshold sets correct base without counting additive boost')
    ck(c['flying']==(level>=2),'Flying enters/exits at exact threshold')
    ck((card(n,ids['looter'])['power'],card(n,ids['looter'])['toughness'])==((2,2)if level>=4 else(1,1)),'Only own other Merfolk receives threshold bonus')
    ck((card(n,ids['otherlooter'])['power'],card(n,ids['otherlooter'])['toughness'])==(1,1),'Opponent Merfolk unchanged')
    ck((card(n,ids['bear'])['power'],card(n,ids['bear'])['toughness'])==(2,2),'Own non-Merfolk unchanged')
    for view in m['views']:
        w=next(x for x in view['players'][0]['battlefield']if x['id']==ids['commander'])
        ck((w['power'],w['toughness'])==(power,toughness),'Both public views exact final PT')
        ck((w['basePower'],w['baseToughness'])==(2,2)and(w['effectiveBasePower'],w['effectiveBaseToughness'])==(natural,natural),'Wire preserves printed/effective baseline distinction')
        ck(w['powerBaseChanged']==(level>=2)and w['toughnessBaseChanged']==(level>=2),'Base rewrite flags do not mistake additive +3/+3 for base rewrite')
        ck(('FLYING'in w['grantedKeywords'])==(level>=2),'Wire keyword change and reversal exact')
    ck(n['players'][0]['life']==20 and n['players'][1]['life']==20,'Life ledger unaffected')
    ck(n['players'][0]['library']==base['players'][0]['library']and n['players'][1]['library']==base['players'][1]['library'],'Exact private library orders unchanged before next draw')
    ck(not m['views'][0]['players'][1]['hand']and not m['views'][1]['players'][0]['hand'],'Unrevealed hand identities private')
for p,name in [(Path(__file__),'driver.py'),(fix[0],'fixture.java'),(fix[1],'state-fixture.java'),(RT/'runtime.json','runtime.json')]:shutil.copy2(p,F/name)
deck=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
wr(F/'start.json',dict(session='level-threshold',auditDir=str(audit),seed=4,startingLife=20,firstPlayerSeat=1,initialLevel=4 if remove_tail else 0,decks=[deck,deck],own=dict(Hand=['Giant Growth']if tail else['Coralhelm Commander','Giant Growth'],Battlefield=['Merfolk Looter','Grizzly Bears','Vampire Hexmage']+(['Coralhelm Commander']if tail else[])),other=dict(Battlefield=['Merfolk Looter'])))
with tempfile.TemporaryDirectory(prefix='mtgb-level-')as tmp,(F/'stderr.log').open('w',encoding='utf8')as log,(F/'trace.jsonl').open('w',encoding='utf8')as trace:
    p=subprocess.Popen([str(RT/cfg['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[C,RT/'classes',RT/'lib/*'])),'mtgb.bridge.LevelThresholdFixture',str(RT/cfg['assets']),str(F/'start.json')],cwd=RT/cfg['assets'],env=dict(os.environ,APPDATA=tmp,LOCALAPPDATA=tmp,TEMP=tmp,TMP=tmp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');q=queue.Queue()
    def reader():
        try:
            for line in p.stdout:q.put(json.loads(line))
        except Exception as e:q.put(e)
        finally:q.put(None)
    threading.Thread(target=reader,daemon=True).start()
    try:
        for step in range(240):
            m=q.get(timeout=30);ck(isinstance(m,dict),'Native alive');trace.write(json.dumps(m)+'\n');trace.flush();ck(m['type']not in['fatal','ended','rejected'],'No unexpected terminal/rejection')
            if m['type']!='request':continue
            r=m['request'];rid=r['requestId'];kind=r['kind'];en=[o['id']for o in r['options']if not o.get('disabled')]
            if rid in seen:continue
            seen.add(rid);v=m['views'][0];n=obs(rid)if'Mulligan'not in kind else None;pick=None
            if 'Mulligan'in kind:pick='keep'if'keep'in en else'ok'
            elif kind=='InputPassPriority':
                if stage=='start'and r['seat']==1 and v['phase']=='MAIN1'and not v['stackItems']:
                    own=v['players'][0];ids={key:next(c['id']for c in own[zone]if c['name']==name)for key,zone,name in [('commander','battlefield'if tail else'hand','Coralhelm Commander'),('growth','hand','Giant Growth'),('looter','battlefield','Merfolk Looter'),('bear','battlefield','Grizzly Bears'),('hexmage','battlefield','Vampire Hexmage')]};ids['otherlooter']=next(c['id']for c in v['players'][1]['battlefield']if c['name']=='Merfolk Looter');base=n;snap(m,n,'before');pick='card:'+str(ids['commander']);stage='cast'
                    if tail:
                        stamp=card(n,ids['commander'])['timestamp']
                        if remove_tail:stats(m,n,4,4,4);snap(m,n,'initial-level4');stage='remove';pick='card:'+str(ids['hexmage'])
                        elif expiry_tail:stats(m,n,0,2,2);snap(m,n,'level0');stage='post-remove-growth';pick='card:'+str(ids['growth'])
                        else:stats(m,n,0,2,2);snap(m,n,'level0');stage='level1'
                elif ids and r['seat']==1 and not v['stackItems']and v['phase']=='MAIN1'and v['turnSeat']==1:
                    if stage=='cast':stamp=card(n,ids['commander'])['timestamp'];stats(m,n,0,2,2);snap(m,n,'level0');stage='level1';pick='card:'+str(ids['commander'])
                    elif stage.startswith('level'):
                        level=int(stage[5:]);stats(m,n,level,2 if level<2 else 3 if level<4 else 4,2 if level<2 else 3 if level<4 else 4);snap(m,n,stage)
                        if level<4:stage='level'+str(level+1);pick='card:'+str(ids['commander'])
                        else:stage='growth';pick='card:'+str(ids['growth'])
                    elif stage=='growth':stats(m,n,4,7,7,3);snap(m,n,'growth');stage='remove';pick='card:'+str(ids['hexmage'])
                    elif stage=='remove':
                        stats(m,n,0,2 if remove_tail else 5,2 if remove_tail else 5);ck(card(n,ids['hexmage'])['zone']=='Graveyard','Original Hexmage actually sacrificed');snap(m,n,'removed');stage='post-remove-growth'if remove_tail else'next-turn';pick='card:'+str(ids['growth'])if remove_tail else'ok'
                    elif stage=='post-remove-growth':stats(m,n,0,5,5,3);snap(m,n,'post-remove-growth');stage='next-turn';pick='ok'
                    else:pick='ok'
                elif stage=='next-turn'and v['turnSeat']==2 and v['phase']=='MAIN1'and not v['stackItems']:
                    c=card(n,ids['commander']);ck(c['level']==0 and(c['power'],c['toughness'])==(2,2)and not c['flying'],'Natural end-turn expiry restores2/2 without level/flight');ck(not any(a['canPlay']for a in c['abilities']if a['levelUp']),'Level up unavailable on opponents turn');ck('card:'+str(ids['commander'])not in en,'Native opponent request cannot activate own level up');snap(m,n,'expired');expected=1 if remove_tail or expiry_tail else 5 if tail else 7;ck(len(paid)==(1 if remove_tail or expiry_tail else 5 if tail else 6),'Exact actual native payments for requested full or tail scope');ck(sum(c['tapped']for c in n['players'][0]['cards']if c['name']in['Island','Forest'])==expected,'Exact original mana sources committed');wr(F/'result.json',dict(status='PASS',checks=len(checks),assertions=checks,paid=paid,tail=tail,removalTail=remove_tail,expiryTail=expiry_tail,actualPaidOperations=len(paid),actualSacrificeActivations=0 if expiry_tail else 1,committedMana=expected,sourceIds=ids,runtimeHash=cfg['runtimeHash'],folder=F.relative_to(R).as_posix(),completedInputIds=['level-threshold-baseline']));wr(B/'native-result.json',dict(status='PASS',folder=F.relative_to(R).as_posix()));print('PASS',F,len(checks),flush=True);break
                else:
                    if ids and v['stackItems']and r['seat']==1 and card(n,ids['commander'])['zone']=='Battlefield':ck(not any(a['canPlay']for a in card(n,ids['commander'])['abilities']if a['levelUp']),'Sorcery-only level up unavailable while stack occupied')
                    pick='ok'
            elif 'PayMana'in kind:snap(m,n,'payment-'+str(len(paid)));paid.append(dict(stage=stage,cost=r.get('remainingManaCost'),requestId=rid));pick='ok'
            elif kind=='InputSelectTargets':pick='ok'if r.get('remainingCount')==0 and'ok'in en else'card:'+str(ids['commander'])
            elif kind=='dialog':
                options=[o for o in r['options']if not o.get('disabled')];option=next((o for o in options if 'level up'in o['label'].lower()),None)if stage.startswith('level')else next((o for o in options if 'remove all counters'in o['label'].lower()),None)
                ck(option is not None,'Exact generated level-up or native Hexmage ability choice');snap(m,n,'choice-'+str(rid));pick=option['id']
            elif kind=='OrderAbilities':pick='confirm-order'
            elif kind=='InputConfirm':ck(stage=='remove'and r['message']=='Sacrifice Vampire Hexmage?','Exact native sacrifice cost confirmation');snap(m,n,'sacrifice-confirm');pick='ok'
            elif kind=='InputAttack':ck(stage=='next-turn','Natural no-attack turn traversal after completed result');pick='ok'
            else:raise AssertionError(('Unhandled native input',stage,r))
            ck(pick in en,('Exact enabled native option',stage,kind,pick,en));z=dict(type='respond',session='level-threshold',seat=r['seat'],requestId=rid,option=pick);trace.write(json.dumps(z)+'\n');trace.flush();p.stdin.write(json.dumps(z)+'\n');p.stdin.flush();print(step,stage,kind,pick,flush=True)
        else:raise AssertionError('Step limit')
    except Exception:
        wr(F/'result.json',dict(status='FAIL',stage=stage,paid=paid,checks=len(checks),error=traceback.format_exc(),runtimeHash=cfg['runtimeHash']));print('FAIL',F,traceback.format_exc(),flush=True);raise
    finally:
        if p.poll()is None:p.kill();p.wait()
