"""One actual combined replacement flow, preserving failures and original native choices."""
from pathlib import Path
from collections import Counter
import argparse,hashlib,json,os,queue,re,subprocess,tempfile,threading,time,traceback,sys
ROOT=Path(__file__).resolve().parents[2];BASE=ROOT/'.tools/primitive-api-tail-1011/replacements';RT=ROOT/'ForgeRuntime'
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def ref(p):return dict(path=p.relative_to(ROOT).as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
def allcards(n):return list({c['id']:c for p in n['players']for z in p['zones'].values()for c in z}.values())
def find(n,name,owner=1,zone=None,untapped=False):return next(c for c in allcards(n)if c['name']==name and c['owner']==owner and(zone is None or c['zone']==zone)and(not untapped or not c['tapped']))
def pool(n,seat=1):return n['players'][seat-1]['mana']
def multiset(ms):return Counter(json.dumps(m,sort_keys=True)for m in ms)
def parse_args(argv=None):
 parser=argparse.ArgumentParser(description=__doc__)
 parser.add_argument('case',nargs='?',default='combined',choices=['combined'])
 return parser.parse_args(argv)
def canonical_effect(name):return re.sub(r' \([0-9]+\)','',name)
def validate_float(before,after,op,source,check):
 actor=op.get('seat',1);old=multiset(pool(before,actor));current=multiset(pool(after,actor))
 check(not(old-current),'Every original native pool unit retained')
 added=current-old;produced=[json.loads(key)for key,count in added.items()for _ in range(count)]
 c=next(c for c in allcards(after)if c['id']==source and c['zone']=='Battlefield')
 # A different ability on the same card retains its previous lastProduced history.
 # The real new pool units, counted with multiplicity, identify this activation.
 check(c['tapped'],'Exact original production source tapped')
 check(len(produced)==op['count'],'Exact newly produced native quantity')
 check(all(x['color']==op['color']for x in produced),'Exact newly produced native type')
 check(all(x['source']==source and x['originalPlayer']==actor and x['abilityIdentity']>0 for x in produced),'Actual triggering source/player/ability metadata')
 check(len({x['abilityIdentity']for x in produced})==1,'One actual source mana ability produced these new units')
 check(all(x['snow']==op.get('snow',False)and x['restricted']==op.get('restricted',False)for x in produced),'Actual snow/restriction metadata')
 check(not(multiset(produced)-multiset(c['lastProduced'])),'Every added unit belongs to actual source ability production')
 check(current==old+multiset(produced),'Original pool multiset plus exact new production')
 check(pool(after,3-actor)==pool(before,3-actor),'Other actual pool unchanged')
 if op.get('restricted'):check(all(x['restriction']=='Spell.Creature'for x in produced),'Native Ancient Ziggurat restriction retained after replacement')
 if 'life'in op:check(after['players'][actor-1]['life']==op['life'],'Original source actual life outcome')
 return produced
def color_response(r,n,op,view,check):
 enabled=[o for o in r['options']if not o.get('disabled')]
 candidates=[o for o in enabled if o['id'].startswith(('choice:','select:'))]
 check(len(candidates)==5 and {o['label'].lower()for o in candidates}=={'white','blue','black','red','green'},'Exactly actual WUBRG candidates, no colorless')
 check(r['seat']==r['playerSeat'],'Actual original color chooser authority')
 hall=r['kind']=='ChooseOptions'
 if hall:
  check(r['message']=='Choose a color'and r['minCount']==r['maxCount']==1,'Actual Hall required single-color contract')
  check(n['phase']=='UPKEEP'and r['seat']==view['turnSeat'],'Actual Hall active-turn chooser')
  check(any(sa['trigger']and 'Hall of Gemstone'in sa['name']for sa in n['stack']),'Actual Hall native upkeep trigger source')
  want=op.get('hallChoice','Blue')
  if r['selectedCount']==1:
   check('confirm-cards'in[o['id']for o in enabled],'Actual Hall explicit confirmation')
   selected=[o for o in candidates if o.get('selected')]
   check(len(selected)==1 and selected[0]['label'].lower()==want.lower(),'Actual Hall selected planned original color')
   return 'confirm-cards'
  check(r['selectedCount']==0,'Actual Hall has no prior selected color')
 else:
  check(r['kind']=='ChooseColor'or r['kind']=='dialog'and r['message'].endswith(': Choose a color'),'Actual native single-color selector')
  check(r['seat']==op.get('seat',1)and r.get('sourceCardId',0)>0,'Actual Any production chooser/source')
  want=op.get('choice','Green')
 return next(o['id']for o in candidates if o['label'].lower()==want.lower())
def replacement_response(r,op,check):
 check(r.get('selectionPurpose')=='replacementOrder'and r['seat']==r['playerSeat'],'Actual replacement-order authority/context')
 enabled=[o for o in r['options']if not o.get('disabled')];remaining=[o for o in enabled if o['id'].startswith('order:')]
 proposed=r.get('orderedAbilities',[]);selected=r.get('selectedCount',0);maximum=r['maxCount']
 check(len(proposed)==selected and len({a['id']for a in proposed})==selected,'Exact original selected replacement identities')
 check(all(o['ability']['id']==int(o['id'].split(':')[1])for o in remaining),'Exact original offered replacement identities')
 # After one replacement applies, native cached ordering excludes that consumed effect.
 # Compare the remaining actual candidate universe, never reset against an absent one.
 universe=proposed+[o['ability']for o in remaining];want=op.get('order',[])
 def rank(a):
  text=(a.get('source')or{}).get('name','')+' '+a.get('description','')
  return next((i for i,name in enumerate(want)if name in text),len(want))
 expected=sorted(universe,key=rank)
 if [a['id']for a in proposed]!=[a['id']for a in expected[:selected]]:
  check('reset-order'in[o['id']for o in enabled],'Native cached different order can reset')
  return 'reset-order'
 if selected==maximum:
  check('confirm-order'in[o['id']for o in enabled],'Native complete replacement order can confirm')
  return 'confirm-order'
 check(selected<maximum and remaining,'Native order has remaining actual candidates')
 return next(o['id']for o in remaining if o['ability']['id']==expected[selected]['id'])
def completion_status(endpoints,operations,failures):
 complete=len(endpoints)==len(operations)and[ep['operation']for ep in endpoints]==list(range(len(operations)))
 if not complete:return 'FAIL'
 if failures or any(ep.get('status')=='RAW_FAILURE_INDEPENDENT_AUDIT_REQUIRED'for ep in endpoints):return 'COMPLETE_WITH_RAW_FAILURES'
 return 'PASS_BOUNDED'
def run(case='combined'):
 if case!='combined':raise ValueError('Unsupported replacement case: '+case)
 runtime=json.loads((RT/'runtime.json').read_text('utf-8-sig'));plan=json.loads((BASE/'plan.json').read_text('utf8'))
 folder=BASE/'native'/'combined'/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir();classes=folder/'classes';classes.mkdir()
 fixtures=[ROOT/'Tools/ForgeBridge/tests/RoyalStateFixture.java',ROOT/'Tools/ForgeBridge/tests/ManaReplacementTailFixture.java'];sources=fixtures+[Path(__file__),BASE/'plan.json',RT/'runtime.json']
 for s in sources:(folder/s.name).write_bytes(s.read_bytes())
 checks=[];paid=[];endpoints=[];requests=[];failures=[];p=None;index=0;stage='next';source=0;before=None;paidSeen=False;selectedTargets=[];discardId=0
 def check(ok,label):
  if not ok:raise AssertionError(label)
  checks.append(label)
 def observe(rid):
  end=time.monotonic()+8
  while time.monotonic()<end:
   try:return json.loads((audit/(str(rid)+'.json')).read_text('utf8'))
   except(FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
  raise AssertionError('Missing read-only observation '+str(rid))
 def freeze(m,n,label):write(folder/(label+'-wire.json'),m);write(folder/(label+'-observation.json'),n)
 def effect_names(n):return [c['name']for pl in n['players']for c in pl['zones']['Command']]
 def endpoint(m,n,op):
  nonlocal index,stage
  freeze(m,n,'after-'+str(index));actor=op.get('seat',1);check(not n['stack'],'Actual empty stack endpoint '+str(index));check(m['request']['seat']==m['request']['playerSeat'],'Actual original priority operator restored '+str(index))
  if op['action']=='float':
   validate_float(before,n,op,source,lambda ok,label:check(ok,label+' '+str(index)))
  else:
   check(paidSeen,'Actual committed original cast/activation '+str(index))
   if 'removed'in op:check(any(c['id'] in [t[1]for t in selectedTargets]and c['name']==op['removed'] for pl in n['players']for c in pl['zones']['Graveyard']),'Real original replacement source destroyed '+str(index))
   if 'enters'in op:check(next(c for c in allcards(n)if c['id']==source)['zone']=='Battlefield','Real original spell enters '+str(index))
   if 'effect'in op:check(canonical_effect(op['effect'])in[canonical_effect(name)for name in effect_names(n)],'Actual generated native replacement effect exists '+str(index))
   if 'discard'in op:check(any(c['id']==discardId for c in n['players'][0]['zones']['Graveyard']),'Exact original discard cost remains paid '+str(index));check(next(c for c in allcards(n)if c['id']==source)['tapped'],'Actual original Harvest tap cost remains paid')
  if op.get('expired'):check(not any(canonical_effect(name)in[canonical_effect(x)for x in effect_names(n)]for name in op['expired']),'Original temporary replacement effects naturally expired '+str(index))
  endpoints.append(dict(operation=index,action=op['action'],source=source,name=op['source'],requestId=m['request']['requestId'],count=op.get('count'),color=op.get('color'),turn=n['turn'],phase=n['phase']));write(folder/'endpoints.json',endpoints);index+=1;stage='next'
 def pause(error,m,n):
  record=dict(index=len(failures)+1,operation=index,stage=stage,error=error,requestId=m['request']['requestId'],completedEndpoints=len(endpoints),paidOperations=len(paid));failures.append(record);write(folder/('raw-failure-'+str(len(failures))+'.json'),record);write(folder/'failures.json',failures)
  freeze(m,n,'pause-'+str(len(failures)));write(folder/'pause.json',record);print('PAUSED',folder,'operation',index,error,flush=True)
  # Keep this same real JVM/input open for a bounded driver-only correction.
  # A control response is always an original enabled option, never a result edit.
  end=time.monotonic()+300
  while p.poll()is None and time.monotonic()<end:
   control=folder/'control.json'
   if control.exists():
    value=json.loads(control.read_text('utf8'));control.replace(folder/('control-used-'+str(len(failures))+'.json'));return value
   time.sleep(.2)
  raise AssertionError('Bounded same-JVM driver pause expired')
 try:
  compiler=Path(os.environ.get('MTGB_JAVAC',r'C:/Program Files/Java/jdk-17/bin/javac.exe'))
  with(folder/'compile.log').open('w',encoding='utf8')as log:subprocess.run([str(compiler),'-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*map(str,fixtures)],check=True,stdout=log,stderr=subprocess.STDOUT,timeout=60)
  with tempfile.TemporaryDirectory(prefix='profile-',dir=folder,ignore_cleanup_errors=True)as temp,(folder/'stderr.log').open('w',encoding='utf8')as err,(folder/'trace.jsonl').open('w',encoding='utf8')as trace:
   deck=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[]);cfg=dict(session='mana-replacement-combined',seed=4,startingLife=40,firstPlayerSeat=1,decks=[deck,deck],positions=plan['positions'],auditDir=str(audit));write(folder/'start.json',cfg)
   p=subprocess.Popen([str(RT/runtime['java']),'-Xmx512m','-XX:ActiveProcessorCount=3','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.ManaReplacementTailFixture',str(RT/runtime['assets']),str(folder/'start.json')],cwd=RT/runtime['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,encoding='utf8')
   q=queue.Queue()
   def reader():
    try:
     for line in p.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start();seen=set();deadline=time.monotonic()+1200
   for step in range(1400):
    check(time.monotonic()<deadline,'Bounded combined native deadline');m=q.get(timeout=45)
    if isinstance(m,Exception):raise m
    check(m is not None,'Native process still live');trace.write(json.dumps(m)+'\n');trace.flush();check(m['type']not in ['fatal','ended','rejected'],str(m))
    if m['type']!='request':continue
    r=m['request'];rid=r['requestId']
    if rid in seen:continue
    seen.add(rid);n=None if'Mulligan'in r['kind']else observe(rid)
    if n:freeze(m,n,'request-'+str(rid))
    requests.append(dict(id=rid,kind=r['kind'],seat=r['seat'],playerSeat=r.get('playerSeat'),operation=index,stage=stage));write(folder/'requests.json',requests)
    done=False;forced=None
    while True:
     # This process completes the exact plan frozen before starting its native game.
     op=plan['operations'][index];opts=r['options'];enabled=[o['id']for o in opts if not o.get('disabled')];kind=r['kind'];pick='ok';view=m['views'][0]
     try:
      if forced is not None:
       pick=forced;check(pick in enabled,('Explicit correction uses original enabled response',kind,pick,enabled));break
      if stage=='resolve'and not paidSeen and op['action']!='float':
       committed=next((x for x in n['stack']if x['source']==source and not x['trigger']),None)
       if committed:
        check(len(committed['payingMana'])==op['mana'],'Actual original committed mana units '+str(index));check(committed['actor']==op.get('seat',1),'Actual original paying actor '+str(index))
        paid.append(dict(operation=index,source=source,name=op['source'],mana=op['mana'],objects=committed['payingMana'],requestId=rid));write(folder/'paid.json',paid);freeze(m,n,'paid-'+str(index));paidSeen=True
      if kind=='InputPassPriority':
       if stage=='resolve'and not n['stack']:
        endpoint(m,n,op)
        if index==len(plan['operations']):done=True;break
        op=plan['operations'][index]
       if stage=='next'and not n['stack']and view['turnSeat']==op.get('turnSeat',1)and n['turn']>=op.get('minTurn',1)and r['seat']==op.get('seat',1)and view['phase']==op.get('phase','MAIN1'):
        actor=op.get('seat',1);before=n;source=find(n,op['source'],actor,'Hand'if op['action']=='cast'else'Battlefield',op['action']=='float')['id'];selectedTargets=[];paidSeen=False;discardId=0;freeze(m,n,'before-'+str(index));stage='resolve';pick='card:'+str(source)
      elif'Mulligan'in kind:pick='keep'if'keep'in enabled else'ok'
      elif kind in ['InputAttack','InputBlock','RevealCards','InputConfirm']:pick='confirm-cards'if kind=='RevealCards'else'ok'
      elif'PayMana'in kind:pick='ok'
      elif kind=='dialog':
       candidates=[o for o in opts if o['id']in enabled]
       if r['message']=='ManaColor':pick=next(o['id']for o in candidates if o['label'].lower()==op.get('choice','Green').lower())
       elif r['message'].endswith(': Choose a color'):pick=color_response(r,n,op,view,check)
       else:
        check(r['message']=='getAbilityToPlay','Actual source native ability picker');want=op.get('ability');chosen=next((o for o in candidates if want and want in o['label']),candidates[0]);pick=chosen['id']
      elif kind=='InputSelectTargets':
       targets=[(a,find(n,name,a,'Battlefield')['id'])for a,name in op.get('targets',[])];todo=[t for t in targets if t not in selectedTargets]
       if todo:pick='card:'+str(todo[0][1]);selectedTargets.append(todo[0])
       else:pick='ok'
      elif kind=='ChooseColor'or kind=='ChooseOptions'and r['message']=='Choose a color':
       pick=color_response(r,n,op,view,check)
       write(folder/('color-'+str(rid)+'-wire.json'),m)
      elif kind=='OrderAbilities':
       pick=replacement_response(r,op,check)
       write(folder/('replacement-'+str(rid)+'-wire.json'),m)
      elif kind in ['InputSelectCardsFromList','InputSelectEntitiesFromList','SelectCards','SelectEntity','ChooseEntity']:
       check(op.get('discard')is not None,'Expected actual Harvest discard cost '+str(rid));cardOptions=[o for o in opts if o['id']in enabled and(o.get('card')or{}).get('id',0)>0]
       if not discardId:
        chosen=next(o for o in cardOptions if o['card']['name']==op['discard']);discardId=chosen['card']['id'];pick=chosen['id']
       else:pick='confirm-cards'if'confirm-cards'in enabled else'ok'
      else:raise AssertionError(('Unhandled actual replacement native request',r))
      if forced is not None:pick=forced
      check(pick in enabled,('Exact original enabled response',kind,pick,enabled));break
     except Exception:
      correction=pause(traceback.format_exc(),m,n)
      if correction.get('mode')=='stop':raise
      if correction.get('mode')=='skip-endpoint':
       check(stage=='resolve'and kind=='InputPassPriority'and not n['stack'],'Frozen real completed endpoint before offline correction');endpoints.append(dict(operation=index,source=source,name=op['source'],requestId=rid,status='RAW_FAILURE_INDEPENDENT_AUDIT_REQUIRED'));write(folder/'endpoints.json',endpoints);index+=1;stage='next'
      forced=correction.get('option')
    if done:break
    response=dict(type='respond',session=cfg['session'],seat=r['seat'],requestId=rid,option=pick);trace.write(json.dumps(response)+'\n');trace.flush();p.stdin.write(json.dumps(response)+'\n');p.stdin.flush();print(index,kind,pick,flush=True)
   else:raise AssertionError('Combined native request-step bound')
  check(index==len(plan['operations']),'All original frozen planned endpoints complete')
  check(len(paid)==sum(op['action']!='float'for op in plan['operations']),'All original planned paid operations committed exactly once')
  status=completion_status(endpoints,plan['operations'],failures)
  check(status!='FAIL','Exact original endpoint indices complete')
 except Exception:status='FAIL';error=traceback.format_exc();print(error,flush=True)
 finally:
  if p is not None and p.poll()is None:p.kill();p.wait()
 result=dict(status=status,case=case,inputId=plan['inputId'],folder=folder.relative_to(ROOT).as_posix(),checks=len(checks),assertions=checks,endpoints=endpoints,paid=paid,requests=requests,failures=failures,runtimeHash=runtime['runtimeHash'],sources=[ref(x)for x in sources],frozenPlan=ref(folder/'plan.json'),limitations=['One combined ordinary two-human native game, exact seven original named sources only.','Initial positions bypass ETB of opening permanents. No new card-data or effect mutation.','Native Mana identities are counted as a multiset: several units may share a Java Mana handle.','Actual frozen wire and native results are separate from current Unity or remote networking.'])
 if status=='FAIL':result['error']=error
 write(folder/'result.json',result);write(BASE/'combined-latest.json',result);print(status,case,folder,flush=True);return result
if __name__=='__main__':
 result=run(parse_args().case)
 sys.exit(0 if result['status']=='PASS_BOUNDED'else 1)
