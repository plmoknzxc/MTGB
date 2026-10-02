"""Forge 2.0.15: instantiate FRA/FRC, cast No Admittance and verify damage plus Jace token."""
import importlib.util,json,os,queue,subprocess,tempfile,threading
from pathlib import Path
s=importlib.util.spec_from_file_location('base',Path(__file__).with_name('Test-AdvancedInteractions.py'));b=importlib.util.module_from_spec(s);s.loader.exec_module(b)
out=b.root/'.tools/forge-update-2.0.15'; audited=0; cast=False
with tempfile.TemporaryDirectory(prefix='mtgb-fra-') as temp,(out/'fra-engine.log').open('w',encoding='utf8') as log:
 start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session='fra',fixture='fra',seed=4,startingLife=20,firstPlayerSeat=1,decks=[b.deck,b.deck])),encoding='utf8')
 p=subprocess.Popen([str(b.runtime/b.cfg['java']),'-Djava.awt.headless=true','-cp',str(b.classes)+';'+str(b.runtime/'classes')+';'+str(b.runtime/'lib/*'),'mtgb.bridge.AdvancedInteractionFixture',str(b.runtime/b.cfg['assets']),str(start)],cwd=b.runtime/b.cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
 q=queue.Queue();threading.Thread(target=lambda:[q.put(json.loads(l)) for l in p.stdout],daemon=True).start()
 try:
  for _ in range(100):
   m=q.get(timeout=40)
   assert m['type'] not in ('fatal','rejected','ended'),m
   if m['type']=='series-audit':audited=m['uniqueCards'];continue
   if m['type']!='request':continue
   r=m['request'];v=m['views'][0];one,two=v['players'];opts=r['options'];ids=[o['id'] for o in opts];pick='ok'
   if cast and two['life']==17 and v['stackCount']==0:
    tokens=[c for c in one['battlefield'] if c['name']=='Jace Token' and c['loyalty']==1];assert len(tokens)==1,tokens
    (out/'fra-view.json').write_text(json.dumps(v,indent=2),encoding='utf8')
    assert audited>200,audited
    result=f'PASS: {audited} unique FRA/FRC cards instantiated; No Admittance cast, opponent 20 -> 17, Jace token created.'
    (out/'fra-result.txt').write_text(result,encoding='utf8');print(result);break
   if r['kind']=='InputPassPriority' and r['seat']==1 and v['phase']=='MAIN1' and not cast:
    pick=next(o['id'] for o in opts if o['label']=='No Admittance [Hand]');cast=True
   elif r['kind']=='InputSelectTargets' and 'ok' not in ids:pick=next(o['id'] for o in opts if o['label']=='Player 2')
   elif r['kind'].startswith('InputPayMana'):pick='ok'
   assert pick in ids,(r['kind'],opts)
   p.stdin.write(json.dumps(dict(type='respond',session='fra',seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
  else:raise AssertionError('FRA spell did not resolve')
 finally:
  if p.poll() is None:
   p.stdin.write('{"type":"shutdown"}\n');p.stdin.flush()
   try:p.wait(timeout=5)
   except subprocess.TimeoutExpired:p.kill();p.wait()


