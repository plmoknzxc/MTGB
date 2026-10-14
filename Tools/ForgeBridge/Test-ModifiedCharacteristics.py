"""Verify characteristic colors against real Forge continuous-effect state."""
import importlib.util,json,os,queue,subprocess,tempfile,threading
from pathlib import Path
spec=importlib.util.spec_from_file_location('base',Path(__file__).with_name('Test-AdvancedInteractions.py'))
b=importlib.util.module_from_spec(spec);spec.loader.exec_module(b)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(b.runtime/'classes')+';'+str(b.runtime/'lib/*'),'-d',str(b.classes),str(b.root/'Tools/ForgeBridge/tests/ModifiedCharacteristicsFixture.java')],check=True)
out=b.root/'Tools/Validation/Reports/ModifiedCharacteristics';out.mkdir(parents=True,exist_ok=True)
with tempfile.TemporaryDirectory(dir=b.root/'.tools/characteristic-colors-1006') as temp,(out/'engine.log').open('w',encoding='utf8') as log:
 cfg=Path(temp)/'start.json';cfg.write_text(json.dumps(dict(session='colors',projectionOutput=str(out),seed=4,startingLife=20,firstPlayerSeat=1,decks=[b.deck,b.deck])),encoding='utf8')
 p=subprocess.Popen([str(b.runtime/b.cfg['java']),'-Djava.awt.headless=true','-cp',str(b.classes)+';'+str(b.runtime/'classes')+';'+str(b.runtime/'lib/*'),'mtgb.bridge.ModifiedCharacteristicsFixture',str(b.runtime/b.cfg['assets']),str(cfg)],cwd=b.runtime/b.cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
 q=queue.Queue();threading.Thread(target=lambda:[q.put(json.loads(line)) for line in p.stdout],daemon=True).start()
 try:
  for step in range(40):
   m=q.get(timeout=45);assert m['type'] not in ('fatal','ended','rejected'),m
   if m['type']!='request':continue
   r=m['request'];v=m['views'][0]
   if r['kind']=='InputPassPriority' and r['seat']==1 and v['phase']=='MAIN1':
    cards={c['name']:c for c in v['players'][0]['battlefield']}
    assert (cards['Runeclaw Bear']['power'],cards['Runeclaw Bear']['toughness'])==(4,2)
    assert (cards['Balduvian Bears']['power'],cards['Balduvian Bears']['toughness'])==(0,1)
    assert cards['Grizzly Bears']['power']==cards['Grizzly Bears']['basePower']==2
    assert 'FLYING' not in cards['Vampire Nighthawk']['grantedKeywords']
    named=cards['Silvercoat Lion'];assert named['originalName']=='Llanowar Elves' and named['hasAbilityChanges']
    assert named['originalColors']!=named['currentColors'] and named['originalManaCost']!=named['manaCost']
    assert named['originalKeywordDetails']!=named['currentKeywordDetails']
    fixed=cards['Elvish Mystic'];assert fixed['toughnessBaseChanged'] and not fixed['powerBaseChanged']
    assert not cards['Runeclaw Bear']['powerBaseChanged']
    assert m['views'][1]['players'][0]['battlefield']==v['players'][0]['battlefield']
    v.update(request=r,awaitingInput=True);(out/'field.json').write_text(json.dumps(v),encoding='utf8')
    (out/'result.json').write_text('{"status":"PASS"}',encoding='utf8');print('PASS: modified characteristics');break
   p.stdin.write(json.dumps(dict(type='respond',session='colors',seat=r['seat'],requestId=r['requestId'],option='ok'))+'\n');p.stdin.flush()
  else:raise AssertionError('No field snapshot')
 finally:
  if p.poll() is None:p.kill();p.wait()
