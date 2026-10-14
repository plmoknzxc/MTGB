"""Bounded original DigMultiple producer flows; only initial zones are arranged."""
from pathlib import Path
import hashlib,json,os,queue,subprocess,tempfile,threading,time,traceback,sys
ROOT=Path(__file__).resolve().parents[2];BASE=ROOT/'.tools/dig-drain-flip-1011/dig';RT=ROOT/'ForgeRuntime'
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def ref(p):return dict(path=p.relative_to(ROOT).as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
def cards(n):return {c['id']:c for p in n['players']for z in p['zones'].values()for c in z}
def find(n,name,seat=1):return next(c for c in cards(n).values()if name in [c['name'],c['printedName'],c['currentName']]and c['owner']==seat)
def ids(n,seat,zone):return [c['id']for c in n['players'][seat-1]['zones'][zone]]
def run(case):
 runtime=json.loads((RT/'runtime.json').read_text('utf-8-sig'));plan=json.loads((BASE/'cases.json').read_text('utf8'))[case]
 folder=BASE/'native'/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir();classes=folder/'classes';classes.mkdir()
 fixtures=[ROOT/'Tools/ForgeBridge/tests/RoyalStateFixture.java',ROOT/'Tools/ForgeBridge/tests/DigMultipleDeltaFixture.java'];sources=fixtures+[Path(__file__),BASE/'cases.json',RT/'runtime.json']
 if os.environ.get('MTGB_DIG_NATIVE_OVERRIDE')=='1':
  override=ROOT/'Tools/ForgeBridge/java/forge/game/ability/effects/DigMultipleEffect.java';fixtures.append(override);sources.append(override)
 for file in sources:(folder/file.name).write_bytes(file.read_bytes())
 checks=[];paid=[];inputs=[];orders=[];categories=[];reveals=[];endpoints=[];p=None;stage='next';before=None;effectBefore=None;source=None;paidSeen=False;categoryDone=0;selectedNames={};alternative=False;attackSelected=False
 def check(ok,msg):assert ok,msg;checks.append(str(msg))
 def observe(rid):
  until=time.monotonic()+6
  while time.monotonic()<until:
   try:return json.loads((audit/(str(rid)+'.json')).read_text('utf8'))
   except(OSError,json.JSONDecodeError):time.sleep(.01)
  raise AssertionError('Missing original observation '+str(rid))
 def freeze(m,n,label):write(folder/(label+'-wire.json'),m);write(folder/(label+'-observation.json'),n)
 def outcome(m,n):
  freeze(m,n,'after');check(not n['stack'],'Actual final native stack is empty');check(m['request']['seat']==m['request']['playerSeat'],'Actual physical/logical next actor agrees');old=cards(effectBefore);now=cards(n)
  check(set(now)==set(old),'Every original physical identity remains exactly once');check(all(now[cid]['owner']==c['owner']for cid,c in old.items()),'Every original owner preserved')
  for seat in [1,2]:check(len(ids(n,seat,'Library'))==len(set(ids(n,seat,'Library'))),'Unique full private library identities '+str(seat))
  if case=='adaptation':
   responder=find(effectBefore,'Aerial Responder')['id'];spider=find(effectBefore,'Giant Spider')['id'];top=ids(effectBefore,1,'Library')[:7]
   check(categoryDone==8,'Four mandatory categories decline then repeat and choose');check(sum(x['chosen']==responder for x in categories)==3,'One physical responder chosen in three categories');check(sum(x['chosen']==spider for x in categories)==1,'Distinct reach card selected')
   check(now[responder]['zone']=='Battlefield'and now[spider]['zone']=='Hand','Exact extra chosen responder battlefield and other chosen spider hand');check(all(now[cid]['zone']=='Graveyard'for cid in top if cid not in [responder,spider]),'Exact unchosen original seven-card remainder graveyard');check(ids(n,1,'Library')==ids(effectBefore,1,'Library')[7:],'Full untouched adaptation library remainder')
  elif case=='twilight':
   chosen={find(effectBefore,'Grizzly Bears')['id'],find(effectBefore,'Forest')['id']};top=ids(effectBefore,1,'Library')[:6];lib=ids(n,1,'Library');rest=set(top)-chosen
   check(alternative,'Actual native X>=5 alternative destination input answered');check(all(now[cid]['zone']=='Hand'for cid in chosen),'Actual exact remembered original creature and land enter selected Hand');check(lib[:-len(rest)]==ids(effectBefore,1,'Library')[6:]and set(lib[-len(rest):])==rest,'Exact original random bottom set and untouched remainder');check(not now[source]['remembered']and not now[source]['imprinted'],'Real cleanup empties remembered and imprinted')
  elif case=='harper':
   check(categoryDone==4 and all(x['chosen']is None for x in categories),'Optional all four party categories decline once and advance');lib=ids(n,1,'Library');top=ids(effectBefore,1,'Library')[:4];check(lib[:-4]==ids(effectBefore,1,'Library')[4:]and set(lib[-4:])==set(top),'Actual attack effect moves exact four unchosen cards randomly to bottom');check(ids(n,1,'Hand')==ids(effectBefore,1,'Hand'),'Harper optional empty adds no hand objects');check(any(x['trigger']and x['source']==source for x in inputs if 'trigger'in x),'Actual original attack-trigger stack identity observed')
  elif case=='explore':
   chosen={find(effectBefore,'Forest',1)['id'],find(effectBefore,'Lightning Bolt',1)['id']};check([x['seat']for x in categories]==[1,1,2,2],'Real chooser sequence completes player1 then player2');check(all(now[cid]['zone']=='Hand'for cid in chosen),'Exact chosen player1 land/spell hand identities');check([p['life']for p in n['players']]==[33,33],'Actual following each-player gain3')
   for seat in [1,2]:
    top=ids(effectBefore,seat,'Library')[:5];rest=set(top)-(chosen if seat==1 else set());lib=ids(n,seat,'Library');check(lib[:-len(rest)]==ids(effectBefore,seat,'Library')[5:]and set(lib[-len(rest):])==rest,'Exact per-seat original random rest '+str(seat))
  else:
   chosen=find(effectBefore,'Grizzly Bears')['id'];rest=[cid for cid in ids(effectBefore,1,'Library')[:4]if cid!=chosen];check(now[chosen]['zone']=='Hand','Actual Gift chosen original creature hand');check(len(ids(n,1,'Library'))==len(ids(effectBefore,1,'Library'))-1,'Actual exact Gift library count');check(ids(n,2,'Library')==ids(effectBefore,2,'Library'),'Gift leaves other private library unchanged')
   endpoints.append(dict(requestId=m['request']['requestId'],kind=m['request']['kind'],seat=m['request']['seat'],paid=True));write(folder/'endpoints.json',endpoints)
   check(len(orders)==1,'Gift actual any-order selector before rest moved');check(orders[0]['chosen']==list(reversed(rest)),'Exact submitted reversed original Gift bottom order');check(ids(n,1,'Library')==ids(effectBefore,1,'Library')[4:]+orders[0]['chosen'],'Exact full submitted bottom order and untouched library remainder')
  if case!='gift':endpoints.append(dict(requestId=m['request']['requestId'],kind=m['request']['kind'],seat=m['request']['seat'],paid=True));write(folder/'endpoints.json',endpoints)
 try:
  with(folder/'compile.log').open('w',encoding='utf8')as log:subprocess.run([r'C:/Program Files/Java/jdk-17/bin/javac.exe','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*map(str,fixtures)],check=True,stdout=log,stderr=subprocess.STDOUT,timeout=60)
  with tempfile.TemporaryDirectory(prefix='profile-',dir=folder,ignore_cleanup_errors=True)as temp,(folder/'stderr.log').open('w',encoding='utf8')as err,(folder/'trace.jsonl').open('w',encoding='utf8')as trace:
   deck=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[]);cfg=dict(session='dig-'+case,seed=4,startingLife=30,firstPlayerSeat=1,decks=[deck,deck],positions=plan['positions'],auditDir=str(audit));write(folder/'start.json',cfg)
   p=subprocess.Popen([str(RT/runtime['java']),'-Xmx640m','-XX:ActiveProcessorCount=3','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.DigMultipleDeltaFixture',str(RT/runtime['assets']),str(folder/'start.json')],cwd=RT/runtime['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,encoding='utf8')
   q=queue.Queue()
   def reader():
    try:
     for line in p.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start();seen=set();deadline=time.monotonic()+280
   for step in range(600):
    check(time.monotonic()<deadline,'Bounded native deadline');m=q.get(timeout=40)
    if isinstance(m,Exception):raise m
    check(m is not None,'Actual native process live');trace.write(json.dumps(m)+'\n');trace.flush();check(m['type']not in ['fatal','ended','rejected'],str(m))
    if m['type']!='request':continue
    r=m['request'];rid=r['requestId']
    if rid in seen:continue
    seen.add(rid);enabled=[o['id']for o in r['options']if not o.get('disabled')];kind=r['kind'];v=m['views'][0];n=None if'Mulligan'in kind else observe(rid);pick='ok'
    if n:freeze(m,n,'request-'+str(rid))
    if stage=='resolve'and not paidSeen and any(x['source']==source and x['spell']for x in n['stack']):
     prev=cards(before);used=[c for c in n['players'][0]['zones']['Battlefield']if c['name']in ['Forest','Plains','Island']and c['tapped']and not prev[c['id']]['tapped']];check(len(used)-n['players'][0]['pool']+before['players'][0]['pool']==plan['mana'],'Actual mana sources less remaining pool equals cost');paid.append(dict(source=source,mana=plan['mana'],requestId=rid,lands=[c['id']for c in used],stack=n['stack']));write(folder/'paid.json',paid);freeze(m,n,'paid');paidSeen=True
    if n:
     for x in n['stack']:
      if x['trigger']and x['source']==source and x not in inputs:inputs.append(x)
    if kind=='InputPassPriority':
     if stage=='resolve'and not n['stack']:
      if case=='harper':stage='waitAttack';freeze(m,n,'cast-after');check(find(n,'Harper Recruiter')['zone']=='Battlefield','Actual paid Harper entered battlefield')
      else:outcome(m,n);break
     elif stage=='attackResolve'and not n['stack']:outcome(m,n);break
     if stage=='next'and r['seat']==1 and v['turnSeat']==1 and v['phase']=='MAIN1'and not n['stack']:
      before=effectBefore=n;source=find(n,plan['source'])['id'];pick='card:'+str(source);stage='resolve';freeze(m,n,'before')
    elif 'Mulligan'in kind:pick='keep'if'keep'in enabled else'ok'
    elif 'PayMana'in kind:pick='ok'
    elif kind=='InputAttack':
     if case=='harper'and stage=='waitAttack'and r['seat']==1:
      effectBefore=n;freeze(m,n,'effect-before');option=next((o for o in r['options']if o.get('attackerId')==source and o['id'].startswith('attack-remove:')),None)
      if option:pick='ok';stage='attackResolve';attackSelected=True
      else:pick=next(o['id']for o in r['options']if o.get('attackerId')==source and o['id']in enabled)
     else:pick='ok'
    elif kind=='InputBlock':pick='ok'
    elif kind=='RevealCards':reveals.append(dict(requestId=rid,seat=r['seat'],ids=[c['id']for c in r['previewCards']]));pick='confirm-cards'
    elif kind in ['SelectCards','InputSelectCardsFromList','InputSelectEntitiesFromList']:
     pool=[o for o in r['options']if o.get('card')];category='Choose cards ('in r.get('message','');desired=None
     check(r['seat']==r['playerSeat'],'Actual selector physical/logical chooser '+str(rid));check(all(cards(n)[o['card']['id']]['owner']==r['seat']for o in pool),'Actual category candidates owned by exact chooser '+str(rid))
     if category:
      if case=='adaptation'and categoryDone>=4:desired='Giant Spider'if'Reach'in r['message']else'Aerial Responder'
      elif case=='twilight':desired='Forest'if'(Land)'in r['message']else'Grizzly Bears'
      elif case=='gift':desired='Grizzly Bears'if'(Creature)'in r['message']else None
      elif case=='explore'and r['seat']==1:desired='Forest'if'(Land)'in r['message']else'Lightning Bolt'
     elif case=='adaptation':desired='Aerial Responder';check(len(pool)==2 and len({o['card']['id']for o in pool})==2,'Extra chosen picker contains exactly two physical chosen objects')
     else:raise AssertionError(('Unexpected noncategory selector',r))
     if r['selectedCount']==0 and desired is not None:
      wanted=find(n,desired,r['seat'])['id'];pick=next(o['id']for o in pool if o['card']['id']==wanted);selectedNames[(r['seat'],r['message'])]=wanted
     else:
      pick='confirm-cards'if'confirm-cards'in enabled else'ok'
      if category:categories.append(dict(requestId=rid,seat=r['seat'],message=r['message'],chosen=selectedNames.pop((r['seat'],r['message']),None)));categoryDone+=1
     inputs.append(dict(requestId=rid,kind=kind,seat=r['seat'],category=category,wire=ref(folder/('request-'+str(rid)+'-wire.json'))))
    elif kind=='OrderCards':
     pool=[o for o in r['options']if o.get('card')];check(case=='gift','Only original nonrandom Gift rest requires ordering in this batch');check(r['seat']==r['playerSeat']==1,'Original Gift owner controls rest order')
     if not orders:orders.append(dict(firstRequestId=rid,pool=[o['card']['id']for o in pool],chosen=[]));freeze(m,n,'order-before');check(all(cards(n)[o['card']['id']]['zone']=='Library'for o in pool),'Original rest still in library before order')
     selected=[o['card']['id']for o in sorted(pool,key=lambda o:o['order'])if o['selected']];desired=list(reversed(orders[0]['pool']))
     check(r['minCount']==r['maxCount']==len(pool),'Actual fixed complete order count '+str(rid))
     if len(selected)==len(pool):orders[0]['chosen']=selected;orders[0]['confirmationRequestId']=rid;freeze(m,n,'order-confirm');pick='confirm-cards'
     else:check('confirm-cards'not in enabled,'No premature original order confirmation '+str(rid));wanted=next(cid for cid in desired if cid not in selected);pick=next(o['id']for o in pool if o['card']['id']==wanted)
    elif kind in ['dialog','ChooseNumber']:
     if kind=='ChooseNumber'and 'x'in plan:
      pick='confirm-number'if r['selectedValue']==plan['x']else('number-plus-five'if plan['x']-r['selectedValue']>=5 else'number-plus')
     elif case=='explore':pick=next(o['id']for o in r['options']if o['label'].startswith('Each player looks at the top five cards'))
     elif case=='twilight'and any('Battlefield'in o['label']or'battlefield'in o['label']for o in r['options']):alternative=True;freeze(m,n,'change-later');check(ids(n,1,'Library')==ids(effectBefore,1,'Library'),'ChangeLater retains complete original library before child destination');check(set(cards(n)[source]['remembered'])=={find(n,'Forest')['id'],find(n,'Grizzly Bears')['id']},'Exact delayed chosen remembered IDs');pick=next(o['id']for o in r['options']if'battlefield'in o['label'].lower())
     else:raise AssertionError(('Unhandled native dialog',r))
    elif kind=='InputConfirm':
     if case=='twilight'and'battlefield'in r.get('message','').lower():alternative=True;freeze(m,n,'change-later');check(ids(n,1,'Library')==ids(effectBefore,1,'Library'),'ChangeLater retains complete original library before child destination');check(set(cards(n)[source]['remembered'])=={find(n,'Forest')['id'],find(n,'Grizzly Bears')['id']},'Exact delayed chosen remembered IDs');pick='ok'
     else:pick='ok'
    elif kind=='OrderAbilities':pick='confirm-order'if'confirm-order'in enabled else next(x for x in enabled if x.startswith('order:'))
    else:raise AssertionError(('Unhandled original DigMultiple request',r))
    check(pick in enabled,('Actual enabled ordinal',rid,kind,pick,enabled));response=dict(type='respond',session=cfg['session'],seat=r['seat'],requestId=rid,option=pick);trace.write(json.dumps(response)+'\n');trace.flush();p.stdin.write(json.dumps(response)+'\n');p.stdin.flush();print(case,step,stage,kind,pick,flush=True)
   else:raise AssertionError('Bounded request count')
  status='PASS_BOUNDED'
 except Exception:status='FAIL';error=traceback.format_exc();print(error,flush=True)
 finally:
  if p is not None and p.poll()is None:p.kill();p.wait()
 result=dict(status=status,case=case,folder=folder.relative_to(ROOT).as_posix(),checks=len(checks),uniqueChecks=len(set(checks)),assertions=checks,paid=paid,endpoints=endpoints,categories=categories,orders=orders,reveals=reveals,inputs=inputs,runtimeHash=runtime['runtimeHash'],sources=[ref(x)for x in sources])
 if os.environ.get('MTGB_DIG_NATIVE_OVERRIDE')=='1':result['isolatedNativeOverride']=dict(source=ref(override),testedClass=ref(classes/'forge/game/ability/effects/DigMultipleEffect.class'),baseRuntimeHash=runtime['runtimeHash'])
 if status=='FAIL':result['error']=error
 write(folder/'result.json',result);write(BASE/(case+'-latest.json'),result);print(status,case,folder,flush=True);return result
if __name__=='__main__':
 for case in sys.argv[1:]:run(case)
