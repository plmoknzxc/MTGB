"""Actual simultaneous triggers, controller order, targets and optional decisions."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';cfg=json.loads((RT/'runtime.json').read_text('utf-8-sig'));OUT=ROOT/'.tools/trigger-order-1006/native';OUT.mkdir(parents=True,exist_ok=True);classes=OUT/'classes';classes.mkdir(exist_ok=True)
fixture=ROOT/'Tools/ForgeBridge/tests/StateBranchFixture.java';subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),str(fixture)],check=True)
CASES=['apnap-life','apnap-life-seat2','apnap-life-decline','apnap-targets'];deck={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]};sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def run(case):
 actor=2 if case=='apnap-life-seat2' else 1;targeted=case=='apnap-targets';decline=case=='apnap-life-decline';watchers=['Blood Artist','Blood Artist'] if targeted else ['Soul Warden',"Soul's Attendant"];own={'Battlefield':watchers+(['Memnite'] if targeted else []),'Hand':['Shock' if targeted else 'Grizzly Bears']};other={'Battlefield':watchers};folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True)
 meta={'case':case,'folder':folder.relative_to(ROOT).as_posix(),'runtimeHash':cfg['runtimeHash'],'driverSha256':sha(Path(__file__)),'fixtures':[{'path':fixture.relative_to(ROOT).as_posix(),'sha256':sha(fixture)}]}
 def finish(status,message):
  d={**meta,'status':status,'result':status+' '+case+': '+message}
  for p in [folder/'result.json',OUT/(case+'-result.json')]:p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  return d
 with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'trace.jsonl').open('w',encoding='utf8') as trace,(folder/'engine.log').open('w',encoding='utf8') as log:
  audit=Path(temp)/'audit';audit.mkdir();start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session=case,actor=actor,own=own,other=other,auditDir=str(audit),seed=4,firstPlayerSeat=actor,startingLife=30,decks=[deck,deck])),encoding='utf8');p=subprocess.Popen([str(RT/cfg['java']),'-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.StateBranchFixture',str(RT/cfg['assets']),str(start)],cwd=RT/cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');messages=queue.Queue()
  def reader():
   try:
    for line in p.stdout:messages.put(json.loads(line))
   finally:messages.put(None)
  threading.Thread(target=reader,daemon=True).start()
  def snap(m,name):
   for seat,v in enumerate(m['views'],1):
    d=dict(v);d.update(request=m['request'],awaitingInput=True);(folder/(name+('' if seat==actor else '-other')+'.json')).write_text(json.dumps(d,ensure_ascii=False,indent=2),encoding='utf8')
  stage=0;cast_id=None;victim=None;seen_stack=False;order_seats=[];target_seats=[];optional_seats=[];selected={1:[],2:[]};reset_seats=set();progress=set();watch_ids={};deadline=time.monotonic()+160
  try:
   for step in range(300):
    assert time.monotonic()<deadline,'Deadline';m=messages.get(timeout=40);assert m is not None,'Native exited';trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ['fatal','ended','rejected'],m
    if m['type']!='request':continue
    r=m['request'];opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];v=m['views'][actor-1];pl=v['players'][actor-1];op=v['players'][2-actor];stack=v['stackItems'];snap(m,'step-'+str(step))
    assert all([x['life'] for x in view['players']]==[x['life'] for x in v['players']] for view in m['views']), 'Seat life projection disagrees'
    pick=None
    if kind=='InputPassPriority':
     if stage==0 and r['seat']==actor and v['turnSeat']==actor and v['phase']=='MAIN1':
      assert pl['life']==op['life']==30;watch_ids={seat:{c['id'] for c in v['players'][seat-1]['battlefield'] if c['name'] in watchers} for seat in [1,2]};assert all(len(w)==2 for w in watch_ids.values());cast_id=pl['hand'][0]['id'];victim=next((c['id'] for c in pl['battlefield'] if c['name']=='Memnite'),None);snap(m,'before');stage=1;pick='card:'+str(cast_id)
     elif stage==1:
      triggered=[s for s in stack if s['source']['id'] in watch_ids[1]|watch_ids[2]]
      if len(triggered)==4:
       assert [s['controllerSeat'] for s in triggered]==[3-actor]*2+[actor]*2,triggered;assert {s['source']['id'] for s in triggered}==watch_ids[1]|watch_ids[2]
       for seat in [1,2]:
        if selected[seat]:assert [s['source']['id'] for s in triggered if s['controllerSeat']==seat]==selected[seat],(selected,triggered)
       snap(m,'stack-four');seen_stack=True
      if targeted and seen_stack and len(triggered) in [0,1,2,3] and len(triggered) not in progress:
       n=len(triggered);expected={3:(29,31),2:(28,32),1:(29,31),0:(30,30)}[n];assert (pl['life'],op['life'])==expected,(n,pl['life'],op['life']);progress.add(n);snap(m,'resolved-'+str(n))
      resolved=any(c['id']==cast_id for c in pl['graveyardCards']) if targeted else any(c['id']==cast_id for c in pl['battlefield'])
      if resolved and seen_stack and not stack:
       if targeted:assert progress=={0,1,2,3} and sorted(target_seats)==[1,1,2,2] and any(c['id']==victim for c in pl['graveyardCards'])
       else:assert pl['life']==op['life']==(31 if decline else 32) and set(order_seats)=={1,2} and reset_seats=={1,2} and sorted(optional_seats)==[1,2]
       snap(m,'resolved');return finish('PASS','actual '+('Shock kills exact Memnite; four Blood Artist triggers get exact per-controller opposite-player targets; APNAP top-first controller order2/2, life30/30 ->29/31 ->28/32 ->29/31 ->30/30' if targeted else 'Grizzly Bears cast/payment creates four mixed Soul triggers; both controllers choose and reset own order; APNAP nonactive triggers above active, '+('both optional gains declined, life31/31' if decline else 'both optional gains accepted, life32/32'))+'; distinct source IDs, both-seat projections, next priority; actor='+str(actor))
     if pick is None:pick='ok'
    elif 'Mulligan' in kind:pick='keep' if 'keep' in ids else 'ok'
    elif 'PayMana' in kind:pick='ok' if 'ok' in ids and not next(o for o in opts if o['id']=='ok').get('disabled') else next(o['id'] for o in opts if o['id'].startswith('card:'))
    elif kind=='OrderAbilities':
     # Previews omit controllerSeat; bind their source IDs to the actual
     # requesting player's battlefield, then check controllers on the stack.
     assert r.get('selectionPurpose','')!='replacementOrder';seat=r['seat'];assert all(o['ability']['source']['id'] in watch_ids[seat] for o in opts if o.get('ability')),r
     if not r.get('orderedAbilities'):snap(m,'order-seat-'+str(seat)+'-initial');order_seats.append(seat);selected[seat]=[]
     if len(r.get('orderedAbilities',[]))==1 and seat not in reset_seats:pick='reset-order';reset_seats.add(seat);selected[seat]=[];snap(m,'order-before-reset-seat-'+str(seat))
     elif 'confirm-order' in ids:pick='confirm-order';assert set(selected[seat])==watch_ids[seat];snap(m,'order-confirm-seat-'+str(seat))
     else:
      choice=sorted([o for o in opts if o.get('ability')],key=lambda o:o['ability']['source']['id'],reverse=True)[0];pick=choice['id'];selected[seat].append(choice['ability']['source']['id'])
    elif 'SelectTargets' in kind:
     assert targeted
     if r.get('selectedCount') and 'ok' in ids:pick='ok'
     elif r.get('sourceCard',{}).get('name')=='Blood Artist' or 'Blood Artist' in r.get('message',''):
      seat=r['seat'];pick=next(o['id'] for o in opts if o['label']=='Player '+str(3-seat));target_seats.append(seat)
     else:assert r['seat']==actor;pick=next(o['id'] for o in opts if o.get('card',{}).get('id')==victim)
    elif kind=='InputConfirm':assert not targeted;optional_seats.append(r['seat']);snap(m,'optional-seat-'+str(r['seat']));pick='cancel' if decline else 'ok'
    elif kind=='dialog':pick=ids[0]
    elif 'ok' in ids:pick='ok'
    else:raise AssertionError(('Unhandled',r))
    assert pick in ids,(kind,pick,ids);print(case,step,kind,'seat',r['seat'],pick,'stack',len(stack),'life',pl['life'],op['life'],flush=True);p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
   raise AssertionError('Step limit')
  except Exception:finish('FAIL',traceback.format_exc());raise
  finally:
   if p.poll() is None:p.kill();p.wait()
if __name__=='__main__':
 results=[]
 for case in sys.argv[1:] or CASES:
  try:d=run(case);print(d['result'],flush=True);results.append(d)
  except Exception as e:print('FAIL '+case+': '+repr(e),flush=True);results.append(json.loads((OUT/(case+'-result.json')).read_text()))
 (OUT/'results.json').write_text(json.dumps({'status':'PASS' if all(d['status']=='PASS' for d in results) else 'FAIL','results':results},ensure_ascii=False,indent=2)+'\n',encoding='utf8');sys.exit(any(d['status']!='PASS' for d in results))
