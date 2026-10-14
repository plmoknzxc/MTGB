"""R44.01 real player-counter operations with read-only native audit."""
import json,hashlib,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'));OUT=ROOT/'.tools/player-counters-1009';CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIX=[ROOT/'Tools/ForgeBridge/tests/RoyalStateFixture.java',ROOT/'Tools/ForgeBridge/tests/PlayerCounterFixture.java'];sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),*[str(p) for p in FIX]],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
def scenario(case):
 if case=='poison':return dict(Hand=['Ichor Rats','Leeches'],Battlefield=['Vorinclex, Monstrous Raider','Winding Constrictor']),dict(Hand=['Opt'],Battlefield=['Winding Constrictor']),[dict(name='Ichor Rats'),dict(name='Leeches',target='player:1')]
 if case=='multiply-proliferate':
  actions=[dict(name='Attune with Aether'),dict(name='Aetheric Amplifier',zone='battlefield',mode='player'),dict(name='Contentious Plan',choose=[True,False]),dict(name='Contentious Plan',choose=[True,True]),dict(name='Vorel of the Hull Clade',zone='battlefield',target='Grizzly Bears'),dict(name='Invigorating Surge',target='Grizzly Bears'),dict(name='Solemnity'),dict(name='Contentious Plan',choose=[True,True]),dict(name='Attune with Aether')]
  return dict(Hand=[a['name'] for a in actions if 'zone' not in a],Battlefield=['Vorinclex, Monstrous Raider','Tekuthal, Inquiry Dominus','Ezuri, Stalker of Spheres','Aetheric Amplifier','Vorel of the Hull Clade','Grizzly Bears']),dict(Hand=['Opt']),actions
 if case=='radiation':return dict(Hand=['Nuclear Fallout'],Battlefield=['Vorinclex, Monstrous Raider'],Library=['Island','Plains','Opt','Shock','Forest']),dict(Hand=['Opt'],Library=['Mountain','Plains','Opt','Shock','Forest']),[dict(name='Nuclear Fallout',x=2)]
 if case=='multitype-stale':return dict(Hand=['Contentious Plan','Ephemerate'],Battlefield=['Vorinclex, Monstrous Raider','Tekuthal, Inquiry Dominus','Ezuri, Stalker of Spheres','Aetheric Amplifier','Vorel of the Hull Clade','Grizzly Bears']),dict(Hand=['Opt']),[dict(name='Aetheric Amplifier',zone='battlefield',mode='player'),dict(name='Contentious Plan',choose=[True,True]),dict(name='Vorel of the Hull Clade',zone='battlefield',target='Grizzly Bears',interrupt='Ephemerate')]
 if case=='proliferate-mass':return dict(Hand=['Contentious Plan'],Battlefield=['Vorinclex, Monstrous Raider','Tekuthal, Inquiry Dominus','Ezuri, Stalker of Spheres','Grizzly Bears']),dict(Hand=['Opt']),[dict(name='Contentious Plan',choose=[True,False])]
 if case=='radiation-zero-block':return dict(Hand=['Nuclear Fallout','Solemnity','Nuclear Fallout']),dict(Hand=['Opt']),[dict(name='Nuclear Fallout',x=0),dict(name='Solemnity'),dict(name='Nuclear Fallout',x=2)]
 raise ValueError(case)
