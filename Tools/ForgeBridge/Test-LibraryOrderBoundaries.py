"""Actual spells: exact library order, same-name IDs and multiple destinations."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'.tools/library-order-1007/native';OUT.mkdir(parents=True,exist_ok=True);CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURES=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['LibraryCostFixture.java','LibraryOrderFixture.java']];SHA=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES={'index-five':{},'index-empty':{},'index-one':{},'vision-dual':{},'telling-three':{},'feast-same-name':{},'fossil-grave':{}}

def run(case):
 spell='Index' if case.startswith('index') else {'vision-dual':'Diabolic Vision','telling-three':'Telling Time','feast-same-name':'Footbottom Feast','fossil-grave':'Fossil Find'}[case]
 top=['Mountain']*5 if case=='index-five' else [] if case=='index-empty' else ['Mountain'] if case=='index-one' else ['Lightning Bolt','Forest','Island','Swamp','Grizzly Bears']
 grave=['Grizzly Bears']*3 if case=='feast-same-name' else ['Grizzly Bears']*2+['Lightning Bolt','Forest'] if case=='fossil-grave' else []
 folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir()
 meta=dict(case=case,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],driverSha256=SHA(Path(__file__)),fixtures=[dict(path=p.relative_to(ROOT).as_posix(),sha256=SHA(p)) for p in FIXTURES]);(folder/'driver.py').write_bytes(Path(__file__).read_bytes())
 for p in FIXTURES:(folder/p.name).write_bytes(p.read_bytes())
 def finish(status,message):
  r=dict(meta,status=status,result=status+' '+case+': '+message)
  for p in [folder/'result.json',OUT/(case+'-result.json')]:p.write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  return r
 with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'engine.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
  start=dict(session=case,fixture=case,hand1=[spell],hand2=[],board1=['Island']*3+['Swamp','Mountain','Forest'],board2=[],grave1=grave,grave2=[],replaceLibrary=case in ['index-empty','index-one'],top=top,seed=4,firstPlayerSeat=1,startingLife=20,decks=[DECK,DECK],auditDir=str(audit));(folder/'start.json').write_text(json.dumps(start,indent=2)+'\n',encoding='utf8');p=Path(temp)/'start.json';p.write_text(json.dumps(start),encoding='utf8')
  proc=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.LibraryOrderFixture',str(RT/CFG['assets']),str(p)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');messages=queue.Queue()
  def reader():
   try:
    for line in proc.stdout:messages.put(json.loads(line))
   finally:messages.put(None)
  threading.Thread(target=reader,daemon=True).start()
  def snap(m,label):
   (folder/(label+'-request.json')).write_text(json.dumps(m.get('request'),ensure_ascii=False,indent=2)+'\n',encoding='utf8')
   for seat,v in enumerate(m['views'],1):(folder/(label+'-seat'+str(seat)+'.json')).write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  def libraries(q):
   p=audit/(str(q['requestId'])+'.json');until=time.monotonic()+4
   while time.monotonic()<until:
    try:return [x['library'] for x in json.loads(p.read_text())['players']]
    except (OSError,json.JSONDecodeError):time.sleep(.01)
   raise AssertionError('Missing native library observation')
  cast=False;initial=None;source=None;paid=False;before_grave=[];orders=[];active_order=None;chosen=None;target_ids=set();picker=0;reset=False;target_step=0
  try:
   for step in range(160):
    m=messages.get(timeout=35);assert m is not None,'Native exited';trace.write(json.dumps(m,ensure_ascii=False)+'\n');trace.flush();assert m['type'] not in ['fatal','rejected','ended'],m
    if m['type']!='request':continue
    q=m['request'];v=m['views'][0];one,two=v['players'];kind=q['kind'];opts=q['options'];ids=[o['id'] for o in opts];pick='ok'
    assert not m['views'][1]['players'][0]['hand'] and not m['views'][0]['players'][1]['hand'],'Private hand leak'
    if kind=='InputPassPriority':
     if not cast and q['seat']==1 and v['turn']==1 and v['phase']=='MAIN1' and not v['stackItems']:
      initial=libraries(q);source=next(c['id'] for c in one['hand'] if c['name']==spell);before_grave=[c['id'] for c in one['graveyardCards']];pick='card:'+str(source);cast=True;snap(m,'before')
     elif cast and not v['stackItems'] and source in [c['id'] for c in one['graveyardCards']]:
      current=libraries(q);hand=[c['id'] for c in one['hand']];assert paid and one['life']==two['life']==20 and sum(one['manaPool'])+sum(two['manaPool'])==0 and current[1]==initial[1] and v['phase']=='MAIN1'
      if case.startswith('index'):
       n=min(5,len(initial[0]));assert current[0]==list(reversed(initial[0][:n]))+initial[0][n:] and not hand and [c['id'] for c in one['graveyardCards']]==[source]
       assert len(orders)==(0 if n==0 else 1) and (n==0 or orders[0]['chosen']==list(reversed(initial[0][:n])))
      elif case=='vision-dual':
       rest=[i for i in initial[0][:5] if i!=chosen];assert hand==[chosen] and chosen==initial[0][1] and current[0]==list(reversed(rest))+initial[0][5:] and len(orders)==1 and orders[0]['chosen']==list(reversed(rest))
      elif case=='telling-three':
       assert hand==[initial[0][1]] and current[0]==[initial[0][2]]+initial[0][3:]+[initial[0][0]] and not orders and picker==2
      elif case=='feast-same-name':
       assert target_ids==set(before_grave) and len(orders)==1;ordered=orders[0]['chosen'];assert set(ordered)==set(before_grave) and hand==ordered[:1] and current[0]==ordered[1:]+initial[0] and [c['id'] for c in one['graveyardCards']]==[source]
      else:
       assert len(hand)==1 and hand[0] in before_grave and len(orders)==1 and set(orders[0]['chosen'])==set(before_grave)-set(hand);assert [c['id'] for c in one['graveyardCards']]==orders[0]['chosen']+[source] and current[0]==initial[0]
      assert len(current[0])==len(set(current[0]));snap(m,'resolved');(folder/'choices.json').write_text(json.dumps(dict(initialLibraries=initial,finalLibraries=current,orders=orders,chosen=chosen,targetIds=sorted(target_ids),initialGrave=before_grave,source=source,picker=picker),indent=2)+'\n',encoding='utf8');return finish('PASS','Actual '+spell+' casting/payment; exact original-ID order and destinations, private recipient input, reset/reselect where multi-order, full untouched library remainder, resources and main priority.')
    elif 'PayMana' in kind:pick='ok';paid=True
    elif kind=='OrderCards':
     cards=[o for o in opts if o.get('card')];pool=[o['card']['id'] for o in cards]
     if active_order is None:
      active_order=dict(requestId=q['requestId'],pool=pool,chosen=[]);orders.append(active_order);reset=False;snap(m,'order-'+str(len(orders)))
      if case.startswith('index'):assert set(pool)==set(initial[0][:5])
      elif case=='vision-dual':assert set(pool)==set(initial[0][:5])-{chosen}
      elif case=='feast-same-name':assert set(pool)==set(before_grave) and len({o['card']['name'] for o in cards})==1
     selected=[o['card']['id'] for o in sorted(cards,key=lambda o:o.get('order',0)) if o.get('selected')]
     if selected and not reset and len(pool)>1:
      assert 'reset-cards' in ids;pick='reset-cards';reset=True
     elif len(selected)==len(pool):
      assert 'confirm-cards' in ids;active_order['chosen']=selected;snap(m,'order-confirm-'+str(len(orders)));pick='confirm-cards';active_order=None
     else:
      assert q['minCount']==q['maxCount']==len(pool) and q['selectedCount']==len(selected)
      desired=list(reversed(pool));next_id=next(i for i in desired if i not in selected);pick=next(o['id'] for o in cards if o['card']['id']==next_id)
     assert not m['views'][1].get('request'),'Non-recipient order request leaked'
    elif case=='feast-same-name' and kind.startswith('InputSelectTargets'):
     if target_step<3:
      available=[o for o in opts if o.get('card') and o['card']['id'] in before_grave and o['card']['id'] not in target_ids];assert available;option=available[0];target_ids.add(option['card']['id']);pick=option['id'];target_step+=1
     else:pick='ok'
    elif kind=='SelectCards' or kind.startswith('InputSelectEntities'):
     cards=[o for o in opts if o.get('card')];pool={o['card']['id'] for o in cards}
     if q.get('selectedCount'):pick='confirm-cards' if 'confirm-cards' in ids else 'ok'
     else:
      picker+=1;snap(m,'picker-'+str(picker));wanted=initial[0][1] if picker==1 else initial[0][2];assert wanted in pool
      chosen=wanted if picker==1 else chosen;pick=next(o['id'] for o in cards if o['card']['id']==wanted)
    elif kind=='RevealCards':pick='confirm-cards'
    elif kind=='dialog':pick=ids[0]
    elif kind=='InputMulligan':pick='keep'
    elif kind=='OrderAbilities':pick='confirm-order'
    elif kind in ['InputConfirm','InputConfirmMulligan','InputAttack','InputBlock']:pick='ok'
    else:raise AssertionError(('Unhandled ordering request',q))
    assert pick in ids,(case,step,kind,pick,q);print(case,step,v['phase'],kind,pick,flush=True);proc.stdin.write(json.dumps(dict(type='respond',session=case,seat=q['seat'],requestId=q['requestId'],option=pick))+'\n');proc.stdin.flush()
   raise AssertionError('Step limit')
  except Exception as e:finish('FAIL',repr(e));raise
  finally:
   if proc.poll() is None:proc.kill();proc.wait()
if __name__=='__main__':
 selected=list(sys.argv[1:] or CASES);assert all(k in CASES for k in selected);results=[];p=OUT/'run-summary.json';p.write_text(json.dumps(dict(status='RUNNING',selected=selected,results=[])),encoding='utf8')
 try:
  subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),*[str(p) for p in FIXTURES]],check=True)
  for case in selected:r=run(case);results.append(r);print(json.dumps(r),flush=True)
 except BaseException:p.write_text(json.dumps(dict(status='FAIL',selected=selected,results=results)),encoding='utf8');raise
 p.write_text(json.dumps(dict(status='PASS',selected=selected,results=results),ensure_ascii=False,indent=2)+'\n',encoding='utf8')
