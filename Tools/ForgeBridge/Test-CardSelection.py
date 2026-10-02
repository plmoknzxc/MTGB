"""Real rules paths: library, hand, graveyard, exile, surveil, ordering and reveal."""
import json,os,queue,subprocess,tempfile,threading,sys
from pathlib import Path
root=Path(__file__).resolve().parents[2];runtime=root/'ForgeRuntime'
cfg=json.loads((runtime/'runtime.json').read_text(encoding='utf-8-sig'))
out=root/'Tools/Validation/Reports/CardSelection';out.mkdir(parents=True,exist_ok=True)
classes=root/'.tools/selection-tests/classes';bridge=runtime/'classes'
subprocess.run(['javac','-encoding','UTF-8','-cp',str(bridge)+';'+str(runtime/'lib/*'),'-d',str(classes),str(root/'Tools/ForgeBridge/tests/CardSelectionFixture.java')],check=True)
deck={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
cases={'surveil':'Curate','library':'Demonic Tutor','hand':'Mind Rot','graveyard':'Regrowth','exile':'Pull from Eternity','multi':'Cultivate','order':'Ponder','reveal':'Duress'}
cases.update({'library-dialog':'Demonic Tutor','multi-dialog':'Cultivate','gift-dialog':'Gifts Ungiven'})
cases.update({name:'Merfolk Branchwalker' for name in ['explore-land','explore-grave','explore-keep']})
cases['look']='Impulse'
cases.update({'fetch':'Misty Rainforest','fetch-tarn':'Scalding Tarn'})
cases['fetch-foothills']='Wooded Foothills'
def run(case):
 spell=cases[case]
 mode=case.split('-')[0]
 with tempfile.TemporaryDirectory(prefix='mtgb-selection-') as temp,(out/(case+'.log')).open('w',encoding='utf-8') as log,(out/(case+'.jsonl')).open('w',encoding='utf-8') as trace:
  start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session=case,spell=spell,seed=4,startingLife=20,firstPlayerSeat=1,decks=[deck,deck])),encoding='utf-8')
  p=subprocess.Popen([str(runtime/cfg['java']),'-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,bridge,runtime/'lib/*'])),'mtgb.bridge.CardSelectionFixture',str(runtime/cfg['assets']),str(start)],cwd=runtime/cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf-8')
  q=queue.Queue();threading.Thread(target=lambda:[q.put(json.loads(line)) for line in p.stdout],daemon=True).start()
  def send(m,pick,seat=None,request=None):
   r=m['request'];p.stdin.write(json.dumps(dict(type='respond',session=case,seat=seat or r['seat'],requestId=request or r['requestId'],option=pick))+'\n');p.stdin.flush()
  cast=False;seen=set();chosen=[];initial=None;tests=False;toggle_step=0;multi_step=0
  try:
   for _ in range(180):
    m=q.get(timeout=25);trace.write(json.dumps(m)+'\n');trace.flush()
    assert m['type'] not in ('fatal','rejected','ended'),m
    if m['type']!='request':continue
    r=m['request'];v=m['views'][r['seat']-1];kind=r['kind'];opts=r['options'];ids=[o['id'] for o in opts];pick='ok'
    print(case,kind,flush=True)
    assert all('request' not in w for w in m['views']), 'Request leaked into shared views'
    if kind=='InputPassPriority':
     if not cast and r['seat']==1 and v['phase']=='MAIN1':
      c=next(c for c in v['players'][0]['battlefield' if mode=='fetch' else 'hand'] if c['name']==spell);pick='card:'+str(c['id']);cast=True;initial=v
     elif cast and (spell in v['players'][0]['graveyard'] or mode=='explore' and any(c['name']==spell for c in v['players'][0]['battlefield'])) and not v['stackItems']:
      me=v['players'][0];op=v['players'][1]
      if case=='surveil':assert 'Lightning Bolt' in me['graveyard'] and any(c['name']=='Island' for c in me['hand'])
      if mode=='library':assert any(c['name']=='Lightning Bolt' for c in me['hand'])
      if mode=='fetch':
       assert me['life']==initial['players'][0]['life']-1
       assert len(me['battlefield'])==len(initial['players'][0]['battlefield']), 'Fetch failed to put a land onto battlefield'
       assert me['libraryCount']==initial['players'][0]['libraryCount']-1
      if case=='hand':assert op['handCount']==initial['players'][1]['handCount']-2
      if case=='graveyard':assert any(c['name']=='Grizzly Bears' for c in me['hand'])
      if case=='exile':assert 'Goblin Piker' in me['graveyard']
      if mode=='multi':assert len(me['battlefield'])==len(initial['players'][0]['battlefield'])+1 and me['handCount']==initial['players'][0]['handCount']
      if case=='order':assert any(c['name']=='Lightning Bolt' for c in me['hand'])
      if case=='reveal':assert op['handCount']==initial['players'][1]['handCount']-1
      if mode=='gift':assert me['handCount']==initial['players'][0]['handCount']+1 and len(me['graveyard'])==len(initial['players'][0]['graveyard'])+3
      if mode=='explore':
       creature=next(c for c in me['battlefield'] if c['name']==spell)
       assert creature['power']==(2 if case=='explore-land' else 3),creature
       if case=='explore-land':assert any(c['name']=='Forest' for c in me['hand']) and me['handCount']==initial['players'][0]['handCount']
       elif case=='explore-grave':assert 'Lightning Bolt' in me['graveyard']
       else:assert 'Lightning Bolt' not in me['graveyard'] and me['libraryCount']==initial['players'][0]['libraryCount']
      assert seen,(case,'no choices observed');return 'PASS '+case+': '+spell+' resolved; '+', '.join(sorted(seen))
    elif 'PayMana' in kind:
     # Forge's message identifies still-unpaid colors. Offer an untapped basic;
     # the engine rejects unrelated mana sources from its option set.
     lands=[c for c in v['players'][r['seat']-1]['battlefield'] if not c['tapped'] and 'card:'+str(c['id']) in ids]
     preference={'Curate':['Island','Mountain'],'Demonic Tutor':['Swamp','Mountain'],'Mind Rot':['Swamp','Mountain'],'Regrowth':['Forest','Mountain'],'Pull from Eternity':['Plains'],'Cultivate':['Forest','Mountain'],'Ponder':['Island'],'Duress':['Swamp'],'Gifts Ungiven':['Island','Mountain'],'Merfolk Branchwalker':['Forest','Mountain'],'Impulse':['Island','Mountain']}[spell]
     land=next((c for name in preference for c in lands if c['name']==name),None)
     pick='card:'+str(land['id']) if land else next(i for i in ids if i.startswith('mana:'))
    elif kind=='InputSelectTargets':
     payload=[o for o in opts if o.get('card')]
     if case in ('graveyard','exile'):
      name='Grizzly Bears' if case=='graveyard' else 'Goblin Piker';pick=next(o['id'] for o in payload if o['card']['name']==name);seen.add(kind)
     else:pick='player:'+str(v['players'][1]['targetId'])
     if r.get('selectedCount')==1 and 'ok' in ids:pick='ok'
    elif kind=='Surveil':
     seen.add(kind)
     pick='confirm-scry' if 'confirm-scry' in ids else next(o['id'] for o in opts if o.get('card') and o['id'].startswith('scry-bottom:' if o['card']['name']=='Lightning Bolt' else 'scry-top:'))
    elif kind=='RevealCards':seen.add(kind);pick='confirm-cards'
    elif kind in ('SelectCards','OrderCards') or kind.startswith('InputSelect'):
     seen.add(kind);cards=[o for o in opts if o.get('card') and not o.get('selected')]
     if mode=='multi' and any(c.get('selection')=='不可选' for c in r.get('previewCards',[])):
      unavailable=next(c for c in r['previewCards'] if c.get('selection')=='不可选')
      send(m,'card:'+str(unavailable['id']));assert q.get(timeout=5)['error']=='invalid-option'
      if any(c['name']=='Lightning Bolt' for c in r['previewCards']):
       (out/(case+'-library-context.json')).write_text(json.dumps(dict(v,request=r,awaitingInput=True)),encoding='utf-8')
     if case=='look' and cards:assert len(cards)+len(r.get('previewCards',[]))<=4,'Top-four effect exposed deeper library'
     target='Lightning Bolt' if mode in ('library','order') else 'Forest' if mode=='multi' else 'Negate'
     option=next((o for o in cards if o['card']['name']==target),cards[0] if cards else None)
     if 'confirm-cards' in ids and (not cards or kind=='SelectCards' and r.get('selectedCount',0)>=max(r.get('maxCount',1) if mode=='gift' else 2 if mode=='multi' and r.get('maxCount',0)>1 else 1,r.get('minCount',0))):pick='confirm-cards'
     elif 'ok' in ids and r.get('selectedCount',0)>=max(2 if mode=='multi' and r.get('maxCount',0)>1 else 1,r.get('minCount',0)):pick='ok'
     elif option:pick=option['id'];chosen.append(option['card']['name'])
     else:pick='ok' if 'ok' in ids else 'confirm-cards'
    elif kind=='dialog':pick=ids[-1] if case=='order' else ids[0]
    elif kind=='InputConfirm':pick='cancel' if case in ('order','explore-keep') and 'cancel' in ids else 'ok'
    if mode=='library' and (kind=='SelectCards' or kind.startswith('InputSelectEntities')) or case in ('graveyard','exile') and kind=='InputSelectTargets':
     names={'graveyard':('Shock','Grizzly Bears'),'exile':('Ornithopter','Goblin Piker')}.get(case,('Island','Lightning Bolt'))
     a=next(o for o in opts if o.get('card',{}).get('name')==names[0])
     b=next(o for o in opts if o.get('card',{}).get('name')==names[1])
     # Select A, deselect A, select A, replace with B, then explicitly confirm.
     expected=[0,1,0,1,1]
     assert r.get('selectedCount',0)==expected[toggle_step],(toggle_step,r)
     selected=[o['id'] for o in opts if o.get('selected')]
     if toggle_step in (1,3):assert selected==[a['id']],selected
     if toggle_step==4:assert selected==[b['id']],selected
     pick=[a['id'],a['id'],a['id'],b['id'],'confirm-cards' if kind=='SelectCards' else 'ok'][toggle_step]
     toggle_step+=1
    if mode=='multi' and r.get('maxCount')==2 and (kind=='SelectCards' or kind.startswith('InputSelectEntities')):
     a=next(o for o in opts if o.get('card',{}).get('name')=='Forest')
     b=next(o for o in opts if o.get('card',{}).get('name')=='Island')
     c=next(o for o in opts if o.get('card',{}).get('name')=='Mountain')
     assert r.get('selectedCount',0)==[0,1,0,1,2,2,1,2][multi_step],(multi_step,r)
     if multi_step==5:assert {o['id'] for o in opts if o.get('selected')}=={a['id'],b['id']},'Full multi selection was replaced'
     pick=[a['id'],a['id'],a['id'],b['id'],c['id'],a['id'],a['id'],'confirm-cards' if kind=='SelectCards' else 'ok'][multi_step]
     multi_step+=1
    assert pick in ids,(case,kind,pick,opts)
    if mode=='explore' and kind=='InputConfirm':
     assert any(c['name']=='Lightning Bolt' for c in r.get('previewCards',[])),'Explore confirmation omitted revealed card'
     (out/(case+'-choice.json')).write_text(json.dumps(dict(v,request=r,awaitingInput=True)),encoding='utf-8')
    if any(o.get('card') for o in opts):
     snapshot=dict(v,request=r,awaitingInput=True);(out/(case+'-choice.json')).write_text(json.dumps(snapshot),encoding='utf-8')
     if not tests:
      send(m,pick,seat=3-r['seat']);assert q.get(timeout=5)['error']=='wrong-seat'
      send(m,'not-an-option');assert q.get(timeout=5)['error']=='invalid-option';tests=True
    send(m,pick)
   raise AssertionError('Unresolved '+case)
  finally:
   if p.poll() is None:
    p.stdin.write('{"type":"shutdown"}\n');p.stdin.flush()
    try:p.wait(timeout=3)
    except subprocess.TimeoutExpired:p.kill();p.wait()
   (out/(case+'-process.txt')).write_text(f'pid={p.pid} exited={p.returncode}',encoding='utf-8')
if __name__=='__main__':
 results=[]
 try:
  for case in sys.argv[1:] or cases:results.append(run(case));print(results[-1],flush=True)
 finally:(out/'engine-result.txt').write_text('\n'.join(results),encoding='utf-8')
