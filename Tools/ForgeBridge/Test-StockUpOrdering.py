"""Actual Stock Up cast: exact hand/library IDs, reset and bottom order; per-draft latency."""
import argparse,json,os,queue,subprocess,tempfile,threading,time,statistics,hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime'
p=argparse.ArgumentParser();p.add_argument('--out',default='run');p.add_argument('--classes');a=p.parse_args()
W=ROOT/'.tools/sideboard-stockup-1027'/(a.out+'-'+str(time.time_ns()));W.mkdir(parents=True);classes=W/'classes';classes.mkdir();audit=W/'audit';audit.mkdir()
fixtures=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['LibraryCostFixture.java','LibraryOrderFixture.java']]
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+os.pathsep+str(RT/'lib/*'),'-d',str(classes),*map(str,fixtures)],check=True)
deck=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
cfg=dict(session='stock',auditDir=str(audit),replaceLibrary=False,seed=4,startingLife=20,firstPlayerSeat=1,decks=[deck,deck],hand1=['Stock Up','Counterspell','Negate','Consider','Divination','Fact or Fiction'],hand2=['Lightning Bolt']*7,board1=['Island']*6+['Grizzly Bears']*5+['Sol Ring']*3,board2=[],grave1=[],grave2=[],top=['Mountain','Forest','Island','Swamp','Grizzly Bears'])
cast=False;source=None;initial=None;original_hand=None;pools={};groups={};timings={};last={};resets=set();chosen=None;bottom=None;selection_step=0
def library(request):
 path=audit/(str(request['requestId'])+'.json')
 for _ in range(300):
  try:return json.loads(path.read_text())['players']
  except (OSError,json.JSONDecodeError):time.sleep(.005)
 raise AssertionError('Missing read-only library audit')
