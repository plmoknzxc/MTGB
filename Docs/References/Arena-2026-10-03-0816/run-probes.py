import json, os, queue, subprocess, tempfile, threading, time, sys
from pathlib import Path
source=Path(__file__).resolve().parent
root=next(p for p in source.parents if (p/'ForgeRuntime').is_dir())
out=root/'.tools/arena-video-review-2026-10-03/probes';out.mkdir(parents=True,exist_ok=True)
runtime=root/'ForgeRuntime';cfg=json.loads((runtime/'runtime.json').read_text(encoding='utf-8-sig'))
classes=out/'classes';classes.mkdir(exist_ok=True)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(runtime/'classes')+';'+str(runtime/'lib/*'),'-d',str(classes),str(source/'ReferenceStateProbe.java')],check=True)
deck={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
cases=[('blood',['Blood Moon'],'R'),('blue',['Harbinger of the Seas'],'U'),('blood-blue',['Blood Moon','Harbinger of the Seas'],'U'),('blue-blood',['Harbinger of the Seas','Blood Moon'],'R'),('blood-dryad',['Blood Moon','Dryad of the Ilysian Grove'],'WUBRG'),('dryad-blood',['Dryad of the Ilysian Grove','Blood Moon'],'R')]+[(n,[],'') for n in ['story','story-unique','ascend','saga-sba']]
results=[]
if len(sys.argv)>1:
 results=[r for r in json.loads((out/'result.json').read_text(encoding='utf-8'))['results'] if r['case'] not in sys.argv[1:]]
 cases=[c for c in cases if c[0] in sys.argv[1:]]
for case,effects,mana in cases:
 messages=[];passed=False;error=None
 with tempfile.TemporaryDirectory(prefix='mtgb-reference-') as temp,(out/(case+'.stderr.log')).open('w',encoding='utf-8') as log:
  start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session=case,seed=4,startingLife=20,firstPlayerSeat=1,decks=[deck,deck],probe=case,effects=effects,mana=mana)),encoding='utf-8')
  proc=subprocess.Popen([str(runtime/cfg['java']),'-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,runtime/'classes',runtime/'lib/*'])),'mtgb.bridge.ReferenceStateProbe',str(runtime/cfg['assets']),str(start)],cwd=runtime/cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf-8')
  q=queue.Queue()
  def read_output():
   try:
    for line in proc.stdout:q.put(json.loads(line))
   except Exception as exc:q.put({'type':'reader-error','error':str(exc)})
   finally:q.put({'type':'eof'})
  thread=threading.Thread(target=read_output,daemon=True);thread.start()
  try:
   deadline=time.monotonic()+70
   while time.monotonic()<deadline:
    msg=q.get(timeout=max(0.1,deadline-time.monotonic()));messages.append(msg)
    if msg['type'] in ('fatal','reader-error','eof'):raise RuntimeError(msg)
    if msg['type']=='probe-pass':passed=True;break
    if msg['type']=='request':
     r=msg['request'];opts=r['options'];pick=next((o for o in opts if o['id']=='ok'),opts[0])
     proc.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick['id']))+'\n');proc.stdin.flush()
   if not passed:raise TimeoutError(case)
  except Exception as exc:error=str(exc)
  finally:
   if proc.poll() is None:
    proc.stdin.write('{"type":"shutdown"}\n');proc.stdin.flush()
    try:proc.wait(timeout=3)
    except subprocess.TimeoutExpired:proc.kill();proc.wait()
   thread.join(timeout=2)
 (out/(case+'.json')).write_text(json.dumps(messages,ensure_ascii=False,indent=2),encoding='utf-8')
 result=dict(case=case,passed=passed,error=error);results.append(result);print(json.dumps(result,ensure_ascii=False),flush=True)
(out/'result.json').write_text(json.dumps(dict(runtimeRevision=cfg['revision'],scope='Direct state fixtures; natural static triggers/layer evaluation/SBA; not casting/payment or Unity',results=results),ensure_ascii=False,indent=2),encoding='utf-8')
raise SystemExit(0 if all(r['passed'] for r in results) else 1)
