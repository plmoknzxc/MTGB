"""Cast real Preordain through both seats and verify its interactive resolution."""
import json, os, queue, subprocess, tempfile, threading
from pathlib import Path

root=Path(__file__).resolve().parents[2]; runtime=root/'ForgeRuntime'
cfg=json.loads((runtime/'runtime.json').read_text(encoding='utf-8-sig'))
out=root/'Tools/Validation/Reports/Scry'; out.mkdir(parents=True,exist_ok=True)
classes=root/'.tools/scry-tests/classes'
subprocess.run(['javac','-encoding','UTF-8','-cp',str(runtime/'classes')+';'+str(runtime/'lib/*'),'-d',str(classes),str(root/'Tools/ForgeBridge/tests/ScryFixture.java')],check=True)
deck={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}

def run(case):
 with tempfile.TemporaryDirectory(prefix='mtgb-scry-') as temp,(out/(case+'.log')).open('w',encoding='utf-8') as log:
  config=dict(session=case,seed=4,startingLife=20,firstPlayerSeat=1,decks=[deck,deck])
  if case=='failure':config['forceFailure']=True
  start=Path(temp)/'start.json';start.write_text(json.dumps(config),encoding='utf-8')
  p=subprocess.Popen([str(runtime/cfg['java']),'-Djava.awt.headless=true','-cp',str(classes)+';'+str(runtime/'classes')+';'+str(runtime/'lib/*'),'mtgb.bridge.ScryFixture',str(runtime/cfg['assets']),str(start)],cwd=runtime/cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf-8')
  q=queue.Queue();threading.Thread(target=lambda:[q.put(json.loads(line)) for line in p.stdout],daemon=True).start()
  def read():return q.get(timeout=35)
  def req():
   while True:
    m=read()
    if m['type']=='request':return m
    assert m['type'] not in ('fatal','rejected','ended'),m
  def send(m,option,seat=None):
   r=m['request'];p.stdin.write(json.dumps(dict(type='respond',session=case,seat=seat or r['seat'],requestId=r['requestId'],option=option))+'\n');p.stdin.flush()
  try:
   if case=='failure':
    for _ in range(30):
     m=read()
     if m['type']=='fatal':
      assert 'private diagnostic' not in json.dumps(m),m
      p.wait(timeout=5);assert p.returncode!=0;return 'PASS worker failure: explicit fatal, private details withheld, process exited'
     if m['type']=='request':send(m,'ok')
    raise AssertionError('Missing fatal')
   m=req();cast=False;saw=False;reset=False;selections=0;passes=set()
   for _ in range(100):
    r=m['request'];v=m['views'][r['seat']-1];kind=r['kind'];ids=[o['id'] for o in r['options']];pick='ok'
    if kind=='InputPassPriority':
     if v['stackItems']:passes.add(r['seat'])
     if not cast and r['seat']==1 and v['phase']=='MAIN1':
      card=next(c for c in v['players'][0]['hand'] if c['name']=='Preordain');pick='card:'+str(card['id']);cast=True
     elif saw and not v['stackItems']:
      expected={'top':'Lightning Bolt','reverse':'Forest','split':'Forest','bottom':'Mountain'}[case]
      assert any(c['name']==expected for c in v['players'][0]['hand']),v
      assert 'Preordain' in v['players'][0]['graveyard'],v
      assert passes=={1,2} and reset
      (out/(case+'-resolved.json')).write_text(json.dumps(m),encoding='utf-8')
      return 'PASS '+case+': both seats passed, scry/reset/confirm, expected draw, spell resolved'
    elif kind.startswith('InputPayMana'):
     pick='mana:U'
     if pick not in ids:
      land=next(c for c in v['players'][0]['battlefield'] if c['name']=='Island' and not c['tapped']);pick='card:'+str(land['id'])
    elif kind=='Scry':
     saw=True;assert r['seat']==1
     # Only the owner sees these choices in the projected player view.
     assert 'Lightning Bolt' not in json.dumps(m['views'][1]) and 'Forest' not in json.dumps(m['views'][1])
     if 'reset-scry' in ids and not reset:pick='reset-scry';reset=True;selections=0
     elif 'confirm-scry' in ids:pick='confirm-scry'
     else:
      prefix='scry-bottom:' if case=='bottom' or (case=='split' and selections==0) else 'scry-top:'
      choices=[o for o in r['options'] if o['id'].startswith(prefix)]
      pick=choices[-1 if case=='reverse' else 0]['id'];selections+=1
     if not reset and selections==1:
      send(m,'scry-top:999999');assert read()['type']=='rejected'
      send(m,pick,2);assert read()['type']=='rejected'
    elif kind=='dialog':pick=ids[0]
    assert pick in ids,(kind,pick,ids)
    send(m,pick);m=req()
   raise AssertionError('Preordain never resolved')
  finally:
   if p.poll() is None:
    p.stdin.write('{"type":"shutdown"}\n');p.stdin.flush()
    try:p.wait(timeout=3)
    except subprocess.TimeoutExpired:p.kill();p.wait()
   (out/(case+'-process.txt')).write_text(f'pid={p.pid} exited={p.returncode}',encoding='utf-8')

if __name__=='__main__':
 results=[]
 try:
  for case in ['top','reverse','split','bottom','failure']:
   result=run(case);results.append(result);print(result,flush=True)
 finally:(out/'engine-result.txt').write_text('\n'.join(results),encoding='utf-8')
