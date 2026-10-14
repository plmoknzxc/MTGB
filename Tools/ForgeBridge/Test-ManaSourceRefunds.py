"""Actual mana-source undo chains and nonundoable resources during payment."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'.tools/mana-source-refunds-1007/native';OUT.mkdir(parents=True,exist_ok=True);CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURE=ROOT/'Tools/ForgeBridge/tests/CostInteractionFixture.java';SHA=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
DECK=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
CASES={
 'filter-chain':dict(source='Murder',manaSource='Dimir Signet',hand=['Murder'],field=['Dimir Signet','Island','Swamp','Swamp'],enemy=['Grizzly Bears'],grave=[],manualManaPayment=True,color='B',target=True),
 'life-source':dict(source='Lightning Strike',manaSource='Treasonous Ogre',hand=['Lightning Strike'],field=['Treasonous Ogre','Mountain'],enemy=['Grizzly Bears'],grave=[],manualManaPayment=True,color='R',target=True,nonundoable='life'),
 'sacrifice-source':dict(source='Murder',manaSource='Blood Pet',hand=['Murder'],field=['Blood Pet','Swamp','Swamp'],enemy=['Grizzly Bears'],grave=[],manualManaPayment=True,color='B',target=True,nonundoable='sacrifice'),
 'replacement-quantity':dict(source='Giant Spider',manaSource='Forest',hand=['Giant Spider'],field=['Forest','Forest','Mana Reflection'],enemy=[],grave=[],manualManaPayment=True,color='G'),
 'restricted-source':dict(source='Thran Dynamo',manaSource="Mishra's Workshop",hand=['Thran Dynamo'],field=["Mishra's Workshop",'Wastes'],enemy=[],grave=[],manualManaPayment=True,color='C')}
for spec in CASES.values():spec.pop('manualManaPayment',None)

def run(case):
 spec=CASES[case];folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir()
 meta=dict(case=case,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],driverSha256=SHA(Path(__file__)),fixtureSha256=SHA(FIXTURE),spec=spec)
 (folder/'driver.py').write_bytes(Path(__file__).read_bytes());(folder/FIXTURE.name).write_bytes(FIXTURE.read_bytes())
 def finish(status,why):
  result=dict(meta,status=status,result=status+' '+case+': '+why)
  for p in [folder/'result.json',OUT/(case+'-result.json')]:p.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  return result
 with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'engine.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
  start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session=case,spec=spec,seed=4,startingLife=20,firstPlayerSeat=1,decks=[DECK,DECK],auditDir=str(audit))),encoding='utf8');(folder/'start.json').write_bytes(start.read_bytes())
  proc=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.CostInteractionFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');messages=queue.Queue()
  def reader():
   try:
    for line in proc.stdout:messages.put(json.loads(line))
   finally:messages.put(None)
  threading.Thread(target=reader,daemon=True).start()
  def snap(m,label):
   v=dict(m['views'][0],request=m['request'],awaitingInput=True);(folder/(label+'.json')).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf8')
  before=None;source=None;manaSource=None;activated=False;unitPaid=False;cancelled=False;retry=False;stackSeen=False;target=None;answered=set();finalExpectedLife=20
  clean=lambda cards:[{k:v for k,v in c.items() if k not in ['stackablePermanent','stackableSupport']} for c in cards]
  try:
   for step in range(130):
    m=messages.get(timeout=35);assert m is not None,'Native exited';trace.write(json.dumps(m,ensure_ascii=False)+'\n');trace.flush()
    if m['type']=='rejected':assert m['error']=='stale-request',m;continue
    assert m['type'] not in ['fatal','ended'],m
    if m['type']!='request':continue
    q=m['request'];v=m['views'][0];own=v['players'][0];other=v['players'][1];kind=q['kind'];opts=q['options'];ids=[o['id'] for o in opts];pick='ok'
    if q['requestId'] in answered:continue
    assert not other['hand'] and not m['views'][1]['players'][0]['hand'];snap(m,'step-'+str(step))
    if kind=='InputPassPriority' and any(s['source']['id']==source for s in v['stackItems']):assert retry;stackSeen=True;snap(m,'stack')
    if kind=='InputPassPriority' and q['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
     if before is None:
      before=json.loads(json.dumps(own));source=next(c['id'] for c in own['hand'] if c['name']==spec['source']);manaSource=next(c['id'] for c in own['battlefield'] if c['name']==spec['manaSource']);target=other['battlefield'][0]['id'] if spec.get('target') else None;snap(m,'before');pick='card:'+str(source)
     elif cancelled and not retry:
      assert not stackSeen and clean(own['hand'])==clean(before['hand'])
      assert own['libraryCount']==before['libraryCount'] and other['battlefield'][0]['id']==target if spec.get('target') else own['libraryCount']==before['libraryCount']
      if spec.get('nonundoable')=='life':
       assert own['life']==17 and clean(own['battlefield'])==clean(before['battlefield']) and own['manaPool']==[0,0,0,1,0,0];finalExpectedLife=17
      elif spec.get('nonundoable')=='sacrifice':
       assert own['life']==20 and [c['id'] for c in own['battlefield']]==[c['id'] for c in before['battlefield'] if c['id']!=manaSource]
       assert [c['id'] for c in own['graveyardCards']]==[manaSource] and own['manaPool']==[0,0,1,0,0,0]
      else:
       for zone in ['battlefield','graveyardCards','exileCards']:assert clean(own[zone])==clean(before[zone]),('Undo zone',zone)
       assert own['life']==20 and own['manaPool']==before['manaPool']
      snap(m,'cancelled');retry=True;pick='card:'+str(source)
     else:
      assert retry and stackSeen and own['life']==finalExpectedLife and own['libraryCount']==before['libraryCount']
      if spec.get('target'):assert target in [c['id'] for c in other['graveyardCards']] and source in [c['id'] for c in own['graveyardCards']]
      else:assert source in [c['id'] for c in own['battlefield']]
      if spec.get('nonundoable')=='sacrifice':assert manaSource in [c['id'] for c in own['graveyardCards']]
      snap(m,'resolved');return finish('PASS','Actual mana ability automatic partial payment, outer cancellation and real recast/resolve. Exact source/target/resource IDs and native undoable versus retained life/sacrifice boundaries asserted; both private recipient views and main priority recover. Intermediate native prompts may refresh; stale responses are rejected and current request remains actionable.')
    elif 'PayMana' in kind:
     nested=q['message'].startswith(spec['manaSource']+' (')
     if nested:
      assert case=='filter-chain' and ('('+str(manaSource)+')') in q['message']
      if 'mana:U' in ids:pick='mana:U'
      else:pick='card:'+str(next(c['id'] for c in own['battlefield'] if c['name']=='Island' and not c['tapped']))
      snap(m,'nested-filter-payment')
     elif not retry:
      if not activated:pick='card:'+str(manaSource);activated=True
      elif not unitPaid:
       initial={'filter-chain':'{1}{B}{B}','life-source':'{1}{R}','sacrifice-source':'{1}{B}{B}','replacement-quantity':'{3}{G}','restricted-source':'{4}'}[case]
       if q['remainingManaCost']==initial:
        # A nested ability may publish its parent while its worker still
        # completes. Observe whether native refresh supplies a newer request;
        # do not click some other card to manufacture that refresh.
        snap(m,'intermediate-parent');continue
       expected={'filter-chain':'{B}','life-source':'{1}','sacrifice-source':'{1}{B}','replacement-quantity':'{2}','restricted-source':'{1}'}[case]
       assert q['remainingManaCost']==expected,(case,q);snap(m,'generated');snap(m,'part-paid');pick='cancel';cancelled=True;unitPaid=True
      else:assert 'cancel' in ids;snap(m,'part-paid');pick='cancel';cancelled=True
     else:
      # Use retained actual pool first, then ordinary sources; avoid paying
      # a second life/sacrifice cost merely to finish the retry.
      pool=[i for i in ids if i.startswith('mana:') and not i.endswith(':snow')]
      if pool:pick=pool[0]
      else:
       legal=[c for c in own['battlefield'] if not c['tapped'] and 'card:'+str(c['id']) in ids and (c['land'] or c['id']==manaSource and not spec.get('nonundoable'))]
       pick='card:'+str(legal[0]['id']) if legal else 'ok'
    elif kind=='dialog':
     assert q['message']=='getAbilityToPlay',(case,q)
     if any(o['label'].startswith(spec['source']+' -') for o in opts):pick=next(o['id'] for o in opts if o['label'].startswith(spec['source']+' -'))
     else:pick=next(o['id'] for o in opts if 'Add ' in o['label'] or 'Add{' in o['label'])
    elif kind.startswith('InputSelectTargets'):
     pick='ok' if q.get('selectedCount',0) else 'card:'+str(target)
    elif kind=='InputMulligan':pick='keep'
    elif kind in ['InputConfirmMulligan','InputConfirm','InputPassPriority']:pick='ok'
    else:raise AssertionError(('Unknown request',q))
    assert pick in ids,(kind,pick,q);response=dict(type='respond',session=case,seat=q['seat'],requestId=q['requestId'],option=pick);trace.write(json.dumps(dict(response=response))+'\n');trace.flush();answered.add(q['requestId']);print(case,step,kind,pick,flush=True);proc.stdin.write(json.dumps(response)+'\n');proc.stdin.flush()
   raise AssertionError('Step limit')
  except BaseException as e:finish('FAIL',repr(e));raise
  finally:
   if proc.poll() is None:proc.kill();proc.wait()

if __name__=='__main__':
 selected=sys.argv[1:] or list(CASES);assert all(k in CASES for k in selected);summary=OUT/'run-summary.json';results=[];summary.write_text(json.dumps(dict(status='RUNNING',selected=selected,results=[])),encoding='utf8')
 try:
  subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),str(FIXTURE)],check=True)
  for k in selected:results.append(run(k));print(results[-1]['result'],flush=True)
 except BaseException:summary.write_text(json.dumps(dict(status='FAIL',selected=selected,results=results)),encoding='utf8');raise
 summary.write_text(json.dumps(dict(status='PASS',selected=selected,results=results),ensure_ascii=False,indent=2)+'\n',encoding='utf8')
