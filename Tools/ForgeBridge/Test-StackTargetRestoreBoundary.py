"""Actual targeted multi-spell stack restoration after cancelled Synthesis Pod payment."""
import hashlib,json,os,queue,shutil,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
OUT=ROOT/os.environ.get('MTGB_STACK_TARGET_OUTPUT','.tools/stack-target-restore-1008/native')
CASES=('two-spells-target-restore','two-spells-copy-restore');NAME='Synthesis Pod'
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def run(case):
 CASE=case;RESOLVE_ONLY=True;COPY=case=='two-spells-copy-restore'
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
   write(folder/'start.json',dict(session=CASE,seed=4,firstPlayerSeat=1,startingLife=20,decks=[DECK,DECK],auditDir=str(audit),own=dict(Hand=['Opt','Twincast' if COPY else 'Shock','Negate'],Battlefield=([NAME]+['Island']*5 if COPY else [NAME,'Island','Island','Island','Mountain']),Graveyard=['Lightning Bolt']),other=dict(Hand=['Negate'],Battlefield=['Forest','Grizzly Bears'],Graveyard=['Opt'],Library=['Grizzly Bears','Forest','Mountain'])))
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
   opt=None;shock=None;pod=None;target=None;before=None;cancelled=False;retry=False;shockResolved=False;freecast=False;seen=set();deadline=time.monotonic()+180
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
      opt=next(c['id'] for c in one['hand'] if c['name']=='Opt');shock=next(c['id'] for c in one['hand'] if c['name']==('Twincast' if COPY else 'Shock'));pod=next(c['id'] for c in one['battlefield'] if c['name']==NAME);target=next(c['id'] for c in two['battlefield'] if c['name']=='Grizzly Bears');pick='card:'+str(opt)
     elif before is None and len(v['stackItems'])==1:
      pick='card:'+str(shock) if any(c['id']==shock for c in one['hand']) else 'ok'
     elif before is None and len(v['stackItems'])==2:
      if COPY and not v['stackItems'][0]['copied']:pick='ok'
      else:
       if COPY:shock=v['stackItems'][0]['source']['id'];check(v['stackItems'][0]['copied'] and not v['stackItems'][1]['copied'],'Actual Twincast-generated copy above original Opt')
       snap(m,'before');before=observed(r);check([s['sourceId'] for s in before['stack']]==[shock,opt],'Exact two native spell identities and original order');pick='card:'+str(pod)
     elif cancelled and not retry:
      snap(m,'cancelled');state=observed(r)
      check(state['players']==before['players'] and state['stack']==before['stack'],'Exact both-owner resources, ordered two-spell stack IDs and cast history restored')
      check([s['source']['id'] for s in v['stackItems']]==[shock,opt],'Both original spells visible in native order')
      retry=True;pick='ok'
     elif retry and len(v['stackItems'])==1 and not shockResolved:
      snap(m,'shock-resolved');state=observed(r)
      check(v['stackItems'][0]['source']['id']==opt,'Original Opt remains below resolved Shock')
      if COPY:
       check(len(state['players'][0]['zones']['Hand'])==len(before['players'][0]['zones']['Hand'])+1 and len(state['players'][0]['zones']['Library'])==len(before['players'][0]['zones']['Library'])-1,'Restored copy resolves and draws exactly once before original')
       check(not any(c['id']==shock for z in state['players'][0]['zones'].values() for c in z),'Resolved copy ceases to exist')
      else:
       check(any(c['id']==shock for c in state['players'][0]['zones']['Graveyard']),'Original restored Shock resolved to graveyard first')
       check(not any(c['id']==target for c in state['players'][1]['zones']['Battlefield']) and any(c['id']==target for c in state['players'][1]['zones']['Graveyard']),'Restored Shock retains original target identity and kills exactly opposing Bears')
       check(len(state['players'][0]['zones']['Hand'])==len(before['players'][0]['zones']['Hand']),'Opt has not drawn before top spell resolves')
      shockResolved=True
     elif retry and not v['stackItems']:
      snap(m,'resolved');state=observed(r);original=before['players'][0];current=state['players'][0]
      check(shockResolved,'Observed original top Shock before lower Opt')
      check(not one['exileCards'],'No cancelled spell remains exiled')
      if COPY:
       check([c['id'] for c in current['zones']['Graveyard']]==[c['id'] for c in original['zones']['Graveyard']]+[opt],'Copy absent from graveyard, original Opt resolves normally')
       check(len(current['zones']['Hand'])==len(original['zones']['Hand'])+2 and len(current['zones']['Library'])==len(original['zones']['Library'])-2,'Copy and original each draw exactly once')
       check(current['spellsCastThisGame']==original['spellsCastThisGame']==2 and current['spellsCastThisTurn']==original['spellsCastThisTurn']==2,'Only Opt and Twincast cast; copy/restore do not count as casting')
       check(state['players'][1]==before['players'][1],'Copy resolution leaves exact opponent state unchanged')
       check(not next(c for c in one['battlefield'] if c['id']==pod)['tapped'] and sum(c['tapped'] for c in one['battlefield'] if c['name']=='Island')==3 and one['life']==two['life']==20 and not sum(one['manaPool']),'Only original U+UU costs remain, Pod fully refunded')
       result.update(status='PASS',result='PASS '+CASE,checks=checks,requests=len(seen),assertions=['Actually cast Opt then Twincast targeting it; native resolved Twincast creates copied Opt above original, Pod exiles exact copied spell then later mana cancel restores both original native stack IDs/order/resources/history','Restored native copy scries/draws then ceases to exist; original Opt subsequently scries/draws/graveyard, two draws total, cast count2 excludes copying/restoring, Pod mana/tap refunded and opponent state/privacy unchanged']);break
      check([c['id'] for c in current['zones']['Graveyard']]==[c['id'] for c in original['zones']['Graveyard']]+[shock,opt],'Exact original graveyard plus restored Shock then Opt')
      check(len(current['zones']['Hand'])==len(original['zones']['Hand'])+1 and len(current['zones']['Library'])==len(original['zones']['Library'])-1,'Lower original Opt draws exactly once')
      check(current['spellsCastThisGame']==original['spellsCastThisGame']==2 and current['spellsCastThisTurn']==original['spellsCastThisTurn']==2,'Neither restored spell was recast or double-counted')
      check(not next(c for c in one['battlefield'] if c['id']==pod)['tapped'] and sum(c['tapped'] for c in one['battlefield'] if c['name']=='Island')==1 and sum(c['tapped'] for c in one['battlefield'] if c['name']=='Mountain')==1,'Only original Opt U/Shock R paid; Pod mana/tap refunded')
      check(one['life']==two['life']==20 and not sum(one['manaPool']),'Exact life and empty pool')
      for zone in ('Hand','Library','Stack','Exile','Command'):
       check(state['players'][1]['zones'][zone]==before['players'][1]['zones'][zone],'Opponent unrelated zone preserved '+zone)
      check([c['id'] for c in state['players'][1]['zones']['Battlefield']]==[c['id'] for c in before['players'][1]['zones']['Battlefield'] if c['id']!=target],'No other opponent permanent removed')
      check([c['id'] for c in state['players'][1]['zones']['Graveyard']]==[c['id'] for c in before['players'][1]['zones']['Graveyard']]+[target],'Exact opponent ordered graveyard delta')
      result.update(status='PASS',result='PASS '+CASE,checks=checks,requests=len(seen),assertions=['Actually cast/pay Opt then Shock targeting opposing Grizzly Bears; Synthesis Pod exiles exact Shock/taps then cancels mana, preserving exact two-spell original stack order/instance IDs, both-player resources and two cast histories','Original restored targeted Shock resolves first and kills its exact opposing target; original lower Opt then scries/draws once, both enter own graveyard in order; Pod payment fully refunded, no recast/count/resource leak, unrelated opponent zones and privacy preserved']);break
    elif kind=='InputPassPriority':pass
    elif kind=='OrderCards':
     candidates=[o for o in opts if o['id'].startswith('select:') and not o.get('selected')]
     ordered=sorted(candidates,key=lambda o:('Exile' not in o['label'],o['label'].startswith('{')))
     pick=ordered[0]['id'] if ordered else 'confirm-cards'
    elif 'PayMana' in kind:
     if before is not None and not cancelled:
      snap(m,'committed-before-cancel');state=observed(r)
      check([s['sourceId'] for s in state['stack']]==[opt] and ([c['id'] for c in one['exileCards']]==[] if COPY else [c['id'] for c in one['exileCards']]==[shock]),'Actual top spell paid from stack; native copy ceases after leaving stack, original lower Opt remains');pick='cancel';cancelled=True
     else:pick='ok' if any(o['id']=='ok' and not o.get('disabled') for o in opts) else next(o['id'] for o in opts if o['id'].startswith('card:') and not o.get('disabled'))
    elif kind=='InputSelectTargets':
     snap(m,'target-'+str(len(seen)))
     if r['selectedCount']==0:pick=next(o['id'] for o in opts if o['id']=='card:'+str(target) and not o.get('disabled'))
     else:pick='ok'
    elif kind=='SelectStackTarget' and COPY:
     snap(m,'copy-target-'+str(len(seen)));pick='ok' if any(o['id']=='ok' and not o.get('disabled') for o in opts) else next(o['id'] for o in opts if o['id'].startswith('stack:') and not o.get('selected') and not o.get('disabled'))
    elif kind in ('InputConfirmMulligan','InputAttack','InputBlock'):pick='ok'
    elif kind=='InputConfirm':
     snap(m,'confirm-'+str(len(seen)));pick='ok'
     if retry and ('cast' in r['message'].lower() or 'play' in r['message'].lower()):freecast=True
    elif kind=='InputMulligan':pick='keep'
    elif kind=='RevealCards':snap(m,'reveal-'+str(len(seen)));pick='confirm-cards'
    elif kind=='Scry' and RESOLVE_ONLY:
     snap(m,'scry-'+str(len(seen)));pick=next((o['id'] for o in opts if o['id'].startswith('scry-top:')),'confirm-scry')
    elif kind in ('dialog','SelectEntities','InputSelectEntitiesFromList'):
     snap(m,'choice-'+str(len(seen)));pick=next(o['id'] for o in opts if o['label'].startswith(('Opt' if COPY else 'Shock')+' ('+str(shock)+')') and not o.get('disabled')) if r['message']=='oneOrNone: Exile from Stack' else next(o['id'] for o in opts if o['id'] not in ('cancel','no') and not o.get('disabled'))
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
