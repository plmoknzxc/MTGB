"""R26 actual manifest/cloak, private projections, Ring bearer and turn-up lifecycle."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
OUT=ROOT/'.tools/facedown-lifecycle-1007/native';OUT.mkdir(parents=True,exist_ok=True);CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURE=ROOT/'Tools/ForgeBridge/tests/CostInteractionFixture.java'
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES={
 'manifest-creature':dict(hand=['Soul Summons','Unsummon'],field=['Plains']*2+['Forest']*2+['Island'],library=['Island','Grizzly Bears'],actions=[('Soul Summons',None),('@turn-up',None),('Unsummon','hidden')],face='Grizzly Bears',kind='manifest'),
 'manifest-noncreature':dict(hand=['Soul Summons','Unsummon'],field=['Plains']*2+['Island'],library=['Island','Mountain'],actions=[('Soul Summons',None),('Unsummon','hidden')],face='Mountain',kind='manifest'),
 'cloak-creature':dict(hand=['Cryptic Coat'],field=['Island']*5+['Forest']*2,library=['Island','Grizzly Bears'],actions=[('Cryptic Coat',None),('@turn-up',None),('@return-coat',None)],face='Grizzly Bears',kind='cloak'),
 'cloak-noncreature':dict(hand=['Cryptic Coat','Unsummon'],field=['Island']*4,library=['Island','Mountain'],actions=[('Cryptic Coat',None),('Unsummon','hidden')],face='Mountain',kind='cloak'),
 'ring-facedown':dict(hand=['Birthday Escape','Ixidron'],field=['Willbender']+['Island']*8,library=['Island']*3,actions=[('Birthday Escape',None),('Ixidron',None),('@turn-up',None)],face='Willbender',kind='other'),
}
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def run(case):
 spec=CASES[case];folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir()
 meta=dict(case=case,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],driverSha256=sha(__file__),fixtureSha256=sha(FIXTURE))
 (folder/'driver.py').write_bytes(Path(__file__).read_bytes());(folder/FIXTURE.name).write_bytes(FIXTURE.read_bytes())
 def finish(status,why):
  result=dict(meta,status=status,result=status+' '+case+': '+why)
  for p in [folder/'result.json',OUT/(case+'-result.json')]:p.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  return result
 with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as tmp,(folder/'engine.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
  start=Path(tmp)/'start.json';start.write_text(json.dumps(dict(session=case,spec={k:spec[k] for k in ['hand','field','library']},auditDir=str(audit),seed=4,firstPlayerSeat=1,startingLife=20,decks=[DECK,DECK])),encoding='utf8');(folder/'start.json').write_bytes(start.read_bytes())
  proc=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+tmp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.CostInteractionFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=tmp,LOCALAPPDATA=tmp,TEMP=tmp,TMP=tmp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
  messages=queue.Queue()
  def reader():
   try:
    for line in proc.stdout:messages.put(json.loads(line))
   finally:messages.put(None)
  threading.Thread(target=reader,daemon=True).start()
  def snap(m,label):
   for seat,v in enumerate(m['views'],1):(folder/(label+('-other' if seat==2 else '')+'.json')).write_text(json.dumps(dict(v,request=v.get('request',m['request']),awaitingInput=True),ensure_ascii=False,indent=2),encoding='utf8')
  refs={};stage=0;active=False;answered=set();library_before=None
  def hidden_check(m):
   for c in m['views'][1]['players'][0]['battlefield']:
    if c['faceDown']:
     assert c['name']=='Face-down card' and not c.get('printingKey') and not c.get('knownFacePrintingKey') and not c.get('knownFaceName')
     assert all(not c.get(k) for k in ['originalName','originalRules','originalType','originalAbilityText','currentAbilityText']),('Private baseline',c)
     assert spec['face'] not in json.dumps(c),('Private face leaked',c)
   assert not m['views'][0]['players'][1]['hand']
   known=m['views'][1]['players'][0]['hand']
   if known:
    assert case.endswith('noncreature') and all(c['id']==refs.get('hidden') and c['name']==spec['face'] for c in known)
    assert not any(c['id']==refs['hidden'] for c in m['views'][0]['players'][0]['battlefield'])
    assert m['request']['kind']=='RevealCards' or refs.get('publicRevealed')
    refs['publicRevealed']=True
  def outcome(m,at):
   v=m['views'][0];own=v['players'][0];board=own['battlefield'];other=m['views'][1]['players'][0]['battlefield'];ids=[o['id'] for o in m['request']['options']]
   if at<0:return
   if case=='ring-facedown':
    card=next(c for c in board if c['id']==refs['hidden']);assert own['ringBearerId']==card['id'] and card['ringBearer'] and own['ringLevel']==1
    assert card['faceDown']==(at==1)
    if at==1:assert card['knownFaceName']=='Willbender' and card['faceDownKind']=='other' and (card['power'],card['toughness'])==(2,2)
    else:assert card['name']=='Willbender' and not card.get('knownFaceName')
    if at==2:assert any(c['id']==refs['Ixidron'] for c in own['graveyardCards'])
   else:
    if at==0:
     card=next(c for c in board if c['faceDown']);refs['hidden']=card['id']
     assert card['knownFaceName']==spec['face'] and card['knownFacePrintingKey'] and card['faceDownKind']==spec['kind']
     assert (card['power'],card['toughness'])==((3,2) if spec['kind']=='cloak' else (2,2))
     assert ('WARD' in card.get('keywords',[]))==(spec['kind']=='cloak')
     assert own['libraryCount']==library_before-1
     assert ('card:'+str(card['id']) in ids)==(not case.endswith('noncreature'))
     if spec['kind']=='cloak':assert card['attachedByIds']==[refs['Cryptic Coat']]
    elif case.endswith('noncreature') or case=='manifest-creature' and at==2:
     assert not any(c['id']==refs['hidden'] for c in board)
     returned=next(c for c in own['hand'] if c['id']==refs['hidden']);assert returned['name']==spec['face'] and not returned['faceDown'] and not returned.get('knownFaceName')
     if spec['kind']=='cloak':assert not next(c for c in board if c['id']==refs['Cryptic Coat']).get('attachedToKind')
    else:
     card=next(c for c in board if c['id']==refs['hidden']);assert not card['faceDown'] and card['name']=='Grizzly Bears' and not card.get('knownFaceName')
     assert 'WARD' not in card.get('keywords',[]) and (card['power'],card['toughness'])==((3,2) if case=='cloak-creature' and at==1 else (2,2))
     if case=='cloak-creature' and at==2:assert any(c['id']==refs['Cryptic Coat'] for c in own['hand']) and not card['attachedByIds']
   assert not any(own['manaPool']) and own['life']==20 and v['players'][1]['life']==20
   public=lambda cards:[{k:x for k,x in c.items() if not k.startswith('knownFace')} for c in cards]
   assert public(board)==other,('Public projections differ',case,at)
  try:
   for step in range(280):
    m=messages.get(timeout=40);assert m is not None,'Native exited';trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ['fatal','ended','rejected'],m
    if m['type']!='request':continue
    hidden_check(m);r=m['request'];v=m['views'][0];own=v['players'][0];kind=r['kind'];opts=r['options'];ids=[o['id'] for o in opts];pick='ok'
    if r['requestId'] in answered:continue
    if kind=='InputPassPriority' and r['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
     if not refs:
      refs.update({c['name']:c['id'] for c in own['battlefield']+own['hand']});library_before=own['libraryCount']
      if case=='ring-facedown':refs['hidden']=refs['Willbender']
      snap(m,'before')
     if active:outcome(m,stage);snap(m,'after-'+str(stage));stage+=1;active=False
     if stage==len(spec['actions']):snap(m,'resolved');return finish('PASS','actual casts/turn-up/payment/targets; exact private/public face, same-ID status, zone/Ring/ward/attachment lifecycle and main priority. Setup only; no direct outcome mutations.')
     source,target=spec['actions'][stage];pick='card:'+str(refs['hidden'] if source=='@turn-up' else refs['Cryptic Coat'] if source=='@return-coat' else refs[source]);assert pick in ids,(case,stage,pick,r);active=True;snap(m,'action-'+str(stage))
    elif kind=='InputSelectTargets':
     pick='ok' if r.get('selectedCount',0)>=r.get('minCount',1) else 'card:'+str(refs[spec['actions'][stage][1]])
     snap(m,'targets-'+str(stage)+'-'+str(r.get('selectedCount',0)))
    elif kind in ['InputSelectCards','InputSelectEntities']:
     pick='ok' if r.get('selectedCount',0)>=r.get('minCount',1) else 'card:'+str(refs['hidden']);snap(m,'ring-choice-'+str(r.get('selectedCount',0)))
    elif kind=='dialog':
     action=spec['actions'][stage][0]
     if action=='@turn-up':pick=next(o['id'] for o in opts if 'face up' in o['label'].lower())
     elif action=='@return-coat':pick=next(o['id'] for o in opts if 'Return' in o['label'])
     else:raise AssertionError(('Unhandled dialog',case,stage,r))
     snap(m,'dialog-'+str(stage))
    elif kind.startswith('InputPayMana'):snap(m,'payment-'+str(stage))
    elif kind=='RevealCards':pick='confirm-cards';snap(m,'reveal-'+str(stage))
    elif kind in ['InputAttack','InputBlock','InputConfirm','InputConfirmMulligan','InputPassPriority']:pick='ok'
    else:raise AssertionError(('Unhandled',case,stage,r))
    assert pick in ids,(case,stage,kind,pick,r);answered.add(r['requestId']);print(case,step,stage,kind,pick,flush=True)
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
  subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),str(FIXTURE)],check=True)
  for case in sys.argv[1:] or CASES:results.append(run(case));print(results[-1]['result'],flush=True)
  summary('PASS')
 except BaseException:summary('FAIL',traceback.format_exc());raise
