"""R17 actual reveal/private look, public-top source loss and shuffle lifecycles."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
OUT=ROOT/'.tools/knowledge-lifecycle-1007/native';OUT.mkdir(parents=True,exist_ok=True);CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURES=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['StateBranchFixture.java','KnowledgeLifecycleFixture.java']]
DECK={'cards':[{'setCode':'10E','collectorNumber':'368','count':60}],'sideboard':[]}
TOP=['Air Elemental','Grizzly Bears','Mountain','Forest','Plains']
CASES={
 'public-mulch':dict(own=dict(Hand=['Mulch'],Library=TOP),other=dict(Hand=['Negate'],Library=['Serra Angel','Swamp'])),
 'private-index':dict(own=dict(Hand=['Index'],Library=TOP),other=dict(Hand=['Negate','Duress'],Library=['Serra Angel','Swamp'])),
 'private-peek':dict(own=dict(Hand=['Peek'],Library=TOP),other=dict(Hand=['Negate','Duress'],Library=['Serra Angel','Swamp'])),
 'public-source-draw':dict(own=dict(Hand=['Lantern of Insight','Naturalize','Opt'],Library=TOP),other=dict(Hand=['Negate'],Library=['Serra Angel','Swamp'])),
 'public-shuffle':dict(own=dict(Hand=['Lantern of Insight'],Library=TOP),other=dict(Hand=['Negate'],Library=['Serra Angel','Swamp'])),
}
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def run(case):
 folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir()
 meta=dict(case=case,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],driverSha256=sha(__file__),fixtures={p.name:sha(p) for p in FIXTURES})
 (folder/'driver.py').write_bytes(Path(__file__).read_bytes());assertions=[]
 for p in FIXTURES:(folder/p.name).write_bytes(p.read_bytes())
 def check(ok,why):
  assert ok,why
  if why not in assertions:assertions.append(why)
 def finish(status,why):
  result=dict(meta,status=status,assertions=assertions,result=status+' '+case+': '+why)
  for p in [folder/'result.json',OUT/(case+'-result.json')]:p.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  return result
 with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as tmp,(folder/'engine.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
  config=dict(session=case,**CASES[case],auditDir=str(audit),seed=4,firstPlayerSeat=1,startingLife=20,decks=[DECK,DECK])
  start=Path(tmp)/'start.json';start.write_text(json.dumps(config),encoding='utf8');(folder/'start.json').write_bytes(start.read_bytes())
  proc=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+tmp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.KnowledgeLifecycleFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=tmp,LOCALAPPDATA=tmp,TEMP=tmp,TMP=tmp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
  messages=queue.Queue()
  def reader():
   try:
    for line in proc.stdout:messages.put(json.loads(line))
   finally:messages.put(None)
  threading.Thread(target=reader,daemon=True).start()
  def snap(m,label):
   for seat,v in enumerate(m['views'],1):(folder/(label+('-other' if seat==2 else '')+'.json')).write_text(json.dumps(dict(v,request=v.get('request',m['request'] if m['request']['seat']==seat else None),awaitingInput=m['request']['seat']==seat),ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  def native(m):
   p=audit/(str(m['request']['requestId'])+'.json')
   for _ in range(150):
    try:
     if p.exists():return json.loads(p.read_text('utf8'))['cards']
    except (PermissionError,json.JSONDecodeError):pass
    time.sleep(.01)
   raise AssertionError('Read-only audit absent')
  refs={};stage=0;active=False;answered=set();ordered=[];reveals=[];initial=None;opponenthand=None
  actions=['Mulch'] if case=='public-mulch' else ['Index'] if case=='private-index' else ['Peek'] if case=='private-peek' else ['Lantern of Insight','Naturalize','Opt'] if case=='public-source-draw' else ['Lantern of Insight','activate']
  try:
   for step in range(500):
    m=messages.get(timeout=40);check(m is not None,'Native process remains available');trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ['fatal','ended','rejected'],m
    if m['type']!='request':continue
    r=m['request'];v=m['views'][0];kind=r['kind'];opts=r['options'];ids=[o['id'] for o in opts if not o.get('disabled')];pick='ok'
    if r['requestId'] in answered:continue
    if kind=='InputPassPriority':
     cards=native(m)
     if not refs and r['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
      for c in cards:
       if c['zone'] in ['Hand','Library'] and c['name'] not in refs:refs[c['name']]=c['id']
      initial={c['id'] for c in m['views'][0]['players'][0]['hand']};opponenthand={c['id'] for c in m['views'][1]['players'][1]['hand']}
      check(not v['players'][1]['hand'],'Opponent initial hand private');check(all(not p.get('libraryTop') for w in m['views'] for p in w['players']),'Initial library tops private');snap(m,'before')
     if refs and not v['stackItems']:
      if active and r['seat']==1 and v['phase']=='MAIN1':
       name=actions[stage]
       if name=='Mulch':
        check(set(reveals)=={1,2},'Public Mulch reveal acknowledged by both native recipients')
        check({refs['Mountain'],refs['Forest']}<={c['id'] for c in v['players'][0]['hand']},'Mulch moves exact revealed land IDs into hand')
        check({c['id'] for c in m['views'][1]['players'][0]['hand']}=={refs['Mountain'],refs['Forest']},'Publicly revealed land identities remain known in hand to other recipient')
        check({refs['Air Elemental'],refs['Grizzly Bears'],refs['Mulch']}<={c['id'] for c in v['players'][0]['graveyardCards']},'Mulch moves exact revealed nonlands and resolved spell to graveyard')
        check(all(not w['players'][0].get('libraryTop') for w in m['views']),'New unrevealed top hidden after public reveal and zone movement')
       elif name=='Index':
        check(len(ordered)==5 and set(ordered)=={refs[n] for n in TOP},'Private Index orders exact five native IDs')
        check(json.loads((audit/(str(r['requestId'])+'.json')).read_text('utf8'))['libraries'][0][:5]==ordered,'Actual library order equals submitted five-card sequence')
        check(v['players'][0].get('libraryTop',{}).get('id')==ordered[0] and not v['players'][0].get('libraryTopPublic'),'Private ordering preserves only actor known current top, without spectator authorization')
        check(not m['views'][1]['players'][0].get('libraryTop'),'Private look does not grant other seat library knowledge')
        check(any(c['id']==refs[name] for c in v['players'][0]['graveyardCards']),'Index actually resolves to graveyard')
       elif name=='Peek':
        check({c['id'] for c in v['players'][1]['hand']}==opponenthand,'Private Peek preserves exact previously viewed hand identities')
        check(refs['Air Elemental'] in {c['id'] for c in v['players'][0]['hand']},'Peek actually draws initial top card')
        check(not m['views'][1]['players'][0]['hand'],'Peek does not reveal caster hand or drawn card to other seat')
       elif name=='Lantern of Insight':
        for w in m['views']:
         for owner,n in [(0,'Air Elemental'),(1,'Serra Angel')]:check(w['players'][owner].get('libraryTop',{}).get('id')==refs[n] and w['players'][owner].get('libraryTopPublic'),'Lantern publishes exact current top to both seats with public authorization')
        check(any(c['id']==refs[name] for c in v['players'][0]['battlefield']),'Lantern actually resolves to battlefield')
       elif name=='Naturalize':
        check(any(c['id']==refs['Lantern of Insight'] for c in v['players'][0]['graveyardCards']),'Naturalize actually destroys public-top source')
        for w in m['views']:
         for p in w['players']:check(p.get('libraryTop') and not p.get('libraryTopPublic'),'Past top knowledge remains recipient-local after source leaves; spectator permission expires')
       elif name=='Opt':
        check(refs['Air Elemental'] in {c['id'] for c in v['players'][0]['hand']},'Opt actually draws formerly revealed top ID')
        for w in m['views']:check(not w['players'][0].get('libraryTop'),'New hidden library top replaces known top without leaking next card')
        check({c['id'] for c in m['views'][1]['players'][0]['hand']}=={refs['Air Elemental']},'Formerly revealed drawn card retains exact hand identity without revealing other hand cards')
       else:
        check(any(c['id']==refs['Lantern of Insight'] for c in v['players'][0]['graveyardCards']),'Activated Lantern really sacrificed as cost')
        for w in m['views']:check(not w['players'][1].get('libraryTop'),'Native shuffle clears target library knowledge for both recipients')
       snap(m,'after-'+str(stage));stage+=1;active=False
       if stage==len(actions):
        check(not v.get('awaitingInput',False) or r['kind']=='InputPassPriority','Native priority restored')
        return finish('PASS','real spell/ability, exact IDs, reveal acknowledgement, seat-local knowledge and native priority restoration')
      if not active and stage<len(actions) and r['seat']==1 and v['phase']=='MAIN1':
       name=actions[stage];pick='card:'+str(refs['Lantern of Insight'] if name=='activate' else refs[name]);check(pick in ids,'Native legal spell or ability offered');active=True;snap(m,'action-'+str(stage))
    elif kind=='InputSelectTargets':
     pick='ok' if r.get('selectedCount',0)>=r.get('minCount',1) else 'card:'+str(refs['Lantern of Insight']) if actions[stage]=='Naturalize' else 'player:'+str(v['players'][1]['targetId'])
     snap(m,'target-'+str(stage)+'-'+str(r.get('selectedCount',0)))
    elif kind=='RevealCards':
     reveals.append(r['seat']);snap(m,'reveal-'+str(stage)+'-'+str(len(reveals)))
     if case!='public-mulch':check(r['seat']==1,'Private reveal request belongs only to actor')
     check(not m['views'][2-r['seat']].get('request'),'Noncurrent recipient has no active reveal request')
     shown={c['id'] for c in r['previewCards']};expected=opponenthand if case=='private-peek' else {refs[n] for n in TOP[:4]} if case=='public-mulch' else {refs[n] for n in TOP}
     check(shown==expected,'Reveal preview contains exact authorized native card IDs');pick='confirm-cards'
    elif kind=='OrderCards':
     options=[o for o in opts if o.get('card') and not o.get('selected')]
     if options:
      option=options[-1];ordered.append(option['card']['id']);pick=option['id']
     else:pick='confirm-cards'
     check(not m['views'][1].get('request'),'Other seat cannot inspect private order request');snap(m,'order-'+str(len(ordered)))
    elif kind.startswith('InputPayMana'):snap(m,'payment-'+str(stage))
    elif kind=='dialog':
     valid=[o for o in opts if o['id'].startswith('choice:') and not o.get('disabled')];pick=valid[0]['id'];snap(m,'dialog-'+str(stage))
    elif kind in ['InputScry','Scry']:
     pick=next((i for i in ids if i.startswith('scry-top:')),'confirm-scry');snap(m,'optional-'+str(stage))
    elif kind.startswith('InputSelectCards'):
     pick='ok' if 'ok' in ids else 'confirm-cards';snap(m,'optional-'+str(stage))
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
