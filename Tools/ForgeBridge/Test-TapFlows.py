"""Bounded actual producers for tap/untap API result deltas. No effect simulation."""
import json,os,queue,subprocess,tempfile,threading,time,traceback,shutil,sys
from pathlib import Path
R=Path(__file__).resolve().parents[2];B=R/'.tools/tap-flows-1010';B.mkdir(exist_ok=True);RT=R/'ForgeRuntime';cfg=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
C=B/'classes';C.mkdir(exist_ok=True);fix=[R/'Tools/ForgeBridge/tests/TapFlowFixture.java',R/'Tools/ForgeBridge/tests/StateBranchFixture.java']
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(C),*map(str,fix)],check=True)
arg=sys.argv[1]if len(sys.argv)>1 else'choices';binary_tail=arg=='binary-tail'or'--binary-tail'in sys.argv;event_tail=arg=='untap-event';whole_set=arg=='whole-set';case='choices'if binary_tail or whole_set else'upto'if event_tail else arg;F=B/'native'/(case+('-binary-tail'if binary_tail else'-event-tail'if event_tail else''))/str(time.time_ns());F.mkdir(parents=True);audit=F/'audit';audit.mkdir();checks=[];paid=[];seen=set();stage='start';base=None;idx=0;choices=[];targets=[];last=None;cycles=0
def wr(p,z):p.write_text(json.dumps(z,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def ck(v,label):assert v,label;checks.append(label)
def obs(rid):
 end=time.monotonic()+5
 while time.monotonic()<end:
  try:return json.loads((audit/(str(rid)+'.json')).read_text('utf8'))
  except(FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
 raise AssertionError('Read-only snapshot missing')
def nc(n,name,seat=1,zone='Battlefield'):return next(c for c in n['players'][seat-1]['cards']if c['name']==name and c['zone']==zone)
def snap(m,n,label):wr(F/(label+'.json'),m);wr(F/(label+'-native.json'),n)
def states(n,expected):
 for seat,name,tapped in expected:ck(nc(n,name,seat)['tapped']==tapped,'Exact tap state '+str((seat,name,tapped)))
 for view in last['views']:
  for seat,name,tapped in expected:ck(next(c for c in view['players'][seat-1]['battlefield']if c['id']==nc(n,name,seat)['id'])['tapped']==tapped,'Both legal views exact tap '+str((seat,name)))
def target_options(r):return [o for o in r['options']if o.get('card')and not o.get('disabled')]
deck=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
own=dict(Hand=['Turnabout','Turnabout','Hidden Strings'],Battlefield=['Grizzly Bears','Hill Giant','Savannah Lions'],tapped=['Hill Giant']);other=dict(Battlefield=['Grizzly Bears','Hill Giant','Savannah Lions'],tapped=['Grizzly Bears'])
if case=='mass':own=dict(Hand=['Blinding Light',"Angel's Trumpet"],Battlefield=['Grizzly Bears','Hill Giant','Savannah Lions'],tapped=['Hill Giant']);other=dict(Battlefield=['Grizzly Bears','Hill Giant','Savannah Lions'],tapped=['Grizzly Bears'])
if case=='upto':own=dict(Hand=['Cloud of Faeries','Cloud of Faeries'],Battlefield=['Grizzly Bears']);other={}
if event_tail:own=dict(Hand=['Cloud of Faeries'],Battlefield=['Grizzly Bears','Mesmeric Orb'])
if case=='exact':own=dict(Battlefield=['White Plume Adventurer','Grizzly Bears','Hill Giant'],tapped=['Grizzly Bears','Hill Giant']);other={}
if case=='etb':own=dict(Hand=['Azorius Guildgate'],Battlefield=['Gond Gate','Mesmeric Orb']);other={}
if case=='aggregate':own=dict(Hand=['Blinding Light'],Battlefield=['Deeproot Pilgrimage','Merfolk Looter','Silvergill Adept','Coral Trickster'],tapped=['Coral Trickster']);other=dict(Battlefield=['Grizzly Bears'])
for p,name in[(Path(__file__),'driver.py'),(fix[0],'fixture.java'),(fix[1],'state-fixture.java'),(RT/'runtime.json','runtime.json')]:shutil.copy2(p,F/name)
wr(F/'start.json',dict(session='tap-flow',auditDir=str(audit),seed=4,startingLife=40,firstPlayerSeat=1,decks=[deck,deck],own=own,other=other))
with tempfile.TemporaryDirectory(prefix='mtgb-tap-')as tmp,(F/'stderr.log').open('w',encoding='utf8')as log,(F/'trace.jsonl').open('w',encoding='utf8')as trace:
 p=subprocess.Popen([str(RT/cfg['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[C,RT/'classes',RT/'lib/*'])),'mtgb.bridge.TapFlowFixture',str(RT/cfg['assets']),str(F/'start.json')],cwd=RT/cfg['assets'],env=dict(os.environ,APPDATA=tmp,LOCALAPPDATA=tmp,TEMP=tmp,TMP=tmp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');q=queue.Queue()
 def reader():
  try:
   for line in p.stdout:q.put(json.loads(line))
  except Exception as e:q.put(e)
  finally:q.put(None)
 threading.Thread(target=reader,daemon=True).start()
 try:
  for step in range(260):
   m=q.get(timeout=40);ck(isinstance(m,dict),'Native alive');trace.write(json.dumps(m)+'\n');trace.flush();ck(m['type']not in['fatal','ended','rejected'],'No native terminal or rejection')
   if m['type']!='request':continue
   r=m['request'];rid=r['requestId'];kind=r['kind'];en=[o['id']for o in r['options']if not o.get('disabled')]
   if rid in seen:continue
   seen.add(rid);v=m['views'][0];n=obs(rid)if'Mulligan'not in kind else None;pick=None;last=m
   if 'Mulligan'in kind:pick='keep'if'keep'in en else'ok'
   elif kind=='InputPassPriority':
    empty=not v['stackItems']
    if stage=='start'and r['seat']==1 and v['phase']=='MAIN1'and empty:
     base=n;snap(m,n,'before');stage='cast';idx=2 if binary_tail else 1 if event_tail else 0
    if stage=='cast'and empty and r['seat']==1:
     names={'choices':['Turnabout','Turnabout','Hidden Strings'],'mass':['Blinding Light',"Angel's Trumpet"],'upto':['Cloud of Faeries','Cloud of Faeries'],'etb':['Azorius Guildgate'],'exact':[],'aggregate':['Blinding Light']}[case]
     if idx<len(names):
      name=names[idx];src=next(c for c in v['players'][0]['hand']if c['name']==name);pick='card:'+str(src['id']);targets=[];choices=[];stage='resolve';idx+=1
     else:stage='end'if case in['mass','exact']else'done'
    if stage=='resolve'and empty and pick is None:
     snap(m,n,'resolved-'+str(idx));ck(r['seat']in[1,2],'Actual priority restored after effect')
     if case=='choices':
      if idx<=2:
       states(n,[(2,'Grizzly Bears',idx==1),(2,'Hill Giant',idx==1),(2,'Savannah Lions',idx==1),(1,'Grizzly Bears',False),(1,'Hill Giant',True)]);ck(len(choices)==1,'One binary for whole set');ck(n['players'][1]['life']==40,'Other resources unchanged')
      else:states(n,[(1,'Grizzly Bears',True),(1,'Hill Giant',False),(1,'Savannah Lions',False)]);ck(len(choices)==2,'One binary per original target')
     elif case=='mass':
      states(n,[(1,'Grizzly Bears',True),(1,'Hill Giant',True),(1,'Savannah Lions',False),(2,'Grizzly Bears',True),(2,'Hill Giant',True),(2,'Savannah Lions',False)])
     elif case=='upto':
      ck(len([c for c in n['players'][0]['cards']if c['zone']=='Battlefield'and c['name']=='Cloud of Faeries'])==idx-(1 if event_tail else 0),'Actual paid Faerie entered');ck(len(targets)==(0 if idx==1 else 2),'Chosen zero/two distinct native lands')
      for id in targets:ck(next(c for c in n['players'][0]['cards']if c['id']==id)['tapped']==False,'Selected paid land actually untapped')
      if event_tail:ck(n['players'][0]['library']==base['players'][0]['library'][2:],'Normal two Untaps actually mill exact first two library IDs via Orb');ck({c['id']for c in n['players'][0]['cards']if c['zone']=='Graveyard'}==set(base['players'][0]['library'][:2]),'Normal Untaps exact two original cards in graveyard');ck(n['players'][1]['library']==base['players'][1]['library'],'Normal own Untaps do not mill opponent')
     elif case=='etb':ck(nc(n,'Azorius Guildgate')['tapped']==False,'Actual Gate enters untapped through replacement');ck(n['players'][0]['library']==base['players'][0]['library'],'ETB suppressed normal Untaps: no Orb mill');ck(not any(c['name']=='Mesmeric Orb'for c in v['stackItems']),'No normal-untap trigger on ETB')
     elif case=='aggregate':
      states(n,[(1,'Merfolk Looter',True),(1,'Silvergill Adept',True),(1,'Coral Trickster',True),(2,'Grizzly Bears',True)]);old={c['id']for c in base['players'][0]['cards']};added=[c for c in n['players'][0]['cards']if c['zone']=='Battlefield'and c['id']not in old];ck(len(added)==1,'One aggregate TapAll trigger creates one token for two newly tapped Merfolk, not one per card or already tapped card');token=next(c for c in v['players'][0]['battlefield']if c['id']==added[0]['id']);ck(token['power']==token['toughness']==1 and'Merfolk'in token['currentType'],'Actual aggregate token exact1/1 Merfolk');ck(n['players'][0]['library']==base['players'][0]['library'],'Aggregate trigger no private library changes')
     stage='cast';pick='ok'
     limits={'choices':2 if whole_set else 3,'mass':2,'upto':2,'etb':1,'aggregate':1}
     if idx>=limits.get(case,0):stage='end'if case=='mass'else'done'
     elif r['seat']==1 and v['phase']in['MAIN1','MAIN2']:
      names={'choices':['Turnabout','Turnabout','Hidden Strings'],'mass':['Blinding Light',"Angel's Trumpet"],'upto':['Cloud of Faeries','Cloud of Faeries']}[case];src=next(c for c in v['players'][0]['hand']if c['name']==names[idx]);pick='card:'+str(src['id']);idx+=1;targets=[];choices=[];stage='resolve'
    if stage=='end'and empty:
     if case=='mass'and v['phase']=='END_OF_TURN'and n['players'][0]['life']==39:
      states(n,[(1,'Savannah Lions',True),(2,'Savannah Lions',False)]);ck(nc(n,"Angel's Trumpet")['remembered']==[],'Remembered cleared after exact one damage');ck(n['players'][1]['life']==40,'Only active player damaged');snap(m,n,'end-result');stage='done'
     if case=='exact'and v['turnSeat']==2 and v['phase']=='UPKEEP'and cycles:
      states(n,[(1,'Grizzly Bears',False),(1,'Hill Giant',True)]);ck(cycles==1,'Exactly one mandatory chooser');snap(m,n,'exact-result');stage='done'
     pick='ok'
    if stage=='done':
     ck(not v['stackItems'],'Completed native effects leave empty stack');wr(F/'result.json',dict(status='PASS',case=case,checks=len(checks),assertions=checks,paid=paid,runtimeHash=cfg['runtimeHash'],folder=F.relative_to(R).as_posix()));print('PASS',case,F,len(checks),flush=True);break
    if pick is None:pick='ok'
   elif 'PayMana'in kind:paid.append(dict(requestId=rid,cost=r.get('remainingManaCost'),source=idx));snap(m,n,'payment-'+str(len(paid)));pick='ok'
   elif kind=='InputSelectTargets':
    if r.get('remainingCount')==0 and'ok'in en:pick='ok'
    elif case=='choices'and idx<=2:pick='player:'+str(v['players'][1]['targetId'])
    else:
     name='Grizzly Bears'if not targets else'Hill Giant';id=nc(n,name)['id'];pick='card:'+str(id);targets.append(id)
   elif kind=='InputConfirm':
    snap(m,n,'confirm-'+str(idx)+'-'+str(len(choices)))
    if case=='choices'and any('tap'in o['label'].lower()for o in r['options']):pick='ok'if idx==1 or(idx==3 and not choices)else'cancel';choices.append(pick)
    else:pick='cancel'if'cancel'in en else'ok'
   elif kind=='dialog':
    snap(m,n,'dialog-'+str(rid));opts=[o for o in r['options']if not o.get('disabled')];o=next((o for o in opts if o['label']=='Creature'),None)if case=='choices'else next((o for o in opts if o['label'].startswith('Cloud of Faeries - Creature')),None);ck(o is not None,'Actual native type or spell choice');pick=o['id']
   elif kind in['InputSelectCardsFromList','InputSelectCardsFromListN','InputSelectCards']or 'SelectEntities'in kind:
    snap(m,n,'select-'+str(idx)+'-'+str(len(targets)))
    if case=='upto':
     if idx==1:ck(r.get('minCount',0)==0,'Up-to allows zero');pick='ok'if'ok'in en else'cancel'
     elif len(targets)>=2:pick='ok'if'ok'in en else'confirm-cards'
     else:
      o=next(o for o in target_options(r)if o['card']['id']not in targets and o['card']['tapped']);pick=o['id'];targets.append(o['card']['id'])
    elif case=='exact':
     ck(r.get('minCount')==1 and r.get('maxCount')==1,'Exactly singleton native min/max')
     if r.get('selectedCount')==1:pick='ok';snap(m,n,'exact-confirm')
     else:ck('ok'not in en,'Early empty confirmation unavailable');snap(m,n,'exact-choice');o=next(o for o in target_options(r)if o['card']['name']=='Grizzly Bears');pick=o['id'];cycles+=1
    else:raise AssertionError(('Unhandled cards',case,r))
   elif kind=='InputAttack':pick='ok'
   elif kind=='OrderAbilities':
    snap(m,n,'replacement-'+str(rid));pick='confirm-order'if'confirm-order'in en else next(o['id']for o in r['options']if o['id'].startswith('order:')and not o.get('selected')and not o.get('disabled'))
   else:raise AssertionError(('Unhandled',case,stage,r))
   ck(pick in en,('Native enabled option',case,stage,kind,pick,en));z=dict(type='respond',session='tap-flow',seat=r['seat'],requestId=rid,option=pick);trace.write(json.dumps(z)+'\n');trace.flush();p.stdin.write(json.dumps(z)+'\n');p.stdin.flush();print(step,case,stage,kind,pick,flush=True)
  else:raise AssertionError('Step limit')
 except Exception:
  wr(F/'result.json',dict(status='FAIL',case=case,stage=stage,checks=len(checks),paid=paid,error=traceback.format_exc(),runtimeHash=cfg['runtimeHash']));print('FAIL',case,F,traceback.format_exc(),flush=True);raise
 finally:
  if p.poll()is None:p.kill();p.wait()
