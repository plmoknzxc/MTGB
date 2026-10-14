"""R38 real conjure final-zone memory and Seek imprint/remembered child identities."""
import hashlib,json,os,queue,subprocess,tempfile,threading,time,traceback,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'));OUT=ROOT/'.tools/digital-identity-1010'
FIX=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['RoyalStateFixture.java','ControlVariantFixture.java','DigitalIdentityFixture.java','DigitalMemoryFixture.java']]
DECK=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
def rd(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def wr(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def z(o,s=1):return o['players'][s-1]['zones']
def run():
 case='memory-imprint-final-zone';f=OUT/'native'/case/str(time.time_ns());f.mkdir(parents=True);audit=f/'audit';audit.mkdir();classes=f/'classes';classes.mkdir();checks=0;p=None
 for x in FIX+[Path(__file__)]: (f/x.name).write_bytes(x.read_bytes())
 result=dict(folder=f.relative_to(ROOT).as_posix(),case=case,runtimeHash=CFG['runtimeHash'],driver=sha(Path(__file__)),fixtures={x.name:sha(x) for x in FIX},producerClassesLib={x.relative_to(RT).as_posix():sha(x) for d in ['classes','lib'] for x in (RT/d).rglob('*') if x.is_file()},status='RUNNING');wr(f/'result.json',result)
 def ck(ok,msg):
  nonlocal checks
  assert ok,msg;checks+=1
 try:
  with (f/'compile.log').open('w') as log:subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*[str(f/x.name) for x in FIX]],check=True,stdout=log,stderr=subprocess.STDOUT)
  with tempfile.TemporaryDirectory(prefix='mtgb-digital-recipient-') as temp,(f/'trace.jsonl').open('w',encoding='utf8') as trace,(f/'stderr.log').open('w') as log:
   positions=[dict(Battlefield=['Prototype X-8','Puppet Raiser','Val, Marooned Surveyor'],Hand=['Naktamun Shines Again','Grizzly Bears'],Library=['Swamp','Swamp','Swamp','Centaur Courser','Runeclaw Bear']),dict(Battlefield=['Containment Priest'],Hand=['Negate'],Library=['Shock','Mountain','Forest'])]
   cfg=dict(session=case,seed=4,fixtureSeed=11,startingLife=30,firstPlayerSeat=1,decks=[DECK,DECK],positions=positions,auditDir=str(audit));wr(f/'start.json',cfg)
   p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.DigitalMemoryFixture',str(RT/CFG['assets']),str(f/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');q=queue.Queue()
   def reader():
    try:
     for line in p.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start()
   def obs(r):
    end=time.monotonic()+4
    while time.monotonic()<end:
     try:return rd(audit/(str(r['requestId'])+'.json')),rd(audit/(str(r['requestId'])+'-identity.json'))
     except (FileNotFoundError,json.JSONDecodeError,PermissionError):time.sleep(.01)
    raise AssertionError('native read-only observers missing')
   def save(m,o,t,n):wr(f/(n+'-wire.json'),m);wr(f/(n+'-native.json'),o);wr(f/(n+'-identity.json'),t)
   def memory(r):
    end=time.monotonic()+4
    while time.monotonic()<end:
     try:return rd(audit/(str(r['requestId'])+'-memory.json'))
     except (FileNotFoundError,json.JSONDecodeError,PermissionError):time.sleep(.01)
    raise AssertionError('memory observer missing')
   initial=None;stage=0;casts=[];seen=set();deadline=time.monotonic()+150;bear=None;clone=None;courser=None;rune=None;seekSeen=[]
   for step in range(500):
    ck(time.monotonic()<deadline,'deadline');m=q.get(timeout=35)
    if isinstance(m,Exception):raise m
    ck(m is not None,'process exited');trace.write(json.dumps(m)+'\n');trace.flush();ck(m['type'] not in ['fatal','ended','rejected'],m)
    if m['type']!='request':continue
    r=m['request'];kind=r['kind'];opts=r['options'];ids=[x['id'] for x in opts];pick='ok'
    if r['requestId'] in seen:continue
    seen.add(r['requestId']);o,t=(None,None) if 'Mulligan' in kind else obs(r);v=m['views'][0]
    if o is not None:
     mm=memory(r)
     ck(not m['views'][0]['players'][1]['hand'] and not m['views'][1]['players'][0]['hand'],'unseen private hand withheld in opposite views')
     for stack in mm['stack']:
      if stack['source']=='Val, Marooned Surveyor' and stack['triggerCards'] not in seekSeen:seekSeen.append(stack['triggerCards'])
    if 'Mulligan' in kind:pick='keep' if 'keep' in ids else 'ok'
    elif kind=='InputPassPriority' and r['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
     if initial is None:
      initial=o;save(m,o,t,'initial');wr(f/'initial-memory.json',mm)
      bear=next(c for c in z(o)['Hand'] if c['name']=='Grizzly Bears')['id'];courser=next(c for c in z(o)['Library'] if c['name']=='Centaur Courser')['id'];rune=next(c for c in z(o)['Library'] if c['name']=='Runeclaw Bear')['id']
     if stage==0:
      c=next(c for c in z(o)['Hand'] if c['name']=='Naktamun Shines Again');casts.append(c['id']);pick='card:'+str(c['id']);stage=1
     elif stage==1:
      save(m,o,t,'after-chapter-one');wr(f/'after-chapter-one-memory.json',mm)
      for cid,zone in [(bear,'Hand'),(rune,'Library')]:ck(next(c for c in z(o)[zone] if c['id']==cid)['power']==3,'actual chapter I perpetual accurate original '+zone)
      ck(next(c for c in z(o)['Library'] if c['id']==courser)['power']==3,'CMC3 excluded from original +1')
      casts.append(bear);pick='card:'+str(bear);stage=2
     elif stage==2:
      save(m,o,t,'after-conjure');wr(f/'after-conjure-memory.json',mm)
      made=[c for c in mm['cards'] if c['name']=='Grizzly Bears' and c['id']!=bear];ck(len(made)==1,'exact one actual new printing duplicate');c=made[0];clone=c['id']
      ck(c['zone']=='Exile' and c['owner']==1 and not c['token'],'actual priest replacement final returned created object is owned non-token Exile')
      ck('Artifact' in c['type'] and 'Robot' in c['type'] and c['power']==2 and c['toughness']==2 and c['perpetualCount']==1,'RememberMade child acts on exact final Exile object; new original printing not prior 3/2 source')
      ck(any(c['id']==bear and c['power']==3 for c in z(o)['Graveyard']),'native sacrifice preserves original perpetual source in grave')
      source=next(c for c in mm['cards'] if c['name']=='Prototype X-8');ck(source['remembered']==[] and source['imprinted']==[],'native Prototype exact memory cleanup');stage=3
     elif stage==3 and v['turn']==3:
      save(m,o,t,'resolved');wr(f/'resolved-memory.json',mm)
      final=z(o);exile={c['id'] for c in final['Exile']};ck(exile=={bear,clone,rune},'exact original grave target/new duplicate/original sought card Exile IDs')
      sought=next(c for c in mm['cards'] if c['id']==courser);ck(sought['zone']=='Hand' and sought['owner']==1 and 'Menace' in sought['keywords'] and sought['perpetualCount']==1,'ImprintFound accurate original hand object actual perpetual Menace child')
      ns=next(c for c in mm['cards'] if c['name']=='Naktamun Shines Again');ck(ns['remembered']==[rune],'RememberFound exact original Runeclaw retained after subsequent Priest replacement')
      pr=next(c for c in mm['cards'] if c['name']=='Puppet Raiser');ck(pr['remembered']==[] and pr['imprinted']==[],'actual Puppet clears both exact original remembered and imprinted bindings')
      ck(next(c for c in final['Exile'] if c['id']==rune)['power']==3,'perpetual +1 retained across original library/hand/replaced exile')
      ck([p['life'] for p in o['players']]==[34,26],'two actual SeekAll Val triggers each exactly two life/damage')
      ck(seekSeen==[[courser],[rune]],'two actual SeekAll carry precise successful hand objects, later BF replacement does not negate Seek success')
      events=[json.loads(line) for line in (audit/'events.jsonl').read_text().splitlines()]
      def event_card(e,name):return next((c for c in e['cards'] if c['name']==name),{})
      ck(any(event_card(e,'Prototype X-8').get('remembered')==[clone] and any(c['id']==clone and 'Robot' in c['type'] for c in e['cards']) for e in events),'actual native stats event captures transient remembered replaced duplicate before cleanup')
      ck(any(event_card(e,'Puppet Raiser').get('remembered')==[bear] and event_card(e,'Puppet Raiser').get('imprinted')==[courser] and any(c['id']==courser and 'Menace' in c['keywords'] for c in e['cards']) for e in events),'actual native stats event captures accurate remembered grave target and Seek imprint before cleanup')
      for cid in [courser,rune]:ck(any(e['event']=='move' and e['affected']['id']==cid and e['affected']['from']=='Library' and e['affected']['to']=='Hand' for e in events),'original sought exact library-hand event '+str(cid))
      ck(any(e['event']=='move' and e['affected']['id']==rune and e['affected']['from']=='Hand' and e['affected']['to']=='Exile' for e in events),'separate actual subsequent remembered Hand-to-Exile replacement')
      oldlib=z(initial)['Library'];ck(final['Library']==[c for c in oldlib if c['id'] not in [courser,rune,oldlib[0]['id']]],'remaining original library exact order minus two Seek and own natural draw')
      ck(len(casts)==2 and len(set(casts))==2 and not final['Graveyard'],'only two actual paid spells, Naktamun stays battlefield and sacrificed Bear exiled by own end target')
      ck(sum(c['tapped'] for c in final['Battlefield'] if c['name'] in ['Plains','Island','Swamp','Mountain','Forest'])==0,'natural own turn untaps all actual five-mana payment lands')
      ck(next(c for c in final['Battlefield'] if c['name']=='Naktamun Shines Again')['counters'].get('LORE')==2,'natural chapter II exact lore counter')
      result.update(status='PASS_BOUNDED',checks=checks,requests=len(seen),spellIDs=casts,originalBear= bear,createdClone=clone,imprintedSeek=courser,rememberedSeek=rune,seekAllCards=seekSeen,scope='Two real paid spells / natural end and next own turn; accurate created final Exile object/RememberMade/Robot and cleanup, accurate grave target/Seek ImprintFound/Menace/cleanup; Seek RememberFound original Hand object and independent subsequent Priest BF replacement, precise SeekAll original IDs and actual two triggers. Direct Seek-hand replacement/conditional mode/factory scope remains separate.');break
    elif kind=='InputSelectTargets':
     pick='ok' if r.get('selectedCount') else next(x['id'] for x in opts if x.get('card',{}).get('id')==bear)
     save(m,o,t,'grave-target-'+str(step));wr(f/('grave-target-'+str(step)+'-memory.json'),mm)
    elif 'PayMana' in kind:save(m,o,t,'payment-'+str(step));pick='ok'
    elif kind=='dialog':save(m,o,t,'dialog-'+str(step));pick=ids[0]
    elif kind=='OrderAbilities':pick='confirm-order' if 'confirm-order' in ids else next(x for x in ids if x.startswith('order:'))
    elif kind=='RevealCards':pick='confirm-cards'
    elif 'ok' not in ids:raise AssertionError(('unhandled',stage,r))
    ck(pick in ids and not next(x for x in opts if x['id']==pick).get('disabled'),('original legal option',r,pick));print(step,kind,r['seat'],pick,flush=True);p.stdin.write(json.dumps(dict(type='respond',session=cfg['session'],seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
   else:raise AssertionError('step limit')
   ck(result['status']=='PASS_BOUNDED','actual final result')
 except Exception:result.update(status='FAIL',checks=checks,error=traceback.format_exc())
 finally:
  if p is not None and p.poll() is None:p.kill();p.wait()
 wr(f/'result.json',result);wr(OUT/'memory-result.json',result);print(result['status'],result.get('error',''),flush=True);return result
if __name__=='__main__':raise SystemExit(run()['status']!='PASS_BOUNDED')
