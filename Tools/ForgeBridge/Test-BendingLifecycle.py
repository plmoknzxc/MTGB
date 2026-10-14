"""R27 real Earthbend/Airbend actions, original identity and exile permission lifecycle."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
OUT=ROOT/'.tools/bending-lifecycle-1007/native';OUT.mkdir(parents=True,exist_ok=True);CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURE=ROOT/'Tools/ForgeBridge/tests/CostInteractionFixture.java'
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES={
 'earth-destroy':dict(hand=['Earthbending Lesson','Doom Blade','Stone Rain'],field=['Forest']*4+['Swamp']+['Mountain']*4,actions=[('Earthbending Lesson','Forest',1),('Doom Blade','Forest',1),('Stone Rain','Forest',1)]),
 'earth-exile':dict(hand=['Earthbending Lesson','Swords to Plowshares','Stone Rain'],field=['Forest']*4+['Plains']+['Mountain']*3,actions=[('Earthbending Lesson','Forest',1),('Swords to Plowshares','Forest',1),('Stone Rain','Forest',1)]),
 'air-own-cancel':dict(hand=['Airbending Lesson'],field=['Air Elemental']+['Plains']*5,library=['Mountain'],actions=[('Airbending Lesson','Air Elemental',1),('Air Elemental',None,1)]),
 'air-source-gone':dict(hand=['Aang, Swift Savior','Unsummon'],field=['Air Elemental']+['Island']*3+['Plains']*3,actions=[('Aang, Swift Savior','Air Elemental',1),('Unsummon','Aang, Swift Savior',1),('Air Elemental',None,1)]),
 'air-owner-permission':dict(hand=['Airbending Lesson'],field=['Plains']*3,enemy=['Air Elemental']+['Plains']*2,actions=[('Airbending Lesson','Air Elemental',1),('Air Elemental',None,2)]),
 'air-permission-leaves':dict(hand=['Airbending Lesson','Pull from Eternity'],field=['Air Elemental']+['Plains']*4,actions=[('Airbending Lesson','Air Elemental',1),('Pull from Eternity','Air Elemental',1)]),
 'air-spell':dict(hand=['Air Elemental','Aang, Swift Savior'],field=['Island']*6+['Plains']*4,actions=[('Air Elemental',None,1),('Aang, Swift Savior','Air Elemental',1),('Air Elemental',None,1)]),
 'air-token':dict(hand=['Raise the Alarm','Airbending Lesson'],field=['Plains']*5,actions=[('Raise the Alarm',None,1),('Airbending Lesson','@token',1)]),
 'air-decline':dict(hand=['Aang, Swift Savior'],field=['Air Elemental','Island','Plains','Plains'],actions=[('Aang, Swift Savior','@finish',1)]),
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
  start=Path(tmp)/'start.json';start.write_text(json.dumps(dict(session=case,spec={k:spec[k] for k in ['hand','field','enemy','library'] if k in spec},auditDir=str(audit),seed=4,firstPlayerSeat=1,startingLife=20,decks=[DECK,DECK])),encoding='utf8');(folder/'start.json').write_bytes(start.read_bytes())
  proc=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+tmp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.CostInteractionFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=tmp,LOCALAPPDATA=tmp,TEMP=tmp,TMP=tmp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
  messages=queue.Queue()
  def reader():
   try:
    for line in proc.stdout:messages.put(json.loads(line))
   finally:messages.put(None)
  threading.Thread(target=reader,daemon=True).start()
  def snap(m,label):
   for seat,v in enumerate(m['views'],1):(folder/(label+('-other' if seat==2 else '')+'.json')).write_text(json.dumps(dict(v,request=v.get('request',m['request']),awaitingInput=m['request']['seat']==seat),ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  refs={};stage=0;active=False;answered=set();cancelled=False;retry_pending=False;initial=None;initial_public=None
  def locate(v,zone,name,seat=None):
   values=[c for p in v['players'] if seat is None or p['seat']==seat for c in p[zone]]
   return next(c for c in values if c['id']==refs[name])
  def option(m,id):return 'card:'+str(id) in [o['id'] for o in m['request']['options'] if not o.get('disabled')]
  def outcome(m,at):
   v=m['views'][0];own=v['players'][0];other=v['players'][1];board=own['battlefield']
   assert all(not any(p['manaPool']) for p in v['players']),('Pool',case,at)
   assert own['life']==(24 if case=='earth-exile' and at>=1 else 20) and other['life']==20
   public=lambda cards:[{k:x for k,x in c.items() if not k.startswith('knownFace')} for c in cards]
   for i in range(2):assert public(v['players'][i]['battlefield'])==public(m['views'][1]['players'][i]['battlefield'])
   if case.startswith('earth-'):
    identity=refs['Forest'];card=next((c for c in board if c['id']==identity),None)
    if at==0:
     assert card and card['land'] and card['creature'] and (card['power'],card['toughness'])==(4,4)
     assert 'HASTE' in card['keywords'] and any(c['kind']=='P1P1' and c['count']==4 for c in card['counters'])
    elif at==1:
     assert card and card['land'] and not card['creature'] and card['tapped'] and not card['counters']
     assert 'HASTE' not in card['keywords'] and card['currentType']==initial_public['currentType']
     assert not any(c['id']==identity for p in v['players'] for zone in ['exileCards','graveyardCards'] for c in p[zone])
    else:
     assert card is None and locate(v,'graveyardCards','Forest')['land'] and not locate(v,'graveyardCards','Forest')['creature']
   elif case=='air-decline':
    assert locate(v,'battlefield','Air Elemental',1) and locate(v,'battlefield','Aang, Swift Savior',1)
    assert not own['exileCards'] and not v['playableExile']
   elif case=='air-token':
    tokens=[c for c in board if c.get('token') and c['creature']]
    if at==0:assert len(tokens)==2;refs['@token']=tokens[0]['id']
    else:
     assert len(tokens)==1 and tokens[0]['id']!=refs['@token']
     assert not any(c['id']==refs['@token'] for p in v['players'] for zone in ['battlefield','exileCards','graveyardCards'] for c in p[zone])
     assert not any(c['id']==refs['@token'] for c in v['playableExile'])
   else:
    id=refs['Air Elemental'];owner=2 if case=='air-owner-permission' else 1
    if case=='air-spell' and at==0:
     assert len(v['stackItems'])==1 and v['stackItems'][0]['source']['id']==id
     assert not any(c['id']==id for c in board);return
    air_at=1 if case=='air-spell' else 0
    if at==air_at or case=='air-source-gone' and at==1:
     card=locate(v,'exileCards','Air Elemental',owner);assert card['name']=='Air Elemental' and not card['faceDown']
     if case=='air-owner-permission':assert not any(c['id']==id for c in v['playableExile']) and not option(m,id)
     else:assert any(c['id']==id for c in v['playableExile']) and option(m,id)
     if case=='air-source-gone' and at==1:
      assert locate(v,'hand','Aang, Swift Savior',1) and not any(c['id']==refs['Aang, Swift Savior'] for c in board)
    elif case=='air-permission-leaves':
     assert locate(v,'graveyardCards','Air Elemental')['id']==id
     assert not any(c['id']==id for c in v['playableExile']) and not option(m,id)
    else:
     card=locate(v,'battlefield','Air Elemental',owner);assert (card['power'],card['toughness'])==(4,4) and not card['faceDown']
     assert not any(c['id']==id for p in v['players'] for c in p['exileCards'])
     assert not any(c['id']==id for c in v['playableExile'])
  try:
   for step in range(360):
    m=messages.get(timeout=40);assert m is not None,'Native exited';trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ['fatal','ended','rejected'],m
    if m['type']!='request':continue
    r=m['request'];v=m['views'][0];kind=r['kind'];opts=r['options'];ids=[o['id'] for o in opts];pick='ok'
    if r['requestId'] in answered:continue
    # Ordinary hidden hands remain private, regardless of exile permissions.
    assert not m['views'][0]['players'][1]['hand'] and not m['views'][1]['players'][0]['hand']
    if kind=='InputPassPriority':
     if initial is None and r['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
      initial=v
      for p in v['players']:
       for c in p['battlefield']+p['hand']:refs.setdefault(c['name'],c['id'])
      if case.startswith('earth-'):initial_public=locate(v,'battlefield','Forest',1)
      snap(m,'before')
     ready=initial is not None and v['phase']=='MAIN1' and (not v['stackItems'] or case=='air-spell' and stage==0 and any(x['source']['id']==refs['Air Elemental'] for x in v['stackItems']))
     if ready and active and r['seat']==spec['actions'][stage][2]:
      outcome(m,stage);snap(m,'after-'+str(stage));stage+=1;active=False
      if stage==len(spec['actions']):snap(m,'resolved');return finish('PASS','actual spells/payments/targets; exact land animation/return reset, exile ownership/alternate cast/permission removal or token/stack lifecycle; source/hand identities and main priority. Setup only, no outcome mutation.')
     if ready and not active and stage<len(spec['actions']) and r['seat']==spec['actions'][stage][2] and (case!='air-owner-permission' or stage!=1 or v['turnSeat']==2):
      if retry_pending:
       card=locate(v,'exileCards','Air Elemental',1);assert option(m,card['id']) and any(c['id']==card['id'] for c in v['playableExile'])
       assert sum(c['land'] and c['tapped'] for c in v['players'][0]['battlefield'])==3 and not any(v['players'][0]['manaPool'])
       snap(m,'cancelled');retry_pending=False
      source,target,seat=spec['actions'][stage];pick='card:'+str(refs[source]);assert pick in ids,(case,stage,pick,r);active=True;snap(m,'action-'+str(stage))
    elif kind=='InputSelectTargets':
     target=spec['actions'][stage][1];assert target is not None,(case,stage,r)
     pick='ok' if r.get('selectedCount',0)>=r.get('minCount',1) else 'card:'+str(refs[target]);snap(m,'targets-'+str(stage)+'-'+str(r.get('selectedCount',0)))
    elif kind=='dialog':
     valid=[o for o in opts if o['id'].startswith('choice:') and not o.get('disabled')]
     target=spec['actions'][stage][1]
     if r['message'].startswith('one: Select up to another target creature or spell'):
      finish_choice=next((o['id'] for o in valid if o['label']=='[FINISH TARGETING]'),None)
      pick=finish_choice if target=='@finish' else next((o['id'] for o in valid if o.get('card') and o['card']['id']==refs[target]),finish_choice)
     else:pick=next((o['id'] for o in valid if 'Airbend' in o['label']),valid[0]['id'] if len(valid)==1 else None)
     assert pick is not None,('Unhandled dialog',case,stage,r);snap(m,'dialog-'+str(stage))
    elif kind.startswith('InputPayMana'):
     snap(m,'payment-'+str(stage))
     if case=='air-own-cancel' and stage==1 and not cancelled:pick='cancel';cancelled=True;active=False;retry_pending=True
    elif kind=='RevealCards':pick='confirm-cards';snap(m,'reveal-'+str(stage))
    elif kind.startswith('InputSelectCards') and 'discard' in r['message'].lower():
     pick='ok' if r.get('selectedCount',0)>=r.get('minCount',1) else next(i for i in ids if i.startswith('card:'));snap(m,'cleanup-discard-'+str(r.get('selectedCount',0)))
    elif kind in ['InputAttack','InputBlock','InputConfirm','InputConfirmMulligan']:pick='ok'
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
