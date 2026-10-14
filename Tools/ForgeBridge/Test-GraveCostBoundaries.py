"""Actual collect-evidence/forage payment rollback and retry, opening-only fixture."""
import hashlib,json,os,queue,shutil,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
OUT=ROOT/os.environ.get('MTGB_GRAVE_COST_OUTPUT','.tools/grave-cost-boundary-1008/native')
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES=('evidence-before-mana','forage-grave-before-mana','forage-food-before-mana')
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def run(case):
 assert case in CASES
 evidence=case.startswith('evidence');food=case.startswith('forage-food')
 source_name='Tenth District Hero' if evidence else 'Camellia, the Seedmiser'
 field=[source_name,'Plains','Forest','Island']+([] if evidence else ['Squirrel Mob','Gingerbrute'])
 folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True);fixtures=['StateBranchFixture.java','CostPrimitiveFixture.java']
 for n in fixtures:shutil.copyfile(ROOT/'Tools/ForgeBridge/tests'/n,folder/n)
 shutil.copyfile(__file__,folder/'driver.py')
 result=dict(case=case,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],revision=CFG['revision'],driverSha256=sha(folder/'driver.py'),fixtures={n:sha(folder/n) for n in fixtures},status='RUNNING')
 write(folder/'result.json',result);p=None;checks=0
 def check(ok,why):
  nonlocal checks
  assert ok,why;checks+=1
 try:
  classes=folder/'classes';classes.mkdir();subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*[str(folder/n) for n in fixtures]],check=True)
  with tempfile.TemporaryDirectory(prefix='mtgb-grave-cost-') as temp,(folder/'stderr.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
   audit=folder/'audit';audit.mkdir()
   write(folder/'start.json',dict(session=case,seed=4,firstPlayerSeat=1,startingLife=20,decks=[DECK,DECK],auditDir=str(audit),own=dict(Hand=['Negate'],Battlefield=field,Graveyard=['Lightning Bolt','Opt','Negate'],Exile=['Grizzly Bears'],Library=['Island','Forest','Swamp','Mountain','Plains']*6),other=dict(Hand=['Negate'],Battlefield=['Elvish Visionary'],Exile=['Lightning Bolt'],Graveyard=['Opt'])))
   p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.CostPrimitiveFixture',str(RT/CFG['assets']),str(folder/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
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
    check(value['requestId']==r['requestId'],'exact observer version');return value['players']
   def snap(m,label):
    write(folder/(label+'-envelope.json'),m)
    for s,v in enumerate(m['views'],1):write(folder/(label+'-seat'+str(s)+'.json'),dict(v,request=m['request'],awaitingInput=True))
   ids_of=lambda cards:[c['id'] for c in cards]
   def comparable(players):
    value=json.loads(json.dumps(players))
    for player in value:player['zones']['Exile'].sort(key=lambda c:c['id'])
    return value
   before=None;cancelled=False;retried=False;stackseen=False;seen=set();wanted=[];deadline=time.monotonic()+180
   for step in range(130):
    check(time.monotonic()<deadline,'Deadline');m=messages.get(timeout=35)
    if isinstance(m,Exception):raise m
    check(m is not None,'Native exited');trace.write(json.dumps(m)+'\n');trace.flush();check(m['type'] not in ('fatal','ended','rejected'),m)
    if m['type']!='request':continue
    r=m['request']
    if r['requestId'] in seen:continue
    seen.add(r['requestId']);v=m['views'][0];one,two=v['players'];kind=r['kind'];opts=r['options'];ids=[o['id'] for o in opts];pick='ok'
    check(not m['views'][0]['players'][1]['hand'] and not m['views'][1]['players'][0]['hand'],'both private hands concealed')
    if kind=='InputPassPriority' and r['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
     if before is None:
      before=observed(r);source=next(c['id'] for c in one['battlefield'] if c['name']==source_name);snap(m,'before')
      if food:wanted=[next(c['id'] for c in one['battlefield'] if c['name']=='Gingerbrute')]
      else:wanted=[c['id'] for c in before[0]['zones']['Graveyard'] if not evidence or c['name'] in ('Lightning Bolt','Opt')]
      check(len(wanted)==(1 if food else 2 if evidence else 3),'Exact planned payment IDs');pick='card:'+str(source)
     elif cancelled and not retried:
      state=observed(r);snap(m,'cancelled');check(comparable(state)==comparable(before),'cancel restores exact both-owner resources/identities and no pending cost-trigger effect')
      check(not sum(one['manaPool']) and not sum(two['manaPool']) and not v['stackItems'],'no cancelled effect/mana remains')
      retried=True;pick='card:'+str(source)
     elif retried:
      state=observed(r);snap(m,'resolved');check(stackseen,'actual retry entered stack');bz,az=before[0]['zones'],state[0]['zones']
      check(state[1]==before[1],'opponent resources/ordered zones/identities unchanged')
      check(all(az[z]==bz[z] for z in bz if z not in ('Graveyard','Exile','Battlefield')),'own private/library/command unchanged')
      if food:
       check(ids_of(az['Graveyard'])==ids_of(bz['Graveyard'])+wanted,'exact food sacrificed once into owner graveyard')
       check(az['Exile']==bz['Exile'],'food path does not exile graveyard cards')
      else:
       check(ids_of(az['Graveyard'])==[i for i in ids_of(bz['Graveyard']) if i not in wanted],'exact chosen graveyard cards removed with remaining order retained')
       check(set(ids_of(az['Exile']))==set(ids_of(bz['Exile'])+wanted),'exact chosen identities exiled once')
      check(sum(c['land'] and c['tapped'] for c in one['battlefield'])==2 and not sum(one['manaPool']) and not sum(two['manaPool']),'exact two mana once/no remainder')
      check(one['life']==two['life']==20,'life unchanged')
      if evidence:
       hero=next(c for c in one['battlefield'] if c['id']==source)
       check(hero['power']==4 and hero['toughness']==4 and any(k.upper()=='VIGILANCE' for k in hero.get('keywords',[])),'actual Hero becomes base4/4 vigilance')
       check(ids_of(az['Battlefield'])==ids_of(bz['Battlefield']),'original battlefield identities/order retained')
      else:
       mob=next(c for c in one['battlefield'] if c['name']=='Squirrel Mob')
       check(any(c.get('count')==1 and (c.get('kind')=='P1P1' or c.get('name')=='+1/+1') for c in mob.get('counters',[])),'actual other squirrel receives one counter')
       new=[c for c in one['battlefield'] if c['id'] not in ids_of(bz['Battlefield'])]
       check(len(new)==(1 if food else 0),'exact food-only sacrifice trigger token count')
       if food:check(new[0].get('token') and new[0]['power']==2 and new[0]['toughness']==2,'cost-trigger squirrel enters before resolved counter effect')
       check([c['id'] for c in az['Battlefield'] if c['id'] in ids_of(bz['Battlefield'])]==[i for i in ids_of(bz['Battlefield']) if i not in (wanted if food else [])],'original retained battlefield order/IDs')
      result.update(status='PASS',result='PASS '+case,checks=checks,requests=len(seen),assertions=['Actual '+source_name+' payment ordered before mana and selected exact own IDs; committed cost before cancel; exact both-owner resource/ordered-zone rollback and no cancelled trigger effect','Retry pays two mana once and performs exact '+('collect-evidence two MV2 exile and base4/4 vigilance' if evidence else 'Food sacrifice, one cost-trigger Squirrel then two counter recipients' if food else 'forage three graveyard cards exile and one squirrel counter')+'; private hands and main-priority recovery']);break
    elif kind=='InputPassPriority':
     if retried and v['stackItems']:stackseen=True;snap(m,'retry-stack')
    elif kind=='OrderCards':
     candidates=[o for o in opts if o['id'].startswith('select:') and not o.get('selected')]
     pick=next((o['id'] for o in candidates if not o['label'].startswith('{')),candidates[0]['id']) if candidates else 'confirm-cards'
    elif kind in ('InputSelectCardsFromList','InputSelectEntitiesFromList','SelectCards'):
     snap(m,'picker-'+str(len(seen)))
     candidates=[o for o in opts if o.get('card',{}).get('id') in wanted and not o.get('selected') and not o.get('disabled')]
     actions=[o for o in opts if o['id'] in ('ok','confirm-cards') and not o.get('disabled')]
     if actions and not candidates:pick=actions[0]['id']
     elif candidates:pick=candidates[0]['id']
     else:raise AssertionError(('No planned payment action',kind,ids,wanted))
    elif 'PayMana' in kind:
     if not cancelled:
      state=observed(r);az,bz=state[0]['zones'],before[0]['zones'];snap(m,'committed-before-cancel')
      if food:check(ids_of(az['Graveyard'])==ids_of(bz['Graveyard'])+wanted and not any(i in ids_of(az['Battlefield']) for i in wanted),'food actually sacrificed before cancellation')
      else:check(set(ids_of(az['Exile']))==set(ids_of(bz['Exile'])+wanted) and not set(wanted)&set(ids_of(az['Graveyard'])),'graveyard cost actually committed before cancellation')
      pick='cancel';cancelled=True
     else:pick='ok' if any(o['id']=='ok' and not o.get('disabled') for o in opts) else next(o['id'] for o in opts if o['id'].startswith('card:') and not o.get('disabled'))
    elif kind in ('InputConfirmMulligan','InputAttack','InputBlock'):pick='ok'
    elif kind=='InputConfirm':snap(m,'confirm-'+str(len(seen)));pick='cancel' if r['message']=='Sacrifice Food' and not food else 'ok'
    elif kind=='InputMulligan':pick='keep'
    elif kind=='dialog':pick=next(o['id'] for o in opts if o['id'] not in ('cancel','no') and not o.get('disabled'))
    else:raise AssertionError(('Unhandled',kind,r['message'],ids))
    check(pick in ids and not next(o for o in opts if o['id']==pick).get('disabled'),('legal option',kind,pick,ids))
    print(case,step,kind,pick,r['message'][:90].replace('\n',' '),flush=True)
    response=dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick);trace.write(json.dumps(dict(response=response))+'\n');trace.flush();p.stdin.write(json.dumps(response)+'\n');p.stdin.flush()
   else:raise AssertionError('Step limit')
 except Exception:result.update(status='FAIL',checks=checks,result='FAIL '+case+': '+traceback.format_exc())
 finally:
  if p is not None and p.poll() is None:p.kill();p.wait()
 write(folder/'result.json',result);write(OUT/(case+'-result.json'),result);return result
if __name__=='__main__':
 OUT.mkdir(parents=True,exist_ok=True);selected=sys.argv[1:] or list(CASES);results=[];write(OUT/'run-summary.json',dict(status='RUNNING',selected=selected))
 for case in selected:r=run(case);results.append(r);print(r['result'],flush=True)
 status='PASS' if all(r['status']=='PASS' for r in results) else 'FAIL';write(OUT/'run-summary.json',dict(status=status,results=results));sys.exit(status!='PASS')
