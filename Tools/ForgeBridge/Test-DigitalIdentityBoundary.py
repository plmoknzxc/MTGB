"""R38 exact successive Draft/MakeCard candidates and a natural next-turn trigger.
Opening only uses the unchanged native fixture; observer is read-only.
"""
import hashlib,json,os,queue,subprocess,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'));OUT=ROOT/'.tools/digital-identity-1010'
FIX=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['RoyalStateFixture.java','ControlVariantFixture.java','DigitalIdentityFixture.java']]
DECK=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
def rd(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def wr(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def zones(o,s=1):return o['players'][s-1]['zones']
def run():
 f=OUT/'native/successive-and-next-turn'/str(time.time_ns());f.mkdir(parents=True);audit=f/'audit';audit.mkdir();classes=f/'classes';classes.mkdir();checks=0;p=None
 for x in FIX+[Path(__file__)]: (f/x.name).write_bytes(x.read_bytes())
 result=dict(folder=f.relative_to(ROOT).as_posix(),case='successive-and-next-turn',runtimeHash=CFG['runtimeHash'],driver=sha(Path(__file__)),fixtures={x.name:sha(x) for x in FIX},producerClassesLib={x.relative_to(RT).as_posix():sha(x) for d in ['classes','lib'] for x in (RT/d).rglob('*') if x.is_file()},status='RUNNING');wr(f/'result.json',result)
 def ck(ok,msg):
  nonlocal checks
  assert ok,msg;checks+=1
 try:
  with (f/'compile.log').open('w') as log:subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*[str(f/x.name) for x in FIX]],check=True,stdout=log,stderr=subprocess.STDOUT)
  with tempfile.TemporaryDirectory(prefix='mtgb-digital-identity-') as temp,(f/'trace.jsonl').open('w',encoding='utf8') as trace,(f/'stderr.log').open('w') as log:
   positions=[dict(Battlefield=["Xander's Wake",'Grizzly Bears','Runeclaw Bear'],Hand=['Relics of the Rubblebelt','Swine Rebellion','Shock','Shock'],Library=['Island','Plains','Forest','Mountain']),dict(Battlefield=[],Hand=['Negate'],Library=['Forest','Mountain','Island','Plains'])]
   cfg=dict(session='digital-identity-boundary',seed=4,fixtureSeed=11,startingLife=30,firstPlayerSeat=1,decks=[DECK,DECK],positions=positions,auditDir=str(audit));wr(f/'start.json',cfg)
   p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.DigitalIdentityFixture',str(RT/CFG['assets']),str(f/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');q=queue.Queue()
   def reader():
    try:
     for line in p.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start()
   def obs(r):
    end=time.monotonic()+4
    while time.monotonic()<end:
     try:return rd(audit/(str(r['requestId'])+'.json')),rd(audit/(str(r['requestId'])+'-identity.json'))
     except (FileNotFoundError,json.JSONDecodeError,PermissionError):time.sleep(.01)
    raise AssertionError('native read-only observers missing')
   def save(m,o,t,n):wr(f/(n+'-wire.json'),m);wr(f/(n+'-native.json'),o);wr(f/(n+'-identity.json'),t)
   initial=None;stage=0;cast='';seen=set();casts=[];drafts=[];draftIDs=set();selected=[];pigNames=[];pigs=[];relBF=0;pigBF=0;deadline=time.monotonic()+200
   for step in range(650):
    ck(time.monotonic()<deadline,'deadline');m=q.get(timeout=35)
    if isinstance(m,Exception):raise m
    ck(m is not None,'process exited');trace.write(json.dumps(m)+'\n');trace.flush();ck(m['type'] not in ['fatal','ended','rejected'],m)
    if m['type']!='request':continue
    r=m['request'];kind=r['kind'];opts=r['options'];ids=[x['id'] for x in opts];pick='ok'
    if r['requestId'] in seen:continue
    seen.add(r['requestId']);o,t=(None,None) if 'Mulligan' in kind else obs(r);v=m['views'][0]
    ck(not m['views'][0]['players'][1]['hand'] and not m['views'][1]['players'][0]['hand'],'unseen opposite hands concealed')
    if 'Mulligan' in kind:pick='keep' if 'keep' in ids else 'ok'
    elif kind=='InputPassPriority':
     if initial is None and r['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
      initial=o;save(m,o,t,'initial')
     ready=r['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']
     if ready and stage<3:
      if stage==1:
       ck(len(drafts)==2 and len(selected)==2,'DraftNum2 two separate original requests')
       z=zones(o);ck(relBF in {c['id'] for c in z['Battlefield']} and next(c for c in z['Battlefield'] if c['id']==relBF)['tapped'],'one exact drafted ID enters tapped')
       ck({c['id'] for c in z['Hand']}&draftIDs==set(selected)-{relBF},'only remaining selected drafted ID in private hand');save(m,o,t,'after-relics')
      if stage==2:
       ck(len(pigNames)==2 and len(set(pigNames))==2,'SpellbookAmount2 distinct selected names')
       made=[c for c in zones(o)['Hand']+zones(o)['Battlefield'] if c['name'] in pigNames];pigs=[c['id'] for c in made]
       ck(len(made)==2 and len(set(pigs))==2 and {c['name'] for c in made}==set(pigNames),'exact two new conjured IDs, no unchosen pig')
       ck(pigBF in {c['id'] for c in zones(o)['Battlefield']} and len([c for c in zones(o)['Hand'] if c['id'] in pigs])==1,'remembered exact conjured split hand/battlefield')
       ck(all(not c['token'] and c['owner']==1 for c in t if c['id'] in pigs),'actual non-token own conjured cards')
       swine=next(c for c in t if c['id']==casts[1]);ck(not swine['remembered'],'actual Cleanup cleared original source memory');save(m,o,t,'after-swine')
      cast=['Relics of the Rubblebelt','Swine Rebellion','Shock'][stage];card=next(c for c in zones(o)['Hand'] if c['name']==cast);casts.append(card['id']);pick='card:'+str(card['id']);stage+=1
     elif ready and stage==3:
      ck(len(drafts)==3 and len(selected)==3,'first natural Wake death draft');ck(v['turn']==1,'first trigger turn1');save(m,o,t,'after-first-wake');stage=4
     elif ready and stage==4 and v['turn']==3:
      ck(len(drafts)==3,'no repeated draft while passing original turn/other turn');save(m,o,t,'before-next-turn-death');cast='Shock';card=next(c for c in zones(o)['Hand'] if c['name']==cast);casts.append(card['id']);pick='card:'+str(card['id']);stage=5
     elif ready and stage==5:
      ck(v['turn']==3 and len(drafts)==4 and len(selected)==4,'natural next own turn reopens Wake request');save(m,o,t,'resolved')
      z=zones(o);old=zones(initial);own0={c['id'] for vv in old.values() for c in vv};finalIDs={c['id'] for vv in z.values() for c in vv}
      ck(len(casts)==4 and len(set(casts))==4,'four actual distinct paid spells')
      ck(len(draftIDs)==12 and len(set(selected))==4 and all(c not in own0 for c in draftIDs),'four fresh disjoint three-card candidates; independent selected IDs')
      ck(finalIDs-own0==set(selected)|set(pigs),'only selected four draft/two conjured objects persist')
      ck({c['id'] for c in z['Graveyard']}==set(casts)|{next(c['id'] for c in old['Battlefield'] if c['name']==n) for n in ['Grizzly Bears','Runeclaw Bear']},'exact four paid spells and two actual deaths grave')
      ck({c['id'] for c in z['Battlefield']}=={c['id'] for c in old['Battlefield'] if c['name'] not in ['Grizzly Bears','Runeclaw Bear']}|{relBF,pigBF},'only original dead bears removed and exact chosen entrants')
      ck({c['id'] for c in z['Hand']}&draftIDs==set(selected)-{relBF},'three exact drafted chosen hand IDs survive')
      ck(z['Library']==old['Library'][1:] and any(c['id']==old['Library'][0]['id'] for c in z['Hand']),'one later native own draw; no shuffle/unrelated library changes')
      for c in t:
       if c['id'] in selected+pigs:ck(c['owner']==1 and not c['token'],'new actual own non-token identity')
      ck([p['life'] for p in o['players']]==[30,30],'no player damage')
      for view in m['views']:
       for pv in view['players']:ck(sum(pv['manaPool'])==sum(pv['snowManaPool'])==0,'zero residual paid mana')
      ck(r['seat']==1 and not v['stackItems'],'original next-turn actor/priority restored')
      result.update(status='PASS_BOUNDED',checks=checks,requests=len(seen),spellIDs=casts,draftChoices=drafts,selectedDraftIDs=selected,pigNames=pigNames,pigIDs=pigs,chosenBattlefieldIDs=[relBF,pigBF],scope='Four actual casts/eight mana over two native own turns. Relics DraftNum2 fresh candidates/remembered exact move, Swine SpellbookAmount2 removed names/new non-token IDs/cleanup, Wake natural next-turn reentry with fresh three-card IDs. No multi-recipient/Amount/Seek/replace/full-mode/network/global PASS.');break
    elif kind in ['SelectCards','InputSelectEntitiesFromList','InputSelectCardsFromList']:
     if r.get('selectedCount'):pick='confirm-cards' if 'confirm-cards' in ids else 'ok';save(m,o,t,'selected-'+str(step))
     else:
      cards=[x['card'] for x in opts if x.get('card')]
      if len(cards)==3 and all(c['id'] not in {x['id'] for vv in zones(o).values() for x in vv} for c in cards):
       ck(len({c['id'] for c in cards})==len({c['name'] for c in cards})==3,'actual three distinct draft options');ck(not ({c['id'] for c in cards}&draftIDs),'no stale candidate ID reuse')
       for c in cards:ck(c.get('printingKey') and c.get('currentType') and c.get('currentRules'),'actual unplaced native preview fields')
       chosen=next((c for c in cards if drafts and c['name']==drafts[-1]['selectedName']),cards[0]);drafts.append(dict(requestId=r['requestId'],turn=v['turn'],cards=cards,selectedID=chosen['id'],selectedName=chosen['name']));draftIDs.update(c['id'] for c in cards);selected.append(chosen['id']);save(m,o,t,'draft-'+str(len(drafts)))
       pick=next(x['id'] for x in opts if x.get('card',{}).get('id')==chosen['id'])
      elif len(cards)==2:
       wanted=selected[:2] if stage==1 else pigs
       # Conjured IDs first become visible in this original remembered-card request.
       if stage==2:
        ck({c['name'] for c in cards}==set(pigNames),'only remembered chosen names offered');pigs=[c['id'] for c in cards];wanted=pigs
       ck({c['id'] for c in cards}==set(wanted),'exact current remembered two-card candidates')
       chosen=cards[0]['id']
       if stage==1:relBF=chosen
       else:pigBF=chosen
       save(m,o,t,'put-battlefield-'+str(stage));pick=next(x['id'] for x in opts if x.get('card',{}).get('id')==chosen)
      else:raise AssertionError(('unexpected cards',stage,r))
    elif r.get('selectionPurpose')=='cardName':
     names={x['cardName'] for x in opts};ck(stage==2 and names=={'First Little Pig','Second Little Pig','Third Little Pig'}-set(pigNames),'second native spellbook request removes selected name');name='First Little Pig' if not pigNames else 'Second Little Pig';pigNames.append(name);save(m,o,t,'pig-name-'+str(len(pigNames)));pick=next(x['id'] for x in opts if x.get('cardName')==name)
    elif kind=='InputSelectTargets':
     if r.get('selectedCount'):pick='ok'
     else:
      name='Grizzly Bears' if stage==3 else 'Runeclaw Bear';target=next(c['id'] for c in zones(o)['Battlefield'] if c['name']==name);save(m,o,t,'death-target-'+str(stage));pick=next(x['id'] for x in opts if x.get('card',{}).get('id')==target)
    elif 'PayMana' in kind:save(m,o,t,'payment-'+str(step));pick='ok'
    elif kind=='dialog':save(m,o,t,'dialog-'+str(step));pick=ids[0]
    elif kind=='OrderAbilities':pick='confirm-order' if 'confirm-order' in ids else next(x for x in ids if x.startswith('order:'))
    elif kind=='RevealCards':pick='confirm-cards'
    elif 'ok' not in ids:raise AssertionError(('unhandled',stage,r))
    ck(pick in ids and not next(x for x in opts if x['id']==pick).get('disabled'),('original legal option',r,pick));print(step,kind,r['seat'],pick,flush=True);p.stdin.write(json.dumps(dict(type='respond',session=cfg['session'],seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
   else:raise AssertionError('step limit')
   ck(result['status']=='PASS_BOUNDED','actual final result')
 except Exception:result.update(status='FAIL',checks=checks,error=traceback.format_exc())
 finally:
  if p is not None and p.poll() is None:p.kill();p.wait()
 wr(f/'result.json',result);wr(OUT/'native-result.json',result);print(result['status'],result.get('error',''),flush=True);return result
if __name__=='__main__':raise SystemExit(run()['status']!='PASS_BOUNDED')
