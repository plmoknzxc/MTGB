"""Native snapshot/group safety metadata only; does not count as effect evidence."""
import json,os,queue,subprocess,tempfile,threading
from pathlib import Path
root=Path(__file__).resolve().parents[2];runtime=root/'ForgeRuntime';out=root/'.tools/battlefield-compact-1005';out.mkdir(parents=True,exist_ok=True)
cfg=json.loads((runtime/'runtime.json').read_text(encoding='utf-8-sig'));classes=out/'native-classes'
subprocess.run(['javac','-encoding','UTF-8','-cp',str(runtime/'classes')+';'+str(runtime/'lib/*'),'-d',str(classes),str(root/'Tools/ForgeBridge/tests/PermanentGroupingFixture.java')],check=True)
deck={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
with tempfile.TemporaryDirectory(prefix='mtgb-grouping-') as temp,(out/'native.log').open('w',encoding='utf-8') as log:
 data=Path(temp)/'metadata.json';start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session='grouping',groupingOutput=str(data),seed=4,startingLife=20,firstPlayerSeat=1,decks=[deck,deck])),encoding='utf-8')
 p=subprocess.Popen([str(runtime/cfg['java']),'-Djava.awt.headless=true','-cp',str(classes)+';'+str(runtime/'classes')+';'+str(runtime/'lib/*'),'mtgb.bridge.PermanentGroupingFixture',str(runtime/cfg['assets']),str(start)],cwd=runtime/cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf-8')
 q=queue.Queue();threading.Thread(target=lambda:[q.put(json.loads(line)) for line in p.stdout],daemon=True).start()
 try:
  for _ in range(20):
   m=q.get(timeout=45);assert m['type']!='fatal',m
   if not data.exists():
    if m['type']=='request':
     r=m['request'];p.stdin.write(json.dumps(dict(type='respond',session='grouping',seat=r['seat'],requestId=r['requestId'],option='ok'))+'\n');p.stdin.flush()
    continue
   field=json.loads(data.read_text(encoding='utf-8'));assert len(field)==15
   v={'seat':1,'players':[{'seat':1,'battlefield':field}]}
   for name in ['Grizzly Bears','Command Tower','Sol Ring','Ghostly Prison','Jace Beleren','Invasion of Zendikar']:
    cards=[c for c in field if c['name']==name];assert len(cards)>=2,(name,field)
    assert all(c['stackableStateKnown'] and c['stackablePermanent'] for c in cards[:2]),name
   rings=[c for c in field if c['name']=='Sol Ring'];assert not rings[-1]['stackablePermanent'],'Remembered state folded'
   walkers=[c for c in field if c['name']=='Jace Beleren'];assert [c['loyalty'] for c in walkers]==[4,4,3]
   battles=[c for c in field if c['name']=='Invasion of Zendikar'];assert [c['protectingPlayerSeat'] for c in battles]==[2,2,1]
   (out/'native-grouping.json').write_text(json.dumps(v,ensure_ascii=False),encoding='utf-8')
   (out/'native-result.txt').write_text('PASS: native snapshot exports safe grouping for six permanent categories; remembered state excluded; loyalty and battle protector remain distinct. Metadata adapter only, no new card effect PASS.',encoding='utf-8')
   print('PASS: native grouping metadata and state exclusions');break
  else:raise AssertionError('No snapshot')
  assert (out/'native-result.txt').exists(),'Missing battlefield fixture'
 finally:p.terminate();p.wait(timeout=10)
