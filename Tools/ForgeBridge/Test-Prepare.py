"""Prepared spell ownership, cancellation, payment, stack identity and resolution in real Forge."""
import importlib.util,json,os,queue,subprocess,tempfile,threading
from pathlib import Path
spec=importlib.util.spec_from_file_location('advanced',Path(__file__).with_name('Test-AdvancedInteractions.py'))
base=importlib.util.module_from_spec(spec);spec.loader.exec_module(base)
out=base.root/'Tools/Validation/Reports/Prepare';out.mkdir(parents=True,exist_ok=True)
with tempfile.TemporaryDirectory(prefix='mtgb-prepare-') as temp,(out/'engine.log').open('w',encoding='utf-8') as log,(out/'trace.jsonl').open('w',encoding='utf-8') as trace:
 config=Path(temp)/'start.json';config.write_text(json.dumps(dict(session='prepare',fixture='cast-prepare',seed=4,startingLife=20,firstPlayerSeat=1,decks=[base.deck,base.deck])),encoding='utf-8')
 p=subprocess.Popen([str(base.runtime/base.cfg['java']),'-Djava.awt.headless=true','-cp',str(base.classes)+';'+str(base.runtime/'classes')+';'+str(base.runtime/'lib/*'),'mtgb.bridge.AdvancedInteractionFixture',str(base.runtime/base.cfg['assets']),str(config)],cwd=base.runtime/base.cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf-8')
 q=queue.Queue();threading.Thread(target=lambda:[q.put(json.loads(line)) for line in p.stdout],daemon=True).start()
 def snap(m,name):
  r=m['request'];v=dict(m['views'][r['seat']-1]);v.update(request=r,awaitingInput=True);(out/(name+'.json')).write_text(json.dumps(v),encoding='utf-8')
 try:
  creature_cast=False;cancelled=False;spell_started=False;seen_stack=False;prepared_id=None;source_id=None;searched=False;lands_before=0;hand_before=0
  for step in range(130):
   while True:
    m=q.get(timeout=40);trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ('fatal','ended','rejected'),m
    if m['type']=='request':break
   r=m['request'];opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];one,two=m['views'][0]['players'];stack=m['views'][0]['stackItems'];pick='ok'
   print(step,kind,[(o['id'],o['label'][:45]) for o in opts],flush=True)
   source=next((c for c in one['battlefield'] if c['name']=='Emeritus of Woe'),None)
   if kind=='InputPassPriority':
    if source:
     source_id=source['id']
     if source.get('preparedSpell'):
      prepared=source['preparedSpell'];prepared_id=prepared['id']
      assert prepared['name']=='Demonic Tutor' and prepared['stateName']=='PreparedSpell' and prepared['currentType']=='Sorcery'
      assert prepared['manaCost']=='{1}{B}' and prepared_id!=source_id and source['creature']
      if r['seat']==2:assert 'card:'+str(prepared_id) not in ids
      if r['seat']==1 and not stack and not spell_started:
       snap(m,'ready');assert 'card:'+str(prepared_id) in ids
       assert any(c['id']==prepared_id for c in m['views'][0]['playableExile'])
       assert not any(c['id']==prepared_id for c in m['views'][1]['playableExile'])
       lands_before=sum(c['tapped'] for c in one['battlefield'] if c['land']);hand_before=one['handCount']
       pick='card:'+str(prepared_id);spell_started=True
     if any(e['source']['name']=='Demonic Tutor' for e in stack):
      e=next(e for e in stack if e['source']['name']=='Demonic Tutor');assert e['kind']=='spell' and e['source']['stateName']=='PreparedSpell' and not e['source']['creature']
      assert not source.get('preparedSpell');seen_stack=True;snap(m,'stack')
     if seen_stack and searched and not stack:
      assert not source.get('preparedSpell') and source['id']==source_id and one['handCount']==hand_before+1
      assert sum(c['tapped'] for c in one['battlefield'] if c['land'])==lands_before+2
      assert 'Demonic Tutor' not in one['graveyard'] and 'Emeritus of Woe' not in one['graveyard']
      snap(m,'resolved');assert cancelled
      result='PASS: Emeritus of Woe enters prepared; own spell ID/action; opponent cannot cast; cancel preserves preparation; two mana; separate Demonic Tutor spell; search adds card; source stays; preparation consumed.'
      (out/'result.txt').write_text(result,encoding='utf-8');print(result);break
    if r['seat']==1 and not creature_cast and not stack:
     card=next((c for c in one['hand'] if c['name']=='Emeritus of Woe'),None)
     if card and 'card:'+str(card['id']) in ids:pick='card:'+str(card['id']);creature_cast=True
   elif kind.startswith('InputPayMana'):
    if spell_started and not cancelled:
     snap(m,'cancel-payment');pick='cancel';cancelled=True;spell_started=False
    else:
     if spell_started:snap(m,'payment')
     pick=next((i for color in ['B','U','G','R','W'] for i in ids if i=='mana:'+color),None)
     if pick is None:pick=next(o['id'] for name in ['Swamp','Island','Forest','Mountain','Plains'] for o in opts if o['label'].startswith(name+' ['))
   elif kind=='SelectCards':
    snap(m,'search');pick='confirm-cards' if r.get('selectedCount') else next(o['id'] for o in opts if o.get('card',{}).get('name')=='Mountain');searched=True
   elif kind=='dialog':pick=next(i for i in ids if i.startswith('choice:'))
   elif kind.startswith('InputSelect'):
    if seen_stack:searched=True;snap(m,'search')
    pick='ok' if 'ok' in ids else next(i for i in ids if i.startswith('card:'))
   assert pick in ids,(kind,pick)
   p.stdin.write(json.dumps(dict(type='respond',session='prepare',seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
  else:raise AssertionError('No completion')
 finally:
  if p.poll() is None:
   p.stdin.write('{"type":"shutdown"}\n');p.stdin.flush()
   try:p.wait(timeout=3)
   except subprocess.TimeoutExpired:p.kill();p.wait()
