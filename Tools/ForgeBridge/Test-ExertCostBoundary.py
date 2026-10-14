"""Actual exert-before-mana cancellation, retry and exactly one skipped untap."""
import hashlib,json,os,queue,shutil,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
OUT=ROOT/os.environ.get('MTGB_EXERT_COST_OUTPUT','.tools/exert-cost-1008/native')
CASE='exert-before-mana';NAME="Basri, Tomorrow's Champion"
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def run():
 folder=OUT/CASE/str(time.time_ns());folder.mkdir(parents=True)
 fixtures=['StateBranchFixture.java','ExertCostFixture.java']
 for name in fixtures:shutil.copyfile(ROOT/'Tools/ForgeBridge/tests'/name,folder/name)
 shutil.copyfile(__file__,folder/'driver.py')
 result=dict(case=CASE,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],revision=CFG['revision'],driverSha256=sha(folder/'driver.py'),fixtures={n:sha(folder/n) for n in fixtures},status='RUNNING')
 write(folder/'result.json',result);p=None;checks=0
 def check(ok,why):
  nonlocal checks
  assert ok,why;checks+=1
 try:
  classes=folder/'classes';classes.mkdir()
  subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*[str(folder/n) for n in fixtures]],check=True)
  with tempfile.TemporaryDirectory(prefix='mtgb-exert-cost-') as temp,(folder/'stderr.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
   audit=folder/'audit';audit.mkdir()
   write(folder/'start.json',dict(session=CASE,seed=4,firstPlayerSeat=1,startingLife=20,decks=[DECK,DECK],auditDir=str(audit),initialExertedOpponent=True,own=dict(Hand=['Negate'],Battlefield=[NAME,'Plains','Resolute Survivors'],Graveyard=['Opt'],Library=['Island','Forest','Swamp','Mountain','Plains']*6),other=dict(Hand=['Negate'],Battlefield=['Grizzly Bears'],Graveyard=['Lightning Bolt'])))
   p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.ExertCostFixture',str(RT/CFG['assets']),str(folder/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
   messages=queue.Queue()
   def reader():
    try:
     for line in p.stdout:messages.put(json.loads(line))
    except Exception as e:messages.put(e)
    finally:messages.put(None)
   threading.Thread(target=reader,daemon=True).start()
   def observed(r):
    end=time.monotonic()+5
    while time.monotonic()<end:
     try:value=json.loads((audit/(str(r['requestId'])+'.json')).read_text());break
     except (FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
    else:raise AssertionError('Missing exact observer '+str(r['requestId']))
    check(value['requestId']==r['requestId'],'observer request identity');return value['players']
   def snap(m,label):
    write(folder/(label+'-envelope.json'),m)
    for seat,v in enumerate(m['views'],1):write(folder/(label+'-seat'+str(seat)+'.json'),dict(v,request=m['request'],awaitingInput=True))
   before=None;source=None;cancelled=False;retried=False;resolved=False;skipped=False;stackseen=False;seen=set();deadline=time.monotonic()+180
   def source_state(players):return next(c for c in players[0]['zones']['Battlefield'] if c['id']==source)
   for step in range(260):
    check(time.monotonic()<deadline,'deadline');m=messages.get(timeout=35)
    if isinstance(m,Exception):raise m
    check(m is not None,'native process alive');trace.write(json.dumps(m)+'\n');trace.flush();check(m['type'] not in ('fatal','ended','rejected'),m)
    if m['type']!='request':continue
    r=m['request'];v=m['views'][0];one,two=v['players'];kind=r['kind'];opts=r['options'];ids=[o['id'] for o in opts];pick='ok'
    if r['requestId'] in seen:continue
    seen.add(r['requestId'])
    check(not m['views'][0]['players'][1]['hand'] and not m['views'][1]['players'][0]['hand'],'both private hands hidden')
    if kind=='InputPassPriority' and r['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
     if before is None:
      before=observed(r);source=next(c['id'] for c in one['battlefield'] if c['name']==NAME);snap(m,'before')
      check(source_state(before)['exertedThisTurn']==0 and not source_state(before)['exertedBy'],'initial exert state empty');pick='card:'+str(source)
      check(before[1]['zones']['Battlefield'][0]['exertedBy']==[2],'opening opponent marker belongs to seat2')
     elif cancelled and not retried:
      state=observed(r);snap(m,'cancelled')
      check(state==before,'cancel restores exact both-owner resources/zones/exert counters and next-untap markers')
      check(not sum(one['manaPool']) and not v['stackItems'],'cancel leaves no pool or stack')
      retried=True;pick='card:'+str(source)
     elif retried and not resolved:
      state=observed(r);snap(m,'resolved');s=source_state(state)
      check(stackseen,'retry actual stack entry');check(s['tapped'] and s['exertedThisTurn']==1 and s['exertedBy']==[1],'retry exerts once and marks correct controller')
      new=[c for c in one['battlefield'] if c['id'] not in [b['id'] for b in before[0]['zones']['Battlefield']]]
      check(len(new)==1 and new[0].get('token') and new[0]['power']==new[0]['toughness']==1 and any(str(k).upper()=='LIFELINK' for k in new[0].get('keywords',[])),'one actual1/1 lifelink Cat, no cancelled token')
      check(state[1]==dict(before[1],life=19),'opponent loses one from actual exert trigger; all other resources and existing exert state unchanged')
      check(next(c for c in state[0]['zones']['Battlefield'] if c['name']=='Plains')['tapped'] and not sum(one['manaPool']),'one W paid with no remainder')
      check(state[0]['zones']['Hand']==before[0]['zones']['Hand'] and state[0]['zones']['Library']==before[0]['zones']['Library'] and state[0]['zones']['Graveyard']==before[0]['zones']['Graveyard'],'retry private and graveyard identity/order unchanged')
      check(one['life']==21 and two['life']==19,'one actual Survivors trigger; no cancelled damage/lifegain');resolved=True
     elif resolved:
      s=source_state(observed(r))
      if v['turn']==3:
       snap(m,'skipped-untap');check(s['tapped'] and s['exertedThisTurn']==0 and not s['exertedBy'],'exact next own untap skipped; marker consumed');skipped=True
      elif v['turn']==5:
       snap(m,'recovered-untap');check(skipped and not s['tapped'] and not s['exertedBy'],'following own untap recovers, no extra exert debt')
       result.update(status='PASS',result='PASS '+CASE,checks=checks,requests=len(seen),assertions=['Actual Basri W+tap+exert activation ordered with exert committed before mana cancellation; exact both-owner resources/ordered zones/exert history and untap markers restored including pre-existing opponent marker; no cancelled token or exert-trigger damage/lifegain','Retry pays W once, exerts once, resolves one1/1 lifelink Cat and one Survivors damage/lifegain trigger; exact next own untap skipped, following untap recovers; both private hands and native priority continue']);break
    elif kind=='InputPassPriority':
     if retried and v['stackItems']:stackseen=True;snap(m,'retry-stack')
    elif kind=='OrderCards':
     candidates=[o for o in opts if o['id'].startswith('select:') and not o.get('selected')]
     ordered=sorted(candidates,key=lambda o:('Exert' not in o['label'],o['label'].startswith('{')))
     pick=ordered[0]['id'] if ordered else 'confirm-cards'
    elif 'PayMana' in kind:
     if not cancelled:
      s=source_state(observed(r));snap(m,'committed-before-cancel')
      check(s['exertedThisTurn']==1 and s['exertedBy']==[1],'exert actually committed before cancelling mana');pick='cancel';cancelled=True
     else:pick='ok' if any(o['id']=='ok' and not o.get('disabled') for o in opts) else next(o['id'] for o in opts if o['id'].startswith('card:') and not o.get('disabled'))
    elif kind in ('InputSelectCardsFromList','SelectCards'):
     pick=next((o['id'] for o in opts if o.get('card',{}).get('id')==source and not o.get('selected') and not o.get('disabled')),next((o['id'] for o in opts if o['id'] in ('ok','confirm-cards') and not o.get('disabled')),'invalid'))
    elif kind in ('InputConfirmMulligan','InputAttack','InputBlock'):pick='ok'
    elif kind=='InputConfirm':snap(m,'confirm-'+str(len(seen)));pick='ok'
    elif kind=='InputMulligan':pick='keep'
    elif kind=='dialog':pick=next(o['id'] for o in opts if o['id'] not in ('cancel','no') and not o.get('disabled'))
    else:raise AssertionError(('Unhandled',kind,r['message'],ids))
    check(pick in ids and not next(o for o in opts if o['id']==pick).get('disabled'),('legal option',kind,pick,ids))
    if kind!='InputPassPriority' or pick!='ok':print(CASE,step,kind,pick,r['message'][:90].replace('\n',' '),flush=True)
    response=dict(type='respond',session=CASE,seat=r['seat'],requestId=r['requestId'],option=pick);trace.write(json.dumps(dict(response=response))+'\n');trace.flush();p.stdin.write(json.dumps(response)+'\n');p.stdin.flush()
   else:raise AssertionError('step limit')
 except Exception:result.update(status='FAIL',checks=checks,result='FAIL '+CASE+': '+traceback.format_exc())
 finally:
  if p is not None and p.poll() is None:p.kill();p.wait()
 write(folder/'result.json',result);write(OUT/(CASE+'-result.json'),result);return result
if __name__=='__main__':
 OUT.mkdir(parents=True,exist_ok=True);write(OUT/'run-summary.json',dict(status='RUNNING',selected=[CASE]));r=run();print(r['result'],flush=True);write(OUT/'run-summary.json',dict(status=r['status'],results=[r]));sys.exit(r['status']!='PASS')
