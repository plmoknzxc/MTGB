"""R28 actual impulse play, natural expiration, source/control and prepared permission."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
OUT=ROOT/'.tools/permission-lifecycle-1007/native';OUT.mkdir(parents=True,exist_ok=True);CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURES=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['StateBranchFixture.java','PermissionLifecycleFixture.java']]
DECK={'cards':[{'setCode':'10E','collectorNumber':'368','count':60}],'sideboard':[]}
CASES={
 'impulse-next-expiry':dict(own=dict(Hand=['Reckless Impulse'],Library=['Grizzly Bears','Forest']),other={}),
 'impulse-current-expiry':dict(own=dict(Hand=['Act on Impulse'],Library=['Grizzly Bears','Forest','Air Elemental']),other={}),
 'gonti-control-source':dict(own=dict(Hand=['Gonti, Lord of Luxury','Donate','Unsummon']),other=dict(Library=['Air Elemental','Forest','Mountain','Plains'])),
 'prepared-control-source':dict(own=dict(Hand=['Emeritus of Woe','Donate','Unsummon']),other={}),
}
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def run(case):
 folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir()
 meta=dict(case=case,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],driverSha256=sha(__file__),fixtures={p.name:sha(p) for p in FIXTURES})
 (folder/'driver.py').write_bytes(Path(__file__).read_bytes())
 for p in FIXTURES:(folder/p.name).write_bytes(p.read_bytes())
 assertions=[]
 def finish(status,why):
  result=dict(meta,status=status,assertions=assertions,result=status+' '+case+': '+why)
  for p in [folder/'result.json',OUT/(case+'-result.json')]:p.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  return result
 with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as tmp,(folder/'engine.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
  config=dict(session=case,**CASES[case],auditDir=str(audit),seed=4,firstPlayerSeat=1,startingLife=20,decks=[DECK,DECK])
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
  def native(m):
   p=audit/(str(m['request']['requestId'])+'.json')
   for _ in range(100):
    try:
     if p.exists():return json.loads(p.read_text('utf8'))['cards']
    except (PermissionError,json.JSONDecodeError):pass
    time.sleep(.01)
   raise AssertionError('Read-only audit absent')
  def check(ok,why):
   assert ok,why
   if why not in assertions:assertions.append(why)
  refs={};stage=0;active=False;answered=set();exiled=[];cancelled=False;retry=False;prepared=None
  actions=['Reckless Impulse','Grizzly Bears'] if case=='impulse-next-expiry' else ['Act on Impulse','Grizzly Bears','Forest'] if case=='impulse-current-expiry' else ['Gonti, Lord of Luxury','Donate','Unsummon','Air Elemental'] if case=='gonti-control-source' else ['Emeritus of Woe','Donate','Unsummon']
  try:
   for step in range(600):
    m=messages.get(timeout=40);check(m is not None,'Native process remains available');trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ['fatal','ended','rejected'],m
    if m['type']!='request':continue
    r=m['request'];v=m['views'][0];kind=r['kind'];opts=r['options'];ids=[o['id'] for o in opts if not o.get('disabled')];pick='ok'
    if r['requestId'] in answered:continue
    check(not m['views'][0]['players'][1]['hand'] and not m['views'][1]['players'][0]['hand'],'Unrelated hidden hands remain private in both seats')
    if kind=='InputPassPriority':
     cards=native(m)
     if not refs and r['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
      for c in cards:
       if c['zone'] in ['Hand','Library'] and c['name'] not in refs:refs[c['name']]=c['id']
      snap(m,'before')
     if refs and v['phase']=='MAIN1' and not v['stackItems']:
      if active and r['seat']==1:
       name=actions[stage];card=next(c for c in cards if c['id']==refs[name])
       if stage==0:
        if case.startswith('impulse-'):
         exiled=[c['id'] for c in cards if c['zone']=='Exile'];expected=2 if case=='impulse-next-expiry' else 3
         check(len(exiled)==expected,'Actual spell exiles exact top library IDs')
         check(set(exiled)=={refs[n] for n in CASES[case]['own']['Library']},'Exiled identities match initial top cards')
        elif case=='gonti-control-source':
         stolen=next(c for c in cards if c['id']==refs['Air Elemental']);exiled=[stolen['id']]
         check(stolen['zone']=='Exile' and stolen['faceDown'] and stolen['owner']==2,'Gonti actually exiles chosen opposing card face down')
         check(stolen['knownSeats']==[1] and stolen['permissionSeats']==[1],'Only original trigger controller knows and may cast stolen card')
         check(any(c['id']==stolen['id'] and c.get('knownFaceName')=='Air Elemental' for c in v['playableExile']),'Authorized face-down exile has an identifiable casting preview')
         check(not m['views'][1]['playableExile'] and all(not c.get('knownFaceName') for c in m['views'][1]['players'][1]['exileCards']),'Other recipient has neither casting preview nor stolen face')
        else:
         source=next(c for c in v['players'][0]['battlefield'] if c['id']==refs[name]);prepared=source['preparedSpell']['id'];exiled=[prepared]
       elif name=='Donate':
        source=refs[actions[0]];check(next(c for c in cards if c['id']==source)['controller']==2,'Real Donate transfers source control to opponent')
        if case.startswith('prepared'):
         check(any(c['id']==prepared for c in m['views'][1]['playableExile']) and not any(c['id']==prepared for c in v['playableExile']),'Prepared permission transfers with source control')
        else:check(next(c for c in cards if c['id']==exiled[0])['permissionSeats']==[1],'Independent Gonti permission stays with original controller after donation')
       elif name=='Unsummon':
        check(next(c for c in cards if c['id']==refs[actions[0]])['zone']=='Hand','Actual Unsummon removes source to owner hand')
        if case.startswith('prepared'):
         check(all(not any(c['id']==prepared for c in w['playableExile']) for w in m['views']),'Prepared permission removed for both seats when source leaves')
        else:check(next(c for c in cards if c['id']==exiled[0])['permissionSeats']==[1],'Gonti permission survives source leaving')
       else:
        check(card['zone']=='Battlefield','Actual exile play resolves same ID onto battlefield')
        check(all(not any(c['id']==card['id'] for c in w['playableExile']) for w in m['views']),'Consumed exile permission removed from both projections')
       snap(m,'after-'+str(stage));stage+=1;active=False
       if stage==len(actions) and case.endswith('source'):return finish('PASS','native permissions, source control/leaving, exact IDs, actual inputs/payments and recovery')
      if case.startswith('impulse-') and stage>=1:
       remaining=[c for c in cards if c['id'] in exiled and c['zone']=='Exile']
       deadline=3 if case=='impulse-next-expiry' else 1
       if v['turn']<=deadline:
        check(all(c['permissionSeats']==[1] for c in remaining),'Remaining exiled cards retain only caster permission through allowed turns')
        check({c['id'] for c in v['playableExile']}=={c['id'] for c in remaining},'Unity available exile projection matches native permissions')
        check(not m['views'][1]['playableExile'],'Opponent projection excludes caster-only impulse permissions')
       elif stage==len(actions):
        check(bool(remaining) and all(not c['permissionSeats'] for c in remaining),'Natural deadline removes permissions without moving remaining exiled cards')
        check(all(not w['playableExile'] for w in m['views']),'Both client projections clear expired impulse cards')
        check(not any('card:'+str(c['id']) in ids for c in remaining),'Expired cards have no native action')
        snap(m,'expired');return finish('PASS','actual impulse spell/land play, exact identity, cancel recovery, natural allowed-turn persistence and deadline expiration')
       if case=='impulse-next-expiry' and v['turn']==2:snap(m,'opponent-turn')
       if case=='impulse-next-expiry' and v['turn']==3:snap(m,'next-turn')
      seat=1
      ready=stage<len(actions) and r['seat']==seat and (case!='impulse-next-expiry' or stage!=1 or v['turn']==3)
      if ready and not active:
       name=actions[stage];pick='card:'+str(refs[name]);check(pick in ids,'Intended spell/land is offered by native engine at legal timing');active=True;snap(m,'action-'+str(stage))
       if retry:snap(m,'cancelled-retry');retry=False
    elif kind=='InputSelectTargets':
     if r.get('selectedCount',0)>=r.get('minCount',1):pick='ok'
     elif actions[stage]=='Donate' or actions[stage]=='Gonti, Lord of Luxury':
      pick='player:'+str(v['players'][1]['targetId']) if any(i.startswith('player:') for i in ids) else 'card:'+str(refs[actions[0]])
     elif actions[stage]=='Unsummon':pick='card:'+str(refs[actions[0]])
     else:raise AssertionError(('Unhandled target',r))
     snap(m,'targets-'+str(stage)+'-'+str(r.get('selectedCount',0)))
    elif kind in ['SelectCards','InputSelectCardsFromList','InputSelectEntitiesFromList']:
     if r.get('selectedCount',0)>=r.get('minCount',1):pick='confirm-cards' if 'confirm-cards' in ids else 'ok'
     else:pick=next(o['id'] for o in opts if o.get('card',{}).get('name')=='Air Elemental')
     snap(m,'choose-stolen-'+str(r.get('selectedCount',0)))
    elif kind.startswith('InputPayMana'):
     snap(m,'payment-'+str(stage))
     if case=='impulse-next-expiry' and stage==1 and not cancelled:pick='cancel';cancelled=True;active=False;retry=True
    elif kind=='dialog':
     valid=[o for o in opts if o['id'].startswith('choice:') and not o.get('disabled')]
     pick=valid[0]['id'];snap(m,'dialog-'+str(stage))
    elif kind=='RevealCards':pick='confirm-cards';snap(m,'reveal-'+str(stage))
    elif kind.startswith('InputSelectCards') and 'discard' in r['message'].lower():pick='ok' if r.get('selectedCount',0)>=r.get('minCount',1) else next(i for i in ids if i.startswith('card:'))
    elif kind not in ['InputAttack','InputBlock','InputConfirm','InputConfirmMulligan']:raise AssertionError(('Unhandled',case,stage,r))
    assert pick in ids,(case,stage,kind,pick,r);answered.add(r['requestId']);print(case,step,stage,v['turn'],v['phase'],kind,pick,flush=True)
    proc.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');proc.stdin.flush()
   raise AssertionError('No completion')
  except BaseException:finish('FAIL',traceback.format_exc());raise
  finally:
   if proc.poll() is None:proc.kill();proc.wait()
if __name__=='__main__':
 results=[]
 def summary(status,error=None):(OUT/'run-summary.json').write_text(json.dumps(dict(status=status,results=results,error=error),ensure_ascii=False,indent=2)+'\n',encoding='utf8')
 summary('RUNNING')
 try:
  subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),*[str(p) for p in FIXTURES]],check=True)
  for case in sys.argv[1:] or CASES:results.append(run(case));print(results[-1]['result'],flush=True)
  summary('PASS')
 except BaseException:summary('FAIL',traceback.format_exc());raise