with tempfile.TemporaryDirectory(dir=W) as tmp,(W/'engine.log').open('w',encoding='utf-8') as log,(W/'trace.jsonl').open('w',encoding='utf-8') as trace:
 start=Path(tmp)/'start.json';start.write_text(json.dumps(cfg));q=queue.Queue();cp=os.pathsep.join(([str(Path(a.classes).resolve())] if a.classes else [])+[str(classes),str(RT/'classes'),str(RT/'lib/*')])
 proc=subprocess.Popen([str(RT/'jre/bin/java.exe'),'-Xmx768m','-XX:CICompilerCount=2','-Dmtgb.bridge.lazyCatalog=true','-Djava.awt.headless=true','-cp',cp,'mtgb.bridge.LibraryOrderFixture',str(RT/'assets'),str(start.resolve())],cwd=RT/'assets',env=dict(os.environ,APPDATA=tmp,LOCALAPPDATA=tmp,TEMP=tmp,TMP=tmp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf-8')
 threading.Thread(target=lambda:[q.put(json.loads(s)) for s in proc.stdout],daemon=True).start()
 try:
  for step in range(180):
   m=q.get(timeout=40);trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ['fatal','ended','rejected'],m
   if m['type']!='request':continue
   r=m['request'];v=m['views'][0];own,other=v['players'];kind=r['kind'];opts=[o for o in r['options'] if not o.get('disabled')];ids=[o['id'] for o in opts];pick='ok'
   assert not m['views'][1]['players'][0]['hand'] and not other['hand'],'Private hand leaked'
   if kind=='InputPassPriority':
    if not cast and r['seat']==1 and v['phase']=='MAIN1':
     initial=library(r);original_hand=[c['id'] for c in own['hand']];source=next(c['id'] for c in own['hand'] if c['name']=='Stock Up');pick='card:'+str(source);cast=True
    elif cast and not v['stackItems'] and source in [c['id'] for c in own['graveyardCards']]:
     current=library(r);assert chosen and bottom
     assert sorted(c['id'] for c in own['hand'])==sorted([i for i in original_hand if i!=source]+chosen)
     assert current[0]['library']==initial[0]['library'][5:]+bottom,(current[0]['library'],initial[0]['library'],bottom)
     known={c['card']['id']:c['position'] for c in own.get('libraryCards',[]) if c.get('card')}
     assert all(known.get(card)==len(current[0]['library'])-len(bottom)+n+1 for n,card in enumerate(bottom)),'Known bottom positions disagree with physical library'
     assert not m['views'][1]['players'][0].get('libraryCards'),'Private bottom order leaked to opponent'
     assert current[1]==initial[1] and len(resets)==2 and own['life']==other['life']==20
     result=dict(status='PASS',selectedIds=chosen,bottomOrder=bottom,resetContexts=len(resets),draftStepMs={k:dict(values=ts,median=statistics.median(ts)) for k,ts in timings.items()},runtimeHash=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))['runtimeHash'],driverSha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),overlay=a.classes)
     (W/'result.json').write_text(json.dumps(result,indent=2));(W/'resolved.json').write_text(json.dumps(dict(v,request=r,awaitingInput=True)));print(json.dumps(result));break
   elif kind=='InputSelectEntitiesFromList':
    wanted=[initial[0]['library'][1],initial[0]['library'][4]];key=(kind,r.get('contextId'))
    if key in last:timings.setdefault(kind,[]).append(m['elapsedMs']-last[key])
    last[key]=m['elapsedMs']
    if selection_step==0:assert r['selectedCount']==0;pick='card:'+str(wanted[0])
    elif selection_step==1:assert r['selectedCount']==1;pick='card:'+str(wanted[0])
    elif selection_step==2:assert r['selectedCount']==0;resets.add(key);pick='card:'+str(wanted[0])
    elif selection_step==3:assert r['selectedCount']==1;pick='card:'+str(wanted[1]);chosen=wanted
    else:assert r['selectedCount']==2;pick='ok'
    selection_step+=1
    (W/(kind+'-'+str(r['selectedCount'])+'-'+str(r['requestId'])+'.json')).write_text(json.dumps(dict(v,request=r,awaitingInput=True)))
   elif kind in ['SelectCards','OrderCards']:
    cards=[o for o in opts if o.get('card')];key=(kind,r['contextId']);pool=pools.setdefault(key,[o['card']['id'] for o in cards]);board=json.dumps(m['views'],sort_keys=True)
    assert groups.setdefault(key,board)==board,'Draft changed physical board projection'
    if key in last:timings.setdefault(kind,[]).append(m['elapsedMs']-last[key])
    last[key]=m['elapsedMs'];selected=[o['card']['id'] for o in sorted(cards,key=lambda o:o.get('order',0)) if o.get('selected')]
    desired=[initial[0]['library'][1],initial[0]['library'][4]] if kind=='SelectCards' else list(reversed(pool))
    assert len(pool)==(5 if kind=='SelectCards' else 3) and r['selectedCount']==len(selected)
    if selected and key not in resets:pick='reset-cards';resets.add(key)
    elif len(selected)==len(desired):
     pick='confirm-cards'
     if kind=='SelectCards':chosen=selected
     else:bottom=list(reversed(selected)) if r['message'].replace('\r','').endswith('\nClosest to bottom') else selected
    else:
     wanted=next(i for i in desired if i not in selected);pick=next(o['id'] for o in cards if o['card']['id']==wanted)
    (W/(kind+'-'+str(r['selectedCount'])+'-'+str(r['requestId'])+'.json')).write_text(json.dumps(dict(v,request=r,awaitingInput=True)))
   elif 'PayMana' in kind:pick='ok' if 'ok' in ids else next((i for i in ids if i.startswith('mana:')),None) or next(o['id'] for o in opts if o['label'].startswith('Island ['))
   elif 'Mulligan' in kind or kind in ['InputAttack','InputBlock']:pass
   else:raise AssertionError((kind,ids))
   assert pick in ids,(kind,pick,ids)
   proc.stdin.write(json.dumps(dict(type='respond',session='stock',seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');proc.stdin.flush()
  else:raise AssertionError('No Stock Up completion')
 finally:proc.kill();proc.wait()
