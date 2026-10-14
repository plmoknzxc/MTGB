"""Actual paid Captivating Glance followed by native clash; only initial positions are scripted."""
from pathlib import Path
import hashlib,json,os,queue,subprocess,tempfile,threading,time,traceback,sys
ROOT=Path(__file__).resolve().parents[2];BASE=ROOT/'.tools/balance-fight-clash-1011/clash';RT=ROOT/'ForgeRuntime'
runtime=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def ref(p):return dict(path=p.relative_to(ROOT).as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
def cards(n):return [c for p in n['players']for z in p['zones'].values()for c in z]
def find(n,name,owner,zone=None):return next(c for c in cards(n)if c['name']==name and c['owner']==owner and(zone is None or c['zone']==zone))
def run(case):
 plan=json.loads((BASE/'cases.json').read_text('utf8'))[case];folder=BASE/'native'/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir();classes=folder/'classes';classes.mkdir()
 fixture=ROOT/'Tools/ForgeBridge/tests/ClashDeltaFixture.java';sources=[fixture,Path(__file__),BASE/'cases.json']
 for src in sources:(folder/src.name).write_bytes(src.read_bytes())
 checks=[];paid=[];requests=[];p=None;stage='start';initial=None;before=None;source=None;victim=None;reveals=[];moves=[];endpoint=None
 def check(ok,msg):assert ok,msg;checks.append(msg)
 def observe(rid):
  until=time.monotonic()+6
  while time.monotonic()<until:
   try:return json.loads((audit/(str(rid)+'.json')).read_text('utf8'))
   except(FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
  raise AssertionError('Missing read-only observation '+str(rid))
 def freeze(m,n,label):write(folder/(label+'-wire.json'),m);write(folder/(label+'-observation.json'),n)
 try:
  with(folder/'compile.log').open('w',encoding='utf8')as log:subprocess.run([os.environ.get('MTGB_JAVAC',r'C:/Program Files/Java/jdk-17/bin/javac.exe'),'-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),str(fixture)],check=True,stdout=log,stderr=subprocess.STDOUT,timeout=60)
  with tempfile.TemporaryDirectory(prefix='profile-',dir=folder,ignore_cleanup_errors=True)as temp,(folder/'stderr.log').open('w',encoding='utf8')as err,(folder/'trace.jsonl').open('w',encoding='utf8')as trace:
   deck=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
   positions=[dict(Hand=['Captivating Glance'],Library=plan['libraries'][0]),dict(Battlefield=['Grizzly Bears'],Library=plan['libraries'][1])]
   cfg=dict(session='clash-'+case,seed=4,startingLife=30,firstPlayerSeat=1,decks=[deck,deck],positions=positions,auditDir=str(audit));write(folder/'start.json',cfg)
   p=subprocess.Popen([str(RT/runtime['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.ClashDeltaFixture',str(RT/runtime['assets']),str(folder/'start.json')],cwd=RT/runtime['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,encoding='utf8')
   q=queue.Queue()
   def reader():
    try:
     for line in p.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start();seen=set();deadline=time.monotonic()+150
   for step in range(300):
    check(time.monotonic()<deadline,'Bounded native deadline');m=q.get(timeout=35)
    if isinstance(m,Exception):raise m
    check(m is not None,'Native process live');trace.write(json.dumps(m)+'\n');trace.flush();check(m['type']not in ['fatal','ended','rejected'],str(m))
    if m['type']!='request':continue
    r=m['request'];rid=r['requestId']
    if rid in seen:continue
    seen.add(rid);enabled=[o['id']for o in r['options']if not o.get('disabled')];kind=r['kind'];v=m['views'][0];n=None if'Mulligan'in kind else observe(rid);pick='ok'
    requests.append(dict(id=rid,kind=kind,seat=r['seat'],playerSeat=r['playerSeat'],stage=stage,options=r['options']))
    if kind=='InputPassPriority':
     if initial is None:initial=n;freeze(m,n,'initial')
     if stage in ['cast','paying']and any(x['source']==source and x['spell']for x in n['stack']):
      old=before['players'][0];now=n['players'][0];oldbf={c['id']:c for c in old['zones']['Battlefield']};used=[c for c in now['zones']['Battlefield']if c['tapped']and not oldbf[c['id']]['tapped']]
      check(len(used)==3 and all(c['name']=='Island'for c in used)and now['pool']==old['pool'],'Actual committed 2U payment from three original lands')
      paid.append(dict(name='Captivating Glance',id=source,mana=3,sourceIds=[c['id']for c in used],requestId=rid));freeze(m,n,'paid');stage='waiting-clash'
     if stage=='start'and r['seat']==1 and v['phase']=='MAIN1':
      before=n;source=find(n,'Captivating Glance',1,'Hand')['id'];victim=find(n,'Grizzly Bears',2,'Battlefield')['id'];freeze(m,n,'before');pick='card:'+str(source);stage='cast'
     elif stage=='waiting-clash'and n['clashed']:
      freeze(m,n,'after');current={c['id']:c for c in cards(n)};aura=current[source];bear=current[victim]
      check(not n['stack'],'Normal next empty-stack priority');check(v['phase']=='END_OF_TURN','Clash completes in real end step');check(r['seat']==r['playerSeat'],'Native next input restored to actual operator')
      check(aura['zone']=='Battlefield'and aura['attachedTo']==victim and aura['controller']==aura['owner']==1,'Paid original aura remains attached with exact identities')
      check(bear['zone']=='Battlefield'and bear['owner']==2 and bear['controller']==(1 if plan['winner']==1 else 2),'Exact Win/Otherwise child control result')
      check(aura['remembered']==[],'Remembered opponent cleared after native child')
      check(n['clashed']==[dict(player=1,won='True'if plan['winner']==1 else'False'),dict(player=2,won='True'if plan['winner']==2 else'False')],'Actual per-player Clashed Won parameters')
      expectedMoves=[s for s in [1,2]if before['players'][s-1]['zones']['Library']]
      check([x['seat']for x in moves]==expectedMoves,'Each nonempty owner gets one ordered top/bottom decision; empty owner gets none')
      check([x['seat']for x in reveals]==([1,2]if expectedMoves else[]),'Both actual recipients publicly reveal exact nonempty tops; no reveal for both-empty')
      for seat in [1,2]:
       original=[c['id']for c in before['players'][seat-1]['zones']['Library']];expected=original if not original or plan['top'][seat-1]else original[1:]+original[:1]
       check([c['id']for c in n['players'][seat-1]['zones']['Library']]==expected,'Exact complete final library order '+str(seat))
       check(n['players'][seat-1]['life']==before['players'][seat-1]['life']==30,'Life unchanged '+str(seat))
      actualLogs=[x['message']for x in n['logs']if'clashed and put'in x['message']]
      check(len(actualLogs)==len(expectedMoves),'One actual native movement log per nonempty library')
      for seat in expectedMoves:
       top=before['players'][seat-1]['zones']['Library'][0];location='top'if plan['top'][seat-1]else'bottom'
       check(f"Player {seat} clashed and put {top['name']} to the {location} of library."in actualLogs,'Exact original movement log '+str(seat))
      endpoint=dict(requestId=rid,winner=plan['winner'],controller=bear['controller'],clashed=n['clashed'],moves=moves,reveals=reveals);break
    elif'Mulligan'in kind:pick='keep'if'keep'in enabled else'ok'
    elif'PayMana'in kind:freeze(m,n,'payment-'+str(rid));pick='ok';stage='paying'
    elif kind=='InputSelectTargets':
     freeze(m,n,'target-'+str(rid));pick='ok'if r.get('selectedCount')==1 and'ok'in enabled else next(o['id']for o in r['options']if o.get('card',{}).get('id')==victim)
    elif kind=='RevealCards':
     freeze(m,n,'reveal-'+str(rid));topids=[p['zones']['Library'][0]['id']for p in before['players']if p['zones']['Library']]
     actual=[c['id']for c in r.get('previewCards',[])]
     check(actual==topids,'Public reveal contains exact original nonempty top IDs and ordered owners')
     check(r['seat']==r['playerSeat'],'Reveal recipient is real native chooser');reveals.append(dict(requestId=rid,seat=r['seat'],ids=actual));pick='confirm-cards'
    elif kind=='InputConfirm':
     freeze(m,n,'move-'+str(rid));seat=r['playerSeat'];top=before['players'][seat-1]['zones']['Library'][0]
     check(r['seat']==seat,'Each owner operates own top/bottom choice');check('top or bottom'in r.get('message','').lower(),'Exact inherited willPutCardOnTop native prompt')
     check(currentSource:=find(n,'Captivating Glance',1,'Battlefield'),'Actual source on battlefield')
     check(currentSource['remembered']==[dict(player=2)],'RememberClasher contains exact chosen opponent during decisions')
     moves.append(dict(requestId=rid,seat=seat,id=top['id'],top=plan['top'][seat-1]));pick='ok'if plan['top'][seat-1]else'cancel'
    elif kind in ['InputSelectEntitiesFromList','SelectCards']:
     freeze(m,n,'opponent-'+str(rid));entity=next((o for o in r['options']if o.get('playerSeat')==2 or o.get('label')=='Player 2'),None)
     if r.get('selectedCount')==1:pick='confirm-cards'if'confirm-cards'in enabled else'ok'
     else:check(entity is not None,'Exact native opponent candidate');pick=entity['id']
    elif kind in ['InputAttack','InputBlock']:pick='ok'
    else:raise AssertionError(('Unhandled actual native request',r))
    check(pick in enabled,('Exact enabled native response',kind,pick,enabled));response=dict(type='respond',session=cfg['session'],seat=r['seat'],requestId=rid,option=pick);trace.write(json.dumps(response)+'\n');trace.flush();p.stdin.write(json.dumps(response)+'\n');p.stdin.flush();print(case,step,kind,pick,stage,flush=True)
   else:raise AssertionError('Native step bound')
   check(endpoint is not None and len(paid)==1,'One paid cast and complete native clash endpoint')
   p.kill();p.wait()
  status='PASS_BOUNDED'
 except Exception:status='FAIL';error=traceback.format_exc();print(error,flush=True)
 finally:
  if p is not None and p.poll()is None:p.kill();p.wait()
 result=dict(status=status,case=case,folder=folder.relative_to(ROOT).as_posix(),checks=len(checks),paid=paid,endpoint=endpoint,requests=requests,runtimeHash=runtime['runtimeHash'],sources=[ref(x)for x in sources])
 if status=='FAIL':result['error']=error
 write(folder/'result.json',result);write(BASE/(case+'-latest.json'),result);print(status,case,folder,flush=True);return result
if __name__=='__main__':
 BASE.mkdir(parents=True,exist_ok=True)
 for case in sys.argv[1:]:run(case)
