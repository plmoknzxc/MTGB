"""Original R03 GainControlVariant differences, using only real native actions.
Opening fixture can add AI seats; this does not certify four production clients.
"""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime'
CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'));OUT=ROOT/'.tools/control-variant-1010'
FIX=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['RoyalStateFixture.java','ControlVariantFixture.java']]
DECK=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
LANDS={'Plains','Island','Swamp','Mountain','Forest','Homeward Path'}
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def field(obs):return [c for pv in obs['players'] for c in pv['zones']['Battlefield']]
def find(obs,name,owner=None):return next(c for c in field(obs) if c['name']==name and (owner is None or c['owner']==owner))
def index(obs):return {c['id']:c for c in field(obs)}
def positions(case):
 if case=='owner':
  fields=[['Grizzly Bears','Glorious Anthem','Homeward Path'],['Giant Spider','Sol Ring','Island']];hand=['Switcheroo','Donate']
 elif case=='random':
  fields=[['Grizzly Bears','Mind Stone'],['Guardian Beast','Sol Ring','Island'],['Wall of Air','Glorious Anthem','Island'],['Runeclaw Bear','Mind Stone','Island']];hand=['Scrambleverse']
 else:
  fields=[['Inniaz, the Gale Force','Wind Drake','Mothrider Samurai','Sol Ring'],['Grizzly Bears','Mind Stone','Island'],['Wall of Roots','Sol Ring','Island'],['Runeclaw Bear','Mind Stone','Island']];hand=[]
 return [dict(Battlefield=f,Hand=hand if i==0 else ['Negate'],Library=['Island','Plains','Forest','Mountain'],tapped=[n for n in f if n not in LANDS and n!='Guardian Beast'] if case=='random' else []) for i,f in enumerate(fields)]
