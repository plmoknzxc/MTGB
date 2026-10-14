"""R22 actual token/non-token artifact copy differences and token resource regression."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
OUT=ROOT/'.tools/noncreature-copies-1007/native';OUT.mkdir(parents=True,exist_ok=True);CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURE=ROOT/'Tools/ForgeBridge/tests/CostInteractionFixture.java'
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES={
 'echo-copy':dict(source='Echo Storm',field=['Sol Ring']+['Island']*5,lands=5,token=True),
 'steel-copy':dict(source='Sculpting Steel',field=['Sol Ring']+['Island']*3,lands=3,token=False),
 'steel-reverse-copy':dict(source='Sculpting Steel',field=['Chalice of Life']+['Island']*3,lands=3,token=False,reverse=True),
 'manufactor-regression':dict(source='Gilded Goose',field=['Academy Manufactor','Doubling Season','Forest'],lands=1),
}
def run(case):
 spec=CASES[case];folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir()
 sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
 meta=dict(case=case,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],driverSha256=sha(__file__),fixtureSha256=sha(FIXTURE))
 (folder/'driver.py').write_bytes(Path(__file__).read_bytes());(folder/FIXTURE.name).write_bytes(FIXTURE.read_bytes())
 def finish(status,why):
  r=dict(meta,status=status,result=status+' '+case+': '+why)
  for p in [folder/'result.json',OUT/(case+'-result.json')]:p.write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  return r
 with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as tmp,(folder/'engine.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
  start=Path(tmp)/'start.json';start.write_text(json.dumps(dict(session=case,spec=dict(hand=[spec['source']],field=spec['field'],enemy=[],grave=[],initialLife=30 if spec.get('reverse') else 20),seed=4,firstPlayerSeat=1,startingLife=20,decks=[DECK,DECK],auditDir=str(audit))),encoding='utf8');(folder/'start.json').write_bytes(start.read_bytes())
  proc=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+tmp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.CostInteractionFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=tmp,LOCALAPPDATA=tmp,TEMP=tmp,TMP=tmp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
  messages=queue.Queue()
  def reader():
   try:
    for line in proc.stdout:messages.put(json.loads(line))
   finally:messages.put(None)
  threading.Thread(target=reader,daemon=True).start()
  def snap(m,label):
   for seat,v in enumerate(m['views'],1):
    v=dict(v,request=m['request'],awaitingInput=True);(folder/(label+('-other' if seat==2 else '')+'.json')).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf8')
  source=target=None;initial=set();paid=set();copy_id=None;activated=False;answered=set();transform_started=False
  try:
   for step in range(160):
    m=messages.get(timeout=35);assert m is not None,'Native exited';trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ['fatal','ended','rejected'],m
    if m['type']!='request':continue
    r=m['request'];v=m['views'][0];own=v['players'][0];other=v['players'][1];kind=r['kind'];opts=r['options'];ids=[o['id'] for o in opts];pick='ok'
    if r['requestId'] in answered:continue
    assert not other['hand'] and not m['views'][1]['players'][0]['hand'],'Private hand leak'
    snap(m,'step-'+str(step))
    if kind=='InputPassPriority' and source is None and r['seat']==1 and v['phase']=='MAIN1':
     if spec.get('reverse') and not any(c['name']=='Chalice of Death' for c in own['battlefield']):
      if not transform_started:target=next(c['id'] for c in own['battlefield'] if c['name']=='Chalice of Life');pick='card:'+str(target);transform_started=True;snap(m,'before-transform')
      else:pick='ok'
     elif not v['stackItems']:
      source=next(c['id'] for c in own['hand'] if c['name']==spec['source']);initial={c['id'] for c in own['battlefield']};target=next((c['id'] for c in own['battlefield'] if c['name']==('Chalice of Death' if spec.get('reverse') else 'Sol Ring')),None);snap(m,'before');pick='card:'+str(source)
      if spec.get('reverse'):assert own['life']==31 and next(c for c in own['battlefield'] if c['id']==target)['backFace']
    elif kind=='InputPassPriority' and source is not None and not v['stackItems'] and r['seat']==1:
     assert len(paid)==spec['lands'],paid
     if case=='manufactor-regression':
      tokens=[c for c in own['battlefield'] if c.get('token')];assert len(tokens)==6 and len({c['id'] for c in tokens})==6
      assert sorted(c['name'] for c in tokens)==['Clue Token']*2+['Food Token']*2+['Treasure Token']*2
      assert all(not c['creature'] and not c['copy'] and c['ownerSeat']==1 and c.get('tokenArtKey') for c in tokens)
      assert not any(c.get('token') for c in other['battlefield']);snap(m,'resolved')
      return finish('PASS','existing Goose/Manufactor/doubling actual flow, six exact noncreature IDs/art keys/owners/current projection and priority; regression only')
     if copy_id is None:
      copies=[c for c in own['battlefield'] if c['id']!=target and c['name']==('Chalice of Death' if spec.get('reverse') else 'Sol Ring')];assert len(copies)==1,copies
      copy=copies[0];copy_id=copy['id'];assert copy['copy'] and bool(copy.get('token'))==spec['token'] and not copy['creature']
      assert copy['ownerSeat']==1 and copy['stackableStateKnown'] and not copy['pendingDepartures']
      original=next(c for c in own['battlefield'] if c['id']==target)
      assert copy['currentArtPrintingKey']==original['printingKey'] and copy['currentArtBackFace']==bool(spec.get('reverse'))
      assert spec['token'] or copy['printingKey']!=copy['currentArtPrintingKey'],'Physical source identity overwritten'
      assert copy_id!=source if spec['token'] else copy_id==source
      assert next(c for c in own['battlefield'] if c['id']==target)['tapped']==bool(spec.get('reverse'))
      snap(m,'resolved');pick='card:'+str(copy_id);activated=True
     else:
      assert activated and own['manaPool']==([0]*6 if spec.get('reverse') else [0,0,0,0,0,2]),own['manaPool']
      if spec.get('reverse'):assert own['life']==31 and other['life']==15
      assert next(c for c in own['battlefield'] if c['id']==copy_id)['tapped']
      assert next(c for c in own['battlefield'] if c['id']==target)['tapped']==bool(spec.get('reverse'))
      for pv in m['views']:
       c=next(c for c in pv['players'][0]['battlefield'] if c['id']==copy_id);assert c['copy'] and bool(c.get('token'))==spec['token'] and c['tapped']
      snap(m,'activated');return finish('PASS','actual cast/payment, artifact copy exact identity/token classification and target unchanged, copied ability '+('after actual transform has reverse art and opponent life15' if spec.get('reverse') else 'adds C2/taps only copy')+', both projections/main priority recover')
    elif kind.startswith('InputPayMana'):
     legal=[c for c in own['battlefield'] if c['land'] and not c['tapped'] and 'card:'+str(c['id']) in ids]
     if len(paid)<spec['lands']:assert legal,r;pick='card:'+str(legal[0]['id']);paid.add(legal[0]['id'])
     else:pick='ok'
    elif kind=='InputSelectTargets':
     pick='ok' if r.get('selectedCount') else 'player:'+str(other['targetId']) if spec.get('reverse') and activated else 'card:'+str(target);snap(m,'target-'+str(r.get('selectedCount',0)))
    elif kind in ['OrderCards','OrderAbilities']:
     candidates=[o for o in opts if o['id'].startswith(('select:','order:')) and not o.get('selected')]
     if candidates:pick=next((o['id'] for o in candidates if 'Academy Manufactor' in o['label']),candidates[0]['id'])
     else:pick='confirm-order' if kind=='OrderAbilities' else 'confirm-cards'
    elif kind in ['SelectCards','dialog']:
     candidates=[o for o in opts if o.get('card',{}).get('id')==target or o['label'].startswith('Sol Ring')]
     assert candidates,(case,r);pick=candidates[0]['id'];snap(m,'copy-choice')
    elif kind in ['InputConfirmMulligan','InputConfirm','InputPassPriority']:pick='ok'
    else:raise AssertionError(('Unhandled',r))
    assert pick in ids,(case,kind,pick,r)
    answered.add(r['requestId']);print(case,step,kind,pick,flush=True);proc.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');proc.stdin.flush()
   raise AssertionError('No completion')
  except BaseException:finish('FAIL',traceback.format_exc());raise
  finally:
   if proc.poll() is None:proc.kill();proc.wait()
if __name__=='__main__':
 def summary(status,results,error=None):
  (OUT/'run-summary.json').write_text(json.dumps(dict(status=status,results=results,error=error),ensure_ascii=False,indent=2)+'\n',encoding='utf8')
 results=[];summary('RUNNING',results)
 try:
  subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),str(FIXTURE)],check=True)
  for case in sys.argv[1:] or CASES:
   results.append(run(case));print(results[-1]['result'],flush=True)
  summary('PASS',results)
 except BaseException:
  summary('FAIL',results,traceback.format_exc());raise
