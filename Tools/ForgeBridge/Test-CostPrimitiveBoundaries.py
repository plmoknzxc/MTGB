"""R12 counter-cost cancellation and irreversible top-library cost boundaries."""
import hashlib,json,os,queue,shutil,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
OUT=ROOT/os.environ.get('MTGB_COST_PRIMITIVE_OUTPUT','.tools/cost-primitives-1008/native')
CASES=['counter-before-mana','mana-before-counter','exile-top-before-mana','mill-before-mana','mill-exact-four','mill-insufficient-three','mill-upkeep-exact-two','mill-upkeep-insufficient-one','put-counter-cancel','put-counter-doubled-cancel','remove-any-counter-cancel','remove-two-counter-cancel','remove-two-counter-split-cancel','unattach-before-mana']
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
def write(p,v):Path(p).write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()

def run(case):
 assert case in CASES;put=case.startswith('put-counter-');multiple=case.startswith('remove-two-counter-');split=case=='remove-two-counter-split-cancel';anycounter=case=='remove-any-counter-cancel' or multiple;counter=case in ('counter-before-mana','mana-before-counter');mill=case.startswith('mill-');exile=case=='exile-top-before-mana';manafirst=case=='mana-before-counter';edge=case in ('mill-exact-four','mill-insufficient-three');insufficient=case=='mill-insufficient-three'
 unattach=case=='unattach-before-mana'
 source_name='Pentavus' if counter else 'Rot Farm Skeleton' if mill else 'Arc-Slogger'
 upkeep=case.startswith('mill-upkeep-')
 if upkeep:source_name='Deep Spawn'
 own=dict(Hand=['Negate'],Battlefield=([source_name,'Island'] if counter else ['Swamp','Forest','Island','Mountain'] if mill else [source_name,'Mountain']),Graveyard=[source_name] if mill else [],Library=['Island','Forest','Swamp','Mountain','Plains']*6)
 if edge:own['libraryLimit']=3 if insufficient else 4
 if upkeep:own.update(Battlefield=[source_name],Graveyard=[],libraryLimit=1 if case.endswith('one') else 2)
 if put:
  source_name='Yisan, the Wanderer Bard';expected_counter=2 if 'doubled' in case else 1
  target_name='Grizzly Bears' if expected_counter==2 else 'Birds of Paradise'
  own.update(Battlefield=[source_name,'Forest','Island','Mountain']+(['Vorinclex, Monstrous Raider'] if expected_counter==2 else []),Graveyard=[],Library=[target_name]+own['Library'])
 if anycounter:
  source_name='Novijen Sages' if multiple else 'Moonlit Lamenter';own.update(Battlefield=[source_name,'Plains','Island'],Graveyard=[],counters=[dict(name=source_name,kind='P1P1' if multiple else 'CHARGE',count=3 if multiple else 2),dict(name=source_name,kind='STUN',count=1)])
  if split:own['Battlefield'].append('Grizzly Bears');own['counters'][0]['count']=1;own['counters'].append(dict(name='Grizzly Bears',kind='P1P1',count=1))
 if unattach:
  source_name='Sunforger';own.update(Battlefield=[source_name,'Grizzly Bears','Plains','Mountain'],Graveyard=[],Library=['Lightning Bolt']+own['Library'],attach=dict(equipment=source_name,creature='Grizzly Bears'))
 folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True);fixtures=['StateBranchFixture.java','CostPrimitiveFixture.java']
 for n in fixtures:shutil.copyfile(ROOT/'Tools/ForgeBridge/tests'/n,folder/n)
 shutil.copyfile(__file__,folder/'driver.py');result=dict(case=case,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],revision=CFG['revision'],driverSha256=sha(folder/'driver.py'),fixtures={n:sha(folder/n) for n in fixtures},status='RUNNING');write(folder/'result.json',result)
 p=None;checks=0
 def check(ok,why):
  nonlocal checks
  assert ok,why;checks+=1
 try:
  classes=folder/'classes';classes.mkdir();subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*[str(folder/n) for n in fixtures]],check=True)
  with tempfile.TemporaryDirectory(prefix='mtgb-cost-primitive-') as temp,(folder/'stderr.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
   audit=folder/'audit';audit.mkdir();write(folder/'start.json',dict(session=case,seed=4,firstPlayerSeat=1,startingLife=20,decks=[DECK,DECK],own=own,other=dict(Hand=['Negate']),auditDir=str(audit)))
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
    else:raise AssertionError('Missing observer '+str(r['requestId']))
    check(value['requestId']==r['requestId'],'exact native observer version');return value['players']
   def snap(m,label):
    write(folder/(label+'-envelope.json'),m)
    for s,v in enumerate(m['views'],1):write(folder/(label+'-seat'+str(s)+'.json'),dict(v,request=m['request'],awaitingInput=True))
   before=None;source=None;cancelled=False;retried=False;stackseen=False;selected_order=False;seen=set();paid=0;confirmed_cost=0;decline_seat=1;partialcancelled=False;partialrestored=False;deadline=time.monotonic()+180
   indices=lambda z:[c['id'] for c in z]
   def counter_number(card):return sum(c['count'] for c in card['counters'] if c['kind']==('VERSE' if put else 'CHARGE' if anycounter and not multiple else 'P1P1'))
   for step in range(200):
    check(time.monotonic()<deadline,'Deadline');m=messages.get(timeout=35)
    if isinstance(m,Exception):raise m
    check(m is not None,'Native exited');trace.write(json.dumps(m)+'\n');trace.flush();check(m['type'] not in ('fatal','ended','rejected'),m)
    if m['type']!='request':continue
    r=m['request']
    if r['requestId'] in seen:continue
    seen.add(r['requestId']);v=m['views'][0];one,two=v['players'];kind=r['kind'];opts=r['options'];ids=[o['id'] for o in opts];pick='ok';message=r['message'].lower()
    check(not m['views'][0]['players'][1]['hand'] and not m['views'][1]['players'][0]['hand'],'both private hands concealed')
    if upkeep and kind=='InputPassPriority' and v['phase']=='UPKEEP':
     if before is None:
      check(bool(v['stackItems']) and 'Deep Spawn' in json.dumps(v['stackItems']),'real native Deep Spawn upkeep trigger on stack')
      before=observed(r);source=next(c['id'] for c in one['battlefield'] if c['name']==source_name);snap(m,'before')
      check(one['libraryCount']==own['libraryLimit'],'exact upkeep opening library count')
     elif not v['stackItems']:
      state=observed(r);snap(m,'resolved');bz,az=before[0]['zones'],state[0]['zones'];enough=own['libraryLimit']==2
      check(indices(az['Library'])==([] if enough else indices(bz['Library'])),'exact upkeep library suffix')
      check(indices(az['Graveyard'])==indices(bz['Graveyard'])+(indices(bz['Library']) if enough else [source]),'exact milled identities or native sacrifice destination')
      check(set(indices(az['Battlefield']))==set(indices(bz['Battlefield']))-(set() if enough else {source}),'original source retained when paid or sacrificed when unable')
      check(az['Hand']==bz['Hand'] and az['Exile']==bz['Exile'] and state[1]==before[1] and one['life']==two['life']==20 and not sum(one['manaPool']),'other exact zones/resources and opponent unchanged')
      check(confirmed_cost==(1 if enough else 0),'exact native cost confirmation availability')
      result.update(status='PASS',result='PASS '+case,checks=checks,requests=len(seen),assertions=['Actual Deep Spawn upkeep trigger/resolution-time Mill<2> cost with exact2 or insufficient1: original source retained/all2 exact IDs milled, or no unpayable confirmation and source sacrificed','Other native zones/resources/private hands retained and upkeep-priority recovery']);break
    if edge and before is not None and insufficient:
     state=observed(r);snap(m,'declined-insufficient')
     check(kind=='InputPassPriority' and r['seat']==decline_seat and v['phase']=='MAIN1' and not v['stackItems'],'unpayable activation/absent action preserves main priority flow')
     check(state==before and one['libraryCount']==3 and not sum(one['manaPool']),'insufficient cost leaves all native resources exact')
     check(not any(o['id']=='card:'+str(source) and not o.get('requiresPaymentCheck') for o in opts),'unaffordable source not projected as payable')
     result.update(status='PASS',result='PASS '+case,checks=checks,requests=len(seen),assertions=['Native mill4 activation absent/unpayable with library3; actual main-priority continuation leaves exact native resources/private hands unchanged']);break
    if kind=='InputPassPriority' and r['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
     if before is None:
      before=observed(r);source=next(c['id'] for c in one['graveyardCards' if mill else 'battlefield'] if c['name']==source_name);snap(m,'before');pick='card:'+str(source)
      if counter:check(counter_number(next(c for c in one['battlefield'] if c['id']==source))==5,'native initial five P1P1 counters')
      if put or anycounter:check(counter_number(next(c for c in one['battlefield'] if c['id']==source))==(0 if put else 1 if split else 3 if multiple else 2),'exact native opening source counters')
      if unattach:check(next(c for c in one['battlefield'] if c['name']=='Grizzly Bears')['power']==6,'native opening equipped +4 power')
      if edge:
       check(one['libraryCount']==(3 if insufficient else 4),'exact opening native library count');retried=not insufficient
       if insufficient and pick not in ids:pick='ok';decline_seat=2
     elif edge:
      state=observed(r);snap(m,'resolved');bz,az=before[0]['zones'],state[0]['zones']
      check(stackseen,'equal-library cost activation actually entered stack')
      check(not az['Library'] and one['libraryCount']==0,'mill exact4 leaves library empty')
      check(indices(az['Graveyard'])==[i for i in indices(bz['Graveyard']) if i!=source]+indices(bz['Library']),'exact all four library IDs milled once/source returned')
      check(set(indices(az['Battlefield']))==set(indices(bz['Battlefield']))|{source},'original source ID returned battlefield')
      check(az['Hand']==bz['Hand'] and az['Exile']==bz['Exile'] and state[1]==before[1],'other actor zones and opponent resources unchanged')
      check(one['life']==two['life']==20 and not sum(one['manaPool']) and sum(c['land'] and c['tapped'] for c in one['battlefield'])==4,'exact2BG paid and no empty-library loss before draw')
      result.update(status='PASS',result='PASS '+case,checks=checks,requests=len(seen),assertions=['Actual native mill4 activation with exact4 remaining: confirmed cost, paid2BG, original source ID returned battlefield, exact four IDs milled once and library empty','Both private hands, life and other zones retained, real ability stack and main-priority recovery']);break
     elif multiple and partialcancelled and not partialrestored:
      state=observed(r);snap(m,'partial-cancelled');check(state==before,'cancel partial counter draft leaves all exact resources unchanged');partialrestored=True;selected_order=False;pick='card:'+str(source)
     elif cancelled and not retried:
      state=observed(r);snap(m,'cancelled');bz,az=before[0]['zones'],state[0]['zones'];check(state[1]==before[1],'other seat all native state unchanged')
      if counter or put or anycounter or unattach:check(state==before,'exact reversible cost cancellation resource restore; full native snapshots retained')
      else:
       n=4 if mill else 10;dest='Graveyard' if mill else 'Exile';check(indices(az['Library'])==indices(bz['Library'])[n:],'irreversible exact library suffix')
       check(indices(az[dest])==indices(bz[dest])+indices(bz['Library'])[:n],'exact irreversible cost destination/order')
       check(all(az[z]==bz[z] for z in bz if z not in ('Library',dest)),'all reversible resources unchanged')
      check(one['life']==two['life']==20 and not sum(one['manaPool']),'cancel causes no effect or life/mana loss');retried=True;selected_order=False;pick='card:'+str(source)
      if unattach:check(next(c for c in one['battlefield'] if c['name']=='Grizzly Bears')['power']==6,'equipment power restored after cancellation')
     elif retried:
      state=observed(r);snap(m,'resolved');bz,az=before[0]['zones'],state[0]['zones'];check(stackseen,'actual retried ability on stack');check(state[1]['zones']==before[1]['zones'],'other seat exact native zones unchanged')
      check(one['life']==20 and two['life']==(18 if exile else 17 if unattach else 20),'exact actual effect life');check(not sum(one['manaPool']),'mana spent exactly')
      if unattach:
       bolt=next(c['id'] for c in bz['Library'] if c['name']=='Lightning Bolt')
       check(set(indices(az['Library']))==set(indices(bz['Library']))-{bolt},'shuffle retains all exact other library identities')
       check(indices(az['Graveyard'])==indices(bz['Graveyard'])+[bolt] and az['Hand']==bz['Hand'] and az['Exile']==bz['Exile'],'free-cast exact Bolt resolves into graveyard; other zones unchanged')
       check(indices(az['Battlefield'])==indices(bz['Battlefield']),'all original battlefield identities retained')
       check(next(c for c in az['Battlefield'] if c['id']==source)['attachedTo']==-1,'successful unattach committed')
       check(next(c for c in one['battlefield'] if c['name']=='Grizzly Bears')['power']==2,'successful unattach removes +4 power')
       check(sum(c['land'] and c['tapped'] for c in one['battlefield'])==2,'exact RW paid; free Bolt costs no mana')
      elif put or anycounter:
       c=next(c for c in (one['graveyardCards'] if split else one['battlefield']) if c['id']==source);check(counter_number(c)==(expected_counter if put else 0 if split else 1),'exact successful source counter quantity')
       if split:check(counter_number(next(c for c in one['battlefield'] if c['name']=='Grizzly Bears'))==0,'second exact resource exhausted once')
       check(indices(az['Graveyard'])==indices(bz['Graveyard'])+([source] if split else []) and az['Exile']==bz['Exile'],'exact public zones including0/0 source native state-based death')
       if put:
        targets=[c for c in one['battlefield'] if c['name']==target_name];check(len(targets)==1 and targets[0]['id'] in indices(bz['Library']),'exact native mana-value search result identity')
        target_id=targets[0]['id'];check(set(indices(az['Library']))==set(indices(bz['Library']))-{target_id} and az['Hand']==bz['Hand'],'library shuffle retains all other IDs/hand unchanged')
        check(set(indices(az['Battlefield']))==set(indices(bz['Battlefield']))|{target_id} and c['tapped'],'exact searched battlefield/source tapped')
       else:
        top=indices(bz['Library'])[0];check(indices(az['Library'])==indices(bz['Library'])[1:] and indices(az['Hand'])==indices(bz['Hand'])+[top],'actual draw of exact original top identity')
        check(set(indices(az['Battlefield']))==set(indices(bz['Battlefield']))-({source} if split else set()),'original battlefield identities,0/0 source dies after successful payment')
        if split:check(not c['counters'],'leaving battlefield clears source counters normally')
        else:check(next(k['count'] for k in c['counters'] if k['kind']=='STUN')==1,'unchosen counter kind retained')
       check(sum(c['land'] and c['tapped'] for c in one['battlefield'])==(3 if put else 1 if multiple else 2),'exact successful mana cost source count')
      elif counter:
       c=next(c for c in one['battlefield'] if c['id']==source);check(counter_number(c)==4 and c['power']==c['toughness']==4,'exact one final counter removed/base stats');tokens=[c for c in one['battlefield'] if c.get('token')];check(len(tokens)==1 and tokens[0]['power']==tokens[0]['toughness']==1 and 'FLYING' in tokens[0]['keywords'],'exact native Pentavite token')
       check(indices(az['Library'])==indices(bz['Library']) and az['Hand']==bz['Hand'] and az['Graveyard']==bz['Graveyard'] and az['Exile']==bz['Exile'],'other actor zones unchanged');check(sum(c['land'] and c['tapped'] for c in one['battlefield'])==1,'exact one mana source paid')
      else:
       n=8 if mill else 20;dest='Graveyard' if mill else 'Exile';check(indices(az['Library'])==indices(bz['Library'])[n:],'exact two library payments')
       expected=[i for i in indices(bz[dest]) if not (mill and i==source)]+indices(bz['Library'])[:n];check(indices(az[dest])==expected,'exact two cost destinations/order and source move')
       check(az['Hand']==bz['Hand'] and az['Exile' if mill else 'Graveyard']==bz['Exile' if mill else 'Graveyard'],'other actor hidden/public zones unchanged')
       check(set(indices(az['Battlefield']))==set(indices(bz['Battlefield']))|({source} if mill else set()),'exact resolved source identity')
       check(sum(c['land'] and c['tapped'] for c in one['battlefield'])==(4 if mill else 1),'exact paid land count')
      result.update(status='PASS',result='PASS '+case,checks=checks,requests=len(seen),assertions=['Real native full-control cost order, commit earlier named cost and cancel later native payment/confirmation','Exact reversible resource refund or irreversible library suffix/destination identities, no cancelled effect and priority recovery',('Retry exact source and pay2G; native verse quantity '+str(expected_counter)+' drives searched library identity/mana value' if put else 'Native chosen CHARGE counter removed once, STUN retained, exact top library identity drawn and1W paid' if anycounter and not multiple else 'Native two-card P1P1 selection with exhausted first identity disabled/retained, partial draft and committed cancel/retry; both counters removed once, exact top drawn and1 paid' if split else 'Native same-card two P1P1 selections, partial draft cancel, later committed cancel/retry, exact top identity drawn and1 paid' if multiple else 'Retry same native source, costs paid once, exact counter/token or mill-return or exile-damage effect, private hands and main priority')]);break
    elif kind=='InputPassPriority':
     if retried and v['stackItems']:stackseen=True;snap(m,'retry-stack')
    elif kind=='OrderCards':
     candidates=[o for o in opts if o['id'].startswith('select:') and not o.get('selected')]
     if not candidates:pick='confirm-cards'
     else:
      prefer=lambda o:o['label'].startswith('{') if manafirst else not o['label'].startswith('{')
      pick=next((o['id'] for o in candidates if prefer(o)),candidates[0]['id'])
    elif kind=='dialog' and r['message']=='getAbilityToPlay':pick=next(o['id'] for o in opts if source_name+' -' in o['label'] and ('create' in o['label'].lower() if counter else True))
    elif kind in ('InputSelectCardsFromList','InputSelectEntitiesFromList') and (put or unattach):pick='ok' if 'ok' in ids and not next(o for o in opts if o['id']=='ok').get('disabled') else next(o['id'] for o in opts if o['id'].startswith('card:') and not o.get('disabled'))
    elif kind=='InputSelectCardToRemoveCounter' and anycounter:
     check(r.get('selectionPurpose')=='removeCounters' and r['minCount']==r['maxCount']==(2 if multiple else 1),'native counter count/purpose exact')
     check(r['remainingCount']==r['maxCount']-r['selectedCount'],'native remaining count exact')
     check(not any(o['id']=='reset-cards' for o in opts),'native additive counter input has no fake toggle/reset')
     snap(m,'counter-picker-'+str(len(seen)))
     if multiple and r['selectedCount']==1:
      check(next(o for o in opts if o['id']=='card:'+str(source))['order']==1,'one selected counter on one still selectable identity')
      check('ok' not in ids,'cannot confirm one of two counters')
      if split:check(next(o for o in opts if o['id']=='card:'+str(source))['disabled'],'exhausted native identity is read-only and retains exact selected count')
      if not partialcancelled:pick='cancel';partialcancelled=True
      else:pick=next(o['id'] for o in opts if o['id'].startswith('card:') and not o.get('disabled'))
     else:pick='card:'+str(source)
    elif kind=='InputSelectTargets':pick='ok' if 'ok' in ids and not next(o for o in opts if o['id']=='ok').get('disabled') else 'player:'+str(two['targetId'])
    elif 'PayMana' in kind:
     if not cancelled and not manafirst and not edge:
      if unattach:
       state=observed(r);check(next(c for c in state[0]['zones']['Battlefield'] if c['id']==source)['attachedTo']==-1,'unattach really committed before later cancellation')
      elif put or anycounter:check(counter_number(next(c for c in one['battlefield'] if c['id']==source))==(expected_counter if put else 0 if split else 1),'actual counter cost committed before later cancel')
      elif counter:check(counter_number(next(c for c in one['battlefield'] if c['id']==source))==4,'counter really removed before later cancel')
      else:check(one['libraryCount']==len(before[0]['zones']['Library'])-(4 if mill else 10),'library cost really committed before cancel')
      snap(m,'committed-before-cancel');pick='cancel';cancelled=True
     else:
      available=[o for o in opts if o['id'].startswith('card:') and not o.get('disabled')]
      if 'ok' in ids and not next(o for o in opts if o['id']=='ok').get('disabled'):pick='ok'
      else:pick=available[0]['id'];paid+=1
    elif kind=='InputConfirm':
     if exile:check(r['message']=='Exile 10 card from the top of your library?','native library-top confirmation amount formatted')
     if manafirst and not cancelled:
      check(sum(c['land'] and c['tapped'] for c in one['battlefield'])==1,'mana paid before counter confirmation');snap(m,'committed-before-cancel');pick='cancel';cancelled=True
     else:pick='ok';confirmed_cost+=1
    elif kind in ('InputConfirmMulligan','InputAttack','InputBlock'):pick='ok'
    elif kind=='InputMulligan':pick='keep'
    elif kind=='RevealCards':pick='confirm-cards'
    elif kind=='dialog':pick=next((o['id'] for o in opts if anycounter and 'charge' in o['label'].lower()),next(i for i in ids if i not in ('cancel','no')))
    else:raise AssertionError(('Unhandled',kind,r['message'],ids))
    check(pick in ids and not next(o for o in opts if o['id']==pick).get('disabled'),('legal option',kind,pick,ids))
    print(case,step,kind,pick,'message',r['message'][:90].replace('\n',' '),flush=True)
    response=dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick);trace.write(json.dumps(dict(response=response))+'\n');trace.flush();p.stdin.write(json.dumps(response)+'\n');p.stdin.flush()
   else:raise AssertionError('Step limit')
 except Exception:result.update(status='FAIL',checks=checks,result='FAIL '+case+': '+traceback.format_exc())
 finally:
  if p is not None and p.poll() is None:p.kill();p.wait()
 if unattach and result['status']=='PASS':result['assertions']=['Actual Sunforger unattach before RW payment, cancel restores exact attachment and6/2 bonus; no effect and original priority','Native retry paysRW once, commits unattach, casts exact original library Lightning Bolt without mana cost for3 damage; resolved Bolt in graveyard, other exact library IDs retained through shuffle','Original battlefield/hand/exile/opponent zones, private-hand isolation, empty mana pools, exact land taps and main-priority recovery']
 write(folder/'result.json',result);write(OUT/(case+'-result.json'),result);return result

if __name__=='__main__':
 OUT.mkdir(parents=True,exist_ok=True);selected=sys.argv[1:] or CASES;results=[];write(OUT/'run-summary.json',dict(status='RUNNING',selected=selected))
 for case in selected:r=run(case);results.append(r);print(r['result'],flush=True)
 status='PASS' if all(r['status']=='PASS' for r in results) else 'FAIL';write(OUT/'run-summary.json',dict(status=status,results=results));sys.exit(status!='PASS')
