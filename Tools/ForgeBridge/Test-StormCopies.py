"""Actual storm copies: count, exact stack identity, retargeting and counters."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';cfg=json.loads((RT/'runtime.json').read_text('utf-8-sig'));OUT=ROOT/'.tools/storm-copy-1006/native';OUT.mkdir(parents=True,exist_ok=True);classes=OUT/'classes';classes.mkdir(exist_ok=True)
fixture=ROOT/'Tools/ForgeBridge/tests/StateBranchFixture.java';subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),str(fixture)],check=True)
CASES=['storm-split','original-countered','copy-countered','trigger-countered'];deck={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]};sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def run(case):
 split=case=='storm-split';counter=case!='storm-split';hand=['Ornithopter','Memnite','Grapeshot']+(['Stifle' if case=='trigger-countered' else 'Counterspell'] if counter else []);own={'Hand':hand};folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True);meta={'case':case,'folder':folder.relative_to(ROOT).as_posix(),'runtimeHash':cfg['runtimeHash'],'driverSha256':sha(Path(__file__)),'fixtures':[{'path':fixture.relative_to(ROOT).as_posix(),'sha256':sha(fixture)}]}
 def finish(status,message):
  d={**meta,'status':status,'result':status+' '+case+': '+message}
  for p in [folder/'result.json',OUT/(case+'-result.json')]:p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  return d
 with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'trace.jsonl').open('w',encoding='utf8') as trace,(folder/'engine.log').open('w',encoding='utf8') as log:
  audit=Path(temp)/'audit';audit.mkdir();start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session=case,actor=1,own=own,other={},auditDir=str(audit),seed=4,firstPlayerSeat=1,startingLife=30,decks=[deck,deck])),encoding='utf8');p=subprocess.Popen([str(RT/cfg['java']),'-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.StateBranchFixture',str(RT/cfg['assets']),str(start)],cwd=RT/cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');messages=queue.Queue()
  def reader():
   try:
    for line in p.stdout:messages.put(json.loads(line))
   finally:messages.put(None)
  threading.Thread(target=reader,daemon=True).start()
  def snap(m,name):
   for seat,v in enumerate(m['views'],1):
    d=dict(v);d.update(request=m['request'],awaitingInput=True);(folder/(name+('' if seat==1 else '-other')+'.json')).write_text(json.dumps(d,ensure_ascii=False,indent=2),encoding='utf8')
  stage=0;original=None;original_stack=None;copy_ids=[];counter_target=None;counter_cast=False;storm_started=False;pool_seen=False;deadline=time.monotonic()+180
  try:
   for step in range(250):
    assert time.monotonic()<deadline,'Deadline';m=messages.get(timeout=40);assert m is not None,'Native exited';trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ['fatal','ended','rejected'],m
    if m['type']!='request':continue
    r=m['request'];opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];v=m['views'][0];pl,op=v['players'];stack=v['stackItems'];snap(m,'step-'+str(step));pick=None
    shots=[x for x in stack if x['kind']=='spell' and x['source']['name']=='Grapeshot'];storm=[x for x in stack if x['kind']=='trigger' and x['source']['name']=='Grapeshot']
    if shots:
     roots=[x for x in shots if not x['copied']]
     if roots:assert len(roots)==1;original_stack=roots[0]['id'];original=roots[0]['source']['id']
     assert len({x['id'] for x in shots})==len(shots) and all(x['controllerSeat']==1 for x in shots)
     assert all(view['stackItems']==stack for view in m['views'])
    if storm:storm_started=True;snap(m,'storm-trigger')
    if len(shots)==3:
     assert len([x for x in shots if x['copied']])==2 and len(roots)==1
     copies=[x for x in shots if x['copied']];copy_ids=[x['id'] for x in copies];assert original_stack not in copy_ids and len({x['source']['id'] for x in shots})==3
     pool_seen=True;snap(m,'copies-ready')
    if kind=='InputPassPriority':
     if stage<3 and r['seat']==1 and v['phase']=='MAIN1' and not stack:
      if stage>0:assert len([c for c in pl['battlefield'] if c['name'] in ['Ornithopter','Memnite']])==stage
      pick='card:'+str(next(c['id'] for c in pl['hand'] if c['name']==hand[stage]));stage+=1
     elif counter and not counter_cast and r['seat']==1 and ((case=='trigger-countered' and storm) or (case!='trigger-countered' and pool_seen)):
      counter_target=storm[0]['id'] if case=='trigger-countered' else original_stack if case=='original-countered' else copy_ids[0]
      snap(m,'counter-before');pick='card:'+str(next(c['id'] for c in pl['hand'] if c['name']==hand[-1]));counter_cast=True
     elif stage==3 and storm_started and not stack:
      assert case=='trigger-countered' or pool_seen
      expected=[28,29] if split else [30,29] if case=='trigger-countered' else [30,28]
      assert [p2['life'] for p2 in v['players']]==expected,([p2['life'] for p2 in v['players']],expected)
      assert all([p2['life'] for p2 in view['players']]==expected for view in m['views'])
      assert sum(c['name']=='Grapeshot' for c in pl['graveyardCards'])==1
      assert not any(c['name']=='Grapeshot' and c.get('token') for c in pl['battlefield']+pl['graveyardCards']+pl['hand'])
      assert not counter or counter_cast;snap(m,'resolved');return finish('PASS','actual two zero-cost spells then Grapeshot; storm count2, distinct original/copy stack and source identities when copies exist, exact '+case+' flow and life '+str(expected)+'; copies leave no paper graveyard extras, both public views and next priority')
     if pick is None:pick='ok'
    elif 'Mulligan' in kind:pick='keep' if 'keep' in ids else 'ok'
    elif 'PayMana' in kind:pick='ok' if 'ok' in ids and not next(o for o in opts if o['id']=='ok').get('disabled') else next(o['id'] for o in opts if o['id'].startswith('card:'))
    elif kind=='SelectStackTarget':pick='ok' if 'ok' in ids else 'stack:'+str(counter_target)
    elif 'SelectTargets' in kind:
     if 'ok' in ids and not next(o for o in opts if o['id']=='ok').get('disabled'):pick='ok'
     else:
      desired='Player 1' if split and storm_started else 'Player 2';pick=next(o['id'] for o in opts if o['id'].startswith('player:') and o['label']==desired)
    elif kind=='OrderAbilities':pick='confirm-order' if 'confirm-order' in ids else next(o['id'] for o in opts if o['id'].startswith('order:'))
    elif kind in ['dialog','Option']:pick=next(i for i in ids if i not in ['cancel','no']) if split else ('no' if 'no' in ids else next(i for i in ids if i!='cancel'))
    elif 'ok' in ids:pick='ok'
    else:raise AssertionError(('Unhandled',r))
    assert pick in ids,(kind,pick,ids);print(case,step,kind,r['seat'],pick,'stage',stage,'shots',len(shots),'storm',len(storm),flush=True);p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
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
