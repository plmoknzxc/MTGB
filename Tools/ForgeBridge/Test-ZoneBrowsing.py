"""Real-engine zone counts, duplicate copies, local art keys and hidden-zone privacy."""
import importlib.util,json,os,queue,subprocess,tempfile,threading
from pathlib import Path
spec=importlib.util.spec_from_file_location('base',Path(__file__).with_name('Test-AdvancedInteractions.py'))
b=importlib.util.module_from_spec(spec);spec.loader.exec_module(b)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(b.runtime/'classes')+';'+str(b.runtime/'lib/*'),'-d',str(b.classes),str(b.root/'Tools/ForgeBridge/tests/ZoneBrowsingFixture.java')],check=True)
out=b.root/'Tools/Validation/Reports/ZoneBrowsing';out.mkdir(parents=True,exist_ok=True)
with tempfile.TemporaryDirectory() as temp,(out/'engine.log').open('w',encoding='utf8') as log:
 cfg=Path(temp)/'start.json';cfg.write_text(json.dumps(dict(session='zones',seed=4,startingLife=20,firstPlayerSeat=1,decks=[b.deck,b.deck])),encoding='utf8')
 p=subprocess.Popen([str(b.runtime/b.cfg['java']),'-Djava.awt.headless=true','-cp',str(b.classes)+';'+str(b.runtime/'classes')+';'+str(b.runtime/'lib/*'),'mtgb.bridge.ZoneBrowsingFixture',str(b.runtime/b.cfg['assets']),str(cfg)],cwd=b.runtime/b.cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
 q=queue.Queue();threading.Thread(target=lambda:[q.put(json.loads(line)) for line in p.stdout],daemon=True).start()
 try:
  for step in range(40):
   m=q.get(timeout=45);assert m['type'] not in ('fatal','ended','rejected'),m
   if m['type']!='request':continue
   r=m['request']
   if r['kind']=='InputPassPriority' and r['seat']==1:
    for v in m['views']:
     for player in v['players']:
      grave=player['graveyardCards'];exile=player['exileCards']
      assert len(grave)==3 and len({c['id'] for c in grave})==3,grave
      assert sum(c['name']=='Grizzly Bears' for c in grave)==2
      assert all(c.get('printingKey') for c in grave)
      assert player['exileCount']==len(exile)==2
      hidden=next(c for c in exile if c['faceDown'])
      assert hidden['name']=='Face-down card' and not hidden.get('printingKey'),hidden
      assert 'reviewLibrary' not in player,'Library leaked during play'
      if player['seat']!=v['seat']:assert not player['hand'],'Opponent hand leaked'
    view=m['views'][0];view.update(request=r,awaitingInput=True)
    (out/'field.json').write_text(json.dumps(view),encoding='utf8')
    (out/'result.json').write_text('{"status":"PASS"}',encoding='utf8')
    print('PASS: both seats, zone counts, duplicate copies, printing keys, hidden library/hand/exile');break
   p.stdin.write(json.dumps(dict(type='respond',session='zones',seat=r['seat'],requestId=r['requestId'],option='ok'))+'\n');p.stdin.flush()
  else:raise AssertionError('No field snapshot')
 finally:
  if p.poll() is None:p.kill();p.wait()
