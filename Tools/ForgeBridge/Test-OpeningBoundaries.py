"""Native London identities/order and interrupted opening restarting with new config."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'.tools/opening-boundaries-1007/native';OUT.mkdir(parents=True,exist_ok=True);CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURE=ROOT/'Tools/ForgeBridge/tests/OpeningOrderFixture.java'
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES={'seat-one-order':(1,20,(3,0),False),'seat-two-order':(2,27,(0,3),False),'interrupt-opening':(1,20,(1,0),True),'restart-config':(2,37,(0,0),False),'auto-remainder':(2,27,(0,3),False)}
SHA=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()

def run(case):
 first,life,desired,interrupt=CASES[case];folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir()
 meta=dict(case=case,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],driverSha256=SHA(Path(__file__)),fixtureSha256=SHA(FIXTURE))
 (folder/'driver.py').write_bytes(Path(__file__).read_bytes());(folder/FIXTURE.name).write_bytes(FIXTURE.read_bytes())
 def finish(status,message):
  result=dict(meta,status=status,result=status+' '+case+': '+message)
  for p in [folder/'result.json',OUT/(case+'-result.json')]:p.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  return result
 with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'engine.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
  config=dict(session=case,seed=4,startingLife=life,firstPlayerSeat=first,decks=[DECK,DECK],auditDir=str(audit));start=Path(temp)/'start.json';start.write_text(json.dumps(config),encoding='utf8');(folder/'start.json').write_text(json.dumps(config,indent=2),encoding='utf8')
  p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.OpeningOrderFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');messages=queue.Queue()
  def reader():
   try:
    for line in p.stdout:messages.put(json.loads(line))
   finally:messages.put(None)
  threading.Thread(target=reader,daemon=True).start()
  def observation(q):
   path=audit/(str(q['requestId'])+'.json');deadline=time.monotonic()+4
   while time.monotonic()<deadline:
    try:
     result=json.loads(path.read_text());assert result['requestId']==q['requestId'];return result['players']
    except (OSError,json.JSONDecodeError):time.sleep(.01)
   raise AssertionError('Missing complete native audit')
  counts=[0,0];before={};chosen={};toggled=set();decisions=[]
  try:
   for step in range(100):
    m=messages.get(timeout=35);assert m is not None,'Native exited';trace.write(json.dumps(m,ensure_ascii=False)+'\n');trace.flush();assert m['type'] not in ['fatal','ended','rejected'],m
    if m['type']!='request':continue
    q=m['request'];kind=q['kind'];seat=q['seat'];options=q['options'];ids=[o['id'] for o in options];native=observation(q);pick='ok'
    for viewer in (1,2):assert not m['views'][viewer-1]['players'][2-viewer]['hand'],'Opponent opening leaked'
    assert all(x['life']==life for x in native),'Starting life lost'
    if kind in ['InputMulligan','InputConfirmMulligan']:
     decisions.append(seat);pick='cancel' if counts[seat-1]<desired[seat-1] else 'ok';counts[seat-1]+=1
     if interrupt and counts[seat-1]==2:
      assert q['seat']==first and not before
      (folder/'interrupted-request.json').write_text(json.dumps(m,ensure_ascii=False,indent=2),encoding='utf8')
      return finish('PASS','Real first mulligan completed; process intentionally disposed at the next decision. Restart is a separate native session, not a forged request continuation.')
    elif kind=='InputLondonMulligan':
     before.setdefault(seat,native[seat-1]);chosen.setdefault(seat,[])
     actual={int(o['id'][5:]) for o in options if o['id'].startswith('card:') and o['label'].startswith('[已选')}
     assert actual==set(chosen[seat]) and q['selectedCount']==len(actual) and q['remainingCount']==desired[seat-1]-len(actual)
     if not actual and seat not in toggled:
      pick='card:'+str(before[seat]['hand'][-1]);chosen[seat].append(before[seat]['hand'][-1])
     elif case=='auto-remainder':
      assert len(actual)==1 and 'cancel' in ids
      chosen[seat]+=[i for i in before[seat]['hand'] if i not in actual][:desired[seat-1]-len(actual)];pick='cancel'
     elif seat not in toggled:
      pick='card:'+str(chosen[seat].pop());toggled.add(seat)
     elif 'ok' in ids:pick='ok'
     else:
      target=next(i for i in reversed(before[seat]['hand']) if i not in actual);chosen[seat].append(target);pick='card:'+str(target)
     normalized=dict(m['views'][seat-1],request=q,awaitingInput=True)
     (folder/f'london-seat{seat}-step{step}.json').write_text(json.dumps(normalized,ensure_ascii=False,indent=2),encoding='utf8')
    elif kind=='InputPassPriority':
     expected=[s for n in range(max(desired)+1) for s in (first,3-first) if n<=desired[s-1]]
     assert decisions==expected and seat==first and m['views'][seat-1]['turn']==1 and m['views'][seat-1]['phase']=='UPKEEP'
     for s in (1,2):
      final=native[s-1];assert len(final['hand'])==7-desired[s-1] and len(final['library'])==53+desired[s-1] and len(set(final['hand']+final['library']))==60
      if s in before:
       assert final['library']==before[s]['library']+chosen[s],('Bottom order',before[s],chosen[s],final)
       assert final['hand']==[i for i in before[s]['hand'] if i not in chosen[s]]
     (folder/'choices.json').write_text(json.dumps(dict(before=before,chosen=chosen,final=native,decisions=decisions),indent=2),encoding='utf8')
     (folder/'resolved.json').write_text(json.dumps(m,ensure_ascii=False,indent=2),encoding='utf8')
     return finish('PASS','Exact native full library order and hand identities after reverse-order London selection/toggle; both private views, starting life/first-seat and first upkeep priority. No opening state mutated by fixture.')
    else:raise AssertionError(('Unhandled',q))
    assert pick in ids,(pick,q);print(case,step,kind,seat,pick,flush=True);p.stdin.write(json.dumps(dict(type='respond',session=case,seat=seat,requestId=q['requestId'],option=pick))+'\n');p.stdin.flush()
   raise AssertionError('Step limit')
  except Exception as error:finish('FAIL',repr(error));raise
  finally:
   if p.poll() is None:p.kill();p.wait()

if __name__=='__main__':
 selected=sys.argv[1:] or list(CASES);assert all(x in CASES for x in selected);results=[];summary=OUT/'run-summary.json';summary.write_text(json.dumps(dict(status='RUNNING',selected=selected,results=[])),encoding='utf8')
 try:
  subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),str(FIXTURE)],check=True)
  for case in selected:results.append(run(case));print(results[-1]['result'],flush=True)
 except BaseException:summary.write_text(json.dumps(dict(status='FAIL',selected=selected,results=results)),encoding='utf8');raise
 summary.write_text(json.dumps(dict(status='PASS',selected=selected,results=results),ensure_ascii=False,indent=2)+'\n',encoding='utf8')
