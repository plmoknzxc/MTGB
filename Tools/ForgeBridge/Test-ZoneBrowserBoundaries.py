"""One missing R15 result: real fetch activation with nonempty all-ineligible library."""
import hashlib,json,os,queue,subprocess,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
B=ROOT/'.tools/zone-browser-1007';B.mkdir(exist_ok=True);OUT=B/'native';OUT.mkdir(exist_ok=True);CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
(OUT/'run-summary.json').write_text(json.dumps(dict(status='RUNNING',results=[])),encoding='utf8')
FIX=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['LibraryCostFixture.java','SearchBoundaryFixture.java']]
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),*[str(f) for f in FIX]],check=True)
folder=OUT/str(time.time_ns());folder.mkdir();audit=folder/'audit';audit.mkdir()
deck={'cards':[{'setCode':'10E','collectorNumber':'368','count':60}],'sideboard':[]}
start=dict(session='zone-no-match',fixture='zone-no-match',hand1=[],hand2=[],board1=['Wooded Foothills'],board2=[],grave1=[],grave2=[],emptyLibrary=False,auditDir=str(audit),seed=4,firstPlayerSeat=1,startingLife=20,decks=[deck,deck])
(folder/'start.json').write_text(json.dumps(start),encoding='utf8');(folder/'driver.py').write_bytes(Path(__file__).read_bytes())
meta=dict(case='fetch-nonempty-no-match',folder=folder.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],driverSha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),fixtures=[dict(path=f.relative_to(ROOT).as_posix(),sha256=hashlib.sha256(f.read_bytes()).hexdigest()) for f in FIX])
log=(folder/'engine.log').open('w',encoding='utf8');trace=(folder/'trace.jsonl').open('w',encoding='utf8')
p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',str(CLASSES)+';'+str(RT/'classes')+';'+str(RT/'lib/*'),'mtgb.bridge.SearchBoundaryFixture',str(RT/CFG['assets']),str(folder/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=str(folder),LOCALAPPDATA=str(folder),TEMP=str(folder),TMP=str(folder)),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
q=queue.Queue()
def reader():
 try:
  for line in p.stdout:q.put(json.loads(line))
 finally:q.put(None)
threading.Thread(target=reader,daemon=True).start()
def snap(m,label):
 for seat,v in enumerate(m['views'],1):(folder/(label+'-seat'+str(seat)+'.json')).write_text(json.dumps(dict(v,request=m['request'] if m['request']['seat']==seat else None,awaitingInput=m['request']['seat']==seat),ensure_ascii=False,indent=2),encoding='utf8')
def libraries(r):
 f=audit/(str(r['requestId'])+'.json');until=time.monotonic()+3
 while time.monotonic()<until:
  try:return [x['library'] for x in json.loads(f.read_text(encoding='utf8'))['players']]
  except (OSError,ValueError):time.sleep(.01)
 raise AssertionError('Missing library audit')
activated=False;source=None;before=None;asked=[];answered=set()
try:
 for step in range(100):
  m=q.get(timeout=35);assert m and m['type'] not in ['fatal','rejected','ended'],m;trace.write(json.dumps(m)+'\n');trace.flush()
  if m['type']!='request':continue
  r=m['request'];v=m['views'][0];one,two=v['players'];kind=r['kind'];ids=[o['id'] for o in r['options'] if not o.get('disabled')];pick='ok'
  if r['requestId'] in answered:continue
  assert not m['views'][0]['players'][1]['hand'] and not m['views'][1]['players'][0]['hand'],'Private hand leak'
  if kind=='InputPassPriority':
   if not activated and r['seat']==1 and v['phase']=='MAIN1':
    source=next(c['id'] for c in one['battlefield'] if c['name']=='Wooded Foothills');before=libraries(r);assert len(before[0])>0 and not one['hand'];snap(m,'before');pick='card:'+str(source);activated=True
   elif activated and not v['stackItems'] and any(c['id']==source for c in one['graveyardCards']):
    after=libraries(r);assert one['life']==19 and two['life']==20 and not one['battlefield'] and not one['hand']
    assert set(before[0])==set(after[0]) and before[1]==after[1] and sum(one['manaPool'])+sum(two['manaPool'])==0
    assert not any(o.get('card') for o in r['options']),'No unexpected actions'
    snap(m,'resolved');meta.update(status='PASS',assertions='Actual Wooded Foothills activation, one life and exact source sacrificed; nonempty all-Island library has no Mountain/Forest candidate; no illegal card offered, no blocked required picker; both library identity sets preserved and next main-phase priority restored.',requestKinds=asked,initialLibraries=before,finalLibraries=after);break
  elif kind=='dialog':pick=ids[0]
  elif kind in ['SelectCards','InputSelectEntitiesFromList','InputSelectCardsFromList']:
   assert not any(o.get('card') for o in r['options']),'Ineligible Island offered';pick=next(x for x in ['cancel','ok','confirm-cards'] if x in ids);snap(m,'empty-picker')
  elif kind=='RevealCards':pick='confirm-cards'
  elif kind in ['InputMulligan']:pick='keep'
  elif kind not in ['InputConfirm','InputConfirmMulligan','InputAttack','InputBlock']:raise AssertionError(('Unhandled',kind,r))
  assert pick in ids,(kind,pick,r);asked.append(kind);answered.add(r['requestId']);print(step,kind,pick,flush=True)
  p.stdin.write(json.dumps(dict(type='respond',session='zone-no-match',seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
 else:raise AssertionError('No completion')
except BaseException:meta.update(status='FAIL',error=traceback.format_exc());raise
finally:
 if p.poll() is None:p.kill();p.wait()
 trace.close();log.close()
 if meta['status']=='PASS':meta['result']='PASS '+meta['case']+': '+meta['assertions']
 for f in [folder/'result.json',OUT/'result.json']:f.write_text(json.dumps(meta,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
 (OUT/'run-summary.json').write_text(json.dumps(dict(status=meta['status'],results=[meta]),ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print('PASS fetch nonempty no matching land; native priority restored')
