"""Bounded native checks for the room-wide saved-order / no-shuffle rule."""
import collections, hashlib, json, os, pathlib, queue, subprocess, tempfile, threading, time, uuid
ROOT=pathlib.Path(__file__).resolve().parents[2]
RT=ROOT/'ForgeRuntime'
OUT=pathlib.Path(os.environ.get('MTGB_NO_SHUFFLE_OUT',str(ROOT/'.tools/no-shuffle-1022/native')))
OUT.mkdir(parents=True,exist_ok=True)
JDK=pathlib.Path(os.environ.get('JAVA_HOME','C:/Program Files/Java/jdk-17'))/'bin'
CLASSES=OUT/'classes'; CLASSES.mkdir(exist_ok=True)
CP=os.pathsep.join(map(str,[CLASSES,RT/'classes',RT/'lib/*']))
FIXTURE=ROOT/'Tools/ForgeBridge/tests/NoShuffleFixture.java'
subprocess.run([str(JDK/'javac.exe'),'-encoding','UTF-8','-cp',CP,'-d',str(CLASSES),str(FIXTURE)],check=True)
CAT=[json.loads(line)for line in (RT/'catalog.jsonl').open(encoding='utf8')]
def printing(name,index=0):return [c['key']for c in CAT if c['name']==name and not c.get('ambiguous')][index]
def entry(key,count=1):s,n=key.split('/');return dict(setCode=s,collectorNumber=n,count=count)
ENTRIES=[entry(printing(n))for n in ['Evolving Wilds','Negate','Forest','Duress','Swamp','Island','Mountain','Plains']]+[entry(printing('Forest',1)),entry(printing('Plains'),51)]
def expand(entries):return [e['setCode']+'/'+e['collectorNumber']for e in entries for _ in range(e['count'])]
def zone(cards,seat,name):return [c for c in cards if c['seat']==seat and c['zone']==name]
def run(name,setting,operation='opening'):
 out=OUT/name;out.mkdir(exist_ok=True);audit=out/'audit';audit.mkdir(exist_ok=True)
 spec=dict(session='no-shuffle-'+name,seed=13,firstPlayerSeat=1,startingLife=30,decks=[dict(cards=ENTRIES,sideboard=[]),dict(cards=list(reversed(ENTRIES)),sideboard=[])],auditDir=str(audit))
 if name=='physical-search':spec['decks'][0]=dict(cards=[entry(printing('Evolving Wilds'),30),entry(printing('Plains'),30)],sideboard=[])
 if name.startswith('individual'):
  for deck in spec['decks']:
   order=expand(deck['cards']);order.insert(2,order.pop());order.insert(5,order.pop());deck['mainOrder']=order
  if os.environ.get('MTGB_ORDER_DECK'):spec['decks'][0]=json.loads(pathlib.Path(os.environ['MTGB_ORDER_DECK']).read_text(encoding='utf-8-sig'))
 if setting is not None:spec['noShuffle']=setting
 if os.environ.get('MTGB_SHUFFLE_AUDIT'):
  spec.update(shuffleAuditFile=str(out/(uuid.uuid4().hex+'.jsonl.gz')),runtimeHash=json.loads((RT/'runtime.json').read_text('utf-8-sig'))['runtimeHash'],roomId='audit-test-room',gameNumber=2)
 config=out/'start.json';config.write_text(json.dumps(spec),encoding='utf8')
 checks=[];trace=[]
 def check(ok,label):
  checks.append(dict(pass_=bool(ok),assertion=label));assert ok,label
 def snapshot(req):
  p=audit/('request-'+str(req['requestId'])+'.json')
  for _ in range(100):
   if p.exists():return json.loads(p.read_text())['cards']
   time.sleep(.01)
  raise AssertionError('Missing read-only request audit '+str(req['requestId']))
 with tempfile.TemporaryDirectory(dir=out)as tmp,(out/'stderr.log').open('w',encoding='utf8')as err:
  process=subprocess.Popen([str(RT/'jre/bin/java.exe'),'-Xmx384m','-XX:ActiveProcessorCount=2','-Djava.awt.headless=true','-cp',CP,'mtgb.bridge.NoShuffleFixture',str(RT/'assets'),str(config)],cwd=RT/'assets',env=dict(os.environ,APPDATA=tmp,LOCALAPPDATA=tmp,TEMP=tmp,TMP=tmp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,encoding='utf8')
  mail=queue.Queue()
  def read():
   for line in process.stdout:
    try:mail.put(json.loads(line))
    except json.JSONDecodeError:pass
   mail.put(None)
  threading.Thread(target=read,daemon=True).start()
  opening={};mull=0;before_mull=None;first_main=None;first_turn=None;search_stage=0;selected=None;done=False
  try:
   for step in range(220):
    wire=mail.get(timeout=45);check(wire is not None and wire['type']not in ('fatal','ended','rejected'),'Native session remains healthy')
    if wire['type']!='request':continue
    req=wire['request'];options=[o for o in req['options']if not o.get('disabled')];ids=[o['id']for o in options];cards=snapshot(req);pick=None
    trace.append(dict(request=req,views=wire['views']))
    if req['kind']=='InputConfirmMulligan':
     seat=req['seat'];hand=zone(cards,seat,'Hand');library=zone(cards,seat,'Library')
     if seat not in opening:
      deck=spec['decks'][seat-1];want=deck.get('mainOrder')or expand(deck['cards']);actual=[c['printing']for c in hand+library]
      check(collections.Counter(actual)==collections.Counter(want),'Opening preserves complete card multiset seat '+str(seat))
      check(actual==want if setting else actual!=want,'Saved order enabled / ordinary shuffle otherwise seat '+str(seat));opening[seat]=[c['id']for c in hand+library]
     if operation=='mulligan'and seat==1 and mull<2:
      if mull==0:before_mull=(hand,library);pick='cancel';mull=1
      else:
       check([c['id']for c in hand]==list(reversed([c['id']for c in before_mull[0]])),'Mulligan draws the engine-returned top cards without random permutation')
       check([c['id']for c in library]==[c['id']for c in before_mull[1]],'Mulligan keeps the entire remaining library order');mull=2
     if pick is None:pick='keep'if'keep'in ids else'ok'
    elif req['kind']=='InputPassPriority'and req['seat']==1 and wire['views'][0]['phase']=='MAIN1'and wire['views'][0]['turnSeat']==1:
     if first_main is None:first_main=cards;first_turn=wire['views'][0]['turn']
     if operation=='search':
      if search_stage<2:pick=next(o['id']for o in options if 'Evolving Wilds'in o.get('label',''));search_stage+=1
      elif selected and not wire['views'][0].get('stack'):
       before=[c['id']for c in zone(first_main,1,'Library')];after=[c['id']for c in zone(cards,1,'Library')];removed=set(before)-set(after)
       check(len(removed)==1 and set(after)==set(before)-removed,'Real search retains every unselected physical card')
       check(after==[c for c in before if c not in removed] if setting else after!=[c for c in before if c not in removed],'Search preserves order only in saved-order rooms; ordinary rooms physically shuffle')
       check(any(c['id']in removed and c['name']=='Plains'for c in zone(cards,1,'Battlefield')),'Searched Plains entered battlefield')
       check(any(c['name']=='Evolving Wilds'for c in zone(cards,1,'Graveyard')),'Evolving Wilds sacrifice cost actually paid');done=True;break
     elif operation=='draw':
      if wire['views'][0]['turn']!=first_turn:
       before=[c['id']for c in zone(first_main,1,'Library')];after=[c['id']for c in zone(cards,1,'Library')]
       check(after==before[1:],'Next turn consumes the exact first remaining library card')
       check(any(c['id']==before[0]for c in zone(cards,1,'Hand')),'Exact top physical card arrived in hand');done=True;break
     else:
      if operation=='mulligan':
       check(mull==2 and len(zone(cards,1,'Hand'))==6,'Actual London mulligan finished with six cards')
       check([c['id']for c in zone(cards,1,'Library')][:53]==[c['id']for c in before_mull[1]],'London bottom placement retains original remaining library prefix')
      check(len(opening)==2,'Both players opening order checked');done=True;break
    elif req['kind']in ('InputSelectEntitiesFromList','InputSelectCardsFromList','InputLondonMulligan'):
     if 'ok'in ids and req.get('selectedCount',0)>0:pick='ok'
     else:
      choice=next((o for o in options if operation=='search'and 'Plains'in o.get('label','')),None)or next(o for o in options if o['id'].startswith(('card:','select:')))
      pick=choice['id']
      if operation=='search':selected=pick
    if pick is None:pick='ok'
    check(pick in ids,'Response uses actual enabled native option')
    process.stdin.write(json.dumps(dict(type='respond',session=spec['session'],seat=req['seat'],requestId=req['requestId'],option=pick))+'\n');process.stdin.flush()
   check(done,'Bounded native operation completed')
   result=dict(name=name,status='PASS',operation=operation,noShuffle=setting,checks=checks,steps=step)
   (out/'result.json').write_text(json.dumps(result,indent=2),encoding='utf8');print('PASS',name,len(checks),'checks',flush=True);return result
  finally:
   (out/'trace.json').write_text(json.dumps(trace,ensure_ascii=False),encoding='utf8')
   if process.poll()is None:
    try:process.stdin.write(json.dumps(dict(type='shutdown',session=spec['session']))+'\n');process.stdin.flush();process.wait(timeout=5)
    except (OSError,subprocess.TimeoutExpired):process.kill();process.wait(timeout=5)
import argparse
parser=argparse.ArgumentParser();parser.add_argument('--cases',nargs='*');args=parser.parse_args()
scenarios=[('enabled',True,'opening'),('default',None,'opening'),('disabled',False,'opening'),('mulligan',True,'mulligan'),('draw',True,'draw'),('search',True,'search'),('individual',True,'opening'),('individual-shuffled',False,'opening'),('physical-search',False,'search')]
results=[]
for name,setting,operation in scenarios:
 if args.cases is None or name in args.cases:result=run(name,setting,operation)
 else:result=json.loads((OUT/name/'result.json').read_text())
 results.append(result)
report=dict(status='PASS',runtimeHash=json.loads((RT/'runtime.json').read_text('utf-8-sig'))['runtimeHash'],fixtureSHA256=hashlib.sha256(FIXTURE.read_bytes()).hexdigest(),cases=results)
(OUT/'result.json').write_text(json.dumps(report,indent=2),encoding='utf8');print('PASS all',len(results),'native cases')
