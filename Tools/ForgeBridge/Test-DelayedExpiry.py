"""Actual copied-token expiry after countering, source departure or control transfer."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';cfg=json.loads((RT/'runtime.json').read_text('utf-8-sig'));OUT=ROOT/'.tools/delayed-expiry-1006/native';OUT.mkdir(parents=True,exist_ok=True);classes=OUT/'classes';classes.mkdir(exist_ok=True)
fixture=ROOT/'Tools/ForgeBridge/tests/StateBranchFixture.java';subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),str(fixture)],check=True)
CASES=['self-exile-counter','external-source-leave','external-control-change'];deck={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]};sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def run(case):
 self_exile=case=='self-exile-counter';transfer=case=='external-control-change';own={'Battlefield':['Grizzly Bears']+([] if self_exile else ['Kiki-Jiki, Mirror Breaker']),'Hand':['Heat Shimmer','Stifle'] if self_exile else ['Donate' if transfer else 'Shock']};folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True);meta={'case':case,'folder':folder.relative_to(ROOT).as_posix(),'runtimeHash':cfg['runtimeHash'],'driverSha256':sha(Path(__file__)),'fixtures':[{'path':fixture.relative_to(ROOT).as_posix(),'sha256':sha(fixture)}]}
 def finish(status,message):
  d={**meta,'status':status,'result':status+' '+case+': '+message}
  for p in [folder/'result.json',OUT/(case+'-result.json')]:p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  return d
 with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'trace.jsonl').open('w',encoding='utf8') as trace,(folder/'engine.log').open('w',encoding='utf8') as log:
  audit=Path(temp)/'audit';audit.mkdir();start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session=case,actor=1,own=own,other={},auditDir=str(audit),seed=4,firstPlayerSeat=1,startingLife=30,decks=[deck,deck])),encoding='utf8');p=subprocess.Popen([str(RT/cfg['java']),'-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.StateBranchFixture',str(RT/cfg['assets']),str(start)],cwd=RT/cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');messages=queue.Queue()
  def reader():
   try:
    for line in p.stdout:messages.put(json.loads(line))
   finally:messages.put(None)
  threading.Thread(target=reader,daemon=True).start()
  def snap(m,name):
   for seat,v in enumerate(m['views'],1):
    d=dict(v);d.update(request=m['request'],awaitingInput=True);(folder/(name+('' if seat==1 else '-other')+'.json')).write_text(json.dumps(d,ensure_ascii=False,indent=2),encoding='utf8')
  stage=0;bear=None;source=None;token=None;second_spell=None;stifled=False;countered=False;first_trigger=None;repeated=False;manipulated=False;deadline=time.monotonic()+180
  try:
   for step in range(400):
    assert time.monotonic()<deadline,'Deadline';m=messages.get(timeout=40);assert m is not None,'Native exited';trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ['fatal','ended','rejected'],m
    if m['type']!='request':continue
    r=m['request'];opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];v=m['views'][0];pl,op=v['players'];stack=v['stackItems'];field=pl['battlefield']+op['battlefield'];snap(m,'step-'+str(step));pick=None
    assert all([x['life'] for x in view['players']]==[30,30] for view in m['views'])
    current=next((c for c in field if c['id']==token),None)
    if current:
     other=next(c for p2 in m['views'][1]['players'] for c in p2['battlefield'] if c['id']==token);assert other.get('pendingDepartures',[])==current.get('pendingDepartures',[])
    expiry=[s for s in stack if s['kind']=='trigger' and ('exile' if self_exile else 'sacrifice') in s['description'].lower() and (s['source']['id']==token if self_exile else 'Grizzly Bears' in s['description'])]
    if expiry:
     assert current and current.get('pendingDepartures'),'Expiry stack lost token association';snap(m,'expiry-'+str(v['turn']))
     if first_trigger is None:first_trigger=expiry[0]['id']
     elif self_exile and countered:assert expiry[0]['id']!=first_trigger;repeated=True
    if kind=='InputPassPriority':
     if stage==0 and r['seat']==1 and v['turnSeat']==1 and v['phase']=='MAIN1':
      bear=next(c['id'] for c in pl['battlefield'] if c['name']=='Grizzly Bears');source=next(c['id'] for c in (pl['hand'] if self_exile else pl['battlefield']) if c['name']==('Heat Shimmer' if self_exile else 'Kiki-Jiki, Mirror Breaker'));second_spell=pl['hand'][-1]['id'];snap(m,'before');pick='card:'+str(source);stage=1
     elif stage==1 and not stack:
      copies=[c for c in field if c.get('token') and c['name']=='Grizzly Bears'];assert len(copies)<=1
      if copies:
       c=copies[0];token=c['id'];assert token!=bear and c['power']==c['toughness']==2 and c.get('pendingDepartures') and c['pendingDepartures'][0]['action']==('exile' if self_exile else 'sacrifice');snap(m,'copy-created');stage=2
     if stage==2 and not self_exile and r['seat']==1 and not stack and v['phase']=='MAIN1':pick='card:'+str(second_spell);stage=3
     elif stage>=2 and self_exile:
      if expiry and r['seat']==1 and not stifled:pick='card:'+str(second_spell);stifled=True
      elif stifled and not stack and current and not countered:
       assert any(c['id']==second_spell for c in pl['graveyardCards']) and current.get('pendingDepartures');countered=True;snap(m,'countered-survives')
      elif repeated and not stack and current is None:
       assert any(c['id']==bear for c in pl['battlefield']) and not any(c['id']==token for c in pl['graveyardCards']);snap(m,'next-endstep-exiled');return finish('PASS','actual Heat Shimmer copy; Stifle selects exact first self-exile trigger and counter resolves; copy stays with its intrinsic expiry, distinct trigger at next opponent endstep exiles exact token; original Bear remains, both projections and next priority')
     elif stage==3 and not stack:
      if not manipulated:
       assert current
       if transfer:assert current['ownerSeat']==1 and any(c['id']==token for c in op['battlefield']) and any(c['id']==source for c in pl['battlefield'])
       else:assert any(c['id']==source for c in pl['graveyardCards']) and not any(c['id']==source for c in pl['battlefield'])
       assert current.get('pendingDepartures');manipulated=True;snap(m,'source-left' if not transfer else 'control-changed')
      if first_trigger is not None:
       if transfer:
        assert current and any(c['id']==token for c in op['battlefield']) and not current.get('pendingDepartures') and not pl['effectMarkers'];snap(m,'survives-expiry');return finish('PASS','actual Kiki activation/copy then Donate transfers exact token to opponent while owner remains1; creator-controlled one-shot sacrifice trigger resolves without sacrificing opponent-controlled token; obligation/marker clears, original Bear/source remain; both views and next priority')
       assert current is None and any(c['id']==bear for c in pl['battlefield']) and not pl['effectMarkers'];snap(m,'sacrificed-source-gone');return finish('PASS','actual Kiki activation/copy then Shock kills exact Kiki source; external one-shot delayed sacrifice still associates exact token and resolves at endstep, original Bear remains and marker clears; both views and next priority')
     if pick is None:pick='ok'
    elif 'Mulligan' in kind:pick='keep' if 'keep' in ids else 'ok'
    elif 'PayMana' in kind:pick='ok' if 'ok' in ids and not next(o for o in opts if o['id']=='ok').get('disabled') else next(o['id'] for o in opts if o['id'].startswith('card:'))
    elif kind=='SelectStackTarget':
     if 'ok' in ids:pick='ok'
     else:pick='stack:'+str(first_trigger)
    elif 'SelectTargets' in kind:
     if 'ok' in ids and not next(o for o in opts if o['id']=='ok').get('disabled'):pick='ok'
     elif transfer and stage==3:
      player=next((o for o in opts if o['id'].startswith('player:') and o['label']=='Player 2'),None);pick=player['id'] if player else next(o['id'] for o in opts if o.get('card',{}).get('id')==token)
     else:pick=next(o['id'] for o in opts if o.get('card',{}).get('id')==(source if stage==3 else bear))
    elif kind=='OrderAbilities':pick='confirm-order' if 'confirm-order' in ids else next(o['id'] for o in opts if o['id'].startswith('order:'))
    elif kind in ['dialog','Option']:pick=next(i for i in ids if i not in ['cancel','no'])
    elif 'ok' in ids:pick='ok'
    else:raise AssertionError(('Unhandled',r))
    assert pick in ids,(kind,pick,ids);print(case,step,kind,r['seat'],pick,'stage',stage,'token',token,'expiry',len(expiry),'phase',v['phase'],flush=True);p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
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
