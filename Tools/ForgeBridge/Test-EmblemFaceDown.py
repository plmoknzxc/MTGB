"""Real bridge: Garruk emblem resolution and manifest/cloak turn-face-up actions."""
import importlib.util,json,os,queue,subprocess,tempfile,threading
from pathlib import Path
spec=importlib.util.spec_from_file_location('base',Path(__file__).with_name('Test-AdvancedInteractions.py'))
b=importlib.util.module_from_spec(spec);spec.loader.exec_module(b)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(b.runtime/'classes')+';'+str(b.runtime/'lib/*'),'-d',str(b.classes),str(b.root/'Tools/ForgeBridge/tests/EmblemFaceDownFixture.java')],check=True)
out=b.root/'Tools/Validation/Reports/EmblemFaceDown';out.mkdir(parents=True,exist_ok=True)
(out/'result.json').write_text('{"status":"RUNNING"}',encoding='utf8')
with tempfile.TemporaryDirectory() as temp,(out/'engine.log').open('w',encoding='utf8') as log:
 cfg=Path(temp)/'start.json';cfg.write_text(json.dumps(dict(session='emblem',seed=4,startingLife=20,firstPlayerSeat=1,decks=[b.deck,b.deck])),encoding='utf8')
 p=subprocess.Popen([str(b.runtime/b.cfg['java']),'-Djava.awt.headless=true','-cp',str(b.classes)+';'+str(b.runtime/'classes')+';'+str(b.runtime/'lib/*'),'mtgb.bridge.EmblemFaceDownFixture',str(b.runtime/b.cfg['assets']),str(cfg)],cwd=b.runtime/b.cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
 q=queue.Queue();threading.Thread(target=lambda:[q.put(json.loads(line)) for line in p.stdout],daemon=True).start()
 def snap(m,name):
  v=dict(m['views'][0]);v.update(request=m['request'],awaitingInput=m['request']['seat']==1);(out/(name+'.json')).write_text(json.dumps(v),encoding='utf8')
 phase=0;hidden=[];cast_id=None
 try:
  for step in range(160):
   m=q.get(timeout=45)
   assert m['type'] not in ('fatal','ended','rejected'),m
   if m['type']!='request':continue
   r=m['request'];opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];v=m['views'][0];one=v['players'][0];board=one['battlefield'];pick='ok'
   print(step,phase,kind,[(o['id'],o['label'][:100]) for o in opts if not kind.startswith('InputPayMana')],flush=True)
   if kind=='InputPassPriority' and r['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
    if phase==0:
     hidden=[c['id'] for c in board if c['faceDown']];assert len(hidden)==2
     for vv in m['views'][1:]:
      for c in vv['players'][0]['battlefield']:
       if c['faceDown']:
        assert c['power']==2 and c['toughness']==2 and not c.get('printingKey')
        assert 'Grizzly' not in json.dumps(c) and 'Runeclaw' not in json.dumps(c),c
     assert all(c.get('knownFacePrintingKey') for c in board if c['faceDown'])
     assert any('WARD' in c.get('keywords',[]) for c in board if c['faceDown'])
     snap(m,'face-down');pick='card:'+str(next(c['id'] for c in board if c['planeswalker']));phase=1
    elif phase==1:
     assert len(one['emblems'])==1 and one['emblems'][0]['emblem'],one
     assert all(c['power']==5 and c['toughness']==5 for c in board if c['faceDown'])
     assert len(m['views'][1]['players'][0]['emblems'])==1
     snap(m,'emblem');print('EMBLEM',one['emblems'],flush=True)
     pick='card:'+str(hidden[0]);phase=2
    elif phase==2:
     assert next(c for c in board if c['id']==hidden[0])['name']=='Grizzly Bears'
     snap(m,'manifest-face-up');pick='card:'+str(hidden[1]);phase=3
    elif phase==3:
     assert next(c for c in board if c['id']==hidden[1])['name']=='Runeclaw Bear'
     assert all(not c['faceDown'] for c in board)
     snap(m,'both-face-up');cast_id=next(c['id'] for c in one['hand'] if c['name']=='Willbender');pick='card:'+str(cast_id);phase=4
    elif phase in (4,6):
     c=next(c for c in board if c['id']==cast_id);assert c['faceDown']
     assert ('WARD' in c.get('keywords',[]))==(phase==6)
     snap(m,'morph-down' if phase==4 else 'disguise-down');pick='card:'+str(cast_id);phase+=1
    elif phase==5:
     assert next(c for c in board if c['id']==cast_id)['name']=='Willbender'
     snap(m,'morph-up');cast_id=next(c['id'] for c in one['hand'] if c['name']=='Greenbelt Radical');pick='card:'+str(cast_id);phase=6
    elif phase==7:
     c=next(c for c in board if c['id']==cast_id);assert c['name']=='Greenbelt Radical' and not c['faceDown']
     assert any(x['kind']=='P1P1' and x['count']==1 for x in c['counters'])
     snap(m,'disguise-up');library_before=one['libraryCount'];pick='card:'+str(next(c['id'] for c in one['hand'] if c['name']=='Manifest Dread'));phase=8
    else:
     c=next(c for c in board if c['faceDown']);assert c['faceDownKind']=='manifest' and c['power']==5 and c['toughness']==5
     assert c['knownFaceName']=='Mountain' and 'card:'+str(c['id']) not in ids
     assert one['libraryCount']==library_before-2 and 'Mountain' in one['graveyard']
     snap(m,'manifest-dread');(out/'result.json').write_text(json.dumps(dict(status='PASS',hiddenIds=hidden,checks=['public projection','controller private peek','ward','emblem effect +3/+3','manifest turn face up','cloak turn face up','morph cast and turn face up','disguise cast and turn face up with trigger','manifest dread top two choose one','noncreature cannot turn face up','stable object identity'])),encoding='utf8');break
   elif kind=='RevealCards':pick='confirm-cards'
   elif kind=='SelectCards':
    snap(m,'dread-choice');pick='confirm-cards' if r.get('selectedCount') else next(i for i in ids if i.startswith('card:'))
   elif kind.startswith('InputSelect'):
    snap(m,'dread-choice');pick='ok' if 'ok' in ids else next(i for i in ids if i.startswith('card:'))
   elif kind=='dialog':
    pick=next(o['id'] for o in opts if ('emblem' in o['label'].lower() if phase==1 else 'face down' in o['label'].lower() if phase in (4,6) else 'face up' in o['label'].lower()))
   elif kind.startswith('InputPayMana'):
    pick='ok' if 'ok' in ids else next((i for i in ids if i.startswith('mana:')),None) or next(o['id'] for land in ['Forest','Island','Swamp'] for o in opts if o['label'].startswith(land+' ['))
   assert pick in ids,(kind,pick,opts)
   p.stdin.write(json.dumps(dict(type='respond',session='emblem',seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
  else:raise AssertionError('step limit')
 finally:
  if p.poll() is None:p.kill();p.wait()
