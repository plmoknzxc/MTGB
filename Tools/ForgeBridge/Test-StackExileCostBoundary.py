"""Actual Synthesis Pod stack-exile cost cancellation/retry, then native result."""
import hashlib,json,os,queue,shutil,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
OUT=ROOT/os.environ.get('MTGB_STACK_EXILE_OUTPUT','.tools/stack-exile-cost-1008/native')
CASES=('stack-exile-before-mana','restored-stack-resolves');NAME='Synthesis Pod'
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def run(case):
 CASE=case;RESOLVE_ONLY=case=='restored-stack-resolves'
 folder=OUT/CASE/str(time.time_ns());folder.mkdir(parents=True)
 fixtures=['StateBranchFixture.java','StackExileCostFixture.java']
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
  with tempfile.TemporaryDirectory(prefix='mtgb-stack-exile-') as temp,(folder/'stderr.log').open('w',encoding='utf-8') as log,(folder/'trace.jsonl').open('w',encoding='utf-8') as trace:
   audit=folder/'audit';audit.mkdir()
   write(folder/'start.json',dict(session=CASE,seed=4,firstPlayerSeat=1,startingLife=20,decks=[DECK,DECK],auditDir=str(audit),own=dict(Hand=['Opt','Negate'],Battlefield=[NAME,'Island','Island','Island'],Graveyard=['Lightning Bolt']),other=dict(Hand=['Negate'],Battlefield=['Forest'],Graveyard=['Opt'],Library=['Grizzly Bears','Forest','Mountain'])))
   p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.StackExileCostFixture',str(RT/CFG['assets']),str(folder/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf-8')
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
    check(value['requestId']==r['requestId'],'observer request identity');return value
   def snap(m,label):
    write(folder/(label+'-envelope.json'),m)
    write(folder/(label+'-observer.json'),observed(m['request']))
    for seat,v in enumerate(m['views'],1):write(folder/(label+'-seat'+str(seat)+'.json'),dict(v,request=m['request'],awaitingInput=True))
   opt=None;pod=None;before=None;cancelled=False;retry=False;freecast=False;seen=set();deadline=time.monotonic()+180
   for step in range(220):
    check(time.monotonic()<deadline,'deadline');m=messages.get(timeout=35)
    if isinstance(m,Exception):raise m
    check(m is not None,'native process alive');trace.write(json.dumps(m)+'\n');trace.flush();check(m['type'] not in ('fatal','ended','rejected'),m)
    if m['type']!='request':continue
    r=m['request'];v=m['views'][0];one,two=v['players'];kind=r['kind'];opts=r['options'];ids=[o['id'] for o in opts];pick='ok'
    if r['requestId'] in seen:continue
    seen.add(r['requestId']);check(not m['views'][0]['players'][1]['hand'] and not m['views'][1]['players'][0]['hand'],'both private hands hidden')
    if kind=='InputPassPriority' and r['seat']==1 and v['phase']=='MAIN1':
     if opt is None:
      opt=next(c['id'] for c in one['hand'] if c['name']=='Opt');pod=next(c['id'] for c in one['battlefield'] if c['name']==NAME);pick='card:'+str(opt)
     elif before is None and v['stackItems']:
      snap(m,'before');before=observed(r);check(len(before['stack'])==1 and before['stack'][0]['sourceId']==opt,'actual Opt pending with native stack identity');pick='card:'+str(pod)
     elif cancelled and not retry:
      snap(m,'cancelled');state=observed(r)
      check(state['players']==before['players'] and state['stack']==before['stack'],'cancel restores exact both-owner resources, pending spell identity/order and all history')
      check(len(v['stackItems'])==1 and any(x.get('source',{}).get('id')==opt or x.get('card',{}).get('id')==opt for x in v['stackItems']),'cancelled Opt visible on stack again')
      retry=True;pick='ok' if RESOLVE_ONLY else 'card:'+str(pod)
     elif retry and not v['stackItems']:
      snap(m,'resolved');state=observed(r)
      if RESOLVE_ONLY:
       check(not one['exileCards'],'Restored Opt not left exiled')
       check(any(c['id']==opt for c in state['players'][0]['zones']['Graveyard']),'Restored original Opt resolved to its owner graveyard')
       original=before['players'][0];current=state['players'][0]
       check(len(current['zones']['Hand'])==len(original['zones']['Hand'])+1,'Restored Opt draws exactly one card')
       check(len(current['zones']['Library'])==len(original['zones']['Library'])-1,'Restored Opt consumes exactly one library card')
       check(current['spellsCastThisGame']==original['spellsCastThisGame']==1 and current['spellsCastThisTurn']==original['spellsCastThisTurn']==1,'Restored spell resolution does not recast/increment history')
       check(not next(c for c in one['battlefield'] if c['id']==pod)['tapped'] and sum(c['tapped'] for c in one['battlefield'] if c['name']=='Island')==1,'Cancelled Pod resources preserved after original spell resolves')
       check(one['life']==two['life']==20 and not sum(one['manaPool']),'Exact life and empty mana after restored resolution')
       check(state['players'][1]==before['players'][1],'Opponent resources/ordered zones/history unchanged')
       result.update(status='PASS',result='PASS '+CASE,checks=checks,requests=len(seen),assertions=['Actually cast/pay Opt, exile it for Synthesis Pod then cancel later mana; exact original stack/resources/history restored','Allow restored original Opt to resolve normally through native scry/draw, then graveyard; exactly one draw, one spell cast, no Pod tap/mana leak and original both-seat privacy/main priority restored']);break
      check(freecast,'native offered and accepted free cast of found card')
      check([c['id'] for c in one['exileCards']]==[opt],'exact original Opt exiled once')
      check([c['name'] for c in one['hand']]==['Negate'],'Opt never resolved/drew; unrelated hand unchanged')
      bears=[c for c in one['battlefield'] if c['name']=='Grizzly Bears']
      check(len(bears)==1 and bears[0]['ownerSeat']==2 and bears[0]['power']==bears[0]['toughness']==2,'opponent-owned2/2 found spell actually cast under own control')
      check(one['life']==two['life']==20,'blue Phyrexian payment without life loss')
      check(not sum(one['manaPool']) and next(c for c in one['battlefield'] if c['id']==pod)['tapped'],'one completed Pod mana/tap cost')
      check(sum(c['tapped'] for c in one['battlefield'] if c['name']=='Island')==3,'Opt U then retry1U, cancellation refunded sources')
      check(state['players'][0]['zones']['Library']==before['players'][0]['zones']['Library'],'no cancelled/resolved Opt draw or own library change')
      check(state['players'][0]['zones']['Graveyard']==before['players'][0]['zones']['Graveyard'],'own graveyard unchanged')
      check(state['players'][1]['zones']['Hand']==before['players'][1]['zones']['Hand'] and state['players'][1]['zones']['Graveyard']==before['players'][1]['zones']['Graveyard'],'opponent private/graveyard unchanged')
      result.update(status='PASS',result='PASS '+CASE,checks=checks,requests=len(seen),assertions=['Actually cast/pay Opt then activate Synthesis Pod, target opponent, exile exact pending Opt and tap before cancelled1UP; exact both-owner resources/ordered zones/spell history and original stack identity restored','Retry exiles original Opt once, pays1U once, resolves native DigUntil/shuffle and accepts/casts opponent-owned Grizzly Bears free under own control; no Opt draw, no cancelled resource/history leak, privacy and main priority restored']);break
    elif kind=='InputPassPriority':pass
    elif kind=='OrderCards':
     candidates=[o for o in opts if o['id'].startswith('select:') and not o.get('selected')]
     ordered=sorted(candidates,key=lambda o:('Exile' not in o['label'],o['label'].startswith('{')))
     pick=ordered[0]['id'] if ordered else 'confirm-cards'
    elif 'PayMana' in kind:
     if before is not None and not cancelled:
      snap(m,'committed-before-cancel');state=observed(r)
      check(not state['stack'] and [c['id'] for c in one['exileCards']]==[opt],'actual original Opt exiled from stack before cancel');pick='cancel';cancelled=True
     else:pick='ok' if any(o['id']=='ok' and not o.get('disabled') for o in opts) else next(o['id'] for o in opts if o['id'].startswith('card:') and not o.get('disabled'))
    elif kind=='InputSelectTargets':
     pick=next((o['id'] for o in opts if o.get('player',{}).get('seat')==2 and not o.get('selected') and not o.get('disabled')),next((o['id'] for o in opts if o['id']=='ok' and not o.get('disabled')),'invalid'))
    elif kind in ('InputConfirmMulligan','InputAttack','InputBlock'):pick='ok'
    elif kind=='InputConfirm':
     snap(m,'confirm-'+str(len(seen)));pick='ok'
     if retry and ('cast' in r['message'].lower() or 'play' in r['message'].lower()):freecast=True
    elif kind=='InputMulligan':pick='keep'
    elif kind=='RevealCards':snap(m,'reveal-'+str(len(seen)));pick='confirm-cards'
    elif kind=='Scry' and RESOLVE_ONLY:
     snap(m,'scry-'+str(len(seen)));pick=next((o['id'] for o in opts if o['id'].startswith('scry-top:')),'confirm-scry')
    elif kind in ('dialog','SelectEntities','InputSelectEntitiesFromList'):
     snap(m,'choice-'+str(len(seen)));pick=next(o['id'] for o in opts if o['id'] not in ('cancel','no') and not o.get('disabled'))
     if retry and ('cast' in r['message'].lower() or 'play' in r['message'].lower()):freecast=True
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
 selected=[('restored-stack-resolves' if arg=='--resolve-restored' else arg) for arg in sys.argv[1:]] or list(CASES)
 if any(case not in CASES for case in selected):raise SystemExit('Unknown case: '+str(selected))
 OUT.mkdir(parents=True,exist_ok=True);write(OUT/'run-summary.json',dict(status='RUNNING',selected=selected));results=[]
 for case in selected:
  result=run(case);results.append(result);print(result['result'],flush=True)
 status='PASS' if all(r['status']=='PASS' for r in results) else 'FAIL';write(OUT/'run-summary.json',dict(status=status,selected=selected,results=results));sys.exit(status!='PASS')
