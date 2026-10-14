"""Paid native exchanges, spell control and group assignments; setup is initial only."""
from pathlib import Path
import hashlib,json,os,queue,subprocess,tempfile,threading,time,traceback,sys
ROOT=Path(__file__).resolve().parents[2];BASE=ROOT/'.tools/control-group-flows-1011';RT=ROOT/'ForgeRuntime'
runtime=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def ref(p):return dict(path=p.relative_to(ROOT).as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
def cards(n):return list({c['id']:c for p in n['players']for z in p['zones'].values()for c in z}.values())
def find(n,name,owner=1,zone=None):return next(c for c in cards(n)if c['name']==name and c['owner']==owner and(zone is None or c['zone']==zone))
def run(case):
 plan=json.loads((BASE/'cases.json').read_text('utf8'))[case];folder=BASE/'native'/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir();classes=folder/'classes';classes.mkdir()
 fixtures=[ROOT/'Tools/ForgeBridge/tests/RoyalStateFixture.java',ROOT/'Tools/ForgeBridge/tests/ControlGroupFixture.java'];sources=fixtures+[Path(__file__),BASE/'cases.json']
 for p in sources:(folder/p.name).write_bytes(p.read_bytes())
 checks=[];paid=[];requests=[];endpoints=[];p=None;operation=0;stage='start';before=None;initial=None;source=None;targeted=[];groups=[];selected=[];selector=0;paidBefore=None
 def check(ok,msg):assert ok,msg;checks.append(msg)
 def observe(rid):
  end=time.monotonic()+6
  while time.monotonic()<end:
   try:return json.loads((audit/(str(rid)+'.json')).read_text('utf8'))
   except(FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
  raise AssertionError('Missing read-only pending observation '+str(rid))
 def freeze(m,n,label):write(folder/(label+'-wire.json'),m);write(folder/(label+'-observation.json'),n)
 def endpoint(m,n,op):
  nonlocal operation,stage
  label=op['result'];freeze(m,n,'after-'+str(operation)+'-'+label)
  now={c['id']:c for c in cards(n)};old={c['id']:c for c in cards(before)}
  check(m['request']['seat']==m['request']['playerSeat'],'Exact native next operator');check(not n['stack'],'Normal next empty-stack priority')
  check(now[source]['zone']=='Graveyard','Original paid spell reaches own graveyard')
  if label=='group-exchange':
   mapping={find(before,name,seat,'Battlefield')['id']:3-seat for seat,names in op['exchange'].items()for name in names for seat in [int(seat)]}
   for c in old.values():
    if c['zone']=='Battlefield':
     check(now[c['id']]['controller']==mapping.get(c['id'],c['controller']),'Exact exchanged or unchanged controller '+str(c['id']))
     for k in ['timestamp','owner','damage','counters']:check(now[c['id']][k]==c[k],'Exchange retains '+k+' '+str(c['id']))
   if mapping:check(len({tuple(now[x]['temporaryControllers'].keys())for x in mapping})==1 and all(len(now[x]['temporaryControllers'])==1 for x in mapping),'Exchange shares one native control timestamp')
  elif label.startswith('assign-'):
   check(len(groups)==2,'One group choice per actual player')
   for seat,group in enumerate(op['assign'],1):
    chosen=find(before,'Forest',seat,'Library') if group=='friend' else find(before,'Sol Ring',seat,'Battlefield')
    if group=='friend':
     check(now[chosen['id']]['zone']=='Battlefield'and now[chosen['id']]['controller']==seat and now[chosen['id']]['tapped'],'Exact friendly land enters tapped '+str(seat))
     lib0={c['id']for c in before['players'][seat-1]['zones']['Library']};lib1={c['id']for c in n['players'][seat-1]['zones']['Library']};check(lib1==lib0-{chosen['id']},'Shuffle retains exact other library IDs '+str(seat))
     check(find(n,'Sol Ring',seat)['zone']=='Battlefield','Friend does not sacrifice '+str(seat))
    else:
     check(now[chosen['id']]['zone']=='Graveyard','Exact foe original artifact sacrificed '+str(seat))
     check([c['id']for c in n['players'][seat-1]['zones']['Library']]==[c['id']for c in before['players'][seat-1]['zones']['Library']],'Foe does not search/shuffle '+str(seat))
   check(now[source]['remembered']==[],'No remembered group leaks after native resolution')
  elif label.startswith('control-'):
   victim=find(before,op['victim'],2,'Stack');v=now[victim['id']]
   if op['victim']=='Lightning Bolt':check([x['life']for x in n['players']]==op['life'],'Exact retargeted spell damage to intended player')
   else:check(v['zone']=='Battlefield'and v['controller']==1 and v['owner']==2,'Exact original stolen permanent resolves under new controller')
   check(v['zone'] in ['Battlefield','Graveyard'],'Original stolen source resolves normally')
  elif label.startswith('chimera-'):
   chim=find(before,'Perplexing Chimera',1);c=now[chim['id']]
   if op.get('gone'):check(c['zone']=='Hand'and c['owner']==1,'Real response removes exchanging permanent')
   else:check(c['zone']=='Battlefield'and c['controller']==(2 if op['accept']else 1),'Actual optional exchange or decline permanent controller')
   check(c['remembered']==[],'Exchange remembered cards cleared')
   check([x['life']for x in n['players']]==op['life'],'Spell original or new target damage exact')
  endpoints.append(dict(operation=operation,result=label,requestId=m['request']['requestId']));operation+=1;stage='start'
  write(folder/'endpoints.json',endpoints)
 try:
  compiler=Path(os.environ.get('MTGB_JAVAC',r'C:/Program Files/Java/jdk-17/bin/javac.exe'))
  with(folder/'compile.log').open('w',encoding='utf8')as log:subprocess.run([str(compiler),'-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*map(str,fixtures)],check=True,stdout=log,stderr=subprocess.STDOUT,timeout=60)
  with tempfile.TemporaryDirectory(prefix='profile-',dir=folder,ignore_cleanup_errors=True)as temp,(folder/'stderr.log').open('w',encoding='utf8')as err,(folder/'trace.jsonl').open('w',encoding='utf8')as trace:
   deck=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
   cfg=dict(session='control-group-'+case,seed=4,startingLife=30,firstPlayerSeat=plan.get('firstSeat',1),decks=[deck,deck],positions=plan['positions'],auditDir=str(audit));write(folder/'start.json',cfg)
   p=subprocess.Popen([str(RT/runtime['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.ControlGroupFixture',str(RT/runtime['assets']),str(folder/'start.json')],cwd=RT/runtime['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,encoding='utf8')
   q=queue.Queue()
   def reader():
    try:
     for line in p.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start();seen=set();deadline=time.monotonic()+220
   for step in range(600):
    check(time.monotonic()<deadline,'Bounded native deadline');m=q.get(timeout=40)
    if isinstance(m,Exception):raise m
    check(m is not None,'Native process remains live');trace.write(json.dumps(m)+'\n');trace.flush();check(m['type']not in ['fatal','ended','rejected'],str(m))
    if m['type']!='request':continue
    r=m['request'];rid=r['requestId']
    if rid in seen:continue
    seen.add(rid);opts=r['options'];enabled=[o['id']for o in opts if not o.get('disabled')];kind=r['kind'];v=m['views'][0];n=None if 'Mulligan'in kind else observe(rid);pick='ok'
    requests.append(dict(id=rid,kind=kind,seat=r['seat'],playerSeat=r['playerSeat'],stage=stage,operation=operation))
    op=plan['operations'][operation]if operation<len(plan['operations'])else None
    if kind=='InputPassPriority':
     if initial is None:initial=n;freeze(m,n,'initial')
     if stage=='result'and not n['stack']:
      endpoint(m,n,op)
      if operation==len(plan['operations']):break
      op=plan['operations'][operation]
     if stage=='start'and r['seat']==op['seat']and v['phase']=='MAIN1'and(not n['stack']or op.get('response')):
      before=n;source=find(n,op['cast'],op['seat'],'Hand')['id'];pick='card:'+str(source);stage='cast';targeted=[];selected=[];selector=0;groups=[];paidBefore=None;freeze(m,n,'before-'+str(operation))
     else:pick='ok'
    elif 'Mulligan'in kind:pick='keep'if'keep'in enabled else'ok'
    elif kind in ['InputAttack','InputBlock']:pick='ok'
    elif 'PayMana'in kind:
     if paidBefore is None:paidBefore=n;freeze(m,n,'payment-'+str(operation))
     pick='ok';stage='paying'
    elif kind=='SelectStackTarget':
     freeze(m,n,'stack-target-'+str(operation)+'-'+str(rid));victim=find(n,op['victim'],2,'Stack');si=next(x for x in n['stack']if x['source']==victim['id']and x['spell']);pick='ok'if r.get('selectedCount')==1 and'ok'in enabled else'stack:'+str(si['id']);targeted.append('stack')
    elif kind=='InputSelectTargets':
     freeze(m,n,'target-'+str(operation)+'-'+str(rid));planned=op.get('newTargets',[])if stage=='result'else op.get('targets',[]);todo=[x for x in planned if x not in targeted]
     if stage=='result'and op.get('newTargets'):
      victim=find(n,op.get('victim','Lightning Bolt'),2,'Stack');si=next(x for x in n['stack']if x['source']==victim['id']and x['spell']);oldsi=next(x for x in before['stack']if x['source']==victim['id'])if op.get('victim')else next(x for x in json.loads((folder/('paid-'+str(operation)+'-observation.json')).read_text('utf8'))['stack']if x['source']==victim['id']and x['spell'])
      check(si['id']==oldsi['id']and si['abilityId']==oldsi['abilityId']and si['actor']==si['cardController']==1,'Same native stack ability and source with new controller at retarget');check(r['seat']==r['playerSeat']==1,'Actual new controller chooses new targets')
     if todo:
      x=todo[0]
      if isinstance(x,int):chosen=next(o for o in opts if o['id']in enabled and o.get('label')=='Player '+str(x))
      elif x=='stack':chosen=next(o for o in opts if o['id']in enabled and o.get('card',{}).get('id')==find(n,op['victim'],2,'Stack')['id'])
      else:chosen=next(o for o in opts if o['id']in enabled and o.get('card',{}).get('id')==find(n,x[1],x[0])['id'])
      pick=chosen['id'];targeted.append(x)
     else:pick='ok'
    elif kind=='dialog':
     freeze(m,n,'dialog-'+str(operation)+'-'+str(rid))
     if 'assign'in op:
      check(r['seat']==r['playerSeat']==op['seat'],'Caster assigns all players');group=op['assign'][len(groups)]
      check('Player '+str(len(groups)+1)in r.get('message','')+' '+r.get('title',''),'Original affected player in group question')
      check(find(n,"Pir's Whim",1,'Stack')['remembered']==[],'All group assignments collected before payload')
      chosen=next(o for o in opts if o['id']in enabled and group.lower()in o['label'].lower());pick=chosen['id'];groups.append(group)
     else:pick=enabled[0]
    elif kind=='InputConfirm':
     freeze(m,n,'confirm-'+str(operation)+'-'+str(rid));accept=op.get('accept',True)
     if op.get('gone')and'Unsummon'in r.get('message',''):accept=False
     pick='ok'if accept else'cancel'
    elif kind in ['InputSelectCardsFromList','SelectCards','InputSelectEntitiesFromList']:
     freeze(m,n,'cards-'+str(operation)+'-'+str(rid))
     if 'exchange'in op:
      options=[o for o in opts if o['id']in enabled and o.get('card',{}).get('id',0)>0]
      # Native names identify which candidate pool is being selected, never option ordinal.
      candidateids={o['card']['id']for o in options};poolseat=next((s for s in [1,2]if candidateids and candidateids<=set(c['id']for c in n['players'][s-1]['zones']['Battlefield'])),1)
      wanted=[find(n,name,poolseat,'Battlefield')['id']for name in op['exchange'].get(str(poolseat),[])]
      todo=[id for id in wanted if not next(o for o in options if o['card']['id']==id).get('selected')]
      pick=next(o['id']for o in options if o['card']['id']==todo[0])if todo else('confirm-cards'if'confirm-cards'in enabled else'ok')
     elif 'assign'in op:
      seat=r['playerSeat'];group=op['assign'][seat-1]
      check(len(groups)==2,'Every assignment precedes child results')
      host=find(n,"Pir's Whim",1,'Stack');check({x['player']for x in host['remembered']}=={s+1 for s,g in enumerate(op['assign'])if g==group},'Exact scoped group memory during payload')
      chosen=find(n,'Forest'if group=='friend'else'Sol Ring',seat,'Library'if group=='friend'else'Battlefield')['id']
      option=next(o for o in opts if o.get('card',{}).get('id')==chosen)
      pick=('confirm-cards'if'confirm-cards'in enabled else'ok')if option.get('selected')else option['id']
     else:raise AssertionError(('Unhandled cards',r))
    elif kind=='RevealCards':pick='confirm-cards'
    elif kind=='OrderAbilities':pick='confirm-order'if'confirm-order'in enabled else next(x for x in enabled if x.startswith('order:'))
    else:raise AssertionError(('Unhandled actual native request',r))
    # Payment is proved at the first real stack exposure, before later effects change resources.
    if stage in ['cast','paying']and any(x['source']==source and x['spell']for x in n['stack']):
     cost=op.get('mana',0);a=before['players'][op['seat']-1];b=n['players'][op['seat']-1]
     previous={c['id']:c for c in a['zones']['Battlefield']};used=[c for c in b['zones']['Battlefield']if c['tapped']and not previous[c['id']]['tapped']]
     check(all(c['name']in ['Plains','Island','Swamp','Mountain','Forest','Sol Ring']for c in used),'Only actual known native mana sources')
     produced=sum(2 if c['name']=='Sol Ring'else 1 for c in used)
     check(produced==cost+b['pool']-a['pool'],'Actual committed mana sources and remaining pool '+str(operation));paid.append(dict(operation=operation,name=op['cast'],id=source,seat=op['seat'],mana=cost,produced=produced,remainingPool=b['pool'],sourceIds=[c['id']for c in used],requestId=rid));write(folder/'paid.json',paid);freeze(m,n,'paid-'+str(operation));stage='result'if op.get('result')else'stack-wait'
     if not op.get('result'):
      operation+=1;stage='start';following=plan['operations'][operation]
      if kind=='InputPassPriority'and following.get('response')and r['seat']==following['seat']:
       op=following;before=n;source=find(n,op['cast'],op['seat'],'Hand')['id'];pick='card:'+str(source);stage='cast';targeted=[];selected=[];groups=[];paidBefore=None;freeze(m,n,'before-'+str(operation))
    check(pick in enabled,('Exact enabled response',kind,pick,enabled));response=dict(type='respond',session=cfg['session'],seat=r['seat'],requestId=rid,option=pick);trace.write(json.dumps(response)+'\n');trace.flush();p.stdin.write(json.dumps(response)+'\n');p.stdin.flush();print(case,step,operation,kind,pick,flush=True)
   else:raise AssertionError('Request step bound')
   if p.poll()is None:p.kill();p.wait()
  status='PASS_BOUNDED'
 except Exception:status='FAIL';error=traceback.format_exc();print(error,flush=True)
 finally:
  if p is not None and p.poll()is None:p.kill();p.wait()
 result=dict(status=status,case=case,folder=folder.relative_to(ROOT).as_posix(),checks=len(checks),paid=paid,endpoints=endpoints,requests=requests,runtimeHash=runtime['runtimeHash'],sources=[ref(x)for x in sources])
 if status=='FAIL':result['error']=error
 write(folder/'result.json',result);write(BASE/(case+'-latest.json'),result);print(status,case,folder,flush=True);return result
if __name__=='__main__':
 for case in sys.argv[1:]:run(case)
