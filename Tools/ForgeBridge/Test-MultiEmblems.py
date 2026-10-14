"""Native venture branches, effects and completion; fixture only sets initial positions."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';cfg=json.loads((RT/'runtime.json').read_text('utf-8-sig'));OUT=ROOT/'.tools/multi-emblems-1006/native';OUT.mkdir(parents=True,exist_ok=True);classes=OUT/'classes';classes.mkdir(exist_ok=True)
fixture=ROOT/'Tools/ForgeBridge/tests/RoyalStateFixture.java';subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),str(fixture)],check=True)
CASES=['double-emblems','stolen-source-emblem','mixed-source-left'];deck={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]};sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()

def run(case):
 ally='Gideon, Ally of Zendikar';trials='Gideon of the Trials'
 actions={'double-emblems':[(1,'cast',ally),(1,'activate',ally),(1,'cast',ally),(1,'activate',ally)],'stolen-source-emblem':[(1,'cast',ally),(2,'cast','Confiscate'),(2,'activate',ally)],'mixed-source-left':[(1,'cast',ally),(1,'activate',ally),(1,'cast',trials),(1,'activate',trials),(1,'cast',"Hero's Downfall")]}[case]
 own={'Hand':[ally,ally] if case=='double-emblems' else [ally] if case=='stolen-source-emblem' else [ally,trials,"Hero's Downfall"],'Battlefield':['Grizzly Bears']};other={'Hand':['Confiscate'] if case=='stolen-source-emblem' else [],'Battlefield':['Grizzly Bears']+['Island']*8}
 folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True);meta={'case':case,'folder':folder.relative_to(ROOT).as_posix(),'runtimeHash':cfg['runtimeHash'],'driverSha256':sha(Path(__file__)),'fixtures':[{'path':fixture.relative_to(ROOT).as_posix(),'sha256':sha(fixture)}]}
 def finish(status,msg):
  d={**meta,'status':status,'result':status+' '+case+': '+msg}
  for file in [folder/'result.json',OUT/(case+'-result.json')]:file.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  return d
 with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'trace.jsonl').open('w',encoding='utf8') as trace,(folder/'engine.log').open('w',encoding='utf8') as log:
  audit=Path(temp)/'audit';audit.mkdir();start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session=case,actor=1,own=own,other=other,auditDir=str(audit),seed=4,firstPlayerSeat=1,startingLife=30,decks=[deck,deck])),encoding='utf8')
  p=subprocess.Popen([str(RT/cfg['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.RoyalStateFixture',str(RT/cfg['assets']),str(start)],cwd=RT/cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');messages=queue.Queue()
  def reader():
   try:
    for line in p.stdout:messages.put(json.loads(line))
   finally:messages.put(None)
  threading.Thread(target=reader,daemon=True).start()
  def snap(m,name):
   for seat,v in enumerate(m['views'],1):
    d=dict(v);d.update(request=m['request'],awaitingInput=True);(folder/(name+('' if seat==1 else '-other')+'.json')).write_text(json.dumps(d,ensure_ascii=False,indent=2),encoding='utf8')
  stage=0;pending=False;source=None;trials_id=None;first_emblems=None;deadline=time.monotonic()+300
  try:
   for step in range(500):
    assert time.monotonic()<deadline,'Deadline';m=messages.get(timeout=40);assert m is not None,'Native exit';trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ['fatal','ended','rejected'],m
    if m['type']!='request':continue
    r=m['request'];opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];v=m['views'][0];pl,op=v['players'];stack=v['stackItems'];pick=None
    if kind=='InputPassPriority':
     if pending and not stack:
      actor,action,name=actions[stage];owner=v['players'][actor-1];pending=False
      if action=='cast' and name in [ally,trials]:
       c=next(c for c in owner['battlefield'] if c['name']==name);assert c['planeswalker'] and c['loyalty']==(4 if name==ally else 3)
       if name==ally:source=c['id']
       else:trials_id=c['id']
      elif action=='cast' and name=='Confiscate':assert any(c['id']==source and c['ownerSeat']==1 for c in op['battlefield']) and all(c['id']!=source for c in pl['battlefield'])
      elif action=='activate':
       expected=2 if case=='double-emblems' and stage==3 or case=='mixed-source-left' and stage==3 else 1
       assert len(owner['emblems'])==expected and all(c['emblem'] and c['ownerSeat']==actor for c in owner['emblems']);assert len(set(c['id'] for c in owner['emblems']))==expected
       if name==ally:assert any(c['id']==source for c in pl['graveyardCards']) and all(c['id']!=source for p in v['players'] for c in p['battlefield'])
      elif action=='cast':assert name=="Hero's Downfall" and any(c['id']==trials_id for c in pl['graveyardCards']) and all(c['id']!=trials_id for c in pl['battlefield'])
      for seat,pv in enumerate(v['players'],1):
       n=sum(ally in c['name'] for c in pv['emblems']);bear=next(c for c in pv['battlefield'] if c['name']=='Grizzly Bears');assert bear['power']==bear['toughness']==2+n
       assert len(m['views'][1]['players'][seat-1]['emblems'])==len(pv['emblems'])
      snap(m,'resolved-'+str(stage+1));stage+=1
      if stage==len(actions):first_emblems=[[(c['id'],c['ownerSeat'],c['currentRules']) for c in p['emblems']] for p in v['players']];snap(m,'emblems-created')
     if stage==len(actions) and v['turn']==3 and v['phase']=='MAIN1' and not stack:
      assert first_emblems==[[(c['id'],c['ownerSeat'],c['currentRules']) for c in p['emblems']] for p in v['players']];snap(m,'cross-turn');return finish('PASS','actual '+str(len(actions))+' cast/ultimate/control/removal actions, distinct actual emblems and exact holder/owner IDs, native static Bear boosts/unchanged opponent, source graveyard/control identity, both projections and stable next own main phase turn3; UI contract separately')
     if stage<len(actions) and not pending and not stack and r['seat']==actions[stage][0] and v['phase']=='MAIN1' and v['turnSeat']==r['seat']:
      actor,action,name=actions[stage];owner=m['views'][actor-1]['players'][actor-1];zone='hand' if action=='cast' else 'battlefield';c=next(c for c in owner[zone] if c['name']==name);pick='card:'+str(c['id']);pending=True
     else:pick='ok'
    elif 'Mulligan' in kind:pick='keep' if 'keep' in ids else 'ok'
    elif 'PayMana' in kind:pick='ok' if 'ok' in ids and not next(o for o in opts if o['id']=='ok').get('disabled') else next(o['id'] for o in opts if o['id'].startswith('card:'))
    elif 'SelectTargets' in kind:
     if 'ok' in ids and not next(o for o in opts if o['id']=='ok').get('disabled'):pick='ok'
     else:pick=next(o['id'] for o in opts if o.get('card',{}).get('id')==(source if actions[stage][2]=='Confiscate' else trials_id));snap(m,'target-'+str(stage)+'-'+str(step))
    elif kind=='dialog':pick=next(o['id'] for o in opts if 'emblem' in o['label'].lower());snap(m,'ultimate-'+str(stage))
    elif kind=='OrderAbilities':pick='confirm-order'
    elif 'ok' in ids:pick='ok'
    else:raise AssertionError(('Unhandled',kind,r['message'],ids))
    assert pick in ids,(kind,pick,ids);print(case,step,kind,r['seat'],pick,'stage',stage,r['message'][:95],flush=True);p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
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
