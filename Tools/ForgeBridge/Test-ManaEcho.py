"""Real echo-cost origin and current-life boundaries; fixtures only arrange opening cards."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'.tools/mana-echo-1006/native';OUT.mkdir(parents=True,exist_ok=True);CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURE=ROOT/'Tools/ForgeBridge/tests/RestrictedManaFixture.java';SHA=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),str(FIXTURE)],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES=['echo-X','echo-life-change','echo-fixed-reject']
def run(case):
 source="Urza's Blueprints" if case=='echo-fixed-reject' else 'Volcano Hellion'
 folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True)
 meta=dict(case=case,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],driverSha256=SHA(Path(__file__)),fixtures=[dict(path=FIXTURE.relative_to(ROOT).as_posix(),sha256=SHA(FIXTURE))])
 (folder/'driver.py').write_bytes(Path(__file__).read_bytes());(folder/'RestrictedManaFixture.java').write_bytes(FIXTURE.read_bytes())
 def finish(status,message):
  r=dict(meta,status=status,result=status+' '+case+': '+message)
  for p in [folder/'result.json',OUT/(case+'-result.json')]:p.write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  return r
 with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'engine.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
  board=['Rosheen Meanderer']+(['Wastes']*6 if case=='echo-fixed-reject' else ['Mountain']*4)
  start=dict(session=case,fixture=case,hand=[source],lands=board,opponentHand=['Shock'] if case=='echo-life-change' else [],opponentBoard=['Mountain','Grizzly Bears'],seed=4,firstPlayerSeat=1,startingLife=20,decks=[DECK,DECK])
  (folder/'start.json').write_text(json.dumps(start,ensure_ascii=False,indent=2),encoding='utf8');p=Path(temp)/'start.json';p.write_text(json.dumps(start),encoding='utf8')
  proc=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.RestrictedManaFixture',str(RT/CFG['assets']),str(p)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
  messages=queue.Queue()
  def reader():
   try:
    for line in proc.stdout:messages.put(json.loads(line))
   finally:messages.put(None)
  threading.Thread(target=reader,daemon=True).start()
  def snap(m,label):
   for seat,v in enumerate(m['views'],1):(folder/(label+'-seat'+str(seat)+'.json')).write_text(json.dumps(dict(v,request=m['request'],awaitingInput=True),ensure_ascii=False,indent=2),encoding='utf8')
  cast=False;settled=False;echo_seen=False;floated=False;shock=False;negative=False;paid=0;last=None;echo_cost=None
  try:
   for step in range(400):
    m=messages.get(timeout=35);assert m is not None,'Native exited';trace.write(json.dumps(m,ensure_ascii=False)+'\n');trace.flush();assert m['type'] not in {'fatal','rejected','ended'},m
    if m['type']!='request':continue
    q=m['request'];v=m['views'][0];one,two=v['players'];kind=q['kind'];ids=[o['id'] for o in q['options']];pick='ok';snap(m,'request-'+str(step))
    if kind=='InputPassPriority':
     if not cast and q['seat']==1 and v['phase']=='MAIN1':pick='card:'+str(next(c for c in one['hand'] if c['name']==source)['id']);cast=True
     elif cast and not settled and not v['stackItems'] and any(c['name']==source for c in one['battlefield']):
      assert one['life']==(20 if case=='echo-fixed-reject' else 4);settled=True
     if settled and v['phase']=='UPKEEP' and v['stackItems'] and any(s['source']['name']==source for s in v['stackItems']):
      echo_seen=True
      if case=='echo-life-change' and q['seat']==2 and not shock:pick='card:'+str(next(c for c in m['views'][1]['players'][1]['hand'] if c['name']=='Shock')['id']);shock=True
     if echo_seen and not v['stackItems'] and q['seat']==1:
      assert v['phase']=='UPKEEP', 'Echo must finish before advancing out of its payment phase'
      assert floated and one['life']==(20 if case=='echo-fixed-reject' else 2 if shock else 4)
      assert next(c for c in one['battlefield'] if c['name']=='Rosheen Meanderer')['tapped']
      if case=='echo-fixed-reject':
       assert negative and paid==0 and source in one['graveyard'] and one['manaPool']==[0,0,0,0,0,4]
      else:
       assert echo_cost=='{'+str(2 if shock else 4)+'}' and any(c['name']==source and c['power']==6 and c['toughness']==5 for c in one['battlefield'])
       assert one['manaPool']==[0,0,0,0,0,2 if shock else 0] and 'Grizzly Bears' in two['graveyard']
       if shock:assert 'Shock' in two['graveyard']
      return finish('PASS','Actual cast/ETB or fixed artifact, next upkeep echo, restricted production, current cost/resources/life/zone and priority assertions.')
    elif 'PayMana' in kind:
     if q['seat']==2:pick='ok'
     elif echo_seen and v['phase']=='UPKEEP':
      if not floated:
       echo_cost=q['remainingManaCost'];assert echo_cost=='{'+str(6 if case=='echo-fixed-reject' else 2 if shock else 4)+'}'
       pick='card:'+str(next(c for c in one['battlefield'] if c['name']=='Rosheen Meanderer')['id']);floated=True
      elif case=='echo-fixed-reject':
       assert one['manaPool']==[0,0,0,0,0,4] and q['remainingManaCost']=='{6}'
       pick='cancel' if negative else 'mana:C';negative=True
      else:
       if last is not None:assert last!=(one['manaPool'],q.get('remainingManaCost')),('No payment progress',q);last=None
       expected=2 if shock else 4
       if paid==0:assert q['remainingManaCost']=='{'+str(expected)+'}' and one['life']==expected
       assert 'mana:C' in ids,('Restricted echo mana absent',q);pick='mana:C';paid+=1;last=(one['manaPool'],q.get('remainingManaCost'))
     else:pick='ok' if 'ok' in ids else next(i for i in ids if i.startswith('card:'))
    elif kind=='ChooseNumber':
     n=q['selectedValue'];pick='confirm-number' if n==16 else 'number-plus-five' if n+5<=16 else 'number-plus'
    elif kind=='InputSelectTargets':
     if 'ok' in ids:pick='ok'
     elif shock and echo_seen:pick='player:'+str(one['targetId'])
     else:pick='card:'+str(next(c for c in two['battlefield'] if c['name']=='Grizzly Bears')['id'])
    elif kind in {'InputAttack','InputBlock','InputConfirm','InputConfirmMulligan'}:pick='ok'
    elif kind=='InputMulligan':pick='keep'
    elif kind=='OrderAbilities':pick='confirm-order'
    else:raise AssertionError(('Unhandled echo flow',q))
    assert pick in ids,(case,step,kind,pick,q)
    print(case,step,v['phase'],kind,pick,flush=True);proc.stdin.write(json.dumps(dict(type='respond',session=case,seat=q['seat'],requestId=q['requestId'],option=pick))+'\n');proc.stdin.flush()
   raise AssertionError('Step limit')
  except Exception as e:finish('FAIL',repr(e));raise
  finally:
   if proc.poll() is None:proc.kill();proc.wait()
if __name__=='__main__':
 selected=list(sys.argv[1:] or CASES);results=[];p=OUT/'run-summary.json'
 p.write_text(json.dumps(dict(status='RUNNING',selected=selected,results=results)),encoding='utf8')
 try:
  for case in selected:r=run(case);results.append(r);print(json.dumps(r),flush=True)
 except BaseException:p.write_text(json.dumps(dict(status='FAIL',selected=selected,results=results)),encoding='utf8');raise
 assert len(results)==len(selected) and all(r['status']=='PASS' for r in results)
 p.write_text(json.dumps(dict(status='PASS',selected=selected,results=results),ensure_ascii=False,indent=2)+'\n',encoding='utf8')
