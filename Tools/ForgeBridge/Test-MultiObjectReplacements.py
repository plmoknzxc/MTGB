"""R44.02 incremental native operations; immutable attempts and independent observations."""
import json,hashlib,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'));OUT=ROOT/'.tools/multi-object-replacements-1009';OUT.mkdir(exist_ok=True);CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIX=[ROOT/'Tools/ForgeBridge/tests/RoyalStateFixture.java',ROOT/'Tools/ForgeBridge/tests/MultiObjectReplacementFixture.java'];sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),*[str(p) for p in FIX]],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
def run(case):
 f=OUT/'native'/case/str(time.time_ns());f.mkdir(parents=True);audit=f/'audit';audit.mkdir();checks=0;captures=[]
 producer={p.relative_to(RT).as_posix():sha(p) for d in ['classes','lib'] for p in sorted((RT/d).rglob('*')) if p.is_file()}
 meta=dict(case=case,runtimeHash=CFG['runtimeHash'],producerClassesLib=producer,driver=sha(Path(__file__)),fixtures=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p)) for p in FIX],folder=f.relative_to(ROOT).as_posix())
 for p in FIX+[Path(__file__)]: (f/p.name).write_bytes(p.read_bytes())
 def check(ok,msg):
  nonlocal checks
  assert ok,msg;checks+=1
 def finish(status,note):
  r=dict(**meta,status=status,checks=checks,captures=captures,note=note);(f/'result.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');(OUT/(case+'-result.json')).write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');return r
 if case in ('spark','spark-skip'):
  actions=[dict(name='Spark Double'),dict(name='Spark Double'),dict(name='Spark Double',decline=True),dict(name='Eerie Interlude')]
  if case=='spark-skip':actions=[dict(name='Spark Double'),dict(name='Eerie Interlude')]
  own=dict(Hand=[a['name'] for a in actions],Battlefield=['Grizzly Bears','Runeclaw Bear','Jace Beleren','Hardened Scales']);other=dict(Hand=['Opt'])
 elif case=='tokens':
  actions=[dict(name='Gilded Goose')];own=dict(Hand=['Gilded Goose'],Battlefield=['Academy Manufactor']);other=dict(Hand=['Crafty Cutpurse'],Battlefield=['Chatterfang, Squirrel General','Doubling Season']+['Island']*4)
 elif case=='arc':
  actions=[dict(name='Arc Trail')];own=dict(Hand=['Arc Trail'],Battlefield=['Wall of Roots','Soulfire Grand Master','Torbran, Thane of Red Fell']);other=dict(Hand=['Opt'])
 elif case=='each':
  actions=[dict(name='Band Together')];own=dict(Hand=['Band Together'],Battlefield=["Healer's Hawk",'Grizzly Bears']);other=dict(Hand=['Opt'],Battlefield=['Wall of Roots'])
 elif case=='each-self':
  actions=[dict(name='Wave of Reckoning')];own=dict(Hand=['Wave of Reckoning'],Battlefield=["Healer's Hawk",'Grizzly Bears']);other=dict(Hand=['Opt'],Battlefield=['Wall of Roots'])
 elif case=='each-cross':
  actions=[dict(name='Grim Contest')];own=dict(Hand=['Grim Contest'],Battlefield=["Healer's Hawk"]);other=dict(Hand=['Opt'],Battlefield=['Wall of Roots'])
 elif case=='each-prevent':
  actions=[dict(name='Band Together')];own=dict(Hand=['Band Together'],Battlefield=["Healer's Hawk",'Grizzly Bears']);other=dict(Hand=['Opt'],Battlefield=['Wall of Roots','Hallowed Healer'])
 elif case=='each-stale':
  actions=[dict(name='Band Together')];own=dict(Hand=['Band Together'],Battlefield=["Healer's Hawk",'Grizzly Bears']);other=dict(Hand=['Ephemerate'],Battlefield=['Wall of Roots','Plains'])
 elif case=='each-triggers':
  actions=[dict(name='Band Together')];own=dict(Hand=['Band Together'],Battlefield=["Healer's Hawk",'Grizzly Bears',"Ajani's Pridemate"]);other=dict(Hand=['Opt'],Battlefield=['Ripjaw Raptor'],Library=['Island','Plains','Mountain'])
 else:raise ValueError(case)
 with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(f/'trace.jsonl').open('w',encoding='utf-8') as trace,(f/'engine.log').open('w',encoding='utf-8') as log:
  config=dict(session=case,seed=4,startingLife=30,firstPlayerSeat=1,decks=[DECK,DECK],own=own,other=other,auditDir=str(audit));start=Path(temp)/'start.json';start.write_text(json.dumps(config),encoding='utf-8');(f/'start.json').write_text(json.dumps(config,ensure_ascii=False,indent=2),encoding='utf-8')
  proc=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.MultiObjectReplacementFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf-8')
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
   check(p.exists(),'Missing native read-only observer');return json.loads(p.read_text())
  def save(m,name):
   (f/(name+'-wire.json')).write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');captures.append(name)
  def cards(o):return o['players'][0]['cards']
  at=0;working=False;current=None;initial=None;copy_ids=[];return_choices=0;return_started=False;before=None;interrupted=False;target_round=0
  def card(o,i):return next(c for c in cards(o) if c['id']==i)
  def finish_action(m,o):
   if case=='tokens':
    own_tokens=[c for c in o['players'][0]['cards'] if c['id'] not in {c['id'] for c in initial['players'][0]['cards']} and c['zone']=='Battlefield'];check(not own_tokens,'Stolen original token remained under original controller')
    new=[c for c in o['players'][1]['cards'] if c['id'] not in {c['id'] for c in initial['players'][1]['cards']} and c['zone']=='Battlefield'];counts={n:sum(c['name']==n+' Token' for c in new) for n in ['Food','Clue','Treasure','Squirrel']};check(counts=={'Food':2,'Clue':2,'Treasure':2,'Squirrel':6},'Four real token policies produced wrong controller/type/amount '+str(counts));check(len({c['id'] for c in new})==12,'Tokens did not have twelve distinct IDs');check(card(o,current['cardId'])['zone']=='Battlefield','Actual paid Goose missing');check(interrupted,'Cutpurse was not actually cast');check(sum(c['tapped'] for c in o['players'][0]['cards'])==1 and sum(c['tapped'] for c in o['players'][1]['cards'])==4,'Goose/Cutpurse actual G/3U payment wrong');save(m,'token-result');return
   if case=='arc':
    wall=next(c for c in cards(o) if c['name']=='Wall of Roots');check(wall['damage']==2 and wall['zone']=='Battlefield','Arc first target exact2 damage wrong');check([p['life'] for p in o['players']]==[35,27],'Arc second original1+Torbran2 and lifelink aggregate5 wrong');check(card(o,current['cardId'])['zone']=='Graveyard','Arc paid source final zone wrong');check(sum(c['tapped'] for c in cards(o))==2,'Arc actual1R missing');save(m,'arc-result');return
   if case in ('each','each-prevent'):
    wall=next(c for c in o['players'][1]['cards'] if c['name']=='Wall of Roots');check(wall['damage']==(3 if case=='each' else 1) and wall['zone']=='Battlefield','EachDamage exact per-source1+2 aggregate/prevent2 wrong');check([p['life'] for p in o['players']]==([31,30] if case=='each' else [30,30]),"EachDamage per-source lifelink Hawk1 (not Bear2) or prevented Hawk damage wrong");check(card(o,current['cardId'])['zone']=='Graveyard','EachDamage paid source final zone wrong');check(sum(c['tapped'] for c in cards(o))==3,'Band Together actual2G missing');save(m,'each-result');return
   if case in ('each-self','each-cross'):
    hawk=next(c for c in cards(o) if c['originalName']=="Healer's Hawk");wall=next(c for c in o['players'][1]['cards'] if c['name']=='Wall of Roots');check(hawk['zone']=='Graveyard','EachDamage Hawk should die');check(wall['zone']=='Battlefield' and wall['damage']==(0 if case=='each-self' else 1),'Self/cross recipient matrix incorrect');check([p['life'] for p in o['players']]==[31,30],'Self/cross Hawk exact1 lifelink with LKI wrong');check(card(o,current['cardId'])['zone']=='Graveyard','Real source final zone wrong');check(sum(c['tapped'] for c in cards(o))==(5 if case=='each-self' else 3),'Real self/cross payment wrong')
    if case=='each-self':check(next(c for c in cards(o) if c['originalName']=='Grizzly Bears')['zone']=='Graveyard','Self matrix omitted Bear')
    save(m,'each-result');return
   if case=='each-stale':
    wall=next(c for c in o['players'][1]['cards'] if c['name']=='Wall of Roots');old=next(c for c in initial['players'][1]['cards'] if c['name']=='Wall of Roots');check(wall['id']==old['id'] and wall['timestamp']!=old['timestamp'] and wall['damage']==0,'EachDamage old target hit same-ID new object');check([p['life'] for p in o['players']]==[30,30],'Invalid EachDamage recipient still caused source lifelink');check(interrupted and next(c for c in o['players'][1]['cards'] if c['originalName']=='Ephemerate')['zone']=='Exile','Real W blink and rebound missing');check(sum(c['tapped'] for c in cards(o))==3 and sum(c['tapped'] for c in o['players'][1]['cards'])==1,'Real Band/response payments absent');save(m,'each-result');return
   if case=='each-triggers':
    raptor=next(c for c in o['players'][1]['cards'] if c['name']=='Ripjaw Raptor');check(raptor['damage']==3 and raptor['zone']=='Battlefield','Two-source recipient aggregate damage wrong');check([p['life'] for p in o['players']]==[31,30],'Source Hawk only lifelink wrong');pride=next(c for c in cards(o) if c['name']=="Ajani's Pridemate");check(pride['counters']=={'P1P1':1},'Aggregate lifelink event must trigger Pridemate exactly once');expected=[c['id'] for c in initial['players'][1]['cards'] if c['zone']=='Library'][:1];before_hand={c['id'] for c in initial['players'][1]['cards'] if c['zone']=='Hand'};after_hand={c['id'] for c in o['players'][1]['cards'] if c['zone']=='Hand'};check(after_hand-before_hand==set(expected),'One simultaneous aggregate Enrage trigger must draw exact original top1 ID');check(card(o,current['cardId'])['zone']=='Graveyard' and sum(c['tapped'] for c in cards(o))==3,'Actual Band payment/source wrong');save(m,'each-result');return
   if current['name']=='Spark Double':
    c=card(o,current['cardId'])
    if current.get('decline'):check(c['zone']=='Graveyard' and c['originalName']=='Spark Double' and not c['cloned'],'Optional refusal must leave actual printed0/0 in graveyard, not lose earlier copies')
    else:
     check(c['name']=='Grizzly Bears' and c['originalName']=='Spark Double' and c['zone']=='Battlefield','Paid Spark did not enter as exact bear copy');check(c['counters']=={'P1P1':2} and (c['power'],c['toughness'])==(4,4),'Spark additional1 plus HardenedScales1 must not copy original7');copy_ids.append(c['id'])
   else:
    check(all(card(o,i)['zone']=='Exile' for i in targets),'Interlude must exile all original target IDs once');check(card(o,current['cardId'])['zone']=='Graveyard','Paid Interlude did not resolve to graveyard');save(m,'all-exiled');check(not return_choices,'Return occurred before natural end step')
   check([p['life'] for p in o['players']]==[30,30],'Copy operation changed life');check(card(o,source_id)['counters']=={'P1P1':7},'Original source counters changed')
  try:
   for step in range(650):
    m=read();check(m['type'] not in ('fatal','ended','rejected'),m)
    if m['type']!='request':continue
    r=m['request'];v=m['views'][0];ps=v['players'];kind=r['kind'];ids=[o['id'] for o in r['options']];pick=None
    if case=='tokens' and working and not interrupted and kind=='InputPassPriority' and v['stackItems'] and r['seat']==2:
     c=next(c for c in m['views'][1]['players'][1]['hand'] if c['name']=='Crafty Cutpurse');pick='card:'+str(c['id']);check(pick in ids,'Actual flash Cutpurse not legal');interrupted=True;save(m,'cutpurse-response');observe(r)
    elif case=='each-prevent' and working and not interrupted and kind=='InputPassPriority' and v['stackItems'] and r['seat']==2:
     c=next(c for c in ps[1]['battlefield'] if c['name']=='Hallowed Healer');pick='card:'+str(c['id']);check(pick in ids,'Actual Healer activation not legal');interrupted=True;save(m,'healer-response');observe(r)
    elif case=='each-stale' and working and not interrupted and kind=='InputPassPriority' and v['stackItems'] and r['seat']==2:
     c=next(c for c in m['views'][1]['players'][1]['hand'] if c['name']=='Ephemerate');pick='card:'+str(c['id']);check(pick in ids,'Actual response Ephemerate not legal');interrupted=True;save(m,'blink-response');observe(r)
    elif kind=='InputPassPriority' and not v['stackItems']:
     o=observe(r)
     if initial is None:
      initial=o;save(m,'initial')
      if case.startswith('spark'):source_id=next(c['id'] for c in cards(o) if c['name']=='Grizzly Bears' and c['zone']=='Battlefield');pw_id=next(c['id'] for c in cards(o) if c['name']=='Jace Beleren');moving_id=next(c['id'] for c in cards(o) if c['name']=='Runeclaw Bear')
     if working:save(m,'after-'+str(at));finish_action(m,o);at+=1;working=False
     if at==len(actions) and not case.startswith('spark'):check(r['seat']==r['playerSeat']==1,'Next original actor not restored');return finish('PASS','Real paid native '+case+'; exact original source/target IDs, damage or token matrix, per-source/controller results and next operator. No live network proof.')
     if case=='spark-skip' and at==len(actions) and return_started:
      check(v['phase']=='END_OF_TURN' and r['seat']==r['playerSeat']==1,'Skipped copy return did not restore original end-step actor');c=card(o,copy_ids[0]);check(c['zone']=='Graveyard' and not c['cloned'] and c['timestamp']!=card(before,c['id'])['timestamp'],'Skipped returned printed0/0 must die as new object');check(card(o,moving_id)['zone']=='Battlefield' and card(o,moving_id)['timestamp']!=card(before,moving_id)['timestamp'],'Optional skip lost other simultaneous return');check(card(o,source_id)['counters']=={'P1P1':7} and [p['life'] for p in o['players']]==[30,30],'Skip changed unaffected original state');save(m,'all-returned');return finish('PASS','Two real paid spells; optional copy declined during simultaneous2 return; exact printed0/0 to graveyard, other original bear returned new timestamp, source7 counters unchanged, next controller restored.')
     if at==len(actions) and return_choices==2:
      check(v['phase']=='END_OF_TURN','Simultaneous return did not happen at natural end step');check(r['seat']==r['playerSeat']==1,'Next original operator not restored')
      returned=[card(o,i) for i in copy_ids];check({c['name'] for c in returned}=={'Grizzly Bears','Jace Beleren'},'Return copy choices do not produce both source types')
      for c in returned:
       check(c['zone']=='Battlefield' and c['originalName']=='Spark Double' and c['timestamp']!=card(before,c['id'])['timestamp'],'Actual new returned object identity/timestamp wrong')
       check('Legendary' not in c['type'],'Spark PW must be nonlegendary with original surviving')
       check(c['counters']==({'P1P1':2} if c['name']=='Grizzly Bears' else {'LOYALTY':4}),'Postcopy conditional counter branch missing/doubled/copied original counter')
      check(card(o,pw_id)['counters']=={'LOYALTY':8} and card(o,pw_id)['zone']=='Battlefield','Original PW was altered or legend ruled');check(card(o,moving_id)['zone']=='Battlefield','Simultaneous third object lost');check([p['life'] for p in o['players']]==[30,30],'Unexpected life change');save(m,'all-returned');return finish('PASS','Four real paid spells. Optional0/0 declined -> graveyard without losing other copies. Interlude actual3 exiled/returned same IDs new timestamps; last-state candidate exclusion, creature/PW/nonlegendary branches and exact2P1P1/4loyalty without copying7/8 counters; restored original operator.')
     if at<len(actions) and r['seat']==1 and v['phase']=='MAIN1':
      current=actions[at];c=next(c for c in ps[0]['hand'] if c['name']==current['name']);pick='card:'+str(c['id']);check(pick in ids,'Actual paid spell not legal');current['cardId']=c['id'];working=True;before=o;save(m,'before-'+str(at))
      if current['name']=='Eerie Interlude':targets=copy_ids+[moving_id]
     else:pick='ok'
    elif 'Target' in kind and case in ('arc','each','each-cross','each-prevent','each-stale','each-triggers'):
     opts=[o for o in r['options'] if o.get('card') or o.get('playerSeat')];selected=[o for o in r['options'] if o.get('selected')]
     if case=='arc':want=['card:'+str(next(c['id'] for c in cards(initial) if c['name']=='Wall of Roots'))] if '2 damage' in r['message'] else ['player:'+str(ps[1]['targetId'])]
     elif case=='each-cross' and 'card:'+str(next(c['id'] for c in cards(initial) if c['name']=="Healer's Hawk")) in ids:want=['card:'+str(next(c['id'] for c in cards(initial) if c['name']=="Healer's Hawk"))]
     elif 'up to two' in r['message'].lower():want=['card:'+str(next(c['id'] for c in cards(initial) if c['name']==n)) for n in ["Healer's Hawk",'Grizzly Bears']]
     else:want=['card:'+str(next(c['id'] for c in initial['players'][1]['cards'] if c['name']==('Ripjaw Raptor' if case=='each-triggers' else 'Wall of Roots')))]
     remaining=[i for i in want if i not in {o['id'] for o in selected}];pick=remaining[0] if remaining else 'ok';check(pick in ids,'Actual damage target/confirmation absent '+str(r));save(m,'damage-target-'+str(step));observe(r)
    elif 'Target' in kind and current['name']=='Eerie Interlude':
     selected={o['id'] for o in r['options'] if o.get('selected')};remaining=['card:'+str(i) for i in targets if 'card:'+str(i) not in selected];pick=remaining[0] if remaining else 'ok';check(pick in ids,'Interlude exact original targets/confirm missing');save(m,'interlude-target-'+str(r.get('selectedCount',0)))
    elif kind=='InputConfirm' and r.get('selectionPurpose')=='optionalReplacement':
     check(r['seat']==r['playerSeat']==1,'Optional copy wrong controller');pick='cancel' if current.get('decline') and not return_started or case=='spark-skip' and at==len(actions) else 'ok';save(m,'copy-optional-'+str(step));observe(r)
     if at==len(actions):return_started=True
    elif case=='each-prevent' and 'SelectEntities' in kind:
     check(r['seat']==r['playerSeat']==2,'Damage prevention source must be selected by affected controller2');save(m,'prevent-source-'+str(step));observe(r)
     pick='ok' if r.get('selectedCount') else next(o['id'] for o in r['options'] if o.get('card',{}).get('name')=="Healer's Hawk")
    elif 'SelectCards' in kind or 'SelectEntities' in kind:
     if r.get('selectedCount'):
      pick='confirm-cards' if 'confirm-cards' in ids else 'ok';save(m,'copy-confirm-'+str(step));observe(r)
      if at==len(actions):return_choices+=1
      check(pick in ids,'Native copy confirmation absent');reply(r,pick);continue
     options=[o for o in r['options'] if o.get('card')];check(r['seat']==r['playerSeat']==1,'Copy candidates wrong chooser')
     if at==len(actions):
      return_started=True;check(v['phase']=='END_OF_TURN','Copy-return chooser wrong phase');candidate_ids={o['card']['id'] for o in options};check(not candidate_ids.intersection(targets),'Simultaneous returning object was illegally offered as clone source');check({source_id,pw_id}<=candidate_ids,'Existing last-state source omitted');want=pw_id if return_choices else source_id
     else:want=source_id
     save(m,'copy-candidates-'+str(step));observe(r);matching=[o for o in options if o['card']['id']==want];check(len(matching)==1,'Exact copy source missing');pick=matching[0]['id'];
    elif kind=='OrderAbilities':
     if case=='tokens' and r.get('selectionPurpose')=='replacementOrder' and not r.get('selectedCount'):
      want=next((n for n in ['Academy Manufactor','Doubling Season','Chatterfang'] if any(n in o['label'] for o in r['options'])),None);check(want is not None,'Unknown token replacement source');pick=next(o['id'] for o in r['options'] if want in o['label'])
     else:pick='confirm-order' if 'confirm-order' in ids else next(i for i in ids if i.startswith('order:'))
     save(m,'order-'+str(step));observe(r)
    elif 'PayMana' in kind:pick='ok';save(m,'pay-'+str(at))
    elif case=='each-prevent' and kind=='dialog':pick=next(o['id'] for o in r['options'] if 'Prevent the next 2' in o['label']);save(m,'healer-ability')
    elif 'ok' in ids:pick='ok'
    else:raise AssertionError((kind,r['message'],[(o['id'],o['label']) for o in r['options']]))
    check(pick in ids,(kind,pick,ids));print(case,step,kind,r['seat'],r['playerSeat'],pick,flush=True);reply(r,pick)
   raise AssertionError('Native step limit')
  except Exception:finish('FAIL',traceback.format_exc());raise
  finally:
   if proc.poll() is None:proc.kill();proc.wait()
if __name__=='__main__':
 for case in sys.argv[1:] or ['spark']:print(run(case)['status'],case,flush=True)
