"""R39 actual copied source versus recipient perpetual journals and delayed departure."""
import hashlib,json,os,queue,subprocess,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'));OUT=ROOT/'.tools/perpetual-moved-1010'
FIX=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['RoyalStateFixture.java','ControlVariantFixture.java','DigitalIdentityFixture.java','DigitalMemoryFixture.java','PerpetualRemovalFixture.java','PerpetualFieldFixture.java','PerpetualRetentionFixture.java','PerpetualMovedFixture.java']]
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
   positions=[dict(Battlefield=lands,Hand=['Boareskyr Tollkeeper','Brittle Blast','Pull from Eternity','Shock'],Library=['Swamp','Forest','Plains']),dict(Battlefield=['Grizzly Bears']+lands,Hand=['Elvish Visionary','Unsummon','Regrowth','Plains','Lightning Bolt'],Library=['Mountain','Forest','Plains','Island'])]
   cfg=dict(session='perpetual-moved',seed=4,fixtureSeed=11,startingLife=40,firstPlayerSeat=1,decks=[DECK,DECK],positions=positions,auditDir=str(audit));wr(f/'start.json',cfg)
   p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.PerpetualMovedFixture',str(RT/CFG['assets']),str(f/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');q=queue.Queue()
   def reader():
    try:
     for line in p.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start()
   def obs(r):
    end=time.monotonic()+4
    while time.monotonic()<end:
     try:return rd(audit/(str(r['requestId'])+'.json')),rd(audit/(str(r['requestId'])+'-moved.json'))
     except (FileNotFoundError,json.JSONDecodeError,PermissionError):time.sleep(.01)
    raise AssertionError('native read-only fields missing')
   def save(m,o,t,n):wr(f/(n+'-wire.json'),m);wr(f/(n+'-native.json'),o);wr(f/(n+'-moved.json'),t)
   stage=0;seen=set();paid=[];snapshots={};originals={};journals={};enters=0;beforeEntry={};deadline=time.monotonic()+180
   def native(o,name,zone='Hand',seat=1):return next(c for c in o['players'][seat-1]['zones'][zone] if c['name']==name)
   def field(t,cid):return next(c for c in t['cards'] if c['id']==cid)
   def record(m,o,t,label):save(m,o,t,label);snapshots[label]=r['requestId']
   def cast(o,name,seat=1):
    cid=native(o,name,seat=seat)['id'];
    if name=='Elvish Visionary':beforeEntry.update(library=[c['id'] for c in o['players'][1]['zones']['Library']],hand=len(o['players'][1]['zones']['Hand']))
    paid.append(dict(id=cid,name=name,seat=seat,turn=v['turn'],operation='cast'));return 'card:'+str(cid)
   def visionary(t):return field(t,originals['Elvish Visionary'])
   def exactJournal(c,n):
    ck(len(c['journal'])==n and len(c['replacements'])==n,'exact permanent journal/replacement count')
    if n in journals:ck(c['journal']==journals[n],'journal timestamps unchanged through actual zone change')
    else:journals[n]=c['journal']
   def enter(o,t):
    nonlocal enters
    enters+=1;c=visionary(t);ck(c['zone']=='Battlefield' and c['tapped'] and (c['power'],c['toughness'])==(1,1),'native replacement enters tapped with original1/1');exactJournal(c,1 if enters<3 else 2)
    hand=o['players'][1]['zones']['Hand'];ck(len(hand)==beforeEntry['hand'] and any(x['id']==beforeEntry['library'][0] for x in hand),'one exact ETB card drawn, casting removes one from hand');ck([x['id'] for x in o['players'][1]['zones']['Library']]==beforeEntry['library'][1:],'one exact library-top removed per ETB, no extra trigger')
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
     if stage==0 and r['seat']==1 and v['turn']==1:
      originals={n:native(o,n,z,seat)['id'] for n,z,seat in [('Boareskyr Tollkeeper','Hand',1),('Elvish Visionary','Hand',2),('Grizzly Bears','Battlefield',2)]};record(m,o,t,'initial');pick=cast(o,'Boareskyr Tollkeeper');stage=1
     elif stage==1 and r['seat']==1:
      c=visionary(t);ck(c['zone']=='Hand','affected original still Hand');exactJournal(c,1);ck(field(t,originals['Grizzly Bears'])['journal']==[],'unselected permanent unaffected');record(m,o,t,'granted-hand');stage=2
     elif stage==2 and r['seat']==2 and v['turn']==2:pick=cast(o,'Elvish Visionary',2);stage=3
     elif stage==3 and r['seat']==2:
      enter(o,t);record(m,o,t,'entered-one');pick=cast(o,'Unsummon',2);stage=4
     elif stage==4 and r['seat']==2:
      c=visionary(t);ck(c['zone']=='Hand' and not c['tapped'],'native return hand resets tapped state');exactJournal(c,1);record(m,o,t,'returned-hand');pick=cast(o,'Elvish Visionary',2);stage=5
     elif stage==5 and r['seat']==2:enter(o,t);record(m,o,t,'entered-two');stage=6
     elif stage==6 and r['seat']==1:pick=cast(o,'Brittle Blast');stage=7
     elif stage==7 and r['seat']==1:
      c=visionary(t);ck(c['zone']=='Exile','actual first death redirected Exile');exactJournal(c,2);g=field(t,originals['Grizzly Bears']);ck(g['zone']=='Battlefield' and not g['tapped'] and len(g['journal'])==1 and (g['power'],g['toughness'])==(2,2),'Brittle grants other opponent without damaging it');ck(not field(t,originals['Boareskyr Tollkeeper'])['journal'],'own creature excluded');record(m,o,t,'exiled-one');pick=cast(o,'Pull from Eternity');stage=8
     elif stage==8:
      c=visionary(t);ck(c['zone']=='Graveyard','actual legal exile-to-grave card');exactJournal(c,2);record(m,o,t,'graveyard');stage=9
      if r['seat']==2:pick=cast(o,'Regrowth',2);stage=10
     elif stage==9 and r['seat']==2:pick=cast(o,'Regrowth',2);stage=10
     elif stage==10 and r['seat']==2:
      c=visionary(t);ck(c['zone']=='Hand','actual legal grave-to-hand');exactJournal(c,2);record(m,o,t,'restored-hand');pick=cast(o,'Elvish Visionary',2);stage=11
     elif stage==11 and r['seat']==2:enter(o,t);record(m,o,t,'entered-three');stage=12
     elif stage==12 and r['seat']==1:pick=cast(o,'Shock');stage=13
     elif stage==13 and r['seat']==2:
      c=visionary(t);ck(c['zone']=='Exile','second native death redirected by retained replacement');exactJournal(c,2);ck(enters==3,'exact three completed ETB triggers');record(m,o,t,'resolved');result.update(status='PASS_BOUNDED',nativeFlows=1,actualPaidOperations=len(paid),actualPaidCasts=len(paid),checks=checks,requests=len(seen),paid=paid,originals=originals,snapshots=snapshots,scope='Actual Moved Updated tapped ETB and BF death redirected Exile; exact three ETB draws, opponent permanent grants, original physical recipient Hand/BF/Exile/Grave/Hand/BF/Exile lifetimes with same records; actual engine LKI queries.');break
    elif kind=='InputSelectTargets':
     record(m,o,t,'target-'+str(step));wanted='player:2' if stage==1 else None
     pick='ok' if r.get('selectedCount') else wanted if wanted in ids else next(x['id'] for x in opts if x.get('card',{}).get('id')==originals['Elvish Visionary'])
    elif 'SelectCards' in kind or 'SelectEntities' in kind or kind=='SelectCards':
     record(m,o,t,'choice-'+str(step));op=next((x for x in opts if x.get('card',{}).get('id')==originals['Elvish Visionary'] and not x.get('selected')),None);pick=op['id'] if op else 'confirm-cards' if 'confirm-cards' in ids else 'ok'
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
