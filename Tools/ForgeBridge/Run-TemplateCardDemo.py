"""Two PSD component faces, with real casts/effects and an interactive editor mode."""
import json,os,queue,subprocess,sys,tempfile,threading,time,hashlib,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
VERIFY='--verify' in sys.argv;OUT=ROOT/'.tools/component-card-1008'/('native/'+str(time.time_ns()) if VERIFY else 'demo');OUT.mkdir(parents=True,exist_ok=True)
CLASSES=ROOT/'.tools/component-card-1007/native/classes';ELF='Elvish Visionary';GIANT='Bonecrusher Giant'
def save(name,value):
 p=OUT/(name+'.json');t=p.with_suffix('.tmp');t.write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding='utf8')
 for attempt in range(100):
  try:t.replace(p);return
  except PermissionError:
   if attempt==99:raise
   time.sleep(.02)
def run():
 for name in ['stop.json','action.json','view.json']:(OUT/name).unlink(missing_ok=True)
 save('status',dict(status='STARTING'))
 deck={'cards':[{'setCode':'10E','collectorNumber':'368','count':60}],'sideboard':[]}
 config=dict(session='template-demo',own=dict(Hand=['Reckless Impulse',ELF,ELF,GIANT,GIANT,'Unsummon'],Battlefield=['Future Sight','Muldrotha, the Gravetide','Sphere of Resistance',ELF,GIANT],Graveyard=[ELF,GIANT],Exile=[ELF,GIANT],Library=[ELF,GIANT,ELF,GIANT,ELF,GIANT]),other=dict(Battlefield=[ELF,GIANT]),seed=4,firstPlayerSeat=1,startingLife=20,decks=[deck,deck])
 assertions=[];snapshots=set()
 def check(value,why):
  assert value,why
  if why not in assertions:assertions.append(why)
 def snap(m,label):
  if label in snapshots:return
  snapshots.add(label)
  for seat,v in enumerate(m['views'],1):save(label+('-other' if seat==2 else ''),dict(v,request=v.get('request',m['request']),awaitingInput=m['request']['seat']==seat))
 with tempfile.TemporaryDirectory(dir=OUT,prefix='session-') as tmp,(OUT/'engine.log').open('w',encoding='utf8') as log,(OUT/'trace.jsonl').open('w',encoding='utf8') as trace:
  audit=Path(tmp)/'audit';audit.mkdir();config['auditDir']=str(audit);start=Path(tmp)/'start.json';start.write_text(json.dumps(config),encoding='utf8');save('config',config)
  proc=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+tmp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.PermissionLifecycleFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=tmp,LOCALAPPDATA=tmp,TEMP=tmp,TMP=tmp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
  messages=queue.Queue()
  def reader():
   try:
    for line in proc.stdout:messages.put(json.loads(line))
   finally:messages.put(None)
  threading.Thread(target=reader,daemon=True).start()
  stage=0;active=False;ready=False;answered=set();idsCast={};handBefore=0;giantId=0;privateHandIds=None
  try:
   for step in range(1200):
    if (OUT/'stop.json').exists():break
    m=messages.get(timeout=45)
    if m is None:raise RuntimeError('Native process closed')
    trace.write(json.dumps(m)+'\n');trace.flush()
    assert m['type'] not in ['fatal','ended','rejected'],m
    if m['type']!='request':continue
    r=m['request'];v=m['views'][0];kind=r['kind'];options=r['options'];ids=[o['id'] for o in options if not o.get('disabled')];pick='ok'
    if r['requestId'] in answered:continue
    if privateHandIds is None:privateHandIds={c['id'] for c in v['players'][0]['hand']}
    check(not privateHandIds.intersection(c['id'] for c in m['views'][1]['players'][0]['hand']),'Initially private opponent hand identities remain hidden')
    interactive=ready and not VERIFY
    if interactive:pass
    elif kind=='InputPassPriority':
     stack=v['stackItems'];names=[s.get('source',{}).get('name') for s in stack]
     if ELF in names:snap(m,'stack-visionary')
     if 'Stomp' in names:
      c=next(s['source'] for s in stack if s['source']['name']=='Stomp');check(c.get('castingManaCost')=='{2}{R}','Actual adventure casting cost is {2}{R}, not creature {3}{R}');snap(m,'stack-adventure');ready=True
     if GIANT in names and stage==3:
      c=next(s['source'] for s in stack if s['source']['id']==giantId);check(c.get('castingManaCost')=='{3}{R}','Actual creature casting cost is {3}{R}');snap(m,'stack-giant')
     if active and not stack and r['seat']==1:
      if stage==2:
       check(v['players'][1]['life']==18,'Stomp resolves and deals exactly two damage')
       check(v['players'][0]['handCount']==handBefore-1,'Visionary draws exactly one after two hand spells')
       check(any(c['id']==idsCast[1] for c in v['players'][0]['battlefield']),'Visionary resolves to battlefield')
       check(any(c['id']==idsCast[2] for c in v['playableExile']),'Resolved adventure grants creature casting permission in exile');snap(m,'effects-resolved')
      if stage==3:
       check(any(c['id']==giantId and c['power']==4 and c['toughness']==3 for c in v['players'][0]['battlefield']),'Adventured creature resolves as 4/3');snap(m,'giant-resolved')
      if stage==4:
       check(v['players'][0]['life']==18,'Targeting Giant triggers native two damage to spell controller')
       check(any(c['id']==giantId for c in v['players'][0]['hand']),'Unsummon resolves after Giant trigger');snap(m,'trigger-resolved')
       result=dict(status='PASS',assertions=assertions,runtimeHash=CFG['runtimeHash'],driverSha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),folder=OUT.relative_to(ROOT).as_posix())
       save('result',result);(ROOT/'.tools/component-card-1008/native-result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8');print('PASS',json.dumps(result,ensure_ascii=False),flush=True);return
      stage+=1;active=False
     if active and stage==1 and ELF in names and r['seat']==1:stage=2;active=False
     if not active and r['seat']==1 and v['phase']=='MAIN1' and (not stack or stage==2):
      if stage==1:
       handBefore=v['players'][0]['handCount'];snap(m,'before-hand')
       elf=next(c for c in v['players'][0]['hand'] if c['name']==ELF);g=next(c for c in v['players'][0]['hand'] if c['name']==GIANT)
       check(elf.get('previewManaCost')=='{2}{G}','Visionary hand cost includes Sphere increase')
       check(g.get('previewManaCost')=='{3}{R}' and g.get('adventurePreviewManaCost')=='{2}{R}','Hand exports independent native creature and adventure costs')
      name=['Reckless Impulse',ELF,GIANT,GIANT,'Unsummon'][stage]
      pool=v['playableExile'] if stage==3 else v['players'][0]['hand']
      c=next(c for c in pool if c['name']==name and (stage!=3 or c['id']==idsCast[2]) and 'card:'+str(c['id']) in ids)
      idsCast[stage]=c['id'];pick='card:'+str(c['id']);active=True
      if stage==3:giantId=c['id']
    elif kind=='dialog':
     faces=[o for o in options if o.get('castingFace') and not o.get('disabled')]
     if faces:
      snap(m,'adventure-options' if stage==2 else 'creature-options')
      chosen=next(o for o in faces if o['castingFace']['name']==('Stomp' if stage==2 else GIANT));pick=chosen['id']
      expected='{2}{R}' if stage==2 else '{3}{R}';check(chosen['castingFace'].get('previewManaCost')==expected,'Chosen face preview equals '+expected)
     else:pick=next(o['id'] for o in options if o['id'].startswith('choice:') and not o.get('disabled'))
    elif kind=='InputSelectTargets':
     target='card:'+str(giantId) if stage==4 else 'player:'+str(v['players'][1]['targetId'])
     pick='ok' if r.get('selectedCount',0)>0 or any(o.get('selected') for o in options) else target
     snap(m,'target-'+str(stage))
    elif kind.startswith('InputPayMana'):snap(m,'payment-'+str(stage))
    elif kind=='RevealCards':pick='confirm-cards'
    elif kind not in ['InputAttack','InputBlock','InputConfirm','InputConfirmMulligan']:raise AssertionError(('unhandled',stage,r))
    if ready and not VERIFY:
     save('view',dict(v,request=v.get('request',r),awaitingInput=r['seat']==1));save('status',dict(status='READY',requestId=r['requestId'],kind=kind,runtimeHash=CFG['runtimeHash']))
     if r['seat']==1:
      for wait in range(864000):
       if (OUT/'stop.json').exists():return
       action=OUT/'action.json'
       if action.exists():
        try:command=json.loads(action.read_text('utf8'))
        except (PermissionError,json.JSONDecodeError):time.sleep(.05);continue
        action.unlink(missing_ok=True)
        if command.get('requestId')==r['requestId'] and command.get('option') in ids:pick=command['option'];break
       time.sleep(.05)
    assert pick in ids,(stage,kind,pick,r)
    answered.add(r['requestId']);print(step,stage,kind,pick,flush=True)
    proc.stdin.write(json.dumps(dict(type='respond',session='template-demo',seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');proc.stdin.flush()
   raise RuntimeError('Stopped before verification completed')
  finally:
   if proc.poll() is None:proc.kill();proc.wait()
if __name__=='__main__':
 try:run()
 except BaseException:save('status',dict(status='FAIL',error=traceback.format_exc()));raise
