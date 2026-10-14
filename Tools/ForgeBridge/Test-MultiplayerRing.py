"""R31 missing native four-seat Ring4 result; no four-seat Unity/network claim.

Only opening state and registration are fixtures. Four actual casts, designation,
mana payment, attack, loot and Ring damage trigger use the production bridge/Forge.
Reuse prior two-seat UI contracts; multiplayer routing/layout remain R37/R02.
"""
import hashlib,json,os,queue,shutil,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime'
CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
OUT=ROOT/'.tools/multiplayer-ring-1008/native'
CASES={'ring4-actor1-defender2':(1,2),'ring4-actor2-defender4':(2,4)}
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
def write(p,v):Path(p).write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()

def run(case):
 actor,defender=CASES[case];folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True)
 fixtures=['RoyalStateFixture.java','MultiplayerRingFixture.java']
 for n in fixtures:shutil.copyfile(ROOT/'Tools/ForgeBridge/tests'/n,folder/n)
 shutil.copyfile(__file__,folder/'driver.py')
 result=dict(case=case,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],revision=CFG['revision'],driverSha256=sha(folder/'driver.py'),fixtures={n:sha(folder/n) for n in fixtures},status='RUNNING')
 write(folder/'result.json',result);p=None;checks=0
 def check(ok,why):
  nonlocal checks
  assert ok,why;checks+=1
 try:
  classes=folder/'classes';classes.mkdir()
  subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*[str(folder/n) for n in fixtures]],check=True)
  with tempfile.TemporaryDirectory(prefix='mtgb-ring4-') as temp,(folder/'stderr.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
   audit=folder/'audit';audit.mkdir();write(folder/'start.json',dict(session=case,actor=actor,auditDir=str(audit),seed=4,firstPlayerSeat=actor,startingLife=30,decks=[DECK,DECK]))
   p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.MultiplayerRingFixture',str(RT/CFG['assets']),str(folder/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
   messages=queue.Queue()
   def reader():
    try:
     for line in p.stdout:messages.put(json.loads(line))
    except Exception as e:messages.put(e)
    finally:messages.put(None)
   threading.Thread(target=reader,daemon=True).start()
   def observed(r):
    path=audit/(str(r['requestId'])+'.json');end=time.monotonic()+5
    while time.monotonic()<end:
     try:value=json.loads(path.read_text());break
     except (FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
    else:raise AssertionError('Missing observer '+str(r['requestId']))
    check(value['requestId']==r['requestId'] and len(value['players'])==4,'exact request/all four native seats');return value
   def snap(m,label):
    write(folder/(label+'-envelope.json'),m)
    for seat,v in enumerate(m['views'],1):write(folder/(label+'-seat'+str(seat)+'.json'),dict(v,request=m['request'],awaitingInput=True))
   seen=set();cast=0;ready=None;bear=None;birthday_ids=[];discard=None;attacked=False;initial=None;loot_seen=False;ring_trigger=False;deadline=time.monotonic()+200
   for step in range(450):
    check(time.monotonic()<deadline,'Deadline');m=messages.get(timeout=35)
    if isinstance(m,Exception):raise m
    check(m is not None,'Bridge exited');trace.write(json.dumps(m)+'\n');trace.flush();check(m['type'] not in ('fatal','ended','rejected'),m)
    if m['type']!='request':continue
    r=m['request']
    if r['requestId'] in seen:continue
    seen.add(r['requestId']);v=m['views'][actor-1];own=v['players'][actor-1];opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];stack=v['stackItems'];pick='ok'
    if initial is None and kind=='InputPassPriority' and r['seat']==actor and v['phase']=='MAIN1':
     initial=observed(r);bear=next(c['id'] for c in own['battlefield'] if c['name']=='Grizzly Bears');snap(m,'initial')
    if initial is not None:check(v['turnSeat']==actor and v['turn']==1,'one native turn throughout')
    for item in stack:
     if item['source']['name']=='The Ring' and attacked:ring_trigger=True;snap(m,'ring-trigger')
    if kind=='InputPassPriority':
     if initial is not None and r['seat']==actor and v['phase']=='MAIN1' and not stack and cast<4:
      check(own['ringTemptedCount']==cast,'exact progression after each real cast')
      card=next(c for c in own['hand'] if c['name']=='Birthday Escape');pick='card:'+str(card['id']);birthday_ids.append(card['id']);cast+=1
     elif cast==4 and own['ringLevel']==4 and not stack and ready is None:
      ready=observed(r);check(own['ringBearerId']==bear and own['ringTemptedCount']==4,'exact designated bearer/level4');check(len([c for c in own['effectMarkers'] if c['name']=='The Ring'])==1,'single native Ring helper');snap(m,'ready')
     elif ready is not None and attacked and not stack and v['phase']=='MAIN2':
      final=observed(r);snap(m,'resolved')
      expected=[30 if s==actor else 25 if s==defender else 27 for s in range(1,5)]
      check([pv['life'] for pv in final['players']]==expected,('exact four-seat lives',expected,final['players']))
      check(cast==4 and len(set(birthday_ids))==4 and loot_seen and discard is not None and ring_trigger,'all actual casts/loot/Ring trigger observed')
      check([pv['life'] for pv in ready['players']]==[30]*4,'all seats30 before combat')
      for s,(before,after) in enumerate(zip(ready['players'],final['players']),1):
       bz,az=before['zones'],after['zones'];index=lambda z:[c['id'] for c in z]
       check(index(az['Battlefield'])==index(bz['Battlefield']) and index(az['Exile'])==index(bz['Exile']) and index(az['Command'])==index(bz['Command']),'exact field/exile/command IDs')
       check(after['counters']==before['counters'] and after['ringBearer']==before['ringBearer'] and after['ringTempted']==before['ringTempted'],'exact counters/Ring state')
       if s==actor:
        drawn=bz['Library'][0]['id'];check(index(az['Library'])==index(bz['Library'])[1:],'exact one draw/full library suffix')
        check(set(index(az['Hand']))==(set(index(bz['Hand']))|{drawn})-{discard},'exact drawn/discarded private hand IDs')
        check(index(az['Graveyard'])==index(bz['Graveyard'])+[discard],'exact discard grave identity/order')
        check(all(c['tapped']==(c['id']==bear or next(b for b in bz['Battlefield'] if b['id']==c['id'])['tapped']) for c in az['Battlefield']),'exact bearer tap and unchanged lands')
       else:
        check(all(az[z]==bz[z] for z in ['Hand','Library','Graveyard','Battlefield','Exile','Command']),'all nonactor native zones/order/tap unchanged')
      check(all(not pv['hand'] for s,pv in enumerate(v['players'],1) if s!=actor),'opponent hand remains hidden in actor view')
      for view in m['views']:check([pv['life'] for pv in view['players']]==expected[:2],'both bridge views agree for supported two seats')
      result.update(status='PASS',result='PASS '+case,checks=checks,requests=len(seen),actor=actor,defender=defender,lives=expected,assertions=['Four actual Birthday Escape casts and exact Ring bearer level4','Native attack assigns exact defender among three opposing player identities; attack loot exact draw/discard','All three opponents lose3, defending opponent additionally takes2 combat damage; actor loses0','Full native libraries/private hand IDs/zone order, counters/Ring helper unchanged except named draw/discard; next MAIN2 priority','Native four-seat fixture only; production bridge views/lobby/Unity remain two-seat, no Commander/network PASS'])
      break
    elif 'Mulligan' in kind:pick='keep' if 'keep' in ids else 'ok'
    elif 'PayMana' in kind:pick='ok' if 'ok' in ids and not next(o for o in opts if o['id']=='ok').get('disabled') else next(o['id'] for o in opts if o['id'].startswith('card:'))
    elif kind=='InputAttack':
     if ready is not None and r['seat']==actor and not attacked:
      check({o['defenderId'] for o in opts if o['id'].startswith('attack:') and o.get('attackerId')==bear and o.get('defenderKind')=='player'}=={pv['id'] for pv in ready['players'] if pv['seat']!=actor},'exact three native defender choices')
      target=ready['players'][defender-1]['id'];pick=next(o['id'] for o in opts if o['id'].startswith('attack:') and o.get('attackerId')==bear and o.get('defenderId')==target);attacked=True;snap(m,'attack')
    elif kind=='InputBlock':pick='ok'
    elif 'SelectCards' in kind or 'SelectEntities' in kind:
     if 'ok' in ids and not next(o for o in opts if o['id']=='ok').get('disabled'):pick='ok'
     elif any(o.get('card',{}).get('id')==bear for o in opts):pick=next(o['id'] for o in opts if o.get('card',{}).get('id')==bear)
     else:
      check(attacked and r['seat']==actor,'only expected native attack loot');choice=next(o for o in opts if o.get('card') and not o.get('selected') and not o.get('disabled'));pick=choice['id'];discard=choice['card']['id'];loot_seen=True;snap(m,'loot')
    elif kind=='OrderAbilities':pick='confirm-order' if 'confirm-order' in ids else next(o['id'] for o in opts if o['id'].startswith('order:'))
    elif kind in ('dialog','Option'):pick=next(i for i in ids if i not in ('cancel','no'))
    elif kind=='RevealCards':pick='confirm-cards'
    elif 'ok' not in ids:raise AssertionError(('Unhandled',kind,r['message'],ids))
    check(pick in ids and not next(o for o in opts if o['id']==pick).get('disabled'),('legal native option',kind,pick,ids))
    print(case,step,kind,r['seat'],pick,'Ring',own.get('ringLevel'),'phase',v['phase'],flush=True)
    p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
   else:raise AssertionError('Step limit')
 except Exception:result.update(status='FAIL',checks=checks,result='FAIL '+case+': '+traceback.format_exc())
 finally:
  if p is not None and p.poll() is None:p.kill();p.wait()
 write(folder/'result.json',result);write(OUT/(case+'-result.json'),result);return result

def main():
 OUT.mkdir(parents=True,exist_ok=True);cases=sys.argv[1:] or list(CASES);results=[]
 write(OUT/'run-summary.json',dict(status='RUNNING',cases=cases,started=time.time()))
 for case in cases:
  r=run(case);results.append(r);print(r['result'],flush=True)
 status='PASS' if all(r['status']=='PASS' for r in results) else 'FAIL';write(OUT/'run-summary.json',dict(status=status,results=results));return status!='PASS'
if __name__=='__main__':sys.exit(main())
