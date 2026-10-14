"""Actual native counter producers; declarative decisions never apply outcomes."""
import json,os,queue,subprocess,tempfile,threading,time,traceback,shutil,sys,hashlib
from pathlib import Path
R=Path(__file__).resolve().parents[2];B=R/os.environ.get('MTGB_COUNTER_FLOW_PLAN','.tools/counter-flows-1010');B.mkdir(exist_ok=True);RT=R/'ForgeRuntime';cfg=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
fileHashes={x['path']:x['sha256']for x in json.loads((RT/'files.json').read_text('utf-8-sig'))['files']}
for effect in ['CountersRemoveEffect','CountersPutOrRemoveEffect']:
 name='classes/forge/game/ability/effects/'+effect+'.class';path=RT/name
 if path.exists():assert hashlib.sha256(path.read_bytes()).hexdigest()==fileHashes.get(name),'Runtime class installed before published manifest; await packaging completion before starting native tests: '+name
C=B/'classes';C.mkdir(exist_ok=True);fix=[R/'Tools/ForgeBridge/tests/CounterFlowFixture.java',R/'Tools/ForgeBridge/tests/StateBranchFixture.java']
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(C),*map(str,fix)],check=True)
arg=sys.argv[1];case='add'if arg=='add-prefix'else'mass'if arg=='dust-only'else'remembered-child'if arg=='guile-empty'else arg;plan=json.loads((B/'cases.json').read_text('utf8'))[arg];F=B/'native'/case/str(time.time_ns());F.mkdir(parents=True);audit=F/'audit';audit.mkdir();checks=[];paid=[];seen=set();stage='start';base=None;idx=-1;last=None;targets=[];selection=[];decision=0
def wr(p,z):p.write_text(json.dumps(z,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def ck(v,label):assert v,label;checks.append(label)
def obs(rid):
 end=time.monotonic()+5
 while time.monotonic()<end:
  try:return json.loads((audit/(str(rid)+'.json')).read_text('utf8'))
  except(FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
 raise AssertionError('Read-only snapshot missing')
def nc(n,name,seat=1,zone=None):return next(c for c in n['players'][seat-1]['cards']if c['name']==name and(zone is None or c['zone']==zone))
def snap(m,n,label):wr(F/(label+'.json'),m);wr(F/(label+'-native.json'),n)
def result(n,op):
 for x in op.get('expected',[]):
  seat,name,zone,cs=x;c=nc(n,name,seat,zone);ck(c['counters']==cs,'Exact original counter result '+str(x));ck(c['id']==nc(base,name,seat)['id'],'Same original native ID '+name)
 if 'draw'in op:
  draw=op['draw'];ck(n['players'][0]['library']==base['players'][0]['library'][draw:],'Exact original library sequence after draw '+str(draw));ck(all(any(c['id']==id and c['zone']=='Hand'for c in n['players'][0]['cards'])for id in base['players'][0]['library'][:draw]),'Exact original drawn IDs in hand')
 if 'life'in op:ck(n['players'][0]['life']==op['life'],'Exact downstream life result')
 if 'otherLife'in op:ck(n['players'][1]['life']==op['otherLife'],'Exact other player downstream life result')
 for name in op.get('memoryEmpty',[]):ck(not nc(n,name)['remembered'],'Native remembered objects cleared '+name)
 for seat,cs in op.get('playerCounters',[]):ck(n['players'][seat-1]['counters']==cs,'Exact native player counter multiset '+str(seat))
 if 'tokens'in op:ck(len([c for c in n['players'][0]['cards']if c['zone']=='Battlefield'and c['token']])==op['tokens'],'Exact actual generated token count')
 ck(not last['views'][0]['stackItems'],'Actual complete native resolution/empty stack')
 ck(decision==len(op.get('decisions',[])),('All planned native decisions consumed',decision,op.get('decisions')))
deck=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
for p,name in[(Path(__file__),'driver.py'),(fix[0],'fixture.java'),(fix[1],'state-fixture.java'),(RT/'runtime.json','runtime.json'),(B/'cases.json','cases.json')]:shutil.copy2(p,F/name)
wr(F/'start.json',dict(session='counter-flow',auditDir=str(audit),seed=4,startingLife=40,firstPlayerSeat=1,decks=[deck,deck],own=plan['own'],other=plan.get('other',{})))
with tempfile.TemporaryDirectory(prefix='mtgb-counters-')as tmp,(F/'stderr.log').open('w',encoding='utf8')as log,(F/'trace.jsonl').open('w',encoding='utf8')as trace:
 p=subprocess.Popen([str(RT/cfg['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[C,RT/'classes',RT/'lib/*'])),'mtgb.bridge.CounterFlowFixture',str(RT/cfg['assets']),str(F/'start.json')],cwd=RT/cfg['assets'],env=dict(os.environ,APPDATA=tmp,LOCALAPPDATA=tmp,TEMP=tmp,TMP=tmp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');q=queue.Queue()
 def reader():
  try:
   for line in p.stdout:q.put(json.loads(line))
  except Exception as e:q.put(e)
  finally:q.put(None)
 threading.Thread(target=reader,daemon=True).start()
 try:
  for step in range(360):
   m=q.get(timeout=40);ck(isinstance(m,dict),'Native alive');trace.write(json.dumps(m)+'\n');trace.flush();ck(m['type']not in['fatal','ended','rejected'],'No native terminal or rejection')
   if m['type']!='request':continue
   r=m['request'];rid=r['requestId'];kind=r['kind'];en=[o['id']for o in r['options']if not o.get('disabled')]
   if rid in seen:continue
   seen.add(rid);v=m['views'][0];n=obs(rid)if'Mulligan'not in kind else None;pick=None;last=m
   if 'Mulligan'in kind:pick='keep'if'keep'in en else'ok'
   elif kind=='InputPassPriority':
    empty=not v['stackItems']
    if stage=='start'and r['seat']==1 and v['phase']=='MAIN1'and empty:base=n;snap(m,n,'before');stage='next'
    if stage=='resolve'and empty and (not plan['ops'][idx].get('afterPhase')or v['phase']==plan['ops'][idx]['afterPhase']):
     snap(m,n,'resolved-'+str(idx));result(n,plan['ops'][idx]);stage='next'
    if stage=='wait'and empty and v['turnSeat']==plan['ops'][idx]['turnSeat']and v['phase']==plan['ops'][idx]['phase']:
     snap(m,n,'resolved-'+str(idx));result(n,plan['ops'][idx]);stage='next'
    if stage=='next'and r['seat']==1 and v['phase']in['MAIN1','MAIN2']and empty:
     idx+=1
     if idx==len(plan['ops']):wr(F/'result.json',dict(status='PASS',case=case,checks=len(checks),assertions=checks,paid=paid,runtimeHash=cfg['runtimeHash'],folder=F.relative_to(R).as_posix()));print('PASS',case,F,len(checks),flush=True);break
     op=plan['ops'][idx];targets=[];selection=[];decision=0
     if op.get('action')in['wait','attack']:stage=op['action'];pick='ok'
     else:
      src=nc(n,op['source'],zone=op.get('zone','Hand'));pick='card:'+str(src['id']);ck(pick in en,'Actual original legal source '+op['source']);stage='resolve'
     snap(m,n,'cast-'+str(idx))
    if pick is None:pick='ok'
   elif 'PayMana'in kind:paid.append(dict(requestId=rid,cost=r.get('remainingManaCost'),source=idx));snap(m,n,'payment-'+str(len(paid)));pick='ok'
   elif kind=='InputSelectTargets':
    op=plan['ops'][idx];ts=op.get('targets',[])
    if r.get('remainingCount')==0 and'ok'in en:pick='ok'
    elif len(targets)<len(ts):
     x=ts[len(targets)];pick='player:'+str(v['players'][x[0]-1]['targetId'])if x[1]=='Player'else'card:'+str(nc(n,x[1],x[0])['id']);targets.append(pick)
    else:raise AssertionError(('Unexpected target',r))
   elif kind=='OrderAbilities':pick='confirm-order'if'confirm-order'in en else next(o['id']for o in r['options']if o['id'].startswith('order:')and not o.get('selected')and not o.get('disabled'))
   elif kind=='OrderCards':pick='confirm-cards'if'confirm-cards'in en else next(o['id']for o in r['options']if o['id'].startswith('select:')and not o.get('selected')and not o.get('disabled'))
   elif kind=='InputAttack'and stage=='attack':
    source=nc(n,plan['ops'][idx]['source'])['id'];o=next(o for o in r['options']if o['id']in en and o.get('attackerId')==source);pick='ok'if o['id'].startswith('attack-remove:')else o['id'];snap(m,n,'attack-'+str(idx))
    if pick=='ok':stage='resolve'
   elif kind in['InputAttack','InputBlock']:pick='ok'
   else:
    op=plan['ops'][idx];ds=op.get('decisions',[])
    if kind=='ChooseOptions'and any('Buyback' in o.get('label','')for o in r['options']):pick='confirm-cards'
    elif kind=='InputConfirm'and any('buyback'in o.get('label','').lower()for o in r['options']):pick='cancel'
    elif kind=='dialog'and op.get('ability')and any(op['ability']in o['label']for o in r['options']):pick=next(o['id']for o in r['options']if op['ability']in o['label'])
    elif decision<len(ds):
     d=ds[decision];snap(m,n,'choice-'+str(idx)+'-'+str(decision)+'-'+str(rid));ck(kind==d['kind'],('Exact expected contract',kind,d))
     if 'select'in d:
      ss=d['select']
      if len(selection)<len(ss):
       x=ss[len(selection)];id=nc(n,x[1],x[0])['id'];pick='card:'+str(id);selection.append(id)
      else:pick='ok'if'ok'in en else'confirm-cards';decision+=1;selection=[]
     elif 'label'in d:
      pick=next(o['id']for o in r['options']if(d['label']==o['label']if d.get('exact',True)else d['label']in o['label'])and not o.get('disabled'));decision+=1
     else:pick=d['id'];decision+=1
    elif kind=='ChooseOptions'and any('Buyback' in o.get('label','')for o in r['options']):pick='confirm-cards'
    elif kind=='InputConfirm'and any('buyback'in str(x).lower()for x in [r.get('message',''),r.get('title','')]):pick='cancel'
    else:raise AssertionError(('Unhandled native decision',case,idx,decision,r))
   ck(pick in en,('Native enabled option',case,stage,kind,pick,en));z=dict(type='respond',session='counter-flow',seat=r['seat'],requestId=rid,option=pick);trace.write(json.dumps(z)+'\n');trace.flush();p.stdin.write(json.dumps(z)+'\n');p.stdin.flush();print(step,case,idx,kind,pick,flush=True)
  else:raise AssertionError('Step limit')
 except Exception:
  wr(F/'result.json',dict(status='FAIL',case=case,stage=stage,checks=len(checks),paid=paid,error=traceback.format_exc(),runtimeHash=cfg['runtimeHash']));print('FAIL',case,F,traceback.format_exc(),flush=True);raise
 finally:
  if p.poll()is None:p.kill();p.wait()
