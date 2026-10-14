"""Real restricted-mana tail operations; old complementary paid proofs are reused."""
from pathlib import Path
import hashlib,json,os,queue,subprocess,tempfile,threading,time,traceback,sys
ROOT=Path(__file__).resolve().parents[2];BASE=ROOT/'.tools/primitive-api-tail-1011/restrictions';RT=ROOT/'ForgeRuntime'
JAVAC=Path('C:/Program Files/Java/jdk-17/bin/javac.exe');runtime=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def ref(p):return dict(path=p.relative_to(ROOT).as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
def allcards(n):return list({c['id']:c for p in n['players']for z in p['zones'].values()for c in z}.values())
def find(n,name,seat=1,zone=None,untapped=False):return next(c for c in allcards(n)if name in [c['name'],c.get('printedName')]and c['owner']==seat and(zone is None or c['zone']==zone)and(not untapped or not c['tapped']))
def op(action,source,**kw):return dict(action=action,source=source,**kw)
P='Smoky Lounge // Misty Salon'
CASES={
 'foretell':dict(positions=[dict(Hand=['Behold the Multiverse'],Battlefield=['Karfell Harbinger','Creeping Peeper','Island']),{}],operations=[op('float','Karfell Harbinger'),op('float','Creeping Peeper'),op('special','Behold the Multiverse',choice='foretell',pay=['Karfell Harbinger','Island'],negative='Creeping Peeper',flag='foretelling',zone='Exile',mana=2)]),
 'rooms':dict(positions=[dict(Hand=[P],Battlefield=['Creeping Peeper','Mountain','Forest','Forest']),{}],operations=[op('float','Creeping Peeper'),op('cast',P,choice='Smoky Lounge',pay=['Creeping Peeper','Mountain','Forest'],mana=3,zone='Battlefield'),op('float','Creeping Peeper',minTurn=3),op('special',P,choice='Misty Salon',pay=['Creeping Peeper','Smoky Lounge','Smoky Lounge','Forest'],flag='unlock',mana=4,doors=2)]),
 'morph':dict(positions=[dict(Hand=['Willbender'],Battlefield=['Qarsi Deceiver','Creeping Peeper','Island','Island']),{}],operations=[op('float','Qarsi Deceiver'),op('cast','Willbender',choice='face down',pay=['Qarsi Deceiver','Island','Island'],flag='castFaceDown',mana=3,zone='Battlefield',faceDown=True),op('float','Qarsi Deceiver',minTurn=3),op('float','Creeping Peeper'),op('special','Willbender',choice='Morph',pay=['Qarsi Deceiver','Creeping Peeper'],flag='morphUp',mana=2,faceDown=False)]),
 'manifest':dict(positions=[dict(Hand=['Cloudform'],Battlefield=['Island','Island','Island','Qarsi Deceiver','Overgrown Zealot','Karfell Harbinger'],Library=['Centaur Courser']),{}],operations=[op('cast','Cloudform',pay=['Island']*3,mana=3,zone='Battlefield'),op('float','Qarsi Deceiver'),op('float','Overgrown Zealot',choice='two mana',color='GREEN'),op('float','Karfell Harbinger'),op('special','Centaur Courser',pay=['Overgrown Zealot','Overgrown Zealot','Qarsi Deceiver'],negative='Karfell Harbinger',flag='manifestUp',mana=3,faceDown=False)]),
 'helper':dict(positions=[dict(Hand=['Opt'],Battlefield=['Hydraulic Helper']*4+['Azure Mage']),{}],operations=[*[op('float','Hydraulic Helper')for _ in range(4)],op('activate','Azure Mage',pay=['Hydraulic Helper']*4,mana=4,draw=1)]),
 'boseiju-instant':dict(positions=[dict(Hand=['Think Twice'],Battlefield=['Boseiju, Who Shelters All','Island']),dict(Hand=['Counterspell'],Battlefield=['Island','Island'])],operations=[op('float','Boseiju, Who Shelters All',life=2),op('cast','Think Twice',pay=['Boseiju, Who Shelters All','Island'],mana=2,zone='Graveyard',counter=True,draw=1)]),
 'boseiju-creature':dict(positions=[dict(Hand=['Grizzly Bears'],Battlefield=['Boseiju, Who Shelters All','Forest']),dict(Hand=['Counterspell'],Battlefield=['Island','Island'])],operations=[op('float','Boseiju, Who Shelters All',life=2),op('cast','Grizzly Bears',pay=['Boseiju, Who Shelters All','Forest'],mana=2,zone='Graveyard',counter=True)]),
 'jegantha':dict(positions=[dict(Hand=['Reaper King','Goblin Piker'],Battlefield=['Jegantha, the Wellspring']),{}],operations=[op('float','Jegantha, the Wellspring'),op('cast','Reaper King',pay=['Jegantha, the Wellspring']*5,mana=5,zone='Battlefield')]),
 'piracy':dict(positions=[dict(Hand=['Piracy','Opt'],Battlefield=['Island','Island','Mountain','Deserted Temple']),dict(Hand=['Sunken Citadel'],Battlefield=['Island'])],operations=[op('land','Sunken Citadel',seat=2,color='BLUE'),op('cast','Piracy',minTurn=5,pay=['Island','Island'],mana=2,zone='Graveyard'),op('float','Sunken Citadel',owner=2,choice='two mana'),op('float','Island',owner=2),op('float','Mountain'),op('activate','Deserted Temple',choice='Untap target',pay=['Mountain'],negative=['Sunken Citadel','Island'],target='Island',targetOwner=2,mana=1),op('cast','Opt',pay=['Island'],payOwner=2,negative='Sunken Citadel',mana=1,zone='Graveyard',draw=1),op('wait','Piracy',minTurn=7,expiry=True)])
}
def run(case):
 plan=CASES[case];folder=BASE/'native'/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir();classes=folder/'classes';classes.mkdir()
 fixtures=[ROOT/'Tools/ForgeBridge/tests/RoyalStateFixture.java',ROOT/'Tools/ForgeBridge/tests/DrainManaDeltaFixture.java',ROOT/'Tools/ForgeBridge/tests/RestrictedManaTailFixture.java'];sources=fixtures+[Path(__file__),RT/'runtime.json']
 for s in sources:(folder/s.name).write_bytes(s.read_bytes())
 write(folder/'case.json',plan);checks=[];paid=[];endpoints=[];requests=[];stage='next';index=0;before=None;source=None;counter=False;counterPaid=False;negative=None;triedNegative=[];selected=False;p=None
 def check(ok,label):assert ok,label;checks.append(label)
 def observe(rid):
  end=time.monotonic()+7
  while time.monotonic()<end:
   try:return json.loads((audit/(str(rid)+'.json')).read_text('utf8'))
   except(FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
  raise AssertionError('Missing native snapshot '+str(rid))
 def freeze(m,n,label):write(folder/(label+'-wire.json'),m);write(folder/(label+'-observation.json'),n)
 def complete(m,n,o):
  nonlocal index,stage
  freeze(m,n,'after-'+str(index));check(not n['stack'],'Empty native stack endpoint '+str(index));check(m['request']['seat']==m['request']['playerSeat'],'Native operator restored '+str(index))
  if o['action']=='float':
   c=next(c for c in allcards(n)if c['id']==source);check(c['tapped'],'Original source tapped '+str(index));check(sum(u['source']==source for u in n['players'][0]['mana'])>sum(u['source']==source for u in before['players'][0]['mana']),'Actual source mana multiset production '+str(index))
   check(c['controller']==before['players'][o.get('owner',o.get('seat',1))-1]['seat'],'Mana source original controller retained '+str(index))
   if o.get('life'):check(before['players'][0]['life']-n['players'][0]['life']==o['life'],'Actual Boseiju life payment');paid.append(dict(operation=index,sourceId=source,name=o['source'],mana=0,life=o['life'],objects=[],requestId=m['request']['requestId']))
  elif o['action']in ['cast','activate','special']:
   h=next((a for a in reversed(n['paymentHistory'])if a['source']==source and len(a['payingMana'])==o['mana']),None);check(h is not None,'Exact actual paid mana retained '+str(index));check(not any(x['sourceId']==source and x['operation']==index for x in paid),'No repeated paid endpoint '+str(index))
   if o.get('flag'):check(h[o['flag']],'Real native operation flag '+o['flag']);check(h['rootStatic']if o['action']=='special'else h['spell'],'Actual native root class '+str(index))
   if o['action']=='activate':check(h['activated']and not h['spell'],'Actual nonspell activated root '+str(index))
   paid.append(dict(operation=index,sourceId=source,name=o['source'],mana=o['mana'],objects=h['payingMana'],ability=h,requestId=m['request']['requestId']))
   if o.get('zone'):check(next(c for c in allcards(n)if c['id']==source)['zone']==o['zone'],'Original result zone '+str(index))
   if 'faceDown'in o:check(next(c for c in allcards(n)if c['id']==source)['faceDown']==o['faceDown'],'Original exact face result '+str(index))
   if o.get('doors'):check(len(next(c for c in allcards(n)if c['id']==source)['unlockedDoors'])==o['doors'],'Both original room doors unlocked')
   if o.get('draw'):check(len(n['players'][0]['zones']['Library'])==len(before['players'][0]['zones']['Library'])-o['draw'],'Actual draw count '+str(index))
   if o.get('counter'):check(counterPaid,'Actual opponent Counterspell paid and resolved');check(find(n,'Counterspell',2,'Graveyard'),'Actual counter spell graveyard')
  elif o.get('expiry'):check(not n['players'][0]['piracy'],'Actual Piracy permission expires at EOT');check(not n['players'][0]['mana'],'Real EOT pool cleanup');check(all(c['controller']==2 for c in n['players'][1]['zones']['Battlefield']),'Opponent original controller unchanged')
  endpoints.append(dict(operation=index,action=o['action'],sourceId=source,requestId=m['request']['requestId']));write(folder/'paid.json',paid);write(folder/'endpoints.json',endpoints);index+=1;stage='next'
 try:
  with(folder/'compile.log').open('w',encoding='utf8')as log:subprocess.run([str(JAVAC),'-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*map(str,fixtures)],check=True,stdout=log,stderr=subprocess.STDOUT,timeout=60)
  with tempfile.TemporaryDirectory(prefix='profile-',dir=folder,ignore_cleanup_errors=True)as temp,(folder/'stderr.log').open('w',encoding='utf8')as err,(folder/'trace.jsonl').open('w',encoding='utf8')as trace:
   deck=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[]);cfg=dict(session='restrictions-'+case,seed=4,startingLife=30,firstPlayerSeat=1,decks=[deck,deck],positions=plan['positions'],auditDir=str(audit));write(folder/'start.json',cfg)
   p=subprocess.Popen([str(RT/runtime['java']),'-Xmx512m','-XX:ActiveProcessorCount=3','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.RestrictedManaTailFixture',str(RT/runtime['assets']),str(folder/'start.json')],cwd=RT/runtime['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,encoding='utf8')
   q=queue.Queue()
   def reader():
    try:
     for line in p.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start();seen=set();deadline=time.monotonic()+480
   for step in range(1400):
    check(time.monotonic()<deadline,'Native deadline');m=q.get(timeout=45)
    if isinstance(m,Exception):raise m
    check(m is not None,'Native live');trace.write(json.dumps(m)+'\n');trace.flush();check(m['type']not in ['fatal','ended','rejected'],str(m))
    if m['type']!='request':continue
    r=m['request'];rid=r['requestId']
    if rid in seen:continue
    seen.add(rid);kind=r['kind'];opts=r['options'];enabled=[o['id']for o in opts if not o.get('disabled')];n=None if 'Mulligan'in kind else observe(rid);pick='ok';o=plan['operations'][index]
    requests.append(dict(id=rid,kind=kind,message=r.get('message'),seat=r['seat'],playerSeat=r['playerSeat'],operation=index,stage=stage))
    if n:freeze(m,n,'request-'+str(rid))
    if negative and 'PayMana'in kind:
     check(n['payment']['remaining']==negative['payment']['remaining']and n['players']==negative['players'],'Forbidden unit preserves actual cost, pool and zones '+str(index));negative=None
    if n and counter and not counterPaid:
     a=next((a for a in n['stack']if a['name']=='Counterspell'and a['spell']),None)
     if a:check(len(a['payingMana'])==2,'Actual Counterspell UU payment');paid.append(dict(operation=index,name='Counterspell',sourceId=a['source'],mana=2,objects=a['payingMana'],requestId=rid));counterPaid=True;write(folder/'paid.json',paid)
    if kind=='InputPassPriority':
     if stage=='resolve'and not n['stack']and r['seat']==o.get('seat',1):
      complete(m,n,o)
      if index==len(plan['operations']):break
      o=plan['operations'][index]
     if stage=='resolve'and o.get('counter')and r['seat']==2 and n['stack']and not counter:
      pick='card:'+str(find(n,'Counterspell',2,'Hand')['id']);counter=True
     elif stage=='next'and not n['stack']and r['seat']==o.get('seat',1)and m['views'][0]['turnSeat']==o.get('seat',1)and n['phase']=='MAIN1'and n['turn']>=o.get('minTurn',1):
      before=n;source=None if o['action']=='wait'else find(n,o['source'],o.get('owner',o.get('seat',1)), 'Hand'if o['action']in ['cast','land']or o['action']=='special'and o.get('flag')=='foretelling'else'Battlefield',o['action']=='float')['id'];freeze(m,n,'before-'+str(index));counter=False;counterPaid=False;triedNegative=[];selected=False
      if o['action']=='wait':complete(m,n,o)
      else:pick='card:'+str(source);stage='resolve'
      if index==len(plan['operations']):break
    elif 'Mulligan'in kind:pick='keep'if'keep'in enabled else'ok'
    elif kind in ['InputAttack','InputBlock']:pick='ok'
    elif 'PayMana'in kind:
     if r['seat']==2:
      pick='card:'+str(find(n,'Island',2,'Battlefield',True)['id'])
     elif o.get('negative')and any(name not in triedNegative for name in(o['negative']if isinstance(o['negative'],list)else[o['negative']])):
      name=next(name for name in(o['negative']if isinstance(o['negative'],list)else[o['negative']])if name not in triedNegative);unit=next(x for x in n['payment']['units']if x['sourceName']==name);check(not unit['meetsRestriction'],'Actual wrong restriction arm rejects '+str(index)+' '+name);pick=next(k for k in enabled if k.startswith('mana:')and ':restricted:'+str(unit['source'])+':'in k);negative=n;triedNegative.append(name);freeze(m,n,'forbidden-payment-'+str(index)+'-'+str(len(triedNegative)))
     else:
      done=len(n['payment']['ability']['payingMana']);name=o['pay'][done];units=[u for u in n['payment']['units']if u['sourceName']==name and(o.get('payOwner')is None or next(c for c in allcards(n)if c['id']==u['source'])['owner']==o['payOwner'])]
      if units:
       u=units[0];check(u['meetsRestriction']and u['allowsShard'],'Exact real payment source eligible '+str(index));symbol=u['color'];base='mana:'+symbol;pick=next(k for k in enabled if k==base and not u['restricted']or ':restricted:'+str(u['source'])+':'in k)
      else:pick='card:'+str(find(n,name,o.get('payOwner',1),'Battlefield',True)['id'])
    elif kind=='OrderAbilities':pick='confirm-order'if'confirm-order'in enabled else next(k for k in enabled if k.startswith('order:'))
    elif kind=='SelectStackTarget':pick='ok'if'ok'in enabled else next(k for k in enabled if k.startswith('stack:'))
    elif kind=='InputSelectTargets':
     if'ok'in enabled:pick='ok'
     elif o.get('target'):pick='card:'+str(find(n,o['target'],o.get('targetOwner',1),'Battlefield')['id'])
     else:pick=next(k for k in enabled if k.startswith('player:'))
    elif kind=='Scry':pick='confirm-scry'if'confirm-scry'in enabled else next(k for k in enabled if k.startswith('scry-top:'))
    elif kind=='RevealCards':pick='confirm-cards'
    elif kind in ['dialog','Option','ChooseColor','ChooseOptions']:
     if'confirm-cards'in enabled:pick='confirm-cards'
     elif o.get('color')and any(a['label']==o['color']for a in opts):pick=next(a['id']for a in opts if a['label']==o['color'])
     elif r.get('message')=='getAbilityToPlay':
      wanted=o.get('choice');matches=[a for a in opts if wanted and wanted.lower()in a['label'].lower()];check(len(matches)==1,'Exact original native ability selector '+str(index)+' '+str(opts));pick=matches[0]['id']
     elif o.get('choice')and any(o['choice'].lower()in a['label'].lower()for a in opts):pick=next(a['id']for a in opts if o['choice'].lower()in a['label'].lower())
     elif'ok'in enabled:pick='ok'
     else:check(len(enabled)==1,'Single native option '+str(opts));pick=enabled[0]
    elif kind not in ['InputConfirm','InputConfirmMulligan']:raise AssertionError(('Unhandled real request',r))
    check(pick in enabled,'Actual enabled response '+str(index)+' '+kind+' '+pick+' '+str(opts));print(case,index,rid,kind,pick,flush=True);p.stdin.write(json.dumps(dict(type='respond',session=cfg['session'],seat=r['seat'],requestId=rid,option=pick))+'\n');p.stdin.flush()
   check(index==len(plan['operations']),'All bounded operations complete');status='PASS_BOUNDED';error=None
 except Exception:
  status='FAIL';error=traceback.format_exc();print(error,flush=True)
 finally:
  if p and p.poll()is None:p.kill();p.wait()
  result=dict(status=status,case=case,runtimeHash=runtime['runtimeHash'],baseRuntimeHash=runtime['runtimeHash'],sources=[ref(s)for s in sources],paid=paid,completedEndpoints=len(endpoints),endpoints=endpoints,checks=len(checks),uniqueChecks=len(set(checks)),assertions=checks,requests=requests,error=error)
  write(folder/'result.json',result);write(BASE/(case+'-latest.json'),dict(folder=folder.relative_to(ROOT).as_posix(),ref=ref(folder/'result.json'),status=status));write(folder/'artifact-manifest.json',[ref(s)for s in sorted(folder.rglob('*'))if s.is_file()and 'profile-'not in s.as_posix()and s.name!='artifact-manifest.json'])
 print(json.dumps(dict(case=case,status=status,folder=folder.relative_to(ROOT).as_posix(),paid=len(paid),mana=sum(x['mana']for x in paid),endpoints=len(endpoints))),flush=True);return status
if __name__=='__main__':
 for case in sys.argv[1:]or CASES:
  if run(case)=='FAIL':sys.exit(1)
