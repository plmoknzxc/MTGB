"""Focused bridge adapter difference, no paid cast or new mechanic result."""
from pathlib import Path
import hashlib,json,os,queue,subprocess,tempfile,threading,time,traceback,sys
ROOT=Path(__file__).resolve().parents[2];BASE=ROOT/'.tools/balance-fight-clash-1011/knowledge';RT=ROOT/'ForgeRuntime'
EXPECTED='e3a014e488b3531bc291bfef32aae923b6935a0e2c2a90f1b237df95e8e45e7f'
def write(p,x):p.write_text(json.dumps(x,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def ref(p):return dict(path=p.relative_to(ROOT).as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
def run():
 runtime=json.loads((RT/'runtime.json').read_text('utf-8-sig'));assert runtime['runtimeHash']==EXPECTED,'Published runtime required'
 d=BASE/str(time.time_ns());audit=d/'audit';audit.mkdir(parents=True);classes=d/'classes';classes.mkdir()
 fixture=ROOT/'Tools/ForgeBridge/tests/ClashKnowledgeFixture.java';sources=[Path(__file__),fixture,ROOT/'Tools/ForgeBridge/java/mtgb/bridge/ForgeBridge.java',ROOT/'Tools/ForgeBridge/java/mtgb/bridge/BridgeLibraryKnowledge.java',RT/'runtime.json']
 for p in sources:(d/p.name).write_bytes(p.read_bytes())
 checks=[];inputs=[];p=None;error=None
 def check(ok,label):assert ok,label;checks.append(label)
 def observation(rid):
  deadline=time.monotonic()+5
  while time.monotonic()<deadline:
   try:return json.loads((audit/(str(rid)+'.json')).read_text())
   except(FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
  raise AssertionError('Missing native pending observer '+str(rid))
 def known(cp,owner):return [v['players'][owner-1].get('libraryTop',{}).get('id')for v in cp['views']]
 def lib(cp,owner):return [c['id']for c in cp['players'][owner-1]['library']]
 try:
  with(d/'compile.log').open('w',encoding='utf8')as log:subprocess.run([r'C:/Program Files/Java/jdk-17/bin/javac.exe','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),str(fixture)],stdout=log,stderr=subprocess.STDOUT,check=True,timeout=60)
  with tempfile.TemporaryDirectory(prefix='profile-',dir=d,ignore_cleanup_errors=True)as temp,(d/'stderr.log').open('w',encoding='utf8')as err,(d/'trace.jsonl').open('w',encoding='utf8')as trace:
   deck=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[]);cfg=dict(session='clash-knowledge',seed=4,startingLife=30,firstPlayerSeat=1,decks=[deck,deck],auditDir=str(audit));write(d/'start.json',cfg)
   p=subprocess.Popen([str(RT/runtime['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.ClashKnowledgeFixture',str(RT/runtime['assets']),str(d/'start.json')],cwd=RT/runtime['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,encoding='utf8')
   q=queue.Queue()
   def reader():
    try:
     for line in p.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start();seen=set();continuations=0
   for step in range(80):
    m=q.get(timeout=35)
    if isinstance(m,Exception):raise m
    check(m is not None,'Native adapter remains live');trace.write(json.dumps(m)+'\n');trace.flush();check(m['type']not in ['fatal','ended','rejected'],str(m))
    if m['type']!='request':continue
    r=m['request'];rid=r['requestId']
    if rid in seen:continue
    seen.add(rid);kind=r['kind'];enabled=[o['id']for o in r['options']if not o.get('disabled')]
    if'Mulligan'in kind:pick='keep'if'keep'in enabled else'ok'
    else:
     n=observation(rid);stage=n['stage'];write(d/(stage+'-'+str(rid)+'-wire.json'),m);write(d/(stage+'-'+str(rid)+'-observation.json'),n)
     check(r['seat']==r['playerSeat'],'Actual native owner/recipient '+str(rid))
     if kind=='RevealCards':pick='confirm-cards'
     elif kind=='InputConfirm':
      pick='cancel'if'bottom'in stage else'ok';owner=r['seat'];top=n['players'][owner-1]['library'][0]
      check([c['id']for c in r['previewCards']]==[top['id']],'Exact original native Library preview '+stage)
      check([(o['id'],o['label'])for o in r['options']]==[('ok','Top'),('cancel','Bottom')],'Inherited native binary option IDs '+stage)
     elif kind=='InputPassPriority':
      check(stage=='native-continuation','Real rules worker reaches native pending after all adapters');continuations+=1
      if continuations==2:break
      pick='ok'
     else:raise AssertionError(('Unhandled actual adapter native request',r))
     inputs.append(dict(requestId=rid,stage=stage,kind=kind,seat=r['seat'],wire=ref(d/(stage+'-'+str(rid)+'-wire.json'))))
    check(pick in enabled,'Actual enabled native response '+str(rid));response=dict(type='respond',session=cfg['session'],seat=r['seat'],requestId=rid,option=pick);trace.write(json.dumps(response)+'\n');trace.flush();p.stdin.write(json.dumps(response)+'\n');p.stdin.flush();print(step,kind,pick,flush=True)
   else:raise AssertionError('Native adapter bound')
   check(continuations==2,'Real pending response yields next native priority request')
   checkpoints=json.loads((audit/'checkpoints.json').read_text());by={x['stage']:x for x in checkpoints};initial=by['initial']
   check(len(checkpoints)==9,'Initial plus eight exact adapter checkpoints')
   for cp in checkpoints:
    for owner in [1,2]:
     check(cp['players'][owner-1]['pool']==0 and cp['players'][owner-1]['life']==30,'Adapter consumes no mana/life '+cp['stage']+' '+str(owner))
     check(cp['players'][owner-1]['hand']==initial['players'][owner-1]['hand'],'No spell cast or hand mutation '+cp['stage']+' '+str(owner))
     check(set(lib(cp,owner))==set(lib(initial,owner)),'Same original Library set '+cp['stage']+' '+str(owner))
   for owner in [1,2]:check(known(initial,owner)==[None,None],'Both initial tops hidden '+str(owner))
   for label in ['public-top-1-after','public-top-2-after']:
    for owner in [1,2]:check(known(by[label],owner)==[lib(initial,owner)[0]]*2,'Public top knowledge retained for both real viewers '+label+' '+str(owner))
   for owner in [1,2]:check(lib(by['public-top-2-after'],owner)==lib(initial,owner),'Top preserves exact full original order '+str(owner))
   publicBottom=by['public-bottom-1-after'];check(lib(publicBottom,1)==lib(initial,1)[1:]+lib(initial,1)[:1],'Public Bottom moves exact original top to library end')
   check(known(publicBottom,1)==[None,None],'Public Bottom hides unrevealed next top');check(known(publicBottom,2)==[lib(initial,2)[0]]*2,'Other public top knowledge unaffected')
   for owner in [1,2]:check(known(by['public-shuffle-after'],owner)==[None,None],'Actual public shuffle clears each viewer '+str(owner))
   private1=by['private-top-1-after'];private2=by['private-top-2-after'];check(known(private1,1)==[lib(private1,1)[0],None],'Private top stays known only to actor1');check(known(private1,2)==[None,None],'Private actor1 does not expose other top')
   check(known(private2,1)==[lib(private1,1)[0],None],'Second private decision preserves existing actor1 knowledge');check(known(private2,2)==[None,lib(private2,2)[0]],'Private top stays known only to actor2')
   check(lib(private1,1)==lib(by['public-shuffle-after'],1),'Private Top preserves exact postshuffle order actor1');check(lib(private2,2)==lib(by['public-shuffle-after'],2),'Private Top preserves exact postshuffle order actor2')
   privateBottom=by['private-bottom-1-after'];check(lib(privateBottom,1)==lib(private1,1)[1:]+lib(private1,1)[:1],'Private Bottom moves exact known card to end');check(known(privateBottom,1)==[None,None],'Private Bottom hides unrevealed next top')
   check(known(privateBottom,2)==[None,lib(private2,2)[0]],'Private Bottom does not expand or erase other owner knowledge')
   for owner in [1,2]:check(known(by['private-shuffle-after'],owner)==[None,None],'Actual final shuffle clears private knowledge '+str(owner))
   write(d/'checkpoints.json',checkpoints);write(d/'checks.json',checks);p.kill();p.wait()
  status='PASS_BOUNDED_ADAPTER'
 except Exception:status='FAIL';error=traceback.format_exc();print(error,flush=True)
 finally:
  if p is not None and p.poll()is None:p.kill();p.wait()
 result=dict(status=status,kind='bridge-adapter-difference',paidOperations=0,effectScenes=0,checks=len(checks),folder=d.relative_to(ROOT).as_posix(),runtimeHash=runtime['runtimeHash'],sources=[ref(x)for x in sources],inputs=inputs)
 if error:result['error']=error
 write(d/'result.json',result);write(BASE/'latest.json',result);write(d/'artifact-manifest.json',[ref(x)for x in d.rglob('*')if x.is_file()and'profile-'not in x.as_posix()and x.name!='artifact-manifest.json']);print(status,d,flush=True);return result
if __name__=='__main__':run()
