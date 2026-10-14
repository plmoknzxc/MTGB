"""R39 actual cost replacement/incorporation, actual tax and rezone/recast."""
import hashlib,json,os,queue,subprocess,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'));OUT=ROOT/'.tools/perpetual-fields-1010'
FIX=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['RoyalStateFixture.java','ControlVariantFixture.java','DigitalIdentityFixture.java','DigitalMemoryFixture.java','PerpetualRemovalFixture.java','PerpetualFieldFixture.java']]
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
   lands=['Plains','Island','Swamp','Mountain','Forest']*4
   positions=[dict(Battlefield=['Inspiring Easel','Sphere of Resistance']+lands,Hand=['Shock','Mapping the Maze','Thought Partition'],Library=['Swamp','Forest','Plains','Mountain']),dict(Battlefield=lands,Hand=['Shock','Regrowth'],Library=['Mountain','Forest','Plains'])]
   cfg=dict(session='perpetual-fields',seed=4,fixtureSeed=11,startingLife=40,firstPlayerSeat=1,decks=[DECK,DECK],positions=positions,auditDir=str(audit));wr(f/'start.json',cfg)
   p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.PerpetualFieldFixture',str(RT/CFG['assets']),str(f/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');q=queue.Queue()
   def reader():
    try:
     for line in p.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start()
   def obs(r):
    end=time.monotonic()+4
    while time.monotonic()<end:
     try:return rd(audit/(str(r['requestId'])+'.json')),rd(audit/(str(r['requestId'])+'-fields.json'))
     except (FileNotFoundError,json.JSONDecodeError,PermissionError):time.sleep(.01)
    raise AssertionError('native read-only fields missing')
   def save(m,o,t,n):wr(f/(n+'-wire.json'),m);wr(f/(n+'-native.json'),o);wr(f/(n+'-fields.json'),t)
   stage=0;seen=set();paid=[];shots={};snapshots={};initial=None;deadline=time.monotonic()+150
   def native(o,name,seat=1,zone='Hand'):return next(c for c in o['players'][seat-1]['zones'][zone] if c['name']==name)
   def field(t,cid):return next(c for c in t if c['id']==cid)
   def wire(m,cid):return next(c for pv in m['views'] for pl in pv['players'] for zone in ['hand','graveyardCards','battlefield'] for c in pl[zone] if c['id']==cid)
   def record(m,o,t,label):save(m,o,t,label);snapshots[label]=r['requestId']
   def cast(o,name,seat=1):
    cid=native(o,name,seat)['id'];paid.append(dict(id=cid,name=name,seat=seat,turn=v['turn']));return 'card:'+str(cid)
   for step in range(450):
    ck(time.monotonic()<deadline,'deadline');m=q.get(timeout=35)
    if isinstance(m,Exception):raise m
    ck(m is not None,'process exited');trace.write(json.dumps(m)+'\n');trace.flush();ck(m['type'] not in ['fatal','ended','rejected'],m)
    if m['type']!='request':continue
    r=m['request'];kind=r['kind'];opts=r['options'];ids=[x['id'] for x in opts];pick='ok'
    if r['requestId'] in seen:continue
    seen.add(r['requestId']);o,t=(None,None) if 'Mulligan' in kind else obs(r);v=m['views'][0]
    if 'Mulligan' in kind:pick='keep' if 'keep' in ids else 'ok'
    elif kind=='InputPassPriority' and v['phase']=='MAIN1' and not v['stackItems']:
     if r['seat']==1 and v['turn']==1:
      if stage==0:
       initial=o;shots={s:native(o,'Shock',s)['id'] for s in [1,2]};record(m,o,t,'initial');pick=cast(o,'Thought Partition');stage=1
      elif stage==1:
       c=field(t,shots[2]);ck(c['colors']==1 and wire(m,shots[2])['manaCost']=='{5}' and len(c['journal'])==2,'actual opponent replacement cost5 and overwrite-white journals');record(m,o,t,'cost-replaced');pick='card:'+str(native(o,'Inspiring Easel',1,'Battlefield')['id']);stage=2
      elif stage==2:
       c=field(t,shots[1]);ck(c['colors']==10 and wire(m,shots[1])['manaCost'].count('{U}')==1 and wire(m,shots[1])['manaCost'].count('{R}')==2 and len(c['journal'])==2,'actual Easel adds UR cost/color and copy trigger');record(m,o,t,'incorporated-hand');pick=cast(o,'Shock');stage=3
      elif stage==3:
       c=field(t,shots[1]);ck(c['zone']=='Graveyard' and c['colors']==10 and len(c['journal'])==2 and o['players'][1]['life']==36,'original incorporated Shock plus actual copy each damage2 and grave journal retained');record(m,o,t,'incorporated-grave');pick=cast(o,'Mapping the Maze');stage=4
      elif stage==4:
       c=field(t,shots[1]);ck(c['zone']=='Hand' and c['colors']==10 and len(c['journal'])==4 and wire(m,shots[1])['manaCost'].count('{U}')==2 and wire(m,shots[1])['manaCost'].count('{R}')==2,'native Mapping from grave adds U/draw trigger, recognizes multicolor and returns exact original');record(m,o,t,'mapping-returned');pick=cast(o,'Shock');stage=5
      elif stage==5:
       c=field(t,shots[1]);ck(c['zone']=='Graveyard' and len(c['journal'])==4 and o['players'][1]['life']==32,'actual recast copy triggers without double incorporation');ck(len(o['players'][0]['zones']['Hand'])==1,'actual cast draw trigger exactly one original top card');record(m,o,t,'own-recast');stage=6
     elif r['seat']==2 and v['turn']==2:
      if stage==6:pick=cast(o,'Shock',2);stage=7
      elif stage==7:
       c=field(t,shots[2]);ck(c['zone']=='Graveyard' and c['colors']==1 and wire(m,shots[2])['manaCost']=='{5}' and len(c['journal'])==2,'native opposing actual cast retains cost replacement/white after grave');record(m,o,t,'opponent-grave');pick=cast(o,'Regrowth',2);stage=8
      elif stage==8:
       c=field(t,shots[2]);ck(c['zone']=='Hand' and c['colors']==1 and wire(m,shots[2])['manaCost']=='{5}' and len(c['journal'])==2,'actual Regrowth exact original replacement returned hand');record(m,o,t,'opponent-returned');pick=cast(o,'Shock',2);stage=9
      elif stage==9:
       c=field(t,shots[2]);ck(c['zone']=='Graveyard' and c['colors']==1 and wire(m,shots[2])['manaCost']=='{5}' and len(c['journal'])==2,'actual opponent recast does not duplicate replacement records');ck([x['life'] for x in o['players']]==[36,32],'actual both original spell effects/copies exact final life');record(m,o,t,'resolved');ck(len(paid)==7,'seven actual paid spells plus actual Easel tap activation');result.update(status='PASS_BOUNDED',nativeFlows=1,actualPaidOperations=7,checks=checks,requests=len(seen),paid=paid,originalShocks=shots,snapshots=snapshots,scope='One actual opponent overwriteWhite/ManaCost5 plus own Easel UR/copy and Mapping U/draw, real grave-return/recast both players, actual Sphere tax separated from characteristic. No other record/copy-permanent/moved replacement certification.');break
    elif kind=='InputSelectTargets':
     record(m,o,t,'target-'+str(step))
     if r.get('selectedCount'):pick='ok'
     elif stage==8:pick=next(x['id'] for x in opts if x.get('card',{}).get('id')==shots[2])
     else:pick=next(x['id'] for x in opts if x['id'].startswith('player:') and x['label']=='Player '+str(3-r['seat']))
    elif 'SelectCards' in kind or 'SelectEntities' in kind or kind=='SelectCards':
     record(m,o,t,'choose-'+str(step));wanted=shots[2] if stage==1 else shots[1];op=next((x for x in opts if x.get('card',{}).get('id')==wanted and not x.get('selected')),None)
     if op:pick=op['id']
     else:pick='confirm-cards' if 'confirm-cards' in ids else 'ok'
    elif kind=='dialog':
     record(m,o,t,'dialog-'+str(step));pick=next((x['id'] for x in opts if 'choose an instant' in x['label'].lower()),ids[0])
    elif kind=='InputConfirm':record(m,o,t,'confirm-'+str(step));pick='ok'
    elif 'PayMana' in kind:record(m,o,t,'payment-'+str(step));pick='ok'
    elif kind=='OrderAbilities':record(m,o,t,'order-'+str(step));pick='confirm-order' if 'confirm-order' in ids else next(i for i in ids if i.startswith('order:'))
    elif kind=='RevealCards':record(m,o,t,'reveal-'+str(step));pick='confirm-cards'
    elif 'ok' not in ids:raise AssertionError(('unhandled',stage,r))
    ck(pick in ids and not next(x for x in opts if x['id']==pick).get('disabled'),('original legal option',stage,r,pick));print(step,stage,v['turn'],kind,r['seat'],pick,flush=True);p.stdin.write(json.dumps(dict(type='respond',session=cfg['session'],seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
   else:raise AssertionError('step limit')
   ck(result['status']=='PASS_BOUNDED','actual final result')
 except Exception:result.update(status='FAIL',checks=checks,error=traceback.format_exc())
 finally:
  if p is not None and p.poll() is None:p.kill();p.wait()
 wr(f/'result.json',result);wr(OUT/'native-result.json',result);print(result['status'],result.get('error',''),flush=True);return result
if __name__=='__main__':raise SystemExit(run()['status']!='PASS_BOUNDED')
