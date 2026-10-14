"""R39 actual copied source versus recipient perpetual journals and delayed departure."""
import hashlib,json,os,queue,subprocess,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'));OUT=ROOT/'.tools/perpetual-copy-1010'
FIX=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['RoyalStateFixture.java','ControlVariantFixture.java','DigitalIdentityFixture.java','DigitalMemoryFixture.java','PerpetualRemovalFixture.java','PerpetualFieldFixture.java','PerpetualRetentionFixture.java','PerpetualCopyFixture.java']]
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
   lands=['Plains','Island','Swamp','Mountain','Forest']*12
   positions=[dict(Battlefield=['Second Little Pig','Pearl Collector','Kiki-Jiki, Mirror Breaker']+lands,Hand=['Kami of Transmutation','Clone','Quasiduplicate','Quasiduplicate','Giant Growth','Unsummon','Unsummon'],Library=['Swamp','Forest','Plains']),dict(Battlefield=[],Hand=[],Library=['Mountain','Forest','Plains'])]
   cfg=dict(session='perpetual-copy',seed=4,fixtureSeed=11,startingLife=40,firstPlayerSeat=1,decks=[DECK,DECK],positions=positions,auditDir=str(audit));wr(f/'start.json',cfg)
   p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.PerpetualCopyFixture',str(RT/CFG['assets']),str(f/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');q=queue.Queue()
   def reader():
    try:
     for line in p.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start()
   def obs(r):
    end=time.monotonic()+4
    while time.monotonic()<end:
     try:return rd(audit/(str(r['requestId'])+'.json')),rd(audit/(str(r['requestId'])+'-copies.json'))
     except (FileNotFoundError,json.JSONDecodeError,PermissionError):time.sleep(.01)
    raise AssertionError('native read-only fields missing')
   def save(m,o,t,n):wr(f/(n+'-wire.json'),m);wr(f/(n+'-native.json'),o);wr(f/(n+'-copies.json'),t)
   stage=0;seen=set();paid=[];snapshots={};originals={};copies={};deadline=time.monotonic()+180
   def native(o,name,zone='Hand'):return next(c for c in o['players'][0]['zones'][zone] if c['name']==name)
   def field(t,cid):return next(c for c in t if c['id']==cid)
   def record(m,o,t,label):save(m,o,t,label);snapshots[label]=r['requestId']
   def cast(o,name):
    cid=native(o,name)['id'];paid.append(dict(id=cid,name=name,seat=1,turn=v['turn'],operation='cast'));return 'card:'+str(cid)
   def activate(name,mana=False):
    cid=originals[name]
    if mana:paid.append(dict(id=cid,name=name,seat=1,turn=v['turn'],operation='activate'))
    return 'card:'+str(cid)
   def original(t,name):return field(t,originals[name])
   def actualWire(m,id):return next(c for p in m['views'][0]['players'] for z in ['hand','battlefield','graveyardCards'] for c in p[z] if c['id']==id)
   for step in range(450):
    ck(time.monotonic()<deadline,'deadline');m=q.get(timeout=35)
    if isinstance(m,Exception):raise m
    ck(m is not None,'process exited');trace.write(json.dumps(m)+'\n');trace.flush();ck(m['type'] not in ['fatal','ended','rejected'],m)
    if m['type']!='request':continue
    r=m['request'];kind=r['kind'];opts=r['options'];ids=[x['id'] for x in opts];pick='ok'
    if r['requestId'] in seen:continue
    seen.add(r['requestId']);o,t=(None,None) if 'Mulligan' in kind else obs(r);v=m['views'][0]
    if 'Mulligan' in kind:pick='keep' if 'keep' in ids else 'ok'
    elif kind=='InputPassPriority' and not v['stackItems'] and v['phase']=='MAIN1':
     if r['seat']==1 and v['turn']==1:
      if stage==0:
       originals={n:native(o,n,'Battlefield' if n in ['Second Little Pig','Pearl Collector','Kiki-Jiki, Mirror Breaker'] else 'Hand')['id'] for n in ['Second Little Pig','Pearl Collector','Kiki-Jiki, Mirror Breaker','Clone']};record(m,o,t,'initial');pick=activate('Second Little Pig',True);stage=1
      elif stage==1:
       c=original(t,'Second Little Pig');ck(c['power']==4 and c['toughness']==4 and len(c['journal'])==3 and c['copiablePower']==2 and c['copiableToughness']==3,'actual modified source vs printed copiable baseline');record(m,o,t,'source-perpetual');pick=cast(o,'Kami of Transmutation');stage=2
      elif stage==2:
       c=original(t,'Clone');ck(c['zone']=='Hand' and 'Artifact' in c['type'] and len(c['journal'])==1,'separate recipient Artifact granted before actual Clone');record(m,o,t,'recipient-hand');pick=cast(o,'Giant Growth');stage=3
      elif stage==3:
       c=original(t,'Second Little Pig');ck((c['power'],c['toughness'])==(7,7) and len(c['journal'])==3,'actual ordinary external +3/+3 separate from permanent4/4');record(m,o,t,'source-external');pick=cast(o,'Quasiduplicate');stage=4
      elif stage==4:
       token=next(c for c in t if c['token'] and c['name']=='Second Little Pig');copies['plain']=token['id'];ck((token['power'],token['toughness'])==(2,3) and not token['journal'] and token['keywords']==[],'actual CopyPermanent gets printed2/3 with no source journal or Flying');record(m,o,t,'plain-copy');pick=cast(o,'Clone');stage=5
      elif stage==5:
       c=original(t,'Clone');ck(c['name']=='Second Little Pig' and (c['power'],c['toughness'])==(2,3) and len(c['journal'])==1 and 'Artifact' in c['type'] and not c['keywords'],'actual native Clone uses copied baseline and own independent Artifact');record(m,o,t,'clone-first');pick=activate('Pearl Collector',True);stage=6
      elif stage==6:
       c=original(t,'Clone');ck('Lifelink' in c['keywords'] and len(c['journal'])==2,'recipient second independent perpetual Lifelink grant');record(m,o,t,'clone-own-grant');pick=activate('Kiki-Jiki, Mirror Breaker');stage=7
      elif stage==7:
       token=next(c for c in t if c['token'] and c['name']=='Second Little Pig' and c['id'] not in copies.values());copies['temporary']=token['id'];ck((token['power'],token['toughness'])==(2,3) and token['keywords']==['Haste'] and not token['journal'],'Kiki token printed baseline with copiable Haste exception only');ck(len(actualWire(m,token['id']).get('pendingDepartures',[]))==1,'actual single delayed sacrifice bound to original Kiki token');record(m,o,t,'temporary-copy');pick=cast(o,'Quasiduplicate');stage=8
      elif stage==8:
       token=next(c for c in t if c['token'] and c['name']=='Second Little Pig' and c['id'] not in copies.values());copies['of-temporary']=token['id'];ck((token['power'],token['toughness'])==(2,3) and token['keywords']==['Haste'] and not token['journal'],'second real copy inherits Haste copy exception, no perpetual journal');ck(not actualWire(m,token['id']).get('pendingDepartures',[]),'copy does not inherit original delayed sacrifice');record(m,o,t,'copy-temporary');pick=cast(o,'Unsummon');stage=9
      elif stage==9:
       c=original(t,'Clone');ck(c['name']=='Clone' and c['zone']=='Hand' and (c['power'],c['toughness'])==(0,0) and len(c['journal'])==2 and 'Artifact' in c['type'] and set(c['keywords'])=={'ETBReplacement:Copy:DBCopy:Optional','Lifelink'},'recipient real return hand restores physical Clone but own records remain');record(m,o,t,'clone-hand');pick=cast(o,'Clone');stage=10
      elif stage==10:
       c=original(t,'Clone');ck(c['name']=='Second Little Pig' and (c['power'],c['toughness'])==(2,3) and len(c['journal'])==2 and 'Artifact' in c['type'] and set(c['keywords'])=={'Haste','Lifelink'},'recipient re-copy of living second token: intrinsic Haste and only own Lifelink/Artifact once');ck(not actualWire(m,c['id']).get('pendingDepartures',[]),'recipient does not acquire unrelated sacrifice');record(m,o,t,'clone-recast');pick=cast(o,'Unsummon');stage=11
      elif stage==11:
       c=original(t,'Second Little Pig');ck(c['zone']=='Hand' and (c['power'],c['toughness'])==(4,4) and len(c['journal'])==3,'source leaves keeps perpetual and drops ordinary boost');record(m,o,t,'source-hand');pick=cast(o,'Second Little Pig');stage=12
      elif stage==12:
       c=original(t,'Second Little Pig');ck(c['zone']=='Battlefield' and (c['power'],c['toughness'])==(4,4) and len(c['journal'])==3 and c['keywords']==['Flying'],'source original reenter keeps own field journal only');record(m,o,t,'source-recast');stage=13
     elif r['seat']==2 and v['turn']==2 and stage==13:
      ck(not any(c['id']==copies['temporary'] for c in t),'original Kiki token sacrificed and vanished at native end step');ck(all(field(t,id)['zone']=='Battlefield' and field(t,id)['power']==2 and field(t,id)['toughness']==3 and not field(t,id)['journal'] for id in [copies['plain'],copies['of-temporary']]),'independent permanent token copies survive original delayed departure');ck(len(original(t,'Clone')['journal'])==2 and original(t,'Clone')['zone']=='Battlefield','modified recipient survives with own records');record(m,o,t,'resolved');result.update(status='PASS_BOUNDED',nativeFlows=1,actualPaidOperations=len(paid),actualPaidCasts=sum(x['operation']=='cast' for x in paid),checks=checks,requests=len(seen),paid=paid,originals=originals,copies=copies,snapshots=snapshots,scope='Actual CopyPermanent/Clone source perpetual fields excluded from copied CardState, independent recipient journal retained/recast, copiable Kiki Haste exception versus independent delayed sacrifice, source rezone/recast and natural expiry outcomes.');break
    elif kind=='InputSelectTargets':
     record(m,o,t,'target-'+str(step));wanted=originals['Clone'] if stage in [6,9] else copies['temporary'] if stage==8 else originals['Second Little Pig'];pick='ok' if r.get('selectedCount') else next(x['id'] for x in opts if x.get('card',{}).get('id')==wanted)
    elif kind=='ChooseModes':
     record(m,o,t,'mode-'+str(step));pick='confirm-modes' if any(x.get('selected') for x in opts) else next(x['id'] for x in opts if x['id'].startswith('mode:') and 'artifact' in x['label'].lower())
    elif 'SelectCards' in kind or 'SelectEntities' in kind or kind=='SelectCards':
     record(m,o,t,'choice-'+str(step));wanted=copies['of-temporary'] if stage==10 else originals['Second Little Pig'];op=next((x for x in opts if x.get('card',{}).get('id')==wanted and not x.get('selected')),None);pick=op['id'] if op else 'confirm-cards' if 'confirm-cards' in ids else 'ok'
    elif kind=='dialog':record(m,o,t,'dialog-'+str(step));pick=ids[0]
    elif kind=='InputConfirm':record(m,o,t,'confirm-'+str(step));pick='ok'
    elif 'PayMana' in kind:record(m,o,t,'payment-'+str(step));pick='ok'
    elif kind=='OrderAbilities':pick='confirm-order' if 'confirm-order' in ids else next(i for i in ids if i.startswith('order:'))
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
