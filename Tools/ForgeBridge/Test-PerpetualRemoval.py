"""R39 actual self-removal, unrelated permanent power, zone and recast deltas."""
import hashlib,json,os,queue,subprocess,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'));OUT=ROOT/'.tools/perpetual-removal-1010'
FIX=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['RoyalStateFixture.java','ControlVariantFixture.java','DigitalIdentityFixture.java','DigitalMemoryFixture.java','PerpetualRemovalFixture.java']]
DECK=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
def rd(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def wr(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def run():
 f=OUT/'native'/str(time.time_ns());f.mkdir(parents=True);audit=f/'audit';audit.mkdir();classes=f/'classes';classes.mkdir();checks=0;p=None
 for x in FIX+[Path(__file__)]: (f/x.name).write_bytes(x.read_bytes())
 result=dict(folder=f.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],driver=sha(Path(__file__)),fixtures={x.name:sha(x) for x in FIX},producerClassesLib={x.relative_to(RT).as_posix():sha(x) for d in ['classes','lib'] for x in (RT/d).rglob('*') if x.is_file()},status='RUNNING');wr(f/'result.json',result)
 def ck(ok,msg):
  nonlocal checks
  assert ok,msg;checks+=1
 try:
  with (f/'compile.log').open('w') as log:subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*[str(f/x.name) for x in FIX]],check=True,stdout=log,stderr=subprocess.STDOUT)
  with tempfile.TemporaryDirectory(prefix='mtgb-perpetual-') as temp,(f/'trace.jsonl').open('w',encoding='utf8') as trace,(f/'stderr.log').open('w') as log:
   positions=[dict(Hand=['Naktamun Shines Again','Racketeer Boss','Grizzly Bears','Runeclaw Bear','Unsummon','Unsummon','Pass the Torch','Shivan Dragon'],Library=['Swamp','Swamp','Swamp','Swamp']),dict(Library=['Mountain','Mountain','Mountain','Mountain'])]
   cfg=dict(session='perpetual-removal',seed=4,fixtureSeed=11,startingLife=40,firstPlayerSeat=1,decks=[DECK,DECK],positions=positions,auditDir=str(audit));wr(f/'start.json',cfg)
   p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.PerpetualRemovalFixture',str(RT/CFG['assets']),str(f/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');q=queue.Queue()
   def reader():
    try:
     for line in p.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start()
   def obs(r):
    end=time.monotonic()+4
    while time.monotonic()<end:
     try:return rd(audit/(str(r['requestId'])+'.json')),rd(audit/(str(r['requestId'])+'-perpetual.json'))
     except (FileNotFoundError,json.JSONDecodeError,PermissionError):time.sleep(.01)
    raise AssertionError('native read-only observers missing')
   def save(m,o,t,n):wr(f/(n+'-wire.json'),m);wr(f/(n+'-native.json'),o);wr(f/(n+'-perpetual.json'),t)
   stage=0;seen=set();paid=[];free=[];optional=[];initial=None;cardids={};snapshots={};deadline=time.monotonic()+200;attackTurn=None;attacked=False
   def native(o,name,zone=None):
    zones=o['players'][0]['zones'];return next(c for z,cs in zones.items() if zone is None or z==zone for c in cs if c['name']==name)
   def perp(t,name):return next(c for c in t['current'] if c['name']==name)
   def record(m,o,t,label):save(m,o,t,label);snapshots[label]=r['requestId']
   def cast(o,name):
    cid=native(o,name,'Hand')['id'];paid.append(dict(id=cid,name=name,turn=v['turn']));return 'card:'+str(cid)
   def countTreasure(o):return len([c for c in o['players'][0]['zones']['Battlefield'] if c['name']=='Treasure Token'])
   for step in range(650):
    ck(time.monotonic()<deadline,'deadline');m=q.get(timeout=35)
    if isinstance(m,Exception):raise m
    ck(m is not None,'process exited');trace.write(json.dumps(m)+'\n');trace.flush();ck(m['type'] not in ['fatal','ended','rejected'],m)
    if m['type']!='request':continue
    r=m['request'];kind=r['kind'];opts=r['options'];ids=[x['id'] for x in opts];pick='ok'
    if r['requestId'] in seen:continue
    seen.add(r['requestId']);o,t=(None,None) if 'Mulligan' in kind else obs(r);v=m['views'][0]
    if o is not None:ck(not m['views'][0]['players'][1]['hand'] and not m['views'][1]['players'][0]['hand'],'opposite private hands hidden')
    if 'Mulligan' in kind:pick='keep' if 'keep' in ids else 'ok'
    elif kind=='InputPassPriority' and r['seat']==1 and not v['stackItems']:
     if v['phase']=='MAIN1':
      if stage==0:
       initial=o;record(m,o,t,'initial');cardids={c['name']:c['id'] for c in o['players'][0]['zones']['Hand'] if c['name']!='Unsummon'};pick=cast(o,'Naktamun Shines Again');stage=1
      elif stage==1:
       ck(perp(t,'Grizzly Bears')['power']==perp(t,'Runeclaw Bear')['power']==3,'actual Naktamun +1 power originals');record(m,o,t,'power-granted');pick=cast(o,'Racketeer Boss');stage=2
      elif stage==2:
       for name in ['Grizzly Bears','Runeclaw Bear']:ck(len(perp(t,name)['journal'])==2 and len(perp(t,name)['traits'])==1,'two distinct actual journals after Boss '+name)
       record(m,o,t,'boss-granted');pick=cast(o,'Pass the Torch');stage=3
      elif stage==3:
       ck(len(perp(t,'Runeclaw Bear')['journal'])==3 and len(perp(t,'Runeclaw Bear')['traits'])==2,'Torch separate trigger added beside Boss and unrelated +1');record(m,o,t,'torch-granted');pick=cast(o,'Grizzly Bears');stage=4
      elif stage==4:
       ck(countTreasure(o)==1,'first Bear actual cast yields one Treasure');ck(len(perp(t,'Grizzly Bears')['journal'])==1 and perp(t,'Grizzly Bears')['power']==3 and not perp(t,'Grizzly Bears')['traits'],'Boss timestamp only removed; unrelated +1 remains');record(m,o,t,'bear-first-cast');pick=cast(o,'Unsummon');stage=5
      elif stage==5:
       ck(native(o,'Grizzly Bears','Hand')['power']==3 and len(perp(t,'Grizzly Bears')['journal'])==1,'same original ID returns hand with only unrelated permanent power');record(m,o,t,'bear-returned');pick=cast(o,'Grizzly Bears');stage=6
      elif stage==6:
       ck(countTreasure(o)==1 and native(o,'Grizzly Bears','Battlefield')['power']==3,'actual recast retains +1 without second Treasure');record(m,o,t,'bear-recast');pick=cast(o,'Runeclaw Bear');stage=7
      elif stage==7:
       ck(countTreasure(o)==2 and len(perp(t,'Runeclaw Bear')['journal'])==2 and len(perp(t,'Runeclaw Bear')['traits'])==1 and perp(t,'Runeclaw Bear')['power']==3,'Rune cast removes only Boss and preserves Torch and +1');record(m,o,t,'rune-cast');stage=8
      elif stage==9 and v['turn']==5:
       ck(len(perp(t,'Runeclaw Bear')['journal'])==2,'declining Torch retained exact ability until next own turn');record(m,o,t,'after-decline');stage=10
      elif stage==12 and v['turn']==7:
       ck(native(o,'Runeclaw Bear','Hand')['power']==3 and len(perp(t,'Runeclaw Bear')['journal'])==1,'Torch removal persists return hand and natural turn');record(m,o,t,'rune-returned');pick=cast(o,'Runeclaw Bear');stage=13
      elif stage==13:
       ck(countTreasure(o)==2 and native(o,'Runeclaw Bear','Battlefield')['power']==3 and len(perp(t,'Runeclaw Bear')['journal'])==1 and not perp(t,'Runeclaw Bear')['traits'],'Rune real recast no Boss or Torch trigger returns');record(m,o,t,'rune-recast');stage=14
     elif v['phase']=='MAIN2':
      if stage==8 and v['turn']==3:
       ck(optional==['decline'] and len(perp(t,'Runeclaw Bear')['journal'])==2 and len(perp(t,'Runeclaw Bear')['traits'])==1,'actual optional Play declined, original Torch journal survives');record(m,o,t,'decline-resolved');stage=9
      elif stage==10 and v['turn']==5:
       ck(optional==['decline','accept'] and len(free)==1,'second natural combat actual free Play occurs');ck(len(perp(t,'Runeclaw Bear')['journal'])==1 and not perp(t,'Runeclaw Bear')['traits'] and perp(t,'Runeclaw Bear')['power']==3,'only originating Torch row removed after actual free cast');ck(perp(t,'Runeclaw Bear')['remembered']==[],'successful played-card binding cleanup');ck(len(perp(t,'Shivan Dragon')['journal'])==1 and len(perp(t,'Shivan Dragon')['traits'])==1,'actual replay grant attached to separate original hand Dragon');record(m,o,t,'accept-resolved');pick=cast(o,'Unsummon');stage=11
      elif stage==11:
       ck(native(o,'Runeclaw Bear','Hand')['power']==3 and len(perp(t,'Runeclaw Bear')['journal'])==1,'actual second Unsummon returns original with unrelated power only');record(m,o,t,'rune-bounced');stage=12
      elif stage==14 and v['turn']==9:
       ck(optional==['decline','accept'] and len(free)==1 and countTreasure(o)==2,'third natural attack no removed ability retrigger');ck(perp(t,'Runeclaw Bear')['power']==3 and len(perp(t,'Runeclaw Bear')['journal'])==1,'no double permanent replay');ck([x['life'] for x in o['players']]==[40,27],'three real 3-power attacks plus two actual 2-damage Torch spells');record(m,o,t,'resolved')
       ck(len(paid)==9,'exact nine actual paid spell operations');ck(sum(v['players'][0]['manaPool'])==sum(v['players'][0]['snowManaPool'])==0,'no unspent native pools');result.update(status='PASS_BOUNDED',checks=checks,requests=len(seen),nativeFlows=1,actualPaidOperations=len(paid),paid=paid,free=free,optional=optional,originalIDs=cardids,snapshots=snapshots,scope='Actual two Boss recipients, exact unconditional and conditional self-removal, unrelated perpetual +1/+0 survives hand/BF/recast and natural turns; Torch decline then actual free replay then no third-attack trigger. Original payload/family/copy/replacement scope remains R39.');break
    elif kind=='InputAttack':
     if attackTurn!=v['turn']:attackTurn=v['turn'];attacked=False
     if stage in [8,10,14] and v['turn'] in [3,5,9] and not attacked:
      op=next(x for x in opts if x['id'].startswith('attack:') and x['attackerId']==cardids['Runeclaw Bear'] and x['defenderKind']=='player');pick=op['id'];attacked=True
     else:pick='ok'
    elif kind=='InputBlock':pick='ok'
    elif kind=='InputSelectTargets':
     record(m,o,t,'target-'+str(step))
     if r.get('selectedCount'):pick='ok'
     elif r['sourceCardId']==cardids['Runeclaw Bear'] and any(x.get('card',{}).get('id')==cardids['Pass the Torch'] for x in opts):pick=next(x['id'] for x in opts if x.get('card',{}).get('id')==cardids['Pass the Torch'])
     elif stage in [3,10] and any(x['id'].startswith('player:') for x in opts):pick=next(x['id'] for x in opts if x['id'].startswith('player:') and x['label']=='Player 2')
     else:pick=next(x['id'] for x in opts if x.get('card',{}).get('id')==cardids['Grizzly Bears' if stage==5 else 'Runeclaw Bear'])
    elif 'SelectCards' in kind or 'SelectEntities' in kind or kind=='SelectCards':
     record(m,o,t,'choose-'+str(step));rows=[x for x in opts if x.get('card',{}).get('id',0)>0];wanted=['Grizzly Bears','Runeclaw Bear'] if stage==2 else ['Runeclaw Bear'] if stage==3 else ['Shivan Dragon'];op=next((x for x in rows if x['card']['name'] in wanted and not x.get('selected')),None)
     if op:pick=op['id']
     else:ck(r.get('selectedCount')==len(wanted),'exact native chosen hand count');pick='confirm-cards' if 'confirm-cards' in ids else 'ok'
    elif kind in ['InputConfirm','dialog']:
     record(m,o,t,'optional-'+str(step));ck('play Pass the Torch' in r['message'] and any(c['id']==cardids['Pass the Torch'] for c in r.get('previewCards',[])),'only native optional replay confirmation');decision='decline' if v['turn']==3 else 'accept';optional.append(decision);pick=('cancel' if 'cancel' in ids else ids[-1]) if decision=='decline' else ('ok' if 'ok' in ids else ids[0])
     if decision=='accept':free.append(dict(id=cardids['Pass the Torch'],turn=v['turn']))
    elif 'PayMana' in kind:record(m,o,t,'payment-'+str(step));pick='ok'
    elif kind=='OrderAbilities':record(m,o,t,'order-'+str(step));pick='confirm-order' if 'confirm-order' in ids else next(i for i in ids if i.startswith('order:'))
    elif kind=='RevealCards':pick='confirm-cards'
    elif 'ok' not in ids:raise AssertionError(('unhandled',stage,r))
    ck(pick in ids and not next(x for x in opts if x['id']==pick).get('disabled'),('original legal option',stage,r,pick));print(step,stage,v['turn'],kind,r['seat'],pick,flush=True);p.stdin.write(json.dumps(dict(type='respond',session=cfg['session'],seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
   else:raise AssertionError('step limit')
   ck(result['status']=='PASS_BOUNDED','actual final result')
 except Exception:result.update(status='FAIL',checks=checks,error=traceback.format_exc())
 finally:
  if p is not None and p.poll() is None:p.kill();p.wait()
 wr(f/'result.json',result);wr(OUT/'native-result.json',result);print(result['status'],result.get('error',''),flush=True);return result
if __name__=='__main__':raise SystemExit(run()['status']!='PASS_BOUNDED')
