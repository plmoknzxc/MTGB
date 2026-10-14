"""Real ordinary Behold information payment, later mana cancel, retry and resolution."""
import hashlib,json,os,queue,shutil,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime'
CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
OUT=ROOT/os.environ.get('MTGB_PLAIN_BEHOLD_OUTPUT','.tools/plain-behold-boundary-1008/native')
CASES=('hand-reveal-mana-cancel-retry','battlefield-choice-mana-cancel-retry')
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')

def run(case):
 hand=case==CASES[0];folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True)
 fixtures=['StateBranchFixture.java','StackExileCostFixture.java']
 for name in fixtures:shutil.copyfile(ROOT/'Tools/ForgeBridge/tests'/name,folder/name)
 shutil.copyfile(__file__,folder/'driver.py')
 result=dict(case=case,folder=folder.relative_to(ROOT).as_posix(),status='RUNNING',runtimeHash=CFG['runtimeHash'],revision=CFG['revision'],driverSha256=sha(folder/'driver.py'),fixtures={n:sha(folder/n) for n in fixtures})
 write(folder/'result.json',result);checks=0;p=None
 def check(ok,why):
  nonlocal checks
  assert ok,why;checks+=1
 try:
  classes=folder/'classes';classes.mkdir()
  subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*[str(folder/f) for f in fixtures]],check=True)
  with tempfile.TemporaryDirectory(prefix='mtgb-plain-behold-boundary-') as temp,(folder/'stderr.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
   audit=folder/'audit';audit.mkdir()
   write(folder/'start.json',dict(session=case,seed=4,firstPlayerSeat=1,startingLife=20,decks=[DECK,DECK],auditDir=str(audit),own=dict(Hand=['Caustic Exhale','Shivan Dragon','Negate'],Battlefield=['Swamp','Swamp','Sphere of Resistance','Shivan Dragon'],Graveyard=['Opt']),other=dict(Hand=['Negate'],Battlefield=['Forest','Grizzly Bears'],Graveyard=['Lightning Bolt'])))
   p=subprocess.Popen([str(RT/CFG['java']),'-Xmx512m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.StackExileCostFixture',str(RT/CFG['assets']),str(folder/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
   q=queue.Queue()
   def reader():
    try:
     for line in p.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start()
   def observed(r):
    end=time.monotonic()+5
    while time.monotonic()<end:
     try:d=json.loads((audit/(str(r['requestId'])+'.json')).read_text());break
     except (FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
    else:raise AssertionError('Missing exact native observer')
    check(d['requestId']==r['requestId'],'Native observation belongs to current request');return d
   def snap(m,label):
    write(folder/(label+'-envelope.json'),m);write(folder/(label+'-observer.json'),observed(m['request']))
    for seat,v in enumerate(m['views'],1):write(folder/(label+'-seat'+str(seat)+'.json'),dict(v,request=m['request'],awaitingInput=True))
   before=None;source=None;candidate=None;target=None;attempt=0;cancelled=False;pickerSeen=False;revealSeen=False;stackSeen=False;paidBlack=False;seen=set();deadline=time.monotonic()+150
   for step in range(180):
    check(time.monotonic()<deadline,'Bounded operation deadline');m=q.get(timeout=35)
    if isinstance(m,Exception):raise m
    check(m is not None and m['type'] not in ('fatal','ended','rejected'),m);trace.write(json.dumps(m)+'\n');trace.flush()
    if m['type']!='request':continue
    r=m['request'];v=m['views'][0];one,two=v['players'];opts=r['options'];kind=r['kind'];pick='ok'
    if r['requestId'] in seen:continue
    seen.add(r['requestId']);check(not one['exileCards'] and not two['exileCards'],'Ordinary Behold never exiles resources')
    check(not two['hand'],'Unrelated opposing hand stays private')
    disclosed={c['id'] for c in m['views'][1]['players'][0]['hand']}
    check(disclosed.issubset({source,candidate} if hand else {source}),'Only publicly announced source and actually revealed hand identity can become known')
    if kind=='InputPassPriority' and r['seat']==1 and v['phase']=='MAIN1':
     if before is None:
      source=next(c['id'] for c in one['hand'] if c['name']=='Caustic Exhale');target=next(c['id'] for c in two['battlefield'] if c['name']=='Grizzly Bears')
      candidate=next(c['id'] for c in one['hand' if hand else 'battlefield'] if c['name']=='Shivan Dragon');snap(m,'before');before=observed(r);pick='card:'+str(source)
     elif cancelled and not v['stackItems']:
      snap(m,'cancelled');now=observed(r)
      expected=json.loads(json.dumps(before['players']));produced=next(c['id'] for c in one['battlefield'] if c['name']=='Swamp' and c['tapped'])
      next(c for c in expected[0]['zones']['Battlefield'] if c['id']==produced)['tapped']=True
      check(now['players']==expected and now['stack']==before['stack'],'Exact original zones/order/counters/history preserved apart from legal manual mana production')
      check(one['manaPool']==[0,0,1,0,0,0] and sum(c['tapped'] for c in one['battlefield'])==1,'Actually paid black mana refunded to pool; legal manual mana activation retained')
      check(pickerSeen and revealSeen and paidBlack,'Exact selected Dragon disclosed and black mana actually paid before cancellation')
      if hand:check(candidate in disclosed,'Already revealed Dragon remains known after cancellation')
      attempt=1;cancelled=False;pick='card:'+str(source)
     elif stackSeen and not v['stackItems']:
      snap(m,'resolved');now=observed(r);old=before['players']
      check([c['id'] for c in now['players'][0]['zones']['Graveyard']]==[c['id'] for c in old[0]['zones']['Graveyard']]+[source],'Exactly successfully cast Caustic Exhale enters original graveyard')
      check([c['id'] for c in now['players'][1]['zones']['Graveyard']]==[c['id'] for c in old[1]['zones']['Graveyard']]+[target],'Actual -3/-3 kills original opposing 2/2 once')
      check([c['id'] for c in one['hand']]==[c['id'] for c in old[0]['zones']['Hand'] if c['id']!=source],'Hand Dragon not consumed and unrelated hand order preserved')
      check([c['id'] for c in one['battlefield']]==[c['id'] for c in old[0]['zones']['Battlefield']],'Battlefield Dragon and tax source not consumed')
      check([c['id'] for c in two['battlefield']]==[c['id'] for c in old[1]['zones']['Battlefield'] if c['id']!=target],'Only actual target leaves opposing battlefield')
      for i in range(2):
       for zone in ['Library','Exile','Command']:
        check(now['players'][i]['zones'][zone]==old[i]['zones'][zone],'Unrelated ordered '+zone+' unchanged')
       check(now['players'][i]['life']==20 and now['players'][i]['counters']==old[i]['counters'],'Life and player counters unchanged')
      check(now['players'][1]['zones']['Hand']==old[1]['zones']['Hand'],'Other hand identities and order unchanged')
      check(now['players'][0]['spellsCastThisTurn']==now['players'][0]['spellsCastThisGame']==1,'Cancelled cast adds no spell history')
      check(sum(c['tapped'] for c in one['battlefield'] if c['name']=='Swamp')==2 and not sum(one['manaPool']),'B plus actual mandatory Sphere tax paid exactly once')
      result.update(status='PASS',checks=checks,requests=len(seen),result='PASS '+case,assertions=['Actually cast Caustic Exhale with mandatory Behold and actual Sphere of Resistance +1 tax; choose original '+('hand Dragon and disclose only it' if hand else 'controlled battlefield Dragon without disclosing other hand')+'; after one black mana is actually paid toward B plus tax, cancel later mana request and restore exact both-player zones/order/counters/history, refund the actually paid black mana to pool while retaining its legal manual source activation and legitimate revealed knowledge.','Retry exact spell with same resource identity, pay B plus tax using two Swamps; ordinary Behold consumes/exiles no Dragon; actual -3/-3 puts original Grizzly Bears into graveyard, source into own graveyard, preserves libraries/private identities and resumes main priority.']);break
     elif v['stackItems']:stackSeen=True
    elif kind=='InputPassPriority':pass
    elif kind=='dialog':pick=next(o['id'] for o in opts if 'behold' in o['label'].lower() and o['id'] not in ('cancel','no'))
    elif kind=='OrderCards':
     choices=[o for o in opts if o['id'].startswith('select:') and not o.get('selected')];pick=sorted(choices,key=lambda o:('Behold' not in o['label'],o['label']))[0]['id'] if choices else 'confirm-cards'
    elif kind=='InputSelectTargets':pick='ok' if any(o['id']=='ok' and not o.get('disabled') for o in opts) else next(o['id'] for o in opts if o.get('card',{}).get('id')==target)
    elif kind=='InputSelectCardsFromList':
     snap(m,'picker-'+str(attempt)+'-'+str(len(seen)));pickerSeen=True
     check(r.get('selectionPurpose')=='behold' and r.get('sourceCardId')==source,'Typed ordinary cost and original source stay distinct from BeholdExile')
     dragons=[o['card'] for o in opts if o.get('card',{}).get('name')=='Shivan Dragon']
     check(len(dragons)==2 and {c['zone'] for c in dragons}=={'Hand','Battlefield'},'Exact legal mixed-origin Dragon candidates')
     pick='ok' if r.get('selectedCount')==1 else 'card:'+str(candidate)
    elif kind=='RevealCards':
     snap(m,'reveal-'+str(attempt));check({c['id'] for c in r['previewCards']}=={candidate} and all(c['zone']==('Hand' if hand else 'Battlefield') for c in r['previewCards']),'Only selected actual Dragon disclosed from its original zone');revealSeen=True;pick='confirm-cards'
    elif 'PayMana' in kind:
     snap(m,'mana-'+str(attempt)+'-'+str(len(seen)))
     check(pickerSeen and revealSeen,'Behold committed before later mana node')
     if attempt==0 and paidBlack:
      check(not sum(one['manaPool']),'Black mana actually consumed toward cost before later cancellation');pick='cancel';cancelled=True
     elif attempt==0 and any(o['id']=='mana:B' for o in opts):pick='mana:B';paidBlack=True
     else:
      lands=[c for c in one['battlefield'] if c['name']=='Swamp' and not c['tapped'] and any(o['id']=='card:'+str(c['id']) for o in opts)]
      pick='card:'+str(lands[0]['id']) if lands else 'ok'
    elif kind in ('InputConfirmMulligan','InputConfirm','InputAttack','InputBlock'):pass
    elif kind=='InputMulligan':pick='keep'
    else:raise AssertionError(('Unhandled original request',kind,opts))
    check(any(o['id']==pick and not o.get('disabled') for o in opts),('Legal original option',kind,pick,opts))
    if kind!='InputPassPriority' or pick!='ok':print(case,step,kind,pick,flush=True)
    response=dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick);trace.write(json.dumps(dict(response=response))+'\n');trace.flush();p.stdin.write(json.dumps(response)+'\n');p.stdin.flush()
   else:raise AssertionError('Step limit')
 except Exception:result.update(status='FAIL',checks=checks,result='FAIL '+case+': '+traceback.format_exc())
 finally:
  if p is not None and p.poll() is None:p.kill();p.wait()
 write(folder/'result.json',result);write(OUT/(case+'-result.json'),result);return result

if __name__=='__main__':
 selected=sys.argv[1:] or list(CASES)
 if any(c not in CASES for c in selected):raise SystemExit('Unknown case')
 OUT.mkdir(parents=True,exist_ok=True);results=[]
 for c in selected:
  r=run(c);results.append(r);print(r['result'],flush=True)
 write(OUT/'run-summary.json',dict(status='PASS' if all(r['status']=='PASS' for r in results) else 'FAIL',selected=selected,results=results))
 raise SystemExit(any(r['status']!='PASS' for r in results))
