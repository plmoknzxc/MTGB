"""Native draw-step exemptions, empty-library outcomes and EOT prevention expiry."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'.tools/draw-boundaries-1007/native';OUT.mkdir(parents=True,exist_ok=True);CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURES=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['LibraryCostFixture.java','DrawBoundaryFixture.java']];SHA=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES={
 'draw-first-thief':dict(hand1=[],board1=['Howling Mine','Notion Thief','Hullbreacher'],board2=[],librarySizes=[60,60]),
 'draw-first-token':dict(hand1=[],board1=['Howling Mine','Notion Thief','Hullbreacher'],board2=[],librarySizes=[60,60]),
 'empty-loss':dict(hand1=['Reach Through Mists'],board1=['Island'],board2=[],librarySizes=[0,60],winner=2),
 'empty-lab-win':dict(hand1=['Reach Through Mists'],board1=['Island','Laboratory Maniac'],board2=[],librarySizes=[0,60],winner=1),
 'empty-skip':dict(hand1=['Reach Through Mists'],board1=['Island','Obstinate Familiar'],board2=[],librarySizes=[0,60]),
 'empty-skip-decline':dict(hand1=['Reach Through Mists'],board1=['Island','Obstinate Familiar'],board2=[],librarySizes=[0,60],winner=2),
 'prevention-expiry':dict(hand1=['Ethereal Haze'],board1=['Plains'],board2=['Prodigal Sorcerer'],librarySizes=[60,60]),
}
def run(case):
 spec=CASES[case];folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir()
 meta=dict(case=case,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],driverSha256=SHA(Path(__file__)),fixtures=[dict(path=p.relative_to(ROOT).as_posix(),sha256=SHA(p)) for p in FIXTURES]);(folder/'driver.py').write_bytes(Path(__file__).read_bytes())
 for p in FIXTURES:(folder/p.name).write_bytes(p.read_bytes())
 def finish(status,message):
  r=dict(meta,status=status,result=status+' '+case+': '+message)
  for p in [folder/'result.json',OUT/(case+'-result.json')]:p.write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  return r
 with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'engine.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
  start=dict(session=case,fixture=case,hand2=[],seed=4,firstPlayerSeat=1,startingLife=20,decks=[DECK,DECK],auditDir=str(audit),**{k:v for k,v in spec.items() if k!='winner'});(folder/'start.json').write_text(json.dumps(start,indent=2)+'\n',encoding='utf8');p=Path(temp)/'start.json';p.write_text(json.dumps(start),encoding='utf8')
  proc=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.DrawBoundaryFixture',str(RT/CFG['assets']),str(p)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');messages=queue.Queue()
  def reader():
   try:
    for line in proc.stdout:messages.put(json.loads(line))
   finally:messages.put(None)
  threading.Thread(target=reader,daemon=True).start()
  def snap(m,label):
   for seat,v in enumerate(m['views'],1):(folder/(label+'-seat'+str(seat)+'.json')).write_text(json.dumps(dict(v,request=m.get('request'),awaitingInput=m['type']=='request'),ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  def libraries(q):
   p=audit/(str(q['requestId'])+'.json');until=time.monotonic()+3
   while not p.exists() and time.monotonic()<until:time.sleep(.01)
   assert p.exists(),'Missing native library audit';return [x['library'] for x in json.loads(p.read_text())['players']]
  started=False;initial=None;spell=None;cast=False;paid=False;resolved=False;orders=0;chosen=False;first=False;pings=0;pending_ping=False;sorc=None;initial_board=None
  try:
   for step in range(400):
    m=messages.get(timeout=35);assert m is not None,'Native exited';trace.write(json.dumps(m,ensure_ascii=False)+'\n');trace.flush();assert m['type'] not in ['fatal','rejected'],m
    if m['type']=='ended':
     assert 'winner' in spec and cast and paid and m['winnerSeat']==spec['winner'],('Native ended outcome',m)
     one=m['views'][0]['players'][0];assert one['libraryCount']==0 and one['life']==20 and sum(one['manaPool'])==0 and next(c for c in one['battlefield'] if c['name']=='Island')['tapped'];snap(m,'ended');(folder/'ended.json').write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
     return finish('PASS','Actual one-U Reach cast/pay resolves empty-library draw into native winnerSeat '+str(spec['winner'])+' with exact final views, life20/pool0/library0; no injected outcome.')
    if m['type']!='request':continue
    q=m['request'];v=m['views'][0];one,two=v['players'];kind=q['kind'];opts=q['options'];ids=[o['id'] for o in opts];pick='ok';snap(m,'request-'+str(step))
    # Inspect each private hand only through its own authorized view.
    hand1=m['views'][0]['players'][0]['hand'];hand2=m['views'][1]['players'][1]['hand'];assert not m['views'][1]['players'][0]['hand'] and not m['views'][0]['players'][1]['hand'],'Private hand leak'
    if kind=='InputPassPriority':
     if not started and q['seat']==1 and v['turn']==1 and v['phase']=='MAIN1' and not v['stackItems']:
      initial=libraries(q);initial_board=[[c['id'] for c in p['battlefield']] for p in v['players']];started=True;snap(m,'before')
      if spec['hand1']:spell=hand1[0]['id'];pick='card:'+str(spell);cast=True
     elif started and case.startswith('draw-first') and v['turn']==2 and v['phase']=='DRAW':
      if not first:
       assert [c['id'] for c in hand2]==initial[1][:1] and two['libraryCount']==59,'First draw not exempt';first=True;snap(m,'first-draw-exempt')
      if orders and not v['stackItems']:
       current=libraries(q);assert current[1]==initial[1][1:] and [c['id'] for c in hand2]==initial[1][:1]
       new=[c for c in one['battlefield'] if c['id'] not in initial_board[0]]
       if case=='draw-first-thief':assert current[0]==initial[0][1:] and [c['id'] for c in hand1]==initial[0][:1] and not new
       else:assert current[0]==initial[0] and not hand1 and len(new)==1 and new[0]['token'] and new[0]['name']=='Treasure Token' and new[0]['ownerSeat']==1
       assert one['life']==two['life']==20 and sum(one['manaPool'])+sum(two['manaPool'])==0;snap(m,'resolved');return finish('PASS','Own draw-step first card is exact exempt top ID, only additional Howling Mine draw offers native replacement; chosen Thief/Treasure result, exact both-library suffixes/private hands/token IDs and draw-step priority recover.')
     elif cast and not resolved and not v['stackItems']:
      assert paid and spell in [c['id'] for c in one['graveyardCards']] and not hand1 and one['life']==20 and sum(one['manaPool'])==0;resolved=True;snap(m,'spell-resolved')
      if case=='empty-skip':assert chosen and one['libraryCount']==0 and libraries(q)==initial;return finish('PASS','Actual Reach/U paid on empty library; native optional Familiar accepted skips draw, exact source graveyard and unchanged empty/full libraries, life20/pool0 and main priority restored rather than loss.')
     elif case=='prevention-expiry' and resolved and q['seat']==2 and not v['stackItems']:
      if pending_ping:
       assert one['life']==(20 if pings==1 else 19) and two['life']==20 and sum(one['manaPool'])+sum(two['manaPool'])==0
       pending_ping=False;snap(m,'prevented' if pings==1 else 'expired-damage')
       if pings==2:
        assert v['phase']=='UPKEEP' and libraries(q)==initial;return finish('PASS','Actual Haze/W resolves, same opponent Sorcerer native T/target activation deals0 in turn1 then untaps naturally and deals1 in turn2 upkeep after EOT, before the next draw; life20->19, exact source IDs/unchanged libraries/payment and priority restored.')
      if pings==0 or pings==1 and v['turn']==2:
       c=next(c for c in two['battlefield'] if c['name']=='Prodigal Sorcerer');assert not c['tapped'];assert sorc is None or c['id']==sorc;sorc=c['id'];pick='card:'+str(sorc);pings+=1;pending_ping=True
    elif 'PayMana' in kind:pick='ok';paid=True
    elif kind=='OrderAbilities' and q.get('selectionPurpose')=='replacementOrder':
     assert case.startswith('draw-first') and q['seat']==q['playerSeat']==2 and v['turn']==2 and v['phase']=='DRAW'
     assert [c['id'] for c in hand2]==initial[1][:1] and two['libraryCount']==59;first=True;snap(m,'first-draw-exempt')
     wanted='Notion Thief' if case=='draw-first-thief' else 'Hullbreacher';option=next((o for o in opts if wanted in o['label'] and not o.get('selected')),None)
     if option and not q.get('selectedCount'):pick=option['id']
     else:pick='confirm-order'
     orders+=1;snap(m,'replacement-'+str(orders))
    elif kind=='InputConfirm':assert case.startswith('empty-skip') and q.get('selectionPurpose')=='optionalReplacement';pick='cancel' if case=='empty-skip-decline' else 'ok';chosen=True;snap(m,'skip-choice')
    elif kind=='InputSelectTargets':
     assert case=='prevention-expiry' and q['seat']==2;target='player:'+str(one['targetId']);pick='ok' if q.get('selectedCount') else target;snap(m,'target-'+str(pings))
    elif kind=='InputMulligan':pick='keep'
    elif kind=='OrderAbilities':pick='confirm-order'
    elif kind in ['InputConfirmMulligan','InputAttack','InputBlock']:pick='ok'
    elif kind.startswith('InputSelect') and q['message'].startswith('Cleanup Phase'):pick='ok' if 'ok' in ids else next(o['id'] for o in opts if o['id'].startswith('card:') and not o.get('selected'))
    else:raise AssertionError(('Unhandled boundary request',q))
    assert pick in ids,(case,step,kind,pick,q);print(case,step,v['turn'],v['phase'],kind,pick,flush=True);proc.stdin.write(json.dumps(dict(type='respond',session=case,seat=q['seat'],requestId=q['requestId'],option=pick))+'\n');proc.stdin.flush()
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
 assert len(results)==len(selected) and all(r['status']=='PASS' for r in results);p.write_text(json.dumps(dict(status='PASS',selected=selected,results=results),ensure_ascii=False,indent=2)+'\n',encoding='utf8')
