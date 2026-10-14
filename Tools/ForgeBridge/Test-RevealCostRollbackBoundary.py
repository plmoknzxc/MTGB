"""Actual Martyr payments: sacrifice, reveal, later cancellation and native retry."""
import hashlib,json,os,queue,shutil,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
OUT=ROOT/os.environ.get('MTGB_REVEAL_COST_OUTPUT','.tools/reveal-cost-rollback-1008/native')
CASES=('sacrifice-reveal-before-mana','sacrifice-before-reveal-cancel','reveal-library-information-barrier');NAME='Martyr of Sands'
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')

def run(case):
 barrier=case=='reveal-library-information-barrier'
 folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True);fixtures=['StateBranchFixture.java','StackExileCostFixture.java']
 for name in fixtures:shutil.copyfile(ROOT/'Tools/ForgeBridge/tests'/name,folder/name)
 shutil.copyfile(__file__,folder/'driver.py')
 result=dict(case=case,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],revision=CFG['revision'],driverSha256=sha(folder/'driver.py'),fixtures={n:sha(folder/n) for n in fixtures},status='RUNNING')
 write(folder/'result.json',result);checks=0;p=None
 def check(ok,why):
  nonlocal checks
  assert ok,why;checks+=1
 try:
  classes=folder/'classes';classes.mkdir();subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*[str(folder/n) for n in fixtures]],check=True)
  with tempfile.TemporaryDirectory(prefix='mtgb-reveal-cost-') as temp,(folder/'stderr.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
   audit=folder/'audit';audit.mkdir()
   write(folder/'start.json',dict(session=case,seed=4,firstPlayerSeat=1,startingLife=20,decks=[DECK,DECK],auditDir=str(audit),own=dict(Hand=['Serra Angel','Savannah Lions','White Knight','Negate'],Battlefield=[NAME,'Plains','Plains']+(['Treasonous Ogre','Ashiok, Wicked Manipulator'] if barrier else []),Graveyard=['Lightning Bolt'],counters=[dict(name=NAME,kind='CHARGE',count=3)]+([dict(name='Ashiok, Wicked Manipulator',kind='LOYALTY',count=5)] if barrier else [])),other=dict(Hand=['Negate'],Battlefield=['Forest','Grizzly Bears'],Graveyard=['Opt'])))
   p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.StackExileCostFixture',str(RT/CFG['assets']),str(folder/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
   messages=queue.Queue()
   def reader():
    try:
     for line in p.stdout:messages.put(json.loads(line))
    except Exception as e:messages.put(e)
    finally:messages.put(None)
   threading.Thread(target=reader,daemon=True).start()
   def observed(r):
    until=time.monotonic()+5
    while time.monotonic()<until:
     try:value=json.loads((audit/(str(r['requestId'])+'.json')).read_text());break
     except (FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
    else:raise AssertionError('Missing exact observer '+str(r['requestId']))
    check(value['requestId']==r['requestId'],'observer identity');return value
   def snap(m,label):
    write(folder/(label+'-envelope.json'),m);write(folder/(label+'-observer.json'),observed(m['request']))
    for seat,v in enumerate(m['views'],1):write(folder/(label+'-seat'+str(seat)+'.json'),dict(v,request=m['request'],awaitingInput=True))
   before=None;source=None;revealed=set();wanted=set();cancelled=False;retry=False;stackSeen=False;ogreActivated=False;nestedExileComplete=False;seen=set();until=time.monotonic()+180
   for step in range(180):
    check(time.monotonic()<until,'deadline');m=messages.get(timeout=35)
    if isinstance(m,Exception):raise m
    check(m is not None,'native alive');trace.write(json.dumps(m)+'\n');trace.flush();check(m['type'] not in ('fatal','ended','rejected'),m)
    if m['type']!='request':continue
    r=m['request'];v=m['views'][0];one,two=v['players'];opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];pick='ok'
    if r['requestId'] in seen:continue
    seen.add(r['requestId']);check(one.get('hand') is not None,'Own view present')
    check(not m['views'][0]['players'][1]['hand'],'Unrevealed opponent hand stays hidden')
    disclosed={c['id'] for c in m['views'][1]['players'][0]['hand']}
    check(disclosed.issubset(wanted),'Only chosen white card identities may be disclosed, other white/blue cards stay hidden')
    if revealed:check(disclosed==wanted,'Already revealed identities retained for opponent')
    if kind=='InputPassPriority' and r['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
     if before is None:
      source=next(c['id'] for c in one['battlefield'] if c['name']==NAME);wanted={c['id'] for c in one['hand'] if c['name'] in ('Serra Angel','Savannah Lions')};check(len(wanted)==2,'Exact two white reveal identities');snap(m,'before');before=observed(r);pick='card:'+str(source)
     elif cancelled and not retry:
      snap(m,'cancelled');state=observed(r)
      if barrier:
       old=before['players'][0];now=state['players'][0]
       check(now['zones']['Library']==old['zones']['Library'][3:],'Irreversible library suffix preserved exactly')
       check([c['id'] for c in now['zones']['Exile']]==[c['id'] for c in old['zones']['Library'][:3]],'Exact original three library identities stay exiled')
       check([c['id'] for c in now['zones']['Graveyard']]==[c['id'] for c in old['zones']['Graveyard']]+[source],'Prior sacrifice is not falsely refunded across irreversible payment')
       check(now['zones']['Hand']==old['zones']['Hand'] and one['life']==20 and revealed==wanted,'Revealed original hand and unreplaced life preserved')
       check(state['players'][1]==before['players'][1],'Opponent exact resources unchanged')
       check(all(not c['tapped'] for c in one['battlefield']) and not v['stackItems'],'No unrelated tap/stack effect')
       result.update(status='PASS',result='PASS '+case,checks=checks,requests=len(seen),assertions=['Actual Martyr sacrifice and two-card reveal then nested Treasonous Ogre life-cost mana activation; Ashiok replaces exactly3life with exiling original top3library cards','Outer mana cancellation takes irreversible path: exact three exiled identities/library suffix and earlier sacrificed source remain; no life paid/gained, original hand/reveal memory and unrelated opponent resources unchanged; main priority resumes. Mana remainder separately observed, not a claim that this irreversible ability can be undone.']);break
      check(state['players']==before['players'] and state['stack']==before['stack'],'Exact both-player resources/card identities/order/counters restored after sacrifice/reveal cancellation')
      check(one['life']==20 and not sum(one['manaPool']) and not any(c['tapped'] for c in one['battlefield']),'Cancelled cost has no life gain or mana/tap leak')
      check(revealed==wanted if case=='sacrifice-reveal-before-mana' else not revealed,'Actually revealed only in completed reveal branch')
      retry=True;pick='card:'+str(source)
     elif retry:
      snap(m,'resolved');state=observed(r);old=before['players'][0];now=state['players'][0]
      check(stackSeen,'Retried ability actually entered stack')
      check(one['life']==26 and two['life']==20,'Actual X=2 gains exactly six life once')
      check([c['id'] for c in now['zones']['Graveyard']]==[c['id'] for c in old['zones']['Graveyard']]+[source],'Exactly original graveyard plus sacrificed original Martyr once')
      check([c['id'] for c in now['zones']['Battlefield']]==[c['id'] for c in old['zones']['Battlefield'] if c['id']!=source],'Only exact source sacrificed')
      for zone in ('Hand','Library','Exile','Command'):
       check(now['zones'][zone]==old['zones'][zone],'Reveal/payment preserves exact original zone '+zone)
      check(state['players'][1]==before['players'][1],'Opponent exact resources unchanged')
      check(now['spellsCastThisTurn']==old['spellsCastThisTurn']==0 and now['spellsCastThisGame']==old['spellsCastThisGame']==0,'Activation/restore never count as spells cast')
      check(sum(c['tapped'] for c in one['battlefield'] if c['name']=='Plains')==1 and not sum(one['manaPool']),'Retry spends exactly one mana')
      check(revealed==wanted,'Actual reveal has exact chosen two original identities')
      result.update(status='PASS',result='PASS '+case,checks=checks,requests=len(seen),assertions=['Actual Martyr of Sands X=2 activation pays sacrifice before reveal and later cancels '+('mana' if case=='sacrifice-reveal-before-mana' else 'reveal picker')+'; exact original source/counters, both-player resources and ordered zones restored; completed reveal identities remain observed separately','Retry commits original source and chosen two white cards once, actual ability resolves for exactly6life; one mana, no cast-history/resource leak, same private hand/library/opponent state and continued main priority']);break
    elif kind=='InputPassPriority':
     if retry and v['stackItems']:stackSeen=True;snap(m,'retry-stack')
    elif kind=='OrderCards':
     snap(m,('retry-' if retry else '')+'cost-order-'+str(len(seen)))
     candidates=[o for o in opts if o['id'].startswith('select:') and not o.get('selected')]
     pick=sorted(candidates,key=lambda o:(0 if 'Sacrifice' in o['label'] else 1 if 'Reveal' in o['label'] else 2))[0]['id'] if candidates else 'confirm-cards'
     if barrier and 'exile zone' in r['message'] and pick=='confirm-cards':nestedExileComplete=True
    elif 'PayMana' in kind:
     if barrier and ogreActivated and not nestedExileComplete:continue # Wait for the actual nested exile-order request, not its transient outer payment projection.
     if barrier and not ogreActivated:
      snap(m,'before-nested-mana');pick='card:'+str(next(c['id'] for c in one['battlefield'] if c['name']=='Treasonous Ogre'));ogreActivated=True
     elif barrier and one['libraryCount']<len(before['players'][0]['zones']['Library']):
      snap(m,'library-changed-before-cancel');pick='cancel';cancelled=True
     elif not retry:
      snap(m,'committed-before-cancel');state=observed(r)
      check(source not in [c['id'] for c in one['battlefield']] and source in [c['id'] for c in one['graveyardCards']],'Source actually sacrificed before later cancel')
      check(revealed==wanted,'Two hand cards actually revealed before mana cancellation');pick='cancel';cancelled=True
     else:pick='ok'
    elif kind.startswith('InputSelectCards') or kind in ('SelectCards','InputSelectEntitiesFromList'):
     snap(m,('retry-' if retry else '')+'picker-'+str(len(seen)))
     if 'reveal' in r['message'].lower():
      if case=='sacrifice-before-reveal-cancel' and not retry:
       check(source in [c['id'] for c in one['graveyardCards']],'Source already paid before picker cancellation');pick='cancel';cancelled=True
      else:pick='ok' if r.get('selectedCount')==2 else next(o['id'] for o in opts if o.get('card',{}).get('id') in wanted and not o.get('selected'))
     elif 'sacrifice' in r['message'].lower():pick='ok' if r.get('selectedCount') else 'card:'+str(source)
     else:raise AssertionError(('Unknown picker',r))
    elif kind=='RevealCards':
     snap(m,('retry-' if retry else '')+'reveal-'+str(len(seen)))
     actual={c['id'] for c in r['previewCards']};check(actual==wanted and r['seat']==2,'Only chosen original hand identities disclosed to opponent');revealed.update(actual);pick='confirm-cards'
    elif kind=='dialog':
     snap(m,('retry-' if retry else '')+'dialog-'+str(len(seen)))
     pick=next(o['id'] for o in opts if o['label']=='2')
    elif kind in ('ChooseNumber','InputNumber'):
     snap(m,('retry-' if retry else '')+'number-'+str(len(seen)));pick='number-plus' if r['selectedValue']<2 else 'confirm-number'
    elif kind in ('InputConfirmMulligan','InputConfirm','InputAttack','InputBlock'):pick='ok'
    elif kind=='InputMulligan':pick='keep'
    else:raise AssertionError(('Unhandled',r))
    check(pick in ids and not next(o for o in opts if o['id']==pick).get('disabled'),('Legal original option',kind,pick,ids))
    if kind!='InputPassPriority' or pick!='ok':print(case,step,kind,pick,r['message'][:100].replace('\n',' '),flush=True)
    response=dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick);trace.write(json.dumps(dict(response=response))+'\n');trace.flush();p.stdin.write(json.dumps(response)+'\n');p.stdin.flush()
   else:raise AssertionError('step limit')
 except Exception:result.update(status='FAIL',checks=checks,result='FAIL '+case+': '+traceback.format_exc())
 finally:
  if p is not None and p.poll() is None:p.kill();p.wait()
 write(folder/'result.json',result);write(OUT/(case+'-result.json'),result);return result

if __name__=='__main__':
 selected=sys.argv[1:] or list(CASES)
 if any(c not in CASES for c in selected):raise SystemExit('Unknown case '+str(selected))
 OUT.mkdir(parents=True,exist_ok=True);results=[]
 for c in selected:
  result=run(c);results.append(result);print(result['result'],flush=True)
 status='PASS' if all(r['status']=='PASS' for r in results) else 'FAIL';write(OUT/'run-summary.json',dict(status=status,selected=selected,results=results));sys.exit(status!='PASS')
