"""Actual Fable cast and all three chapter outcomes across native turns."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';cfg=json.loads((RT/'runtime.json').read_text('utf-8-sig'));OUT=ROOT/'.tools/saga-chapters-1006/native';OUT.mkdir(parents=True,exist_ok=True);classes=OUT/'classes';classes.mkdir(exist_ok=True)
fixture=ROOT/'Tools/ForgeBridge/tests/StateBranchFixture.java';subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),str(fixture)],check=True)
CASES=['chapter-draw','chapter-decline','chapter-seat2'];deck={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]};sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def run(case):
 actor=2 if case=='chapter-seat2' else 1;n=0 if case=='chapter-decline' else 2;folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True);meta={'case':case,'folder':folder.relative_to(ROOT).as_posix(),'runtimeHash':cfg['runtimeHash'],'driverSha256':sha(Path(__file__)),'fixtures':[{'path':fixture.relative_to(ROOT).as_posix(),'sha256':sha(fixture)}]}
 def finish(status,message):
  d={**meta,'status':status,'result':status+' '+case+': '+message}
  for p in [folder/'result.json',OUT/(case+'-result.json')]:p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  return d
 with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'trace.jsonl').open('w',encoding='utf8') as trace,(folder/'engine.log').open('w',encoding='utf8') as log:
  audit=Path(temp)/'audit';audit.mkdir();start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session=case,actor=actor,own={'Hand':['Fable of the Mirror-Breaker','Forest','Island']},other={},auditDir=str(audit),seed=4,firstPlayerSeat=actor,startingLife=30,decks=[deck,deck])),encoding='utf8');p=subprocess.Popen([str(RT/cfg['java']),'-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.StateBranchFixture',str(RT/cfg['assets']),str(start)],cwd=RT/cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');messages=queue.Queue()
  def reader():
   try:
    for line in p.stdout:messages.put(json.loads(line))
   finally:messages.put(None)
  threading.Thread(target=reader,daemon=True).start()
  def snap(m,name):
   for seat,v in enumerate(m['views'],1):
    d=dict(v);d.update(request=m['request'],awaitingInput=True);(folder/(name+('' if seat==actor else '-other')+'.json')).write_text(json.dumps(d,ensure_ascii=False,indent=2),encoding='utf8')
  cast=None;discards=[];first=False;second=False;selected=False;choice_library=None;choice_hand=None;goblin=None;initial_library=None;deadline=time.monotonic()+180
  try:
   for step in range(400):
    assert time.monotonic()<deadline,'Deadline';m=messages.get(timeout=40);assert m is not None,'Native exited';trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ['fatal','ended','rejected'],m
    if m['type']!='request':continue
    r=m['request'];opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];v=m['views'][actor-1];pl=v['players'][actor-1];stack=v['stackItems'];snap(m,'step-'+str(step));pick=None
    assert all([x['life'] for x in view['players']]==[30,30] for view in m['views']),'Unrelated life change'
    if kind=='InputPassPriority':
     if cast is None and r['seat']==actor and v['turnSeat']==actor and v['phase']=='MAIN1':
      cast=next(c['id'] for c in pl['hand'] if c['name']=='Fable of the Mirror-Breaker');discards=[c['id'] for c in pl['hand'] if c['id']!=cast];assert len(discards)==2;initial_library=pl['libraryCount'];snap(m,'before');pick='card:'+str(cast)
     elif cast is not None and not stack and v['turnSeat']==actor and v['phase']=='MAIN1':
      fable=next((c for c in pl['battlefield'] if c['name']=='Fable of the Mirror-Breaker'),None)
      if fable:
       lore=next((c['count'] for c in fable['counters'] if c['kind']=='LORE'),0)
       if lore==1 and not first:
        assert fable['id']==cast and not fable['backFace'];tokens=[c for c in pl['battlefield'] if c.get('token')];assert len(tokens)==1 and tokens[0]['power']==tokens[0]['toughness']==2 and 'Goblin' in tokens[0]['currentType'];goblin=tokens[0]['id'];assert pl['libraryCount']==initial_library and len(pl['hand'])==2;first=True;snap(m,'chapter-one')
       elif lore==2 and not second:
        assert first and selected and choice_library is not None;assert pl['libraryCount']==choice_library-n and len(pl['hand'])==choice_hand;assert all(any(c['id']==i for c in pl['graveyardCards'])==bool(n) for i in discards);second=True;snap(m,'chapter-two')
      reflected=[c for c in pl['battlefield'] if c['name']=='Reflection of Kiki-Jiki']
      if reflected:
       assert first and second and len(reflected)==1;copy=reflected[0];assert copy['backFace'] and copy['power']==copy['toughness']==2 and 'Saga' not in copy['currentType'];assert not any(c['name']=='Fable of the Mirror-Breaker' for c in pl['battlefield']) and not any(c['id']==cast for c in pl['graveyardCards']);assert any(c['id']==goblin for c in pl['battlefield']);assert not any(c['kind']=='LORE' for c in copy['counters']);snap(m,'chapter-three');return finish('PASS',f'actual Fable cast/pay2R: chapterI exact 2/2 Goblin; chapterII discard {n} exact original hand IDs/draw {n} from native library; chapterIII exile-return transformed Reflection2/2, no lore/saga/graveyard sacrifice, Goblin remains; both views, next priority; actor={actor}')
     if pick is None:pick='ok'
    elif 'Mulligan' in kind:pick='keep' if 'keep' in ids else 'ok'
    elif 'PayMana' in kind:pick='ok' if 'ok' in ids and not next(o for o in opts if o['id']=='ok').get('disabled') else next(o['id'] for o in opts if o['id'].startswith('card:'))
    elif kind in ['InputSelectCardsFromHand','InputSelectCardsFromList']:
     assert r['seat']==actor and first
     if choice_library is None:choice_library=pl['libraryCount'];choice_hand=len(pl['hand']);snap(m,'discard-initial')
     if r['selectedCount']>=n:pick='ok';selected=True;snap(m,'discard-confirm')
     else:pick=next(o['id'] for o in opts if o.get('card',{}).get('id') in discards and not o.get('selected'))
    elif kind=='InputConfirm':pick='ok'
    elif kind=='dialog':pick=ids[0]
    elif 'ok' in ids:pick='ok'
    else:raise AssertionError(('Unhandled',r))
    assert pick in ids,(kind,pick,ids);print(case,step,kind,r['seat'],pick,'phase',v['phase'],'turn',v['turnSeat'],flush=True);p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
   raise AssertionError('Step limit')
  except Exception:finish('FAIL',traceback.format_exc());raise
  finally:
   if p.poll() is None:p.kill();p.wait()
if __name__=='__main__':
 results=[]
 for case in sys.argv[1:] or CASES:
  try:d=run(case);print(d['result'],flush=True);results.append(d)
  except Exception as e:print('FAIL '+case+': '+repr(e),flush=True);results.append(json.loads((OUT/(case+'-result.json')).read_text()))
 (OUT/'results.json').write_text(json.dumps({'status':'PASS' if all(d['status']=='PASS' for d in results) else 'FAIL','results':results},ensure_ascii=False,indent=2)+'\n',encoding='utf8');sys.exit(any(d['status']!='PASS' for d in results))