def run(case):
 f=OUT/'native'/case/str(time.time_ns());f.mkdir(parents=True);audit=f/'audit';audit.mkdir();checks=0;captures=[];actions=[]
 producer={p.relative_to(RT).as_posix():sha(p) for d in ['classes','lib'] for p in sorted((RT/d).rglob('*')) if p.is_file()}
 meta=dict(case=case,runtimeHash=CFG['runtimeHash'],producerClassesLib=producer,driver=sha(Path(__file__)),fixtures=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p)) for p in FIX],folder=f.relative_to(ROOT).as_posix())
 for p in FIX+[Path(__file__)]: (f/p.name).write_bytes(p.read_bytes())
 def check(ok,msg):
  nonlocal checks
  assert ok,msg;checks+=1
 def finish(status,note):
  r=dict(**meta,status=status,checks=checks,captures=captures,note=note);(f/'result.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');(OUT/(case+'-result.json')).write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');return r
 with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(f/'trace.jsonl').open('w',encoding='utf-8') as trace,(f/'engine.log').open('w',encoding='utf-8') as log:
  own,other,actions=scenario(case)
  config=dict(session=case,seed=4,startingLife=30,firstPlayerSeat=1,decks=[DECK,DECK],own=own,other=other,auditDir=str(audit))
  if case in ('multiply-proliferate','multitype-stale','proliferate-mass'):config['bearCounters']=True
  if case in ('multitype-stale','proliferate-mass'):config['playerMultitype']=True
  start=Path(temp)/'start.json';start.write_text(json.dumps(config),encoding='utf-8');(f/'start.json').write_text(json.dumps(config,ensure_ascii=False,indent=2),encoding='utf-8')
  proc=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.PlayerCounterFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf-8')
  q=queue.Queue()
  def reader():
   for line in proc.stdout:q.put(json.loads(line))
   q.put(None)
  threading.Thread(target=reader,daemon=True).start()
  def read():
   m=q.get(timeout=35);check(m is not None,'Native process ended');trace.write(json.dumps(m)+'\n');trace.flush();return m
  def reply(r,opt,seat=None):proc.stdin.write(json.dumps(dict(type='respond',session=case,seat=seat or r['seat'],requestId=r['requestId'],option=opt))+'\n');proc.stdin.flush()
  def observe(r):
   p=audit/(str(r['requestId'])+'.json');end=time.monotonic()+4
   while not p.exists() and time.monotonic()<end:time.sleep(.02)
   check(p.exists(),'Missing actual native observer');return json.loads(p.read_text())
  def save(m,name):
   (f/(name+'-wire.json')).write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');captures.append(name)
  at=0;working=False;current=None;initial=None;orders=0;prolifs=0;prolif_requests=set();completed_prolifs=0;rad_seen=set();last_rad={};previous=None;mass_count=0
  def counter_checks(o,energy,power_counter,charge):
   check([p['counters'].get('ENERGY',0) for p in o['players']]==[energy,0],'Wrong energy/player identity')
   bear=next(c for c in o['players'][0]['cards'] if c['name']=='Grizzly Bears' and c['zone']=='Battlefield')
   check(bear['counters']=={'P1P1':power_counter,'CHARGE':charge},'Wrong all-type/specific-type counters '+str(bear))
   check((bear['power'],bear['toughness'])==(2+power_counter,2+power_counter),'Counter power/toughness not applied')
  def finished_action(m,o):
   if case=='poison':
    if at==0:check([p['counters'].get('POISON',0) for p in o['players']]==[3,4],'Wrong simultaneous player-specific poison replacement totals');check(orders==2,'Both affected players must choose replacement order');check([p['life'] for p in o['players']]==[30,30],'Counter addition changed life')
    if at==1:check([p['counters'].get('POISON',0) for p in o['players']]==[0,4],'Signed poison removal changed wrong player');check([p['life'] for p in o['players']]==[27,30],'Leeches damage must use prior3 poison');check(sum(c['tapped'] for c in m['views'][0]['players'][0]['battlefield'])==6,'Actual3+3 spell payment missing')
   elif case=='multiply-proliferate':
    expected=[(4,1,2),(12,1,2),(14,3,4),(18,7,8),(18,21,24),(18,69,24),(18,69,24),(18,69,24),(18,69,24)][at];counter_checks(o,*expected)
    check(completed_prolifs==({2:2,3:4,4:4,5:4,6:4,7:6,8:6}.get(at,0)),'Proliferate replacement/event iteration mismatch')
    if current['name']=='Contentious Plan':
     before_ids={c['id'] for c in previous['players'][0]['cards'] if c['zone']=='Hand'};after_ids={c['id'] for c in o['players'][0]['cards'] if c['zone']=='Hand'}
     expected_ids=[c['id'] for c in previous['players'][0]['cards'] if c['zone']=='Library'][:3]
     check(after_ids-before_ids==set(expected_ids),'Two real Ezuri events (including zero entities) plus spell must draw exactly native top3 IDs')
    if current['name']=='Attune with Aether':
     new={c['id'] for c in o['players'][0]['cards'] if c['zone']=='Hand'}-{c['id'] for c in previous['players'][0]['cards'] if c['zone']=='Hand'}
     check(len(new)==1 and next(c for c in o['players'][0]['cards'] if c['id'] in new)['name']=='Mountain','Basic search did not resolve alongside energy action')
    check([p['life'] for p in o['players']]==[30,30],'Non-damage counter actions changed life')
   elif case=='radiation':check([p['counters'].get('RAD',0) for p in o['players']]==[4,4],'Nuclear Fallout X2 source-doubled rad4 must reach both players')
   elif case=='multitype-stale':
    check([p['counters'] for p in o['players']]==[({'ENERGY':6,'POISON':3} if at==0 else {'ENERGY':10,'POISON':7}),{'ENERGY':1,'POISON':1}],'Multitype player all-kind doubling/proliferating or unchosen player result wrong')
    bear=next(c for c in o['players'][0]['cards'] if c['name']=='Grizzly Bears' and c['zone']=='Battlefield')
    check(bear['counters']==[{'P1P1':1,'CHARGE':2},{'P1P1':5,'CHARGE':6},{}][at],'Multitype permanent/stale Vorel outcome wrong')
    if at==2:
     old=next(c for c in previous['players'][0]['cards'] if c['name']=='Grizzly Bears' and c['zone']=='Battlefield');check(bear['id']==old['id'] and bear['timestamp']!=old['timestamp'],'Blink must create a different native object timestamp');check((bear['power'],bear['toughness'])==(2,2),'Stale multiply hit new object')
     check(current.get('interrupted') and any(c['id']==current['interruptId'] and c['zone']=='Exile' for c in o['players'][0]['cards']),'Real blink paid/resolved/rebound exile missing')
   elif case=='radiation-zero-block':check(not any(p['counters'] for p in o['players']),'Zero/block radiation added counters')
   elif case=='proliferate-mass':
    check([p['counters'] for p in o['players']]==[{'ENERGY':4,'POISON':3},{'ENERGY':1,'POISON':1}],'Mass cycling changed unconfirmed native counters/unchosen opponent')
    bear=next(c for c in o['players'][0]['cards'] if c['name']=='Grizzly Bears' and c['zone']=='Battlefield');check(bear['counters']=={'P1P1':3,'CHARGE':4},'Mass cycling plus actual mixed increment wrong');check(mass_count==4 and completed_prolifs==2,'Actual Mine/All/None and repeated Proliferate missing')
    before_ids={c['id'] for c in previous['players'][0]['cards'] if c['zone']=='Hand'};after_ids={c['id'] for c in o['players'][0]['cards'] if c['zone']=='Hand'};top3={c['id'] for c in [c for c in previous['players'][0]['cards'] if c['zone']=='Library'][:3]};check(after_ids-before_ids==top3,'Mass flow both Proliferate events plus Plan draw missing')
   check(current['cardId'] in [c['id'] for c in o['players'][0]['cards'] if c['zone']==('Battlefield' if current.get('zone') or current['name'] in ('Ichor Rats','Solemnity') else 'Graveyard')],'Actual source ID ended in wrong zone')
  try:
   for step in range(1400):
    m=read();check(m['type'] not in ('fatal','ended','rejected'),m)
    if m['type']!='request':continue
    r=m['request'];v=m['views'][0];ps=v['players'];kind=r['kind'];ids=[o['id'] for o in r['options']];pick=None
    if kind=='InputPassPriority' and v['stackItems'] and current and current.get('interrupt') and not current.get('interrupted') and r['seat']==1:
     card=next(c for c in ps[0]['hand'] if c['name']==current['interrupt']);pick='card:'+str(card['id']);check(pick in ids,'Actual response blink not legal');current['interruptId']=card['id'];current['interrupted']=True;save(m,'old-multiply-on-stack');observe(r)
    elif kind=='InputPassPriority' and not v['stackItems']:
     if working:
      save(m,'after-'+str(at));o=observe(r);check(r['seat']==1 and r['playerSeat']==1,'Next own operator not restored');finished_action(m,o);at+=1;working=False
      if at==len(actions) and case!='radiation':return finish('PASS','Actual paid spells/activations, exact original source IDs, counters/life/zone results and next operator. '+case+'. Native only.')
     if case=='radiation' and at==len(actions):
      o=observe(r)
      for p in o['players']:
       n=p['counters'].get('RAD',0)
       if n==2 and p['seat'] not in rad_seen:
        check(v['phase']=='MAIN1','Natural radiation fired in wrong phase');check(p['life']==28,'Radiation nonland2 life loss wrong')
        base=initial['players'][p['seat']-1];lib=[c for c in base['cards'] if c['zone']=='Library'];grave=[c for c in p['cards'] if c['zone']=='Graveyard' and c['name']!='Nuclear Fallout']
        check([c['id'] for c in grave]==[c['id'] for c in lib[1:5]],'Wrong natural radiation mill identities/order')
        check(lib[0]['id'] in [c['id'] for c in p['cards'] if c['zone']=='Hand'],'Natural draw preceding radiation missing exact card')
        rad_seen.add(p['seat']);save(m,'radiation-seat-'+str(p['seat']))
      if rad_seen=={1,2}:return finish('PASS','Actual X2 Fallout pays2BB; source doubles both rad to4. Each natural owner precombat MAIN1 trigger mills exact next4 IDs after draw, two nonlands lose2 life and remove2 rad, ends28life/rad2 both. Native only.')
     if at<len(actions) and r['seat']==1 and v['phase']=='MAIN1':
      current=actions[at];c=next(c for c in ps[0][current.get('zone','hand')] if c['name']==current['name']);pick='card:'+str(c['id']);check(pick in ids,'Actual spell/activation not offered');current['cardId']=c['id'];current['prolifStart']=completed_prolifs;working=True;save(m,'before-'+str(at));previous=observe(r)
      if initial is None:initial=previous
     else:pick='ok'
    elif kind=='OrderAbilities' and r.get('selectionPurpose')=='replacementOrder':
     if r.get('selectedCount'):
      pick='confirm-order';orders+=1;save(m,'order-confirm-'+str(orders));observe(r)
     else:
      save(m,'order-'+str(orders));observe(r);options=r['options'];want='Vorinclex' if r['seat']==1 else 'Winding Constrictor';matching=[x for x in options if want in x['label']];check(len(matching)==1,'Exact source order candidate missing');pick=matching[0]['id']
      reply(r,pick,3-r['seat']);bad=read();check(bad['type']=='rejected' and bad['error']=='wrong-seat','Counter order wrong physical seat accepted')
    elif 'Target' in kind and current and current.get('target'):
     target='player:'+str(next(p for p in ps if p['seat']==int(current['target'].split(':')[1]))['targetId']) if current['target'].startswith('player:') else 'card:'+str(next(c for c in ps[0]['battlefield'] if c['name']==current['target'])['id'])
     pick='ok' if r.get('selectedCount') else target;check(pick in ids,'Target/confirmation missing from native options')
    elif kind=='OrderAbilities':pick='confirm-order' if 'confirm-order' in ids else next(i for i in ids if i.startswith('order:'))
    elif 'SelectEntities' in kind and current and current.get('choose'):
     if case=='proliferate-mass' and completed_prolifs==0 and mass_count<=3:
      labels=['MINE','ALL','CLEAR ALL','MINE'];cancel=next(o for o in r['options'] if o['id']=='cancel');check(cancel['label'].upper()==labels[mass_count],'Native mass select next mode label incorrect '+cancel['label'])
      selected={o['id'] for o in r['options'] if o.get('selected')};bear='card:'+str(next(c for c in ps[0]['battlefield'] if c['name']=='Grizzly Bears')['id']);expected=[set(),{bear},{bear,'player:'+str(ps[0]['targetId']),'player:'+str(ps[1]['targetId'])},set()][mass_count];check(selected==expected,'Native mass-select exact identities wrong');save(m,'mass-'+str(mass_count))
      if mass_count<3:mass_count+=1;reply(r,'cancel');continue
      mass_count=4
     choose=current['choose'][completed_prolifs-current['prolifStart']]
     want=['player:'+str(ps[0]['targetId']),'card:'+str(next(c for c in ps[0]['battlefield'] if c['name']=='Grizzly Bears')['id'])] if choose else []
     selected=[o['id'] for o in r['options'] if o.get('selected')];remaining=[i for i in want if i not in selected]
     check(r['seat']==r['playerSeat']==1 and r.get('minCount')==0,'Mixed proliferate permission/minimum wrong')
     if not r.get('selectedCount'):save(m,'proliferate-'+str(completed_prolifs))
     if remaining:pick=remaining[0]
     else:pick='ok';completed_prolifs+=1
    elif ('SelectCards' in kind or 'SelectEntities' in kind) and current and current['name']=='Attune with Aether':
     pick=('confirm-cards' if 'confirm-cards' in ids else 'ok') if r.get('selectedCount') else next(o['id'] for o in r['options'] if o.get('card',{}).get('name')=='Mountain')
    elif kind in ('dialog','ChooseModes') and current and current.get('mode'):
     matching=[o for o in r['options'] if 'each kind of counter you have' in o['label'] or current['zone']=='battlefield' and 'Choose one' in o['label']]
     if r.get('selectedCount'):pick='confirm-modes'
     else:check(len(matching)==1,'Actual Amplifier ability/mode absent '+str(r));pick=matching[0]['id']
     save(m,'amplifier-mode-'+str(step))
    elif kind=='ChooseNumber' and current and 'x' in current:
     pick='number-plus' if r.get('selectedValue',0)<current['x'] else 'confirm-number';save(m,'choose-x-'+str(at)+'-'+str(r.get('selectedValue',0)))
    elif kind=='RevealCards':pick='confirm-cards'
    elif 'PayMana' in kind:pick='ok'
    elif 'ok' in ids:pick='ok'
    else:raise AssertionError((kind,r['message'],[(o['id'],o['label']) for o in r['options']]))
    check(pick in ids,(kind,pick,ids));print(case,step,kind,r['seat'],r['playerSeat'],pick,flush=True);reply(r,pick)
   raise AssertionError('Native step limit')
  except Exception:finish('FAIL',traceback.format_exc());raise
  finally:
   if proc.poll() is None:proc.kill();proc.wait()
if __name__=='__main__':
 for case in sys.argv[1:] or ['poison']:print(run(case)['status'],case,flush=True)
