"""Interactive editor pilot backed by the real Forge process (no network server)."""
import json,os,queue,subprocess,tempfile,threading,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
OUT=ROOT/'.tools/component-card-1007/demo';OUT.mkdir(parents=True,exist_ok=True)
CLASSES=ROOT/'.tools/component-card-1007/native/classes'
def atomic(name,value):
 p=OUT/name;t=p.with_suffix('.tmp');t.write_text(json.dumps(value,ensure_ascii=False),encoding='utf8')
 # Windows readers may briefly hold the destination without delete sharing.
 # Keep the complete temporary file and retry the atomic replacement.
 for attempt in range(100):
  try:t.replace(p);return
  except PermissionError:
   if attempt==99:raise
   time.sleep(.02)
def run():
 for name in ['stop.json','action.json','view.json']:(OUT/name).unlink(missing_ok=True)
 atomic('status.json',dict(status='STARTING',runtimeHash=CFG['runtimeHash']))
 if not (CLASSES/'mtgb/bridge/PermissionLifecycleFixture.class').exists():
  CLASSES.mkdir(parents=True,exist_ok=True)
  subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),*[str(ROOT/'Tools/ForgeBridge/tests'/n) for n in ['StateBranchFixture.java','PermissionLifecycleFixture.java']]],check=True)
 deck={'cards':[{'setCode':'10E','collectorNumber':'368','count':60}],'sideboard':[]}
 with tempfile.TemporaryDirectory(dir=OUT,prefix='session-') as tmp,(OUT/'engine.log').open('w',encoding='utf8') as log,(OUT/'trace.jsonl').open('w',encoding='utf8') as trace:
  audit=Path(tmp)/'audit';audit.mkdir()
  config=dict(session='component-demo',own=dict(Hand=['Reckless Impulse','Grizzly Bears','Grizzly Bears','Emerald Medallion','Unsummon'],Battlefield=['Future Sight','Lurrus of the Dream-Den','Sphere of Resistance','Grizzly Bears'],Graveyard=['Grizzly Bears'],Exile=['Grizzly Bears'],Library=['Grizzly Bears']*3),other=dict(Battlefield=['Grizzly Bears']),auditDir=str(audit),seed=4,firstPlayerSeat=1,startingLife=20,decks=[deck,deck])
  start=Path(tmp)/'start.json';start.write_text(json.dumps(config),encoding='utf8')
  proc=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+tmp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.PermissionLifecycleFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=tmp,LOCALAPPDATA=tmp,TEMP=tmp,TMP=tmp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
  messages=queue.Queue()
  def reader():
   try:
    for line in proc.stdout:messages.put(json.loads(line))
   finally:messages.put(None)
  threading.Thread(target=reader,daemon=True).start()
  stage=0;active=False;ready=False;answered=set()
  try:
   while not (OUT/'stop.json').exists():
    try:m=messages.get(timeout=.1)
    except queue.Empty:continue
    if m is None:raise RuntimeError('Native process closed')
    trace.write(json.dumps(m)+'\n');trace.flush()
    if m['type'] in ['fatal','ended','rejected']:raise RuntimeError(m)
    if m['type']!='request':continue
    r=m['request'];v=m['views'][0];kind=r['kind'];opts=r['options'];ids=[o['id'] for o in opts if not o.get('disabled')]
    if r['requestId'] in answered:continue
    pick='ok'
    if not ready and kind=='InputPassPriority':
     if stage==1 and any(s.get('source',{}).get('name')=='Grizzly Bears' for s in v['stackItems']):ready=True
     elif active and not v['stackItems'] and r['seat']==1:stage+=1;active=False
     if not ready and not active and r['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
      name='Reckless Impulse' if stage==0 else 'Grizzly Bears';card=next(c for c in v['players'][0]['hand'] if c['name']==name and 'card:'+str(c['id']) in ids)
      pick='card:'+str(card['id']);active=True
    if ready:
     atomic('view.json',dict(v,request=v.get('request',r),awaitingInput=r['seat']==1))
     atomic('status.json',dict(status='READY',requestId=r['requestId'],kind=kind,runtimeHash=CFG['runtimeHash']))
     if r['seat']==1:
      while not (OUT/'stop.json').exists():
       action=OUT/'action.json'
       if action.exists():
        try:command=json.loads(action.read_text('utf8'))
        except (PermissionError,json.JSONDecodeError):time.sleep(.05);continue
        action.unlink(missing_ok=True)
        if command.get('requestId')==r['requestId'] and command.get('option') in ids:pick=command['option'];break
       time.sleep(.05)
      else:break
    if pick not in ids:pick=next((i for i in ids if i not in ['cancel','no']),ids[0])
    answered.add(r['requestId']);proc.stdin.write(json.dumps(dict(type='respond',session='component-demo',seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');proc.stdin.flush()
   atomic('status.json',dict(status='STOPPED'))
  finally:
   if proc.poll() is None:proc.kill();proc.wait()
if __name__=='__main__':
 try:run()
 except BaseException as e:atomic('status.json',dict(status='FAIL',error=str(e)));raise
