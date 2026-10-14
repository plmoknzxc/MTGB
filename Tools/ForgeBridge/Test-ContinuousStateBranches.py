"""R24 actual devotion, continuous layers/dependency, counters and duration."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
OUT=ROOT/'.tools/continuous-state-1007/native';OUT.mkdir(parents=True,exist_ok=True);CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURE=ROOT/'Tools/ForgeBridge/tests/CostInteractionFixture.java'
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES={
 'devotion-mono':dict(hand=['Fugitive Wizard','Unsummon'],field=['Thassa, God of the Sea','Tidebinder Mage','Fugitive Wizard']+['Island']*2,actions=[('Fugitive Wizard',None),('Unsummon','new-wizard')]),
 'devotion-dual':dict(hand=['Boggart Ram-Gang','Unsummon'],field=['Xenagos, God of Revels','Burning-Tree Emissary']+['Forest']*3+['Island'],actions=[('Boggart Ram-Gang',None),('Unsummon','Burning-Tree Emissary')]),
 'pt-counters-duration':dict(hand=['Giant Growth','Turn to Frog','Hunger of the Howlpack'],field=['Grizzly Bears']+['Forest']*3+['Island'],actions=[('Giant Growth','Grizzly Bears'),('Turn to Frog','Grizzly Bears'),('Hunger of the Howlpack','Grizzly Bears')],expire=True),
 'timestamp-frog-first':dict(hand=['Turn to Frog','Ovinize'],field=['Grizzly Bears']+['Island']*4,actions=[('Turn to Frog','Grizzly Bears'),('Ovinize','Grizzly Bears')]),
 'timestamp-ovine-first':dict(hand=['Turn to Frog','Ovinize'],field=['Grizzly Bears']+['Island']*4,actions=[('Ovinize','Grizzly Bears'),('Turn to Frog','Grizzly Bears')]),
 'dependency-urborg-first':dict(hand=['Blood Moon','Naturalize'],field=['Urborg, Tomb of Yawgmoth','Birds of Paradise','Forest','Mountain','Mountain'],actions=[('Blood Moon',None),('Naturalize','Blood Moon')]),
 'dependency-moon-first':dict(hand=['Urborg, Tomb of Yawgmoth','Naturalize'],field=['Blood Moon','Forest','Mountain'],actions=[('Urborg, Tomb of Yawgmoth',None),('Naturalize','Blood Moon')]),
 'multiple-sources':dict(hand=['Glorious Anthem','Glorious Anthem','Naturalize'],field=['Grizzly Bears']+['Plains']*6+['Forest']*2,actions=[('Glorious Anthem',None),('Glorious Anthem',None),('Naturalize','first-anthem')]),
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
  start=Path(tmp)/'start.json';start.write_text(json.dumps(dict(session=case,spec=dict(hand=spec['hand'],field=spec['field'],enemy=[],grave=[]),auditDir=str(audit),seed=4,firstPlayerSeat=1,startingLife=20,decks=[DECK,DECK])),encoding='utf8');(folder/'start.json').write_bytes(start.read_bytes())
  proc=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+tmp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.CostInteractionFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=tmp,LOCALAPPDATA=tmp,TEMP=tmp,TMP=tmp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
  messages=queue.Queue()
  def reader():
   try:
    for line in proc.stdout:messages.put(json.loads(line))
   finally:messages.put(None)
  threading.Thread(target=reader,daemon=True).start()
  def snap(m,label):
   for seat,v in enumerate(m['views'],1):(folder/(label+('-other' if seat==2 else '')+'.json')).write_text(json.dumps(dict(v,request=m['request'],awaitingInput=True),ensure_ascii=False,indent=2),encoding='utf8')
  refs={};stage=0;active=False;answered=set();start_turn=None
  def card(cards,name):return next(c for c in cards if c['name']==name or c['id']==refs.get(name))
  def outcome(v,at):
   own=v['players'][0];cards=own['battlefield'];byid={c['id']:c for c in cards}
   if case.startswith('devotion'):
    god=card(cards,'Thassa, God of the Sea' if case.endswith('mono') else 'Xenagos, God of Revels');threshold=5 if case.endswith('mono') else 7
    expected=4 if at<0 or at==1 and case.endswith('mono') else 5 if at==1 else threshold
    assert god['devotion']==expected and god['devotionRequired']==threshold,(case,at,god)
    assert god['creature']==(at==0) and 'Enchantment' in god['currentType'] and god['id']==refs['god'],(case,at,god)
    if at==1:
     returned=refs['new-wizard'] if case.endswith('mono') else refs['Burning-Tree Emissary']
     assert returned not in byid and any(c['id']==returned for c in own['hand'])
   elif case=='multiple-sources':
    bear=byid[refs['Grizzly Bears']];power=[2,3,4,3][at+1];assert (bear['power'],bear['toughness'])==(power,power)
    ants=[c for c in cards if c['name']=='Glorious Anthem'];assert len(ants)==[0,1,2,1][at+1]
    if at==2:assert ants[0]['id']==refs['second-anthem'] and any(c['id']==refs['first-anthem'] for c in own['graveyardCards'])
   elif case.startswith('timestamp'):
    bear=byid[refs['Grizzly Bears']];value=(1,1) if (case.endswith('frog-first') and at==0) or (case.endswith('ovine-first') and at==1) else (0,1)
    if at<0:value=(2,2)
    assert (bear['power'],bear['toughness'])==value and (at<0 or bear['powerBaseChanged']), (case,at,bear)
   elif case=='pt-counters-duration':
    bear=byid[refs['Grizzly Bears']];value={-1:(2,2),0:(5,5),1:(4,4),2:(5,5),3:(3,3)}[at]
    assert (bear['power'],bear['toughness'])==value,(case,at,bear)
    assert bool(bear['powerBaseChanged'])==(at in [1,2])
    assert sum(c['count'] for c in bear['counters'] if c['kind']=='P1P1')==(1 if at>=2 else 0)
    if at==3:assert bear['effectiveBasePower']==2 and 'Frog' not in bear['currentType']
   elif case.startswith('dependency'):
    forest=card(cards,'Forest');moon=any(c['name']=='Blood Moon' for c in cards);urborg=next((c for c in cards if c['id']==refs.get('Urborg, Tomb of Yawgmoth')),None)
    swamp=urborg is not None and not moon
    assert ('Swamp' in forest['currentType'])==swamp,(case,at,forest)
    if urborg is not None:
     assert ('Mountain' in urborg['currentType'])==moon,(case,at,urborg)
     assert ('Swamp' in urborg['currentType'])==swamp
    if at==1:assert any(c['id']==refs['Blood Moon'] for c in own['graveyardCards'])
   assert not any(own['manaPool']) and own['life']==20 and v['players'][1]['life']==20
  try:
   for step in range(330):
    m=messages.get(timeout=35);assert m is not None,'Native exited';trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ['fatal','ended','rejected'],m
    if m['type']!='request':continue
    r=m['request'];v=m['views'][0];own=v['players'][0];kind=r['kind'];opts=r['options'];ids=[o['id'] for o in opts];pick='ok'
    if r['requestId'] in answered:continue
    assert not v['players'][1]['hand'] and not m['views'][1]['players'][0]['hand'],'Private hand leak'
    if kind=='InputPassPriority' and r['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
     if not refs:
      start_turn=v['turn'];refs.update({c['name']:c['id'] for c in own['battlefield']+own['hand']});refs['god']=next((c['id'] for c in own['battlefield'] if c.get('devotionRequired')),0);snap(m,'before');outcome(v,-1)
     if active:
      action=spec['actions'][stage][0]
      if action=='Fugitive Wizard':refs['new-wizard']=refs[action]
      if action=='Glorious Anthem':refs['first-anthem' if stage==0 else 'second-anthem']=next(c['id'] for c in own['battlefield'] if c['name']==action and c['id']!=refs.get('first-anthem'))
      outcome(v,stage);snap(m,'after-'+str(stage));stage+=1;active=False
     if stage==len(spec['actions']):
      if spec.get('expire') and v['turn']==start_turn:pick='ok'
      else:
       if spec.get('expire'):outcome(v,3);snap(m,'expired')
       snap(m,'resolved');assert m['views'][1]['players'][0]['battlefield']==own['battlefield'];return finish('PASS','actual casts/payments/targets; '+case+' exact same-ID devotion/type/PT/counter/source/layer outcomes, both projections/privacy and main priority; setup only, no direct effect mutations')
     else:
      source,target=spec['actions'][stage];c=next(c for c in own['hand'] if c['name']==source);refs[source]=c['id'];pick='card:'+str(c['id']);assert pick in ids,(case,stage,r);active=True;snap(m,'action-'+str(stage))
    elif kind=='InputSelectTargets':
     if r.get('selectedCount',0)>=r.get('minCount',1):pick='ok'
     else:pick='card:'+str(refs[spec['actions'][stage][1]])
     snap(m,'targets-'+str(stage)+'-'+str(r.get('selectedCount',0)))
    elif kind.startswith('InputPayMana'):snap(m,'payment-'+str(stage))
    elif kind=='Scry':pick=next((s for s in ids if s.startswith('scry-top:')),'confirm-scry')
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