def run(case):
 assert case in ['owner','random','actor']
 f=OUT/'native'/case/str(time.time_ns());f.mkdir(parents=True);audit=f/'audit';audit.mkdir();classes=f/'classes';classes.mkdir()
 for path in FIX+[Path(__file__)]: (f/path.name).write_bytes(path.read_bytes())
 result=dict(case=case,folder=f.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],driver=sha(Path(__file__)),fixtures={p.name:sha(p) for p in FIX},producerClassesLib={p.relative_to(RT).as_posix():sha(p) for d in ['classes','lib'] for p in sorted((RT/d).rglob('*')) if p.is_file()},status='RUNNING');write(f/'result.json',result)
 checks=0;p=None;events=[]
 def check(ok,msg):
  nonlocal checks
  assert ok,msg;checks+=1
 def identities(before,after):
  old=index(before);new=index(after);check(set(old)==set(new),'exact original battlefield ID set')
  for id,c in old.items():
   for key in ['owner','timestamp','damage','counters']:check(new[id][key]==c[key],('unchanged field',id,key,c,new[id]))
 def controls(before,after,mapping):
  identities(before,after);new=index(after)
  for c in field(before):check(new[c['id']]['controller']==mapping.get(c['id'],c['controller']),('exact controller',c,new[c['id']]))
 try:
  with (f/'compile.log').open('w',encoding='utf8') as compilelog:subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*[str(f/p.name) for p in FIX]],check=True,stdout=compilelog,stderr=subprocess.STDOUT)
  with tempfile.TemporaryDirectory(prefix='mtgb-control-') as temp,(f/'stderr.log').open('w',encoding='utf8') as log,(f/'trace.jsonl').open('w',encoding='utf8') as trace:
   cfg=dict(session='control-'+case,seed=4,fixtureSeed=11,startingLife=30,firstPlayerSeat=1,decks=[DECK,DECK],positions=positions(case),auditDir=str(audit));write(f/'start.json',cfg)
   p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.ControlVariantFixture',str(RT/CFG['assets']),str(f/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
   q=queue.Queue()
   def reader():
    try:
     for line in p.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start()
   def observed(r):
    path=audit/(str(r['requestId'])+'.json');end=time.monotonic()+4
    while time.monotonic()<end:
     try:return json.loads(path.read_text())
     except (FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
    raise AssertionError('Missing observation '+str(r['requestId']))
   def save(m,name):write(f/(name+'-wire.json'),m)
   initial=None;stage=0;cast='';selected=[];attacks=[];source_id=0;seen=set();deadline=time.monotonic()+180
   for step in range(400):
    check(time.monotonic()<deadline,'deadline');m=q.get(timeout=35)
    if isinstance(m,Exception):raise m
    check(m is not None,'process exited');trace.write(json.dumps(m)+'\n');trace.flush();check(m['type'] not in ('fatal','ended','rejected'),m)
    if m['type']!='request':continue
    r=m['request']
    if r['requestId'] in seen:continue
    seen.add(r['requestId']);v=m['views'][0];opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];pick='ok';obs=observed(r) if initial is not None or kind=='InputPassPriority' else None
    if kind=='InputPassPriority' and r['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
     if initial is None:initial=obs;save(m,'initial')
     if case=='owner' and stage<3:
      if stage==1:
       controls(initial,obs,{find(initial,'Grizzly Bears')['id']:2,find(initial,'Giant Spider')['id']:1});write(f/'after-exchange-observation.json',obs);save(m,'after-exchange')
      if stage==2:
       controls(initial,obs,{find(initial,'Grizzly Bears')['id']:2,find(initial,'Giant Spider')['id']:1,find(initial,'Glorious Anthem')['id']:2});write(f/'after-donate-observation.json',obs);save(m,'after-donate')
      cast=['Switcheroo','Donate','Homeward Path'][stage];stage+=1;selected=[];source=find(obs,cast)['id'] if cast=='Homeward Path' else next(c['id'] for c in obs['players'][0]['zones']['Hand'] if c['name']==cast);pick='card:'+str(source)
     elif case=='random' and stage==0:
      cast='Scrambleverse';source_id=next(c['id'] for c in obs['players'][0]['zones']['Hand'] if c['name']==cast);pick='card:'+str(source_id);stage=1
     elif case=='actor':pick='ok'
     else:
      save(m,'resolved');write(f/'final-observation.json',obs);check(r['seat']==r['playerSeat']==1,'original actor restored');check([pv['life'] for pv in obs['players']]==[30]*len(obs['players']),'life unchanged');check(v['turn']==1 and v['turnSeat']==1,'original turn unchanged')
      if case=='owner':
       controls(initial,obs,{find(initial,'Glorious Anthem')['id']:2});check(find(obs,'Homeward Path')['tapped'],'real Homeward T paid');check(sum(c['tapped'] for c in field(obs))==9,'real5+3mana and sourceT');check(len(obs['players'][0]['zones']['Graveyard'])==2,'both actual spells in grave');check((find(obs,'Grizzly Bears')['power'],find(obs,'Grizzly Bears')['toughness'])==(2,2),'returned bear no foreign anthem');check((find(obs,'Giant Spider')['power'],find(obs,'Giant Spider')['toughness'])==(3,5),'returned spider receives owner anthem')
      else:
       calls=obs['randomCalls'];check(len(calls)==8 and all(e['bound']==4 for e in calls),'one real four-player RNG draw per original nonland');pool=[c for c in calls[0]['fieldBeforeTransfer'] if c['name'] not in LANDS];check(len(pool)==8,'exact original random eligible pool')
       mapping={c['id']:e['value']+1 for c,e in zip(pool,calls)};protected=find(initial,'Sol Ring',2);guardian=find(initial,'Guardian Beast');check(mapping[protected['id']]!=2 and mapping[guardian['id']]==2,'actual protected foreign destination while native guardian remains');mapping[protected['id']]=2
       controls(initial,obs,mapping)
       for e in calls:check(all(c['controller']==index(initial)[c['id']]['controller'] for c in e['fieldBeforeTransfer']),'all random assignments collected before transfer')
       check(all(not c['tapped'] for c in field(obs) if c['name'] not in LANDS),'actual untap child includes protected original card');check(sum(c['tapped'] for c in field(obs))==8,'actual6RR paid');check(any(c['id']==source_id for c in obs['players'][0]['zones']['Graveyard']),'real Scrambleverse grave ID')
      result.update(status='PASS_BOUNDED',checks=checks,requests=len(seen),choices=events);break
    elif case=='actor' and kind=='InputAttack':
     if initial is None:initial=obs;save(m,'initial')
     todo=[find(initial,n)['id'] for n in ['Inniaz, the Gale Force','Wind Drake','Mothrider Samurai'] if find(initial,n)['id'] not in attacks]
     if todo:pick=next(o['id'] for o in opts if o.get('attackerId')==todo[0] and o.get('label','').endswith('→ Player 2'));attacks.append(todo[0])
     else:pick='ok';stage=1
    elif case=='actor' and kind=='InputPassPriority' and stage==1 and not v['stackItems'] and len(events)==4:
     save(m,'resolved');write(f/'final-observation.json',obs);controls(initial,obs,{e['chosen']:e['recipient'] for e in events});check(set(obs['attackers'])==set(attacks),'same three actual native attackers');check(all(index(obs)[id]['controller']==1 and index(obs)[id]['tapped'] for id in attacks),'all attackers remain actor controlled and actually tapped');check(sum(c['tapped'] for c in field(obs))==3,'no invented mana payment');check([pv['life'] for pv in obs['players']]==[30]*4,'no combat damage before resolution');check(r['seat']==r['playerSeat']==1,'original actor restored after all four choices');result.update(status='PASS_BOUNDED',checks=checks,requests=len(seen),choices=events);break
    elif ('SelectEntities' in kind or 'SelectCards' in kind) and case=='actor':
     save(m,'choice-'+str(step));check(r['seat']==r['playerSeat']==1,'original activator chooses for every recipient')
     if r.get('selectedCount'):pick='ok'
     else:
      recipient=len(events)+1;donor=4 if recipient==1 else recipient-1;label=r.get('title','')+' '+r.get('message','')
      check('Player '+str(recipient) in label,('actual recipient title',r));candidates=[o for o in opts if o.get('card',{}).get('id',0)>0];expected={c['id'] for c in field(initial) if c['controller']==donor and c['name'] not in LANDS};check({o['card']['id'] for o in candidates}==expected,('exact original donor pool',recipient,expected,candidates));check(all(c['controller']==index(initial)[c['id']]['controller'] for c in field(obs)),'no premature individual transfer')
      chosen_names=['Runeclaw Bear','Sol Ring','Grizzly Bears','Sol Ring'];chosen=next(o for o in candidates if o['card']['name']==chosen_names[recipient-1]);pick=chosen['id'];events.append(dict(recipient=recipient,actor=1,donor=donor,chosen=chosen['card']['id'],requestId=r['requestId'],options=sorted(expected)));write(f/'choice-events.json',events)
    elif kind=='InputSelectTargets' and case=='owner':
     save(m,'target-'+str(step))
     if cast=='Switcheroo':
      wanted=[find(initial,n)['id'] for n in ['Grizzly Bears','Giant Spider']];todo=[id for id in wanted if id not in selected];pick=next(o['id'] for o in opts if o.get('card',{}).get('id')==todo[0]) if todo else 'ok'
      if todo:selected.append(todo[0])
     elif cast=='Donate':
      options=[o for o in opts if o['id'] not in ['ok','cancel']]
      cardopts=[o for o in options if o.get('card',{}).get('id',0)==find(initial,'Glorious Anthem')['id']]
      chosen=cardopts[0] if cardopts else next(o for o in options if o.get('label')=='Player 2');pick=chosen['id'] if not r.get('selectedCount') else 'ok'
     else:raise AssertionError(('unexpected target',r))
    elif kind=='dialog' and cast=='Homeward Path':pick=next(o['id'] for o in opts if 'creatures they own' in o['label']);save(m,'owner-ability')
    elif 'Mulligan' in kind:pick='keep' if 'keep' in ids else 'ok'
    elif 'PayMana' in kind:pick='ok';save(m,'payment-'+str(step))
    elif kind=='RevealCards':pick='confirm-cards'
    elif kind=='OrderAbilities':pick='confirm-order' if 'confirm-order' in ids else next(i for i in ids if i.startswith('order:'))
    elif kind=='dialog':pick=ids[0]
    elif 'ok' not in ids:raise AssertionError(('unhandled',r))
    check(pick in ids and not next(o for o in opts if o['id']==pick).get('disabled'),('legal actual response',r,pick));print(case,step,kind,r['seat'],pick,flush=True)
    p.stdin.write(json.dumps(dict(type='respond',session=cfg['session'],seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
   else:raise AssertionError('step limit')
 except Exception:result.update(status='FAIL',checks=checks,error=traceback.format_exc())
 finally:
  if p is not None and p.poll() is None:p.kill();p.wait()
 write(f/'result.json',result);write(OUT/(case+'-result.json'),result);print(result['status'],case,result.get('error',''),flush=True);return result
if __name__=='__main__':
 for case in sys.argv[1:] or ['owner','random','actor']:run(case)
