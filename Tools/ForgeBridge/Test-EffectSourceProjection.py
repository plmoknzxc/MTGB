"""Direct generated-effect projection adapters; no actual spell outcome claim."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';cfg=json.loads((RT/'runtime.json').read_text('utf-8-sig'));OUT=ROOT/'.tools/effect-source-1006/projection';OUT.mkdir(parents=True,exist_ok=True)
folder=OUT/str(time.time_ns());folder.mkdir();classes=folder/'classes';classes.mkdir();fixtures=[ROOT/'Tools/ForgeBridge/tests/StateBranchFixture.java',ROOT/'Tools/ForgeBridge/tests/EffectSourceProjectionFixture.java']
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*map(str,fixtures)],check=True)
result=folder/'result.json';meta={'folder':folder.relative_to(ROOT).as_posix(),'runtimeHash':cfg['runtimeHash'],'driverSha256':sha(Path(__file__)),'fixtures':[{'path':p.relative_to(ROOT).as_posix(),'sha256':sha(p)} for p in fixtures]}
with tempfile.TemporaryDirectory(dir=folder,prefix='temp-') as temp,(folder/'engine.log').open('w',encoding='utf-8') as log:
 deck={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]};start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session='projection-adapter',seed=4,startingLife=30,firstPlayerSeat=1,decks=[deck,deck],output=str(result))),encoding='utf-8')
 p=subprocess.Popen([str(RT/cfg['java']),'-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.EffectSourceProjectionFixture',str(RT/cfg['assets']),str(start)],cwd=RT/cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
 messages=queue.Queue()
 def reader():
  for line in p.stdout:messages.put(json.loads(line))
 threading.Thread(target=reader,daemon=True).start()
 try:
  deadline=time.monotonic()+45
  while time.monotonic()<deadline and not result.exists() and p.poll() is None:
   try:m=messages.get(timeout=.1)
   except queue.Empty:continue
   (folder/'opening.jsonl').open('a',encoding='utf8').write(json.dumps(m)+'\n');assert m['type'] not in ['fatal','rejected','ended'],m
   if m['type']=='request':
    r=m['request'];ids=[o['id'] for o in r['options']];pick='keep' if 'keep' in ids else 'ok' if 'ok' in ids else ids[0];assert 'Mulligan' in r['kind'],r['kind']
    p.stdin.write(json.dumps(dict(type='respond',session='projection-adapter',seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
  assert result.exists(),'Projection did not produce result';data=json.loads(result.read_text());assert data['status']=='PASS' and len(data['checks'])==8,data
  data.update(meta,result='PASS adapter effect-source: public battlefield/graveyard retain exact artwork and effect ID; hidden hand/library and face-down source withhold metadata, mismatched image not inherited, custom name not flattened, ordinary card unchanged. Direct adapters, not casts or effect outcomes.');result.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');(OUT/'result.json').write_bytes(result.read_bytes());print(data['result'])
 except Exception as e:
  data={**meta,'status':'FAIL','error':repr(e)};result.write_text(json.dumps(data,indent=2));(OUT/'result.json').write_bytes(result.read_bytes());raise
 finally:
  if p.poll() is None:p.kill();p.wait()
