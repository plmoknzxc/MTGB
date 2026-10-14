"""R36.01 actual optional QuasiLibrarySearch control-return delta, not terminal rerun."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'));OUT=ROOT/'.tools/controlled-search-1009';OUT.mkdir(exist_ok=True);CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIX=[ROOT/'Tools/ForgeBridge/tests/RoyalStateFixture.java',ROOT/'Tools/ForgeBridge/tests/ControlledSearchFixture.java'];sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),*[str(p) for p in FIX]],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
def run(case):
 cross=case=='opponent-library';ordinary=case=='ordinary-search';spell='Dichotomancy' if cross else 'Rampant Growth' if ordinary else 'Doubling Chant';accept=case!='decline';actor=1 if cross else 2
 f=OUT/'native'/case/str(time.time_ns());f.mkdir(parents=True);audit=f/'audit';audit.mkdir();checks=0
 meta=dict(case=case,runtimeHash=CFG['runtimeHash'],driver=sha(Path(__file__)),fixtures=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p)) for p in FIX],folder=f.relative_to(ROOT).as_posix())
 for p in FIX+[Path(__file__)]: (f/p.name).write_bytes(p.read_bytes())
 def check(ok,msg):
  nonlocal checks
  assert ok,msg;checks+=1
 def finish(status,note):
  result=dict(**meta,status=status,checks=checks,note=note);(f/'result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');(OUT/(case+'-result.json')).write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');return result
 with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(f/'trace.jsonl').open('w',encoding='utf-8') as trace,(f/'engine.log').open('w',encoding='utf-8') as log:
  config=dict(session=case,seed=4,startingLife=30,firstPlayerSeat=1,decks=[DECK,DECK],own={'Hand':['Doubling Chant','Shock'],'Battlefield':['Grizzly Bears'],'Library':['Grizzly Bears','Grizzly Bears','Island']},other={'Hand':['Opt'],'Battlefield':['Opposition Agent']},auditDir=str(audit))
  if ordinary:config['own']['Hand']=['Rampant Growth','Shock']
  if cross:config.update(own={'Hand':['Dichotomancy','Shock'],'Battlefield':[]},other={'Hand':['Opt'],'Battlefield':['Opposition Agent','Grizzly Bears'],'Library':['Grizzly Bears','Grizzly Bears','Island']},tapOther='Grizzly Bears')
  start=Path(temp)/'start.json';start.write_text(json.dumps(config),encoding='utf-8');(f/'start.json').write_text(json.dumps(config,ensure_ascii=False,indent=2),encoding='utf-8')
  proc=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.ControlledSearchFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf-8')
  q=queue.Queue()
  def reader():
   for line in proc.stdout:q.put(json.loads(line))
   q.put(None)
  threading.Thread(target=reader,daemon=True).start()
  def read():
   m=q.get(timeout=35);check(m is not None,'Native process ended');trace.write(json.dumps(m)+'\n');trace.flush();return m
  def reply(r,option,seat=None):proc.stdin.write(json.dumps(dict(type='respond',session=case,seat=seat or r['seat'],requestId=r['requestId'],option=option))+'\n');proc.stdin.flush()
  def save(m,name):
   (f/(name+'-wire.json')).write_text(json.dumps(m,ensure_ascii=False,indent=2),encoding='utf-8')
   for i,v in enumerate(m['views']):
    x=dict(v,awaitingInput=m['request']['seat']==i+1,request=m['request'] if m['request']['seat']==i+1 else None);(f/(name+'-seat'+str(i+1)+'.json')).write_text(json.dumps(x,ensure_ascii=False,indent=2),encoding='utf-8')
  def observed(r):
   end=time.monotonic()+4;p=audit/(str(r['requestId'])+'.json')
   while not p.exists() and time.monotonic()<end:time.sleep(.02)
   return json.loads(p.read_text())
  cast=False;selected=False;picked=None;initial=None;search_known_hand=set()
  try:
   for step in range(220):
    m=read();check(m['type'] not in ('fatal','ended','rejected'),m)
    if m['type']!='request':continue
    r=m['request'];v=m['views'][0];players=v['players'];kind=r['kind'];ids=[o['id'] for o in r['options']];pick=None
    if kind=='InputPassPriority' and not v['stackItems']:
     if selected:
      save(m,'restored');o=observed(r)
      if cross:
       check(r['seat']==1 and r['playerSeat']==1,'Opponent-library next operator wrong');check(o['players'][0]['controllerEntries']==0,'Opponent-library retained controller');check(players[1]['libraryCount']==initial['opponentLibraryCount']-1,'Opponent-library card count wrong');check(any(c['id']==picked and c['ownerSeat']==2 for c in players[0]['battlefield']),'Opponent-library exact card not gained by caster');check(not players[0]['exileCards'] and not players[1]['exileCards'],'Agent incorrectly replaced another player searching this library');check(sum(c.get('tapped',False) for c in players[0]['battlefield'])==9,'Actual9 mana missing');check(any(c['name']==spell for c in players[0]['graveyardCards']),'Dichotomancy source not in graveyard');check(not m['views'][1]['players'][0]['hand'],'Private caster hand leaked through other-library search');return finish('PASS','Actual paid Dichotomancy targeted opponent library, exact2 candidate IDs, correct caster chooser and no Agent control or exile; selected original opponent card gained,9mana, source grave and restored next request. Native only.')
      check(r['seat']==1 and r['playerSeat']==1,'Next operator not restored');check(players[0]['controllingSeat']==0,'Quasi search leaked native controller');check(o['players'][0]['controllerEntries']==0,'Native controller timestamp retained');check(o['players'][0]['searchControllerSeat']==2,'Static Agent search entitlement wrongly removed');check(any(c['name']==spell for c in players[0]['graveyardCards']),'Source not resolved into graveyard');check(players[0]['libraryCount']==initial['libraryCount']-(1 if accept else 0),'Unexpected search library count');check(len(players[0]['battlefield'])==len(initial['battlefield']),'Creature incorrectly entered battlefield under Agent');check(sum(c.get('tapped',False) for c in players[0]['battlefield'])==(2 if ordinary else 6),'Actual mana payment missing');check(players[0]['life']==30 and players[1]['life']==30,'Unexpected life');check(players[0]['manaPool']==[0]*6,'Unexpected mana pool')
      exile=players[0]['exileCards'];check(any(c['id']==picked and c['name']==('Island' if ordinary else 'Grizzly Bears') for c in exile) if accept else not exile,'Agent selected-card exile outcome wrong');check({c['id'] for c in m['views'][1]['players'][0]['hand']}<=search_known_hand,'New unseen private hand leaked to former controller');return finish('PASS','Actual paid '+spell+' and native Agent controlled search '+case+'; exact2 same-name library IDs, native timestamp and logical/physical routing; next own operator/zero temporary controllers restored,6 mana and source grave, '+('selected original card exiled; permission marker/cast checked separately' if accept else 'no selected card/no exile')+'. Native only; UI/privacy replay separate.')
     if not cast and r['playerSeat']==1 and v['phase']=='MAIN1':
      card=next(c for c in players[0]['hand'] if c['name']==spell);pick='card:'+str(card['id']);initial=dict(players[0],opponentLibraryCount=players[1]['libraryCount']);cast=True;save(m,'before')
     else:pick='ok'
    elif cast and r.get('playerSeat')==1 and kind in ('SelectCards','InputSelectEntitiesFromList'):
     save(m,'search');search_known_hand={c['id'] for c in m['views'][1]['players'][0]['hand']};o=observed(r);check(r['seat']==actor,'Wrong actual search operator for library ownership');check(o['players'][0]['controllingSeat']==(0 if cross else 2) and o['players'][0]['searchControllerSeat']==2,'Actual controlled search path not reached');check(o['players'][0]['controllerEntries']==(0 if cross else 1),'Wrong native temporary timestamp');cards=[x for x in r['options'] if x.get('card') and x['card'].get('id')];check((all(c['card']['land'] and c['card']['zone']=='Library' for c in cards) if ordinary else len(cards)==2 and all(c['card']['name']=='Grizzly Bears' and c['card']['zone']=='Library' for c in cards)),'Exact legal matching candidates missing');check(not m['views'][0].get('awaitingInput',False),'Nonoperator falsely awaits');check(not m['views'][0]['players'][1]['hand'],'Unseen Agent controller hand leaked to chooser');
     if not selected:
      reply(r,'cancel',3-actor);rejected=read();check(rejected['type']=='rejected' and rejected['error']=='wrong-seat','Wrong physical chooser accepted');selected=True
      if accept:
       chosen=next(c for c in cards if c['card']['name']=='Island') if ordinary else cards[0];picked=chosen['card']['id'];pick=chosen['id']
      else:pick='cancel'
     else:
      check(accept and r.get('selectedCount')==1 and 'ok' in ids,'Accepted selection missing native confirmation');pick='ok';save(m,'selected')
    elif cross and kind=='dialog' and r['message']=='getAbilityToPlay':pick='choice:0'
    elif cross and kind=='InputSelectTargets':pick='player:2' if 'player:2' in ids else next(x for x in ids if x.startswith('player:'))
    elif 'PayMana' in kind:pick='ok'
    elif 'ok' in ids:pick='ok'
    elif kind=='RevealCards':pick='confirm-cards'
    else:raise AssertionError((kind,r['message'],ids))
    check(pick in ids,(kind,pick,ids));print(case,step,kind,r['seat'],r['playerSeat'],pick,flush=True);reply(r,pick)
   raise AssertionError('Native step limit')
  except Exception:finish('FAIL',traceback.format_exc());raise
  finally:
   if proc.poll() is None:proc.kill();proc.wait()
if __name__=='__main__':
 for case in sys.argv[1:] or ['decline','accept','opponent-library','ordinary-search']:print(run(case)['status'],case,flush=True)
