"""Actual mixed hand/battlefield BeholdExile payments, cancellation and linked return."""
import hashlib,json,os,queue,shutil,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
OUT=ROOT/os.environ.get('MTGB_BEHOLD_EXILE_OUTPUT','.tools/behold-exile-1008/native')
CASES=('hand-cancel-retry-return','battlefield-cancel-retry-return');NAME='Champion of the Clachan';KIN='Goldmeadow Harrier'
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def run(case):
 hand=case==CASES[0];plans=['picker','mana'] if hand else ['mana'];attempt=0
 folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True);fixtures=['StateBranchFixture.java','StackExileCostFixture.java']
 for n in fixtures:shutil.copyfile(ROOT/'Tools/ForgeBridge/tests'/n,folder/n)
 shutil.copyfile(__file__,folder/'driver.py')
 result=dict(case=case,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],revision=CFG['revision'],driverSha256=sha(folder/'driver.py'),fixtures={n:sha(folder/n) for n in fixtures},status='RUNNING');write(folder/'result.json',result)
 checks=0;p=None
 def check(ok,why):
  nonlocal checks
  assert ok,why;checks+=1
 try:
  classes=folder/'classes';classes.mkdir();subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*[str(folder/n) for n in fixtures]],check=True)
  with tempfile.TemporaryDirectory(prefix='mtgb-behold-exile-') as temp,(folder/'stderr.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
   audit=folder/'audit';audit.mkdir()
   write(folder/'start.json',dict(session=case,seed=4,firstPlayerSeat=1,startingLife=20,decks=[DECK,DECK],auditDir=str(audit),own=dict(Hand=[NAME,KIN,'Unsummon','Negate'],Battlefield=['Plains']*4+['Island',KIN],Graveyard=['Lightning Bolt'],counters=[dict(name=KIN,kind='CHARGE',count=3)]),other=dict(Hand=['Negate'],Battlefield=['Forest','Grizzly Bears'],Graveyard=['Opt'])))
   p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.StackExileCostFixture',str(RT/CFG['assets']),str(folder/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
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
     try:d=json.loads((audit/(str(r['requestId'])+'.json')).read_text());break
     except (FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
    else:raise AssertionError('Exact observer missing '+str(r['requestId']))
    check(d['requestId']==r['requestId'],'Observer request identity');return d
   def snap(m,label):
    write(folder/(label+'-envelope.json'),m);write(folder/(label+'-observer.json'),observed(m['request']))
    for seat,v in enumerate(m['views'],1):write(folder/(label+'-seat'+str(seat)+'.json'),dict(v,request=m['request'],awaitingInput=True))
   before=None;champion=None;candidate=None;handkin=None;boardkin=None;bounce=None;cancelled=False;entered=False;bouncing=False;stackSeen=False;pickerSubstage=0;seen=set();deadline=time.monotonic()+180
   for step in range(240):
    check(time.monotonic()<deadline,'Deadline');m=messages.get(timeout=35)
    if isinstance(m,Exception):raise m
    check(m is not None,'Native alive');trace.write(json.dumps(m)+'\n');trace.flush();check(m['type'] not in ('fatal','ended','rejected'),m)
    if m['type']!='request':continue
    r=m['request'];v=m['views'][0];one,two=v['players'];kind=r['kind'];opts=r['options'];ids=[o['id'] for o in opts];pick='ok'
    if r['requestId'] in seen:continue
    seen.add(r['requestId']);check(not m['views'][0]['players'][1]['hand'],'Unrelated opponent hand hidden')
    disclosed={c['id'] for c in m['views'][1]['players'][0]['hand']};check(disclosed.issubset({candidate,champion} if candidate else set()),'Only selected or publicly announced/returned identities may be known; unrelated hand stays hidden')
    if kind=='InputPassPriority' and r['seat']==1 and v['phase']=='MAIN1':
     if before is None:
      champion=next(c['id'] for c in one['hand'] if c['name']==NAME);bounce=next(c['id'] for c in one['hand'] if c['name']=='Unsummon');handkin=next(c['id'] for c in one['hand'] if c['name']==KIN);boardkin=next(c['id'] for c in one['battlefield'] if c['name']==KIN);candidate=handkin if hand else boardkin
      snap(m,'before');before=observed(r);pick='card:'+str(champion)
     elif cancelled and not v['stackItems']:
      snap(m,'cancelled-'+str(attempt));now=observed(r)
      check(now['players']==before['players'] and now['stack']==before['stack'],'Exact original both-player resources/zones/order/counters/history restored after BeholdExile cancellation')
      check(not sum(one['manaPool']) and not any(c['tapped'] for c in one['battlefield']),'No cancelled mana/tap leak')
      attempt+=1;cancelled=False;pick='card:'+str(champion)
     elif not v['stackItems'] and not entered and any(c['id']==champion for c in one['battlefield']):
      snap(m,'entered');now=observed(r);check(stackSeen,'Actually cast Champion entered stack before resolution');check([c['id'] for c in one['exileCards']]==[candidate],'Exactly chosen original Kithkin remains exiled')
      check(any(c['id']==handkin for c in one['hand'])==(not hand),'Unselected hand Kithkin preserved')
      check(any(c['id']==boardkin for c in one['battlefield'])==hand,'Unselected battlefield Kithkin preserved')
      if hand:check(next(c for c in one['battlefield'] if c['id']==boardkin)['power']==2,'Actual Champion static buff affects other Kithkin')
      check(now['players'][0]['spellsCastThisGame']==1,'Only successful Champion cast counts')
      check(sum(c['tapped'] for c in one['battlefield'] if c['name'] in ('Plains','Island'))==4,'Exactly four mana paid for Champion')
      entered=True;bouncing=True;pick='card:'+str(bounce)
     elif bouncing and not v['stackItems']:
      snap(m,'returned');now=observed(r);own=now['players'][0];old=before['players'][0]
      check(not one['exileCards'] and any(c['id']==candidate for c in one['hand']),'Original linked exiled card returns to its owner hand after source leaves')
      check(any(c['id']==champion for c in one['hand']) and not any(c['id']==champion for c in one['battlefield']),'Actual Unsummon returns original Champion')
      check([c['id'] for c in own['zones']['Graveyard']]==[c['id'] for c in old['zones']['Graveyard']]+[bounce],'Only actual Unsummon added to original graveyard')
      check(own['zones']['Library']==old['zones']['Library'],'No cancelled or successful flow changes library')
      expectedHand={c['id'] for c in old['zones']['Hand'] if c['id']!=bounce}|(set() if hand else {boardkin})
      check({c['id'] for c in own['zones']['Hand']}==expectedHand and len(own['zones']['Hand'])==len(expectedHand),'Exact final hand identities with no duplicate/stale linked return')
      check(now['players'][1]==before['players'][1],'Opponent exact original resources and ordered zones unchanged')
      check(own['spellsCastThisGame']==2 and own['spellsCastThisTurn']==2,'Exactly Champion and Unsummon counted, no cancelled cast leakage')
      check(one['life']==two['life']==20 and not sum(one['manaPool']),'Exact life and empty mana at restored main priority')
      check(sum(c['tapped'] for c in one['battlefield'] if c['name'] in ('Plains','Island'))==5,'Exactly four Champion plus one bounce mana paid')
      if hand:check(next(c for c in one['battlefield'] if c['id']==boardkin)['power']==1,'Champion static buff naturally ends on leaving')
      else:check(next(c for c in own['zones']['Hand'] if c['id']==boardkin)['counters']=='[]','Exiled/returned original battlefield card resets its original counters')
      result.update(status='PASS',result='PASS '+case,checks=checks,requests=len(seen),assertions=['Actually cast Champion of the Clachan with mixed hand/battlefield same-name Kithkin candidates; '+('initial picker cancel then committed hand exile/mana cancel' if hand else 'committed battlefield exile/mana cancel')+' restores exact original both-player zones/order/counters/history/resources; retry pays and exiles exact chosen original once','Successful actual Champion enters, correct other-Kithkin static buff; cast/pay Unsummon to return source, native leaves trigger returns exactly originally linked exiled card to owner hand; accurate mana/history/no unrelated private identities/library/opponent changes and resumed main priority']);break
     elif v['stackItems']:stackSeen=True
    elif kind=='InputPassPriority':pass
    elif kind=='OrderCards':
     snap(m,'order-'+str(attempt)+'-'+str(len(seen)));choices=[o for o in opts if o['id'].startswith('select:') and not o.get('selected')];pick=sorted(choices,key=lambda o:('Behold' not in o['label'],o['label']))[0]['id'] if choices else 'confirm-cards'
    elif 'PayMana' in kind:
     if not bouncing and attempt<len(plans) and plans[attempt]=='mana':
      snap(m,'committed-'+str(attempt));check([c['id'] for c in one['exileCards']]==[candidate],'Exact chosen Kithkin actually exiled before later mana cancellation');check(not any(c['id']==candidate for c in one['hand']+one['battlefield']),'Paid Kithkin absent from original zone')
      if not any(c['tapped'] for c in one['battlefield'] if c['name']=='Plains'):pick=next('card:'+str(c['id']) for c in one['battlefield'] if c['name']=='Plains' and not c['tapped'] and 'card:'+str(c['id']) in ids)
      else:pick='cancel';cancelled=True
     else:
      land='Island' if bouncing else 'Plains';available=[c for c in one['battlefield'] if c['name']==land and not c['tapped'] and 'card:'+str(c['id']) in ids]
      pick='card:'+str(available[0]['id']) if available else 'ok'
    elif kind in ('InputSelectCardsFromList','SelectCards','InputSelectEntitiesFromList'):
     snap(m,'picker-'+str(attempt)+'-'+str(len(seen)));candidates=[o for o in opts if o.get('card',{}).get('name')==KIN]
     check({o['card']['id'] for o in candidates}=={handkin,boardkin},'Actual cost candidates include exact same-name hand and battlefield originals')
     check(r.get('selectionPurpose')=='beholdExile' and r.get('sourceCardId')==champion,'Actual currently visited exile cost and source projected without prompt parsing')
     if attempt<len(plans) and plans[attempt]=='picker':
      if pickerSubstage==0:pick='card:'+str(boardkin);pickerSubstage=1
      elif pickerSubstage==1:pick='reset-cards';pickerSubstage=2
      else:pick='cancel';cancelled=True
     else:pick='ok' if r.get('selectedCount')==1 else 'card:'+str(candidate)
    elif kind=='InputSelectTargets':
     pick='ok' if any(o['id']=='ok' and not o.get('disabled') for o in opts) else next(o['id'] for o in opts if o.get('card',{}).get('id')==champion and not o.get('disabled'))
    elif kind=='RevealCards':snap(m,'reveal-'+str(attempt)+'-'+str(len(seen)));check({c['id'] for c in r['previewCards']}=={candidate},'Exactly cost-selected identity disclosed');pick='confirm-cards'
    elif kind in ('InputConfirmMulligan','InputConfirm','InputAttack','InputBlock'):pick='ok'
    elif kind=='InputMulligan':pick='keep'
    elif kind=='dialog':snap(m,'dialog-'+str(len(seen)));pick=next(o['id'] for o in opts if o['id'] not in ('cancel','no') and not o.get('disabled'))
    else:raise AssertionError(('Unhandled',kind,r['message'],ids))
    check(pick in ids and not next(o for o in opts if o['id']==pick).get('disabled'),('Legal original option',kind,pick,ids))
    if kind!='InputPassPriority' or pick!='ok':print(case,step,kind,pick,r['message'][:100].replace('\n',' '),flush=True)
    response=dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick);trace.write(json.dumps(dict(response=response))+'\n');trace.flush();p.stdin.write(json.dumps(response)+'\n');p.stdin.flush()
   else:raise AssertionError('Step limit')
 except Exception:result.update(status='FAIL',checks=checks,result='FAIL '+case+': '+traceback.format_exc())
 finally:
  if p is not None and p.poll() is None:p.kill();p.wait()
 write(folder/'result.json',result);write(OUT/(case+'-result.json'),result);return result
if __name__=='__main__':
 selected=sys.argv[1:] or list(CASES)
 if any(c not in CASES for c in selected):raise SystemExit('Unknown case '+str(selected))
 OUT.mkdir(parents=True,exist_ok=True);results=[]
 for c in selected:
  r=run(c);results.append(r);print(r['result'],flush=True)
 status='PASS' if all(r['status']=='PASS' for r in results) else 'FAIL';write(OUT/'run-summary.json',dict(status=status,selected=selected,results=results));sys.exit(status!='PASS')
