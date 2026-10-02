import importlib.util,json,os,queue,subprocess,tempfile,threading,sys
from pathlib import Path
spec=importlib.util.spec_from_file_location('base',Path(__file__).with_name('Test-AdvancedInteractions.py'));base=importlib.util.module_from_spec(spec);spec.loader.exec_module(base)
out=base.root/'Tools/Validation/Reports/Empower';out.mkdir(parents=True,exist_ok=True)
def run(case):
 with tempfile.TemporaryDirectory(prefix='empower-') as temp,(out/(case+'.log')).open('w',encoding='utf8') as log,(out/(case+'.jsonl')).open('w',encoding='utf8') as trace:
  cfg=Path(temp)/'start.json';cfg.write_text(json.dumps(dict(session=case,fixture='cast-empower-multiple' if case=='multiple' else 'cast-empower',seed=4,startingLife=20,firstPlayerSeat=1,decks=[base.deck,base.deck])),encoding='utf8')
  p=subprocess.Popen([str(base.runtime/base.cfg['java']),'-Djava.awt.headless=true','-cp',str(base.classes)+';'+str(base.runtime/'classes')+';'+str(base.runtime/'lib/*'),'mtgb.bridge.AdvancedInteractionFixture',str(base.runtime/base.cfg['assets']),str(cfg)],cwd=base.runtime/base.cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
  q=queue.Queue();threading.Thread(target=lambda:[q.put(json.loads(line)) for line in p.stdout],daemon=True).start()
  def snap(m,n):
   v=dict(m['views'][0]);v.update(request=m['request'],awaitingInput=True);(out/(case+'-'+n+'.json')).write_text(json.dumps(v),encoding='utf8')
  casts=0;activated=False;initial=None;beforehand=0;turn=None;drawn=False;zero_activated=False;surveil_seen=False
  try:
   for step in range(180):
    while True:
     m=q.get(timeout=40);trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ['fatal','ended','rejected'],m
     if m['type']=='request':break
    r=m['request'];opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];v=m['views'][0];one=v['players'][0];stack=v['stackItems'];tokens=[c for c in one['battlefield'] if c['name']=='Jace Token'];pick='ok'
    print(case,step,kind,[(o['id'],o['label'][:65]) for o in opts],[(c['id'],c.get('loyalty')) for c in tokens],flush=True)
    if kind=='InputPassPriority':
     if case=='multiple' and initial is None:initial={c['id']:c['loyalty'] for c in tokens};assert len(initial)==2
     if r['seat']==1 and v['phase']=='MAIN1' and not stack:
      if case=='multiple' and casts:
       assert len(tokens)==2 and sorted(c['loyalty'] for c in tokens)==[2,5];snap(m,'resolved');return
      if tokens and casts==1 and not activated:snap(m,'created');assert tokens[0]['loyalty']==2
      if activated:
       if case=='surveil':
        assert tokens[0]['loyalty']==1;snap(m,'resolved');return
       if case in ('draw','zero') and not drawn and tokens and tokens[0]['loyalty']==1:
        assert one['handCount']==beforehand+1;snap(m,'draw-resolved')
        if case=='draw':return
        drawn=True
       if case=='zero' and zero_activated and not tokens:
        assert surveil_seen;snap(m,'zero-resolved');return
       if case=='zero' and drawn and tokens and not zero_activated and 'card:'+str(tokens[0]['id']) in ids:
        pick='card:'+str(tokens[0]['id']);zero_activated=True
      if casts<(2 if case in ('draw','zero') else 1):
       card=next(c for c in one['hand'] if c['name']=='Way of the Pyromancer');pick='card:'+str(card['id']);casts+=1
      elif not activated:
       if case in ('draw','zero'):assert tokens[0]['loyalty']==4;snap(m,'increased')
       snap(m,'ready');pick='card:'+str(tokens[0]['id']);activated=True;beforehand=one['handCount']
    elif kind.startswith('InputPayMana'):
     pick=next((i for i in ids if i=='mana:R'),None) or next(o['id'] for o in opts if o['label'].startswith('Mountain ['))
    elif kind=='dialog':
     snap(m,'abilities');key='Surveil' if case=='surveil' or zero_activated else 'Draw a card';pick=next(o['id'] for o in opts if key in o['label'])
    elif case=='multiple' and kind in ['SelectCards','InputSelectEntities','InputSelectCards','InputSelectEntitiesFromList']:
     snap(m,'choose');pick='ok' if 'ok' in ids else next(o['id'] for o in opts if o.get('card',{}).get('id')==max(initial))
    elif kind=='Surveil':
     surveil_seen=True;snap(m,'surveil');pick='confirm-scry' if 'confirm-scry' in ids else next(i for i in ids if i.startswith('scry-bottom:'))
    else:
     snap(m,'request-'+str(step))
     if 'ok' not in ids:pick=ids[0]
    assert pick in ids,(kind,pick)
    p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
   raise AssertionError('no completion')
  finally:
   if p.poll() is None:p.kill();p.wait()
for case in sys.argv[1:] or ['surveil','draw','multiple']:
 run(case);print('PASS',case)
