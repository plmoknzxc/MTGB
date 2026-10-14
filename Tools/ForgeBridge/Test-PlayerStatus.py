"""Actual day/night transitions and speed conditions; native audits are read-only."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
OUT=ROOT/'.tools/player-status-1006/native';CLASSES=OUT/'classes';CLASSES.mkdir(parents=True,exist_ok=True)
FIXTURES=[ROOT/'Tools/ForgeBridge/tests/StateBranchFixture.java',ROOT/'Tools/ForgeBridge/tests/PlayerStatusFixture.java']
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),*map(str,FIXTURES)],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES=['night-explicit','day-switch','day-natural','speed-none','speed-once','speed-self','speed-offturn','speed-source-leave','speed-max']

def run(case):
    source='Into the Night' if case=='night-explicit' else 'The Celestus' if case=='day-switch' else 'Brutal Cathar' if case=='day-natural' else 'Gas Guzzler'
    hand=[source] if not case.startswith('speed') else [] if case=='speed-none' else [source]
    if case=='day-natural' or case.startswith('speed'):hand+=['Shock']*(5 if case=='speed-max' else 2)
    own={'Hand':hand,'Battlefield':['Grizzly Bears'] if case=='speed-max' else []};other={}
    folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True)
    meta={'case':case,'folder':str(folder.relative_to(ROOT)).replace('\\','/'),'runtimeHash':CFG['runtimeHash'],'driverSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'fixtures':[{'path':str(p.relative_to(ROOT)).replace('\\','/'),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in FIXTURES]}
    def result(status,text,error=None):
        v={**meta,'status':status,'result':status+' '+case+': '+text}
        if error:v['error']=repr(error)
        for p in [folder/'result.json',OUT/(case+'-result.json')]:p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        return v
    with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'trace.jsonl').open('w',encoding='utf8') as trace,(folder/'engine.log').open('w',encoding='utf8') as log:
        audit=Path(temp)/'audit';audit.mkdir();start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session=case,actor=1,own=own,other=other,auditDir=str(audit),seed=4,firstPlayerSeat=1,startingLife=30,decks=[DECK,DECK])),encoding='utf8')
        p=subprocess.Popen([str(RT/CFG['java']),'-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.PlayerStatusFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
        messages=queue.Queue()
        def reader():
            try:
                for line in p.stdout:messages.put(json.loads(line))
            finally:messages.put(None)
        threading.Thread(target=reader,daemon=True).start()
        def snap(m,label):
            for seat in [1,2]:
                v=dict(m['views'][seat-1]);v.update(request=m['request'],awaitingInput=True);(folder/(label+('' if seat==1 else '-other')+'.json')).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf8')
        def observe(r,m):
            deadline=time.monotonic()+5;path=audit/(str(r['requestId'])+'.json')
            while time.monotonic()<deadline:
                try:a=json.loads(path.read_text());assert a['requestId']==r['requestId'];break
                except (FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
            else:raise AssertionError('Missing native status audit')
            (folder/('audit-'+str(r['requestId'])+'.json')).write_text(json.dumps(a,indent=2),encoding='utf8')
            for view in m['views']:
                assert [x['speed'] for x in view['players']]==a['speeds'],(view,a)
                expected='' if a.get('dayTime') is None else 'night' if a['dayTime'] else 'day';assert view['dayTime']==expected,(view,a)
            return a
        stage=0;casts=0;source_id=None;target=2;cast_turn=None;previous_turn=None;shocks=0;activation_turn=None;draw_before=None;start_hand=len(hand);deadline=time.monotonic()+240
        try:
            for step in range(750):
                assert time.monotonic()<deadline,'Deadline';m=messages.get(timeout=40);assert m is not None,'Native exited';trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ('fatal','ended','rejected'),m
                if m['type']!='request':continue
                r=m['request'];opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];view=m['views'][0];pl=view['players'][0];opp=view['players'][1];pick='ok';a=observe(r,m) if stage or kind=='InputPassPriority' and view['phase']=='MAIN1' else None
                snap(m,'step-'+str(step))
                def cast(name):return 'card:'+str(next(c for c in pl['hand'] if c['name']==name)['id'])
                if kind=='InputPassPriority' and not view['stackItems']:
                    if stage==0 and r['seat']==1 and view['turnSeat']==1 and view['phase']=='MAIN1':
                        assert pl['speed']==0 and view['dayTime']=='';snap(m,'before');stage=1;cast_turn=view['turn'];pick=cast('Shock' if case=='speed-none' else source);target=2
                        if case=='speed-none':shocks=1
                    elif stage==1 and case=='night-explicit' and any(c['name']==source for c in pl['graveyardCards']):
                        assert view['dayTime']=='night' and pl['handCount']==start_hand;snap(m,'resolved');return result('PASS','actual sorcery/payment and zero-discard draw1; neither->night, both-seat native status audit and next priority')
                    elif stage==1 and case=='day-switch' and any(c['name']==source for c in pl['battlefield']):
                        assert view['dayTime']=='day';snap(m,'day');source_id=next(c['id'] for c in pl['battlefield'] if c['name']==source);stage=2;pick='card:'+str(source_id)
                    elif stage==2 and case=='day-switch' and view['dayTime']=='night' and pl['life']==31:
                        assert next(c for c in pl['battlefield'] if c['id']==source_id)['tapped'];snap(m,'resolved');return result('PASS','actual Celestus cast creates day, pay3/tap native switch creates night and real trigger gains1; both-seat audit, next priority')
                    elif case=='day-natural' and stage==1 and any(c['name']=='Brutal Cathar' for c in pl['battlefield']):
                        assert view['dayTime']=='day';source_id=next(c['id'] for c in pl['battlefield'] if c['name']=='Brutal Cathar');snap(m,'day');stage=2
                    elif case=='day-natural' and stage==2 and view['dayTime']=='night':
                        c=next(c for c in pl['battlefield'] if c['id']==source_id);assert c['name']=='Moonrage Brute' and c['backFace'] and (c['power'],c['toughness'])==(3,3);snap(m,'night');stage=3
                    elif case=='day-natural' and stage==3 and r['seat']==1 and view['turnSeat']==1 and view['phase']=='MAIN1':
                        if shocks<2:pick=cast('Shock');shocks+=1;target=2
                        else:stage=4;snap(m,'two-spells')
                    elif case=='day-natural' and stage==4 and view['dayTime']=='day':
                        c=next(c for c in pl['battlefield'] if c['id']==source_id);assert c['name']=='Brutal Cathar' and not c['backFace'] and (c['power'],c['toughness'])==(2,2);assert opp['life']==26;snap(m,'resolved');return result('PASS','actual Daybound cast creates day; opponent zero-spell turn causes night/same-ID back face; two actual shocks next own turn cause day/same-ID original, both-seat audit and next priority')
                    elif case=='speed-none' and stage==1 and any(c['name']=='Shock' for c in pl['graveyardCards']):
                        assert pl['speed']==0 and opp['life']==28;snap(m,'resolved');return result('PASS','actual opponent life loss without engines does not start speed; both-seat native audit and next priority')
                    elif case.startswith('speed') and case!='speed-none' and stage==1 and any(c['name']==source for c in pl['battlefield']):
                        assert pl['speed']==1 and opp['speed']==0;source_id=next(c['id'] for c in pl['battlefield'] if c['name']==source);stage=2;snap(m,'started')
                    elif case.startswith('speed') and case!='speed-none' and stage>=2:
                        if stage==2 and r['seat']==1 and view['phase']=='MAIN1' and (view['turnSeat']==2 if case=='speed-offturn' else view['turnSeat']==1):
                            if case=='speed-max':
                                if shocks>=4:
                                    assert pl['speed']==4;draw_before=pl['handCount'];snap(m,'max');pick='card:'+str(source_id);stage=3
                                elif previous_turn!=view['turn']:
                                    assert pl['speed']==min(4,shocks+1);snap(m,'speed-'+str(pl['speed']));pick=cast('Shock');shocks+=1;previous_turn=view['turn'];target=2
                            elif shocks==0:
                                pick=cast('Shock');shocks=1;target=1 if case=='speed-self' else 'source' if case=='speed-source-leave' else 2
                            elif case in ['speed-once','speed-source-leave'] and shocks==1:
                                if case=='speed-once':assert pl['speed']==2 and opp['life']==28;snap(m,'first-loss')
                                else:assert not any(c['id']==source_id for c in pl['battlefield']) and pl['speed']==1;snap(m,'source-left')
                                pick=cast('Shock');shocks=2;target=2
                            else:
                                expected=1 if case in ['speed-self','speed-offturn'] else 2;assert pl['speed']==expected
                                assert opp['life']==(30 if case=='speed-self' else 26 if case=='speed-once' else 28);assert pl['life']==(28 if case=='speed-self' else 30);snap(m,'resolved');return result('PASS','actual engines starts1; '+case+' checks real life-loss timing/once-per-turn/source departure; both-seat native status and next priority')
                        elif stage==3 and pl['handCount']==draw_before+1:
                            assert pl['speed']==4 and opp['life']==22 and not any(c['name']=='Grizzly Bears' for c in pl['battlefield']);assert any(c['name']=='Grizzly Bears' for c in pl['graveyardCards']);snap(m,'resolved');return result('PASS','actual engines starts1, opponent life loss on successive own turns raises2/3/4, fourth loss remains4; max-speed ability payB/sacrifice exact Bear draws1; both-seat audit and next priority')
                elif kind=='InputMulligan':pick='keep'
                elif 'SelectTargets' in kind:
                    if r.get('selectedCount') and 'ok' in ids:pick='ok'
                    elif target=='source':pick=next(o['id'] for o in opts if o.get('card',{}).get('id')==source_id)
                    else:pick=next(o['id'] for o in opts if o['label']=='Player '+str(target))
                elif 'PayMana' in kind:pick='ok' if 'ok' in ids and not next(o for o in opts if o['id']=='ok').get('disabled') else next(o['id'] for o in opts if o['id'].startswith('card:'))
                elif kind=='InputConfirm':pick='cancel' if 'cancel' in ids else ids[-1]
                elif kind=='OrderAbilities':pick='confirm-order' if 'confirm-order' in ids else next(i for i in ids if i.startswith('order:'))
                elif kind=='SelectCards' or 'SelectCards' in kind or 'SelectEntities' in kind:
                    if 'confirm-cards' in ids and (r.get('selectedCount',0)>=r.get('minCount',0)):pick='confirm-cards'
                    elif 'ok' in ids and r.get('selectedCount',0)>=r.get('minCount',0):pick='ok'
                    else:pick=next(o['id'] for o in opts if o.get('card',{}).get('name')=='Grizzly Bears') if case=='speed-max' else next(o['id'] for o in opts if o.get('card'))
                elif kind=='dialog':
                    if case=='day-switch' and stage==2:pick=next(o['id'] for o in opts if "becomes day" in o['label'] and 'becomes night' in o['label'])
                    else:pick=ids[0]
                elif 'ok' not in ids:pick=ids[0]
                assert pick in ids,(case,stage,kind,pick,opts)
                print(case,step,kind,pick,'speed',pl['speed'],'day',view['dayTime'],flush=True);p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
            raise AssertionError('Step limit')
        except Exception as error:result('FAIL','native status branch',error);raise
        finally:
            if p.poll() is None:p.kill();p.wait()

if __name__=='__main__':
    results=[]
    for case in sys.argv[1:] or CASES:
        try:v=run(case);print(v['result'],flush=True);results.append(v)
        except Exception as error:print('FAIL '+case+': '+repr(error),flush=True);results.append(json.loads((OUT/(case+'-result.json')).read_text()))
    (OUT/'results.json').write_text(json.dumps({'status':'PASS' if all(v['status']=='PASS' for v in results) else 'FAIL','results':results},ensure_ascii=False,indent=2)+'\n',encoding='utf-8');sys.exit(any(v['status']!='PASS' for v in results))
