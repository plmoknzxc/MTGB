"""Real processor-cost cancellation across owners; opening fixture only arranges state."""
import hashlib,json,os,queue,shutil,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
OUT=ROOT/os.environ.get('MTGB_PROCESSOR_COST_OUTPUT','.tools/processor-cost-1008/native')
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def run(case):
 assert case=='processor-before-mana'
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
  with tempfile.TemporaryDirectory(prefix='mtgb-processor-') as temp,(folder/'stderr.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
   audit=folder/'audit';audit.mkdir()
   write(folder/'start.json',dict(session=case,seed=4,firstPlayerSeat=1,startingLife=20,decks=[DECK,DECK],auditDir=str(audit),
    own=dict(Hand=['Negate'],Battlefield=['Cryptic Cruiser','Island','Forest','Mountain'],Exile=['Grizzly Bears'],Library=['Island','Forest','Swamp','Mountain','Plains']*6),
    other=dict(Hand=['Negate'],Battlefield=['Elvish Visionary'],Exile=['Lightning Bolt','Opt'])))
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
   indices=lambda z:[c['id'] for c in z]
   def comparable(players):
    # Exile is unordered. Preserve every card/attribute and all ordered-zone sequences.
    value=json.loads(json.dumps(players))
    for player in value:player['zones']['Exile'].sort(key=lambda c:c['id'])
    return value
   before=None;cancelled=False;retried=False;stackseen=False;seen=set();deadline=time.monotonic()+180
   for step in range(100):
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
      before=observed(r);source=next(c['id'] for c in one['battlefield'] if c['name']=='Cryptic Cruiser');target=next(c['id'] for c in two['battlefield'] if c['name']=='Elvish Visionary')
      processed=next(c['id'] for c in before[1]['zones']['Exile'] if c['name']=='Lightning Bolt');snap(m,'before');pick='card:'+str(source)
     elif cancelled and not retried:
      state=observed(r);snap(m,'cancelled');check(comparable(state)==comparable(before),'cancel restores exact both-owner resources/identities; only unordered exile sequence ignored')
      check(not sum(one['manaPool']) and not sum(two['manaPool']) and not v['stackItems'],'no cancelled effect or mana remains')
      retried=True;pick='card:'+str(source)
     elif retried:
      state=observed(r);snap(m,'resolved');check(stackseen,'actual retry entered stack');bz,az=before[0]['zones'],state[0]['zones'];pb,pa=before[1]['zones'],state[1]['zones']
      check(indices(pa['Exile'])==[i for i in indices(pb['Exile']) if i!=processed] and indices(pa['Graveyard'])==indices(pb['Graveyard'])+[processed],'exact original opponent-owned ID into owner graveyard')
      check(next(c for c in pa['Battlefield'] if c['id']==target)['tapped'],'actual tap effect on exact target creature')
      check(indices(pa['Battlefield'])==indices(pb['Battlefield']),'opponent battlefield identities retained')
      check(all(pa[z]==pb[z] for z in pb if z not in ('Exile','Graveyard','Battlefield')),'opponent private/other zones exact')
      check(all(az[z]==bz[z] for z in bz if z!='Battlefield'),'own exile/private/library/other zones exact')
      check(indices(az['Battlefield'])==indices(bz['Battlefield']) and not next(c for c in az['Battlefield'] if c['id']==source)['tapped'],'source identity retained/untapped')
      check(sum(c['land'] and c['tapped'] for c in one['battlefield'])==3 and not sum(one['manaPool']) and not sum(two['manaPool']),'exact2U once and no remaining mana')
      check(one['life']==two['life']==20,'life unchanged')
      result.update(status='PASS',result='PASS '+case,checks=checks,requests=len(seen),assertions=['Actual Cryptic Cruiser activation/target and exact opponent-owned exile cost committed before cancel2U; exact both-owner zones/resources restored with unordered exile sequence ignored, no effect','Retry pays2U once, processes original opponent Lightning Bolt into owner graveyard and taps exact Elvish Visionary; own exile card excluded','Both private hands concealed, exact other zones/life/source retained, main-priority recovery']);break
    elif kind=='InputPassPriority':
     if retried and v['stackItems']:stackseen=True;snap(m,'retry-stack')
    elif kind=='InputSelectTargets':pick='ok' if 'ok' in ids and not next(o for o in opts if o['id']=='ok').get('disabled') else 'card:'+str(target)
    elif kind=='OrderCards':
     candidates=[o for o in opts if o['id'].startswith('select:') and not o.get('selected')]
     pick=next((o['id'] for o in candidates if not o['label'].startswith('{')),candidates[0]['id']) if candidates else 'confirm-cards'
    elif kind in ('InputSelectCardsFromList','InputSelectEntitiesFromList','SelectCards'):
     selectable=[o['card']['id'] for o in opts if o.get('card') and not o.get('disabled')]
     check(set(selectable)<=set(indices(before[1]['zones']['Exile'])) and not set(selectable)&set(indices(before[0]['zones']['Exile'])),'only opponent-owned exiled IDs selectable')
     snap(m,'processor-picker-'+str(len(seen)))
     pick='confirm-cards' if 'confirm-cards' in ids else 'ok' if 'ok' in ids and not next(o for o in opts if o['id']=='ok').get('disabled') else next(o['id'] for o in opts if o.get('card',{}).get('id')==processed)
    elif 'PayMana' in kind:
     if not cancelled:
      state=observed(r);pb,pa=before[1]['zones'],state[1]['zones'];check(indices(pa['Graveyard'])==indices(pb['Graveyard'])+[processed] and processed not in indices(pa['Exile']),'cost actually committed before cancellation')
      snap(m,'committed-before-cancel');pick='cancel';cancelled=True
     else:pick='ok' if 'ok' in ids and not next(o for o in opts if o['id']=='ok').get('disabled') else next(o['id'] for o in opts if o['id'].startswith('card:') and not o.get('disabled'))
    elif kind in ('InputConfirmMulligan','InputAttack','InputBlock','InputConfirm'):pick='ok'
    elif kind=='InputMulligan':pick='keep'
    elif kind=='dialog':pick=next(i for i in ids if i not in ('cancel','no'))
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
 OUT.mkdir(parents=True,exist_ok=True);selected=sys.argv[1:] or ['processor-before-mana'];results=[];write(OUT/'run-summary.json',dict(status='RUNNING',selected=selected))
 for case in selected:r=run(case);results.append(r);print(r['result'],flush=True)
 status='PASS' if all(r['status']=='PASS' for r in results) else 'FAIL';write(OUT/'run-summary.json',dict(status=status,results=results));sys.exit(status!='PASS')
