"""One component-face pilot: real casts, native total preview, frozen seat views."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
OUT=ROOT/'.tools/component-card-1007/native';OUT.mkdir(parents=True,exist_ok=True);CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURES=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['StateBranchFixture.java','PermissionLifecycleFixture.java']]
DECK={'cards':[{'setCode':'10E','collectorNumber':'368','count':60}],'sideboard':[]}
BEAR='Grizzly Bears'
CASES={
 'base':(dict(Hand=[BEAR]),['Hand'],'{1}{G}',2),
 'raised':(dict(Hand=[BEAR],Battlefield=['Sphere of Resistance']),['Hand'],'{2}{G}',3),
 'reduced':(dict(Hand=[BEAR],Battlefield=['Emerald Medallion']),['Hand'],'{G}',1),
 'combined':(dict(Hand=[BEAR],Battlefield=['Sphere of Resistance','Emerald Medallion']),['Hand'],'{1}{G}',2),
 'zones':(dict(Hand=['Reckless Impulse',BEAR,BEAR],Battlefield=['Future Sight','Lurrus of the Dream-Den','Sphere of Resistance',BEAR],Graveyard=[BEAR],Exile=[BEAR],Library=[BEAR,BEAR,BEAR]),['impulse','Hand','Graveyard','Exile','Library'],'{2}{G}',3),
}
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def run(case):
 own,actions,expected,total=CASES[case];folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir()
 meta=dict(case=case,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],driverSha256=sha(__file__),fixtures={p.name:sha(p) for p in FIXTURES})
 (folder/'driver.py').write_bytes(Path(__file__).read_bytes())
 for p in FIXTURES:(folder/p.name).write_bytes(p.read_bytes())
 assertions=[]
 def check(ok,why):
  assert ok,why
  if why not in assertions:assertions.append(why)
 def finish(status,why):
  result=dict(meta,status=status,assertions=assertions,result=why)
  for p in [folder/'result.json',OUT/(case+'-result.json')]:p.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  return result
 with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as tmp,(folder/'engine.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
  config=dict(session=case,own=own,other={},auditDir=str(audit),seed=4,firstPlayerSeat=1,startingLife=20,decks=[DECK,DECK])
  start=Path(tmp)/'start.json';start.write_text(json.dumps(config),encoding='utf8');(folder/'start.json').write_bytes(start.read_bytes())
  proc=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+tmp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.PermissionLifecycleFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=tmp,LOCALAPPDATA=tmp,TEMP=tmp,TMP=tmp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
  messages=queue.Queue()
  def reader():
   try:
    for line in proc.stdout:messages.put(json.loads(line))
   finally:messages.put(None)
  threading.Thread(target=reader,daemon=True).start()
  def snap(m,label):
   for seat,v in enumerate(m['views'],1):(folder/(label+('-other' if seat==2 else '')+'.json')).write_text(json.dumps(dict(v,request=v.get('request',m['request']),awaitingInput=m['request']['seat']==seat),ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  stage=0;active=False;answered=set();casting=None;beforeTapped=0;paid=False;stackSeen=False;cancelled=False;retry=False
  try:
   for step in range(350):
    m=messages.get(timeout=45);check(m is not None,'Native process available');trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ['fatal','ended','rejected'],m
    if m['type']!='request':continue
    r=m['request'];v=m['views'][0];kind=r['kind'];opts=r['options'];ids=[o['id'] for o in opts if not o.get('disabled')];pick='ok'
    if r['requestId'] in answered:continue
    check(not m['views'][1]['players'][0]['hand'],'Pilot preview does not reveal hidden opponent hand')
    if kind=='InputPassPriority':
     stack=[s for s in v['stackItems'] if s.get('source',{}).get('id')==casting]
     if active and stack and actions[stage]!='impulse':
      source=stack[0]['source'];check(source['manaCost']=='{1}{G}','Printed mana cost and mana value remain unchanged')
      check(source.get('castingManaCost')==expected,'Stack captures actual native adjusted cost '+expected)
      tapped=sum(c.get('tapped',False) for c in v['players'][0]['battlefield'] if c.get('land'))
      check(tapped-beforeTapped==total,'Native payment taps exactly '+str(total)+' lands')
      if not stackSeen:snap(m,'stack-'+actions[stage]);stackSeen=True
     if active and not v['stackItems'] and r['seat']==1:
      if actions[stage]!='impulse':check(stackSeen and paid,'Cast completed through native mana payment and stack');check(any(c['id']==casting for c in v['players'][0]['battlefield']),'Cast card resolves to battlefield');snap(m,'resolved-'+actions[stage])
      stage+=1;active=False;paid=stackSeen=False
      if stage==len(actions):return finish('PASS','Native pilot costs, cast identities, payment, stack and resolution: '+case)
     if not active and r['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
      origin=actions[stage];p=v['players'][0]
      pool=p['hand'] if origin in ['Hand','impulse'] else v['playableGraveyard'] if origin=='Graveyard' else v['playableExile'] if origin=='Exile' else [p['libraryTop']]
      name='Reckless Impulse' if origin=='impulse' else BEAR
      c=next(c for c in pool if c and c['name']==name and 'card:'+str(c['id']) in ids)
      casting=c['id'];pick='card:'+str(casting);active=True;beforeTapped=sum(c.get('tapped',False) for c in p['battlefield'] if c.get('land'))
      if origin!='impulse':check(c.get('previewManaCost')==expected,'Native '+origin+' preview '+expected)
      snap(m,'before-'+origin+('-retry' if retry else ''));retry=False
    elif kind.startswith('InputPayMana'):
     snap(m,'payment-'+actions[stage]);paid=True
     if case=='combined' and not cancelled:pick='cancel';cancelled=True;active=False;paid=False;retry=True
    elif kind=='RevealCards':pick='confirm-cards'
    elif kind=='dialog':pick=next(o['id'] for o in opts if o['id'].startswith('choice:') and not o.get('disabled'))
    elif kind not in ['InputAttack','InputBlock','InputConfirm','InputConfirmMulligan']:raise AssertionError(('Unhandled',case,stage,r))
    assert pick in ids,(case,stage,kind,pick,r);answered.add(r['requestId']);print(case,step,stage,kind,pick,flush=True)
    proc.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');proc.stdin.flush()
   raise AssertionError('No completion')
  except BaseException:finish('FAIL',traceback.format_exc());raise
  finally:
   if proc.poll() is None:proc.kill();proc.wait()
if __name__=='__main__':
 results=[]
 try:
  subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),*[str(p) for p in FIXTURES]],check=True)
  for case in sys.argv[1:] or CASES:results.append(run(case));print('PASS '+case+': '+results[-1]['result'],flush=True)
  (OUT/'run-summary.json').write_text(json.dumps(dict(status='PASS',results=results),indent=2)+'\n',encoding='utf8')
 except BaseException:
  (OUT/'run-summary.json').write_text(json.dumps(dict(status='FAIL',results=results,error=traceback.format_exc()),indent=2)+'\n',encoding='utf8');raise
