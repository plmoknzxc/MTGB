"""Zero-effect bridge adapter for a detached copied-card confirmation argument."""
from pathlib import Path
import hashlib,json,os,queue,subprocess,tempfile,threading,time,sys
R=Path(__file__).resolve().parents[2];B=R/'.tools/name-consumer-delta-1012';RT=R/'ForgeRuntime'
if '--describe'in sys.argv:
 print('Two explicit native confirmation callback answers; zero casts, mana or rule-effect proof.');sys.exit(0)
assert not os.environ.get('MTGB_NAME_OVERRIDE')and not os.environ.get('MTGB_CHOICE_ASSETS')
F=B/'adapter'/str(time.time_ns());F.mkdir(parents=True);(F/'classes').mkdir()
def wr(p,x):p.write_text(json.dumps(x,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def ref(p):return dict(path=p.relative_to(R).as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
sources=[R/'Tools/ForgeBridge/tests/RoyalStateFixture.java',R/'Tools/ForgeBridge/tests/GeneratedConfirmPreviewFixture.java',R/'Tools/ForgeBridge/java/mtgb/bridge/ForgeBridge.java',Path(__file__)]
frozen=[]
for i,s in enumerate(sources):
 dst=F/'frozen'/str(i)/s.name;dst.parent.mkdir(parents=True);dst.write_bytes(s.read_bytes());frozen.append(dict(source=ref(s),frozen=ref(dst)))
runtime=json.loads((RT/'runtime.json').read_text('utf8'));wr(F/'runtime.json',runtime)
with (F/'compile.log').open('w',encoding='utf8')as log:
 subprocess.run(['C:/Program Files/Java/jdk-17/bin/javac.exe','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(F/'classes'),*map(str,sources[:2])],stdout=log,stderr=subprocess.STDOUT,check=True)
deck=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
wr(F/'start.json',dict(session='generated-confirm-adapter',seed=4,startingLife=20,firstPlayerSeat=1,decks=[deck,deck]))
checks=[]
def ck(ok,msg):assert ok,msg;checks.append(msg)
with tempfile.TemporaryDirectory(prefix='profile-',dir=F,ignore_cleanup_errors=True)as temp,(F/'stderr.log').open('w',encoding='utf8')as err,(F/'trace.jsonl').open('w',encoding='utf8')as trace:
 p=subprocess.Popen([str(RT/runtime['java']),'-Xmx512m','-Djava.awt.headless=true','-cp',';'.join(map(str,[F/'classes',RT/'classes',RT/'lib/*'])),'mtgb.bridge.GeneratedConfirmPreviewFixture',str(RT/'assets'),str(F/'start.json')],cwd=RT/'assets',env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,encoding='utf8')
 q=queue.Queue();threading.Thread(target=lambda:[q.put(json.loads(x))for x in p.stdout],daemon=True).start();count=0;selected=[]
 try:
  for i in range(30):
   m=q.get(timeout=40);trace.write(json.dumps(m)+'\n');trace.flush();ck(m['type']not in ['fatal','rejected'],'Native adapter envelope')
   if m['type']=='adapter-result':
    ck(m['answers']==[True,False]and m['contextRestored']and m['detached']and m['effectOperations']==0,'Both explicit callback branches and exact context cleanup');wr(F/'adapter-result.json',m);break
   if m['type']!='request':continue
   r=m['request']
   if r['kind']=='InputConfirm':
    wr(F/f'confirm-{count}-wire.json',m);c=r['previewCards'][0]
    ck(len(r['previewCards'])==1 and c['name']=='Shivan Dragon'and c['printingKey']and c['basePower']==5 and c['baseToughness']==5,'Exact native detached card complete public face '+str(count))
    ck(c['zone']=='None'and c['ownerSeat']==1 and c['id']>0,'Original detached identity/owner '+str(count))
    ck({o['id']for o in r['options']if not o['disabled']}=={'ok','cancel'},'Native confirmation permissions '+str(count))
    selected.append(c['id']);pick='ok'if count==0 else'cancel';count+=1
   else:pick='keep'if any(o['id']=='keep'and not o['disabled']for o in r['options'])else'ok'
   p.stdin.write(json.dumps(dict(type='respond',session=m['session'],seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
  else:raise AssertionError('Bounded adapter completion')
  ck(count==2 and selected[0]==selected[1],'Same original detached identity restored between callback branches')
 finally:
  if p.poll()is None:
   p.stdin.write('{"type":"shutdown"}\n');p.stdin.flush()
   try:p.wait(timeout=3)
   except subprocess.TimeoutExpired:p.kill();p.wait()
wr(F/'verified.json',dict(status='PASS_ZERO_EFFECT_GENERATED_CONFIRM_ADAPTER',checks=len(checks),assertions=checks,source=frozen,runtimeHash=runtime['runtimeHash'],confirmations=2,nativeEffectOperations=0,limits=['Exact adapter callback/preview only; does not replace N54 actual paid copy effect evidence.']))
wr(B/'adapter-latest.json',dict(folder=F.relative_to(R).as_posix(),verified=ref(F/'verified.json')))
print('PASS_ZERO_EFFECT_GENERATED_CONFIRM_ADAPTER',len(checks))
