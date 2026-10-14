"""R38 conditional registered-commander Seek final-zone replacement accepted/declined."""
import hashlib,json,os,queue,subprocess,tempfile,threading,time,traceback,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'));OUT=ROOT/'.tools/digital-identity-1010'
FIX=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['RoyalStateFixture.java','ControlVariantFixture.java','DigitalIdentityFixture.java','DigitalMemoryFixture.java','DigitalCommanderFixture.java']]
DECK=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
def rd(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def wr(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def z(o,s=1):return o['players'][s-1]['zones']
def run(accept):
 case='seek-commander-'+('accept' if accept else 'decline');f=OUT/'native'/case/str(time.time_ns());f.mkdir(parents=True);audit=f/'audit';audit.mkdir();classes=f/'classes';classes.mkdir();checks=0;p=None
 for x in FIX+[Path(__file__)]: (f/x.name).write_bytes(x.read_bytes())
 result=dict(folder=f.relative_to(ROOT).as_posix(),case=case,runtimeHash=CFG['runtimeHash'],driver=sha(Path(__file__)),fixtures={x.name:sha(x) for x in FIX},producerClassesLib={x.relative_to(RT).as_posix():sha(x) for d in ['classes','lib'] for x in (RT/d).rglob('*') if x.is_file()},status='RUNNING');wr(f/'result.json',result)
 def ck(ok,msg):
  nonlocal checks
  assert ok,msg;checks+=1
 try:
  with (f/'compile.log').open('w') as log:subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*[str(f/x.name) for x in FIX]],check=True,stdout=log,stderr=subprocess.STDOUT)
  with tempfile.TemporaryDirectory(prefix='mtgb-digital-recipient-') as temp,(f/'trace.jsonl').open('w',encoding='utf8') as trace,(f/'stderr.log').open('w') as log:
   positions=[dict(Battlefield=['Val, Marooned Surveyor'],Hand=['Gravitic Herald'],Library=['Swamp','Swamp','Swamp']),dict(Battlefield=[],Hand=['Negate'],Library=['Mountain','Forest'])]
   cfg=dict(session=case,seed=4,fixtureSeed=11,startingLife=30,firstPlayerSeat=1,decks=[DECK,DECK],positions=positions,auditDir=str(audit));wr(f/'start.json',cfg)
   p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.DigitalCommanderFixture',str(RT/CFG['assets']),str(f/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');q=queue.Queue()
   def reader():
    try:
     for line in p.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start()
   def obs(r):
    end=time.monotonic()+4
    while time.monotonic()<end:
     try:return rd(audit/(str(r['requestId'])+'.json')),rd(audit/(str(r['requestId'])+'-memory.json'))['cards']
     except (FileNotFoundError,json.JSONDecodeError,PermissionError):time.sleep(.01)
    raise AssertionError('native read-only observers missing')
   def save(m,o,t,n):wr(f/(n+'-wire.json'),m);wr(f/(n+'-native.json'),o);wr(f/(n+'-identity.json'),t)
   def memory(r):
    end=time.monotonic()+4
    while time.monotonic()<end:
     try:return rd(audit/(str(r['requestId'])+'-memory.json'))
     except (FileNotFoundError,json.JSONDecodeError,PermissionError):time.sleep(.01)
    raise AssertionError('memory observer missing')
   initial=None;seen=set();stage=0;deadline=time.monotonic()+100;command=None;herald=None;choices=[];seekCards=[]
   for step in range(180):
    ck(time.monotonic()<deadline,'deadline');m=q.get(timeout=35)
    if isinstance(m,Exception):raise m
    ck(m is not None,'process exited');trace.write(json.dumps(m)+'\n');trace.flush();ck(m['type'] not in ['fatal','ended','rejected'],m)
    if m['type']!='request':continue
    r=m['request'];kind=r['kind'];opts=r['options'];ids=[x['id'] for x in opts];pick='ok'
    if r['requestId'] in seen:continue
    seen.add(r['requestId']);o,t=(None,None) if 'Mulligan' in kind else obs(r);v=m['views'][0]
    if o is not None:
     mm=memory(r)
     ck(not m['views'][0]['players'][1]['hand'] and not m['views'][1]['players'][0]['hand'],'private exact opposite hand withheld')
     for item in mm['stack']:
      if item['source']=='Val, Marooned Surveyor' and item['triggerCards'] not in seekCards:seekCards.append(item['triggerCards'])
    if 'Mulligan' in kind:pick='keep' if 'keep' in ids else 'ok'
    elif kind=='InputPassPriority' and r['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
     if stage==0:
      initial=o;save(m,o,t,'initial');wr(f/'initial-memory.json',mm);command=next(x['id'] for x in z(o)['Library'] if x['name']=='Isamaru, Hound of Konda');herald=next(x['id'] for x in z(o)['Hand'] if x['name']=='Gravitic Herald');pick='card:'+str(herald);stage=1
     else:
      save(m,o,t,'resolved');wr(f/'resolved-memory.json',mm);cmd=next(x for x in mm['cards'] if x['id']==command);source=next(x for x in mm['cards'] if x['id']==herald)
      ck(cmd['zone']==('Command' if accept else 'Hand') and cmd['owner']==1 and not cmd['token'],'actual returned original object final zone governs Seek success')
      ck(z(o)['Library']==[x for x in z(initial)['Library'] if x['id']!=command],'only actual original commander removed, unrelated original library order retained')
      ck(any(x['id']==herald for x in z(o)['Battlefield']),'actual paid Herald remains original battlefield creature')
      ck(source['remembered']==source['imprinted']==[],'actual native Herald memory cleanup')
      ck(len(choices)==1,'exact optional final destination choice made')
      ck(seekCards==([] if accept else [[command]]),'only Hand result produces exact SeekAll original ID')
      ck([p['life'] for p in o['players']]==([28,30] if accept else [30,28]),'accepted replacement suppresses SeekAll but keeps Herald -2 life; decline gives actual Val +2/-2')
      ck(any(k.startswith('Warp') for k in cmd['keywords'])== (not accept),'RememberFound and actual Warp child only for successful Hand object')
      events=[json.loads(x) for x in (audit/'events.jsonl').read_text().splitlines()]
      ck(any(e['event']=='move' and e['affected']['id']==command and e['affected']['from']=='Library' and e['affected']['to']==('Command' if accept else 'Hand') for e in events),'direct actual Library final destination replacement event')
      if not accept:ck(any(next((x for x in e['cards'] if x['id']==herald),{}).get('remembered')==[command] and any(x['id']==command and any(k.startswith('Warp') for k in x['keywords']) for x in e['cards']) for e in events),'actual transient exact RememberFound Hand object during child before cleanup')
      ck(sum(x['tapped'] for x in z(o)['Battlefield'] if x['name'] in ['Plains','Island','Swamp','Mountain','Forest'])==4,'actual3B four mana')
      ck(not z(o)['Graveyard'] and not z(o)['Exile'] and r['seat']==1 and v['turn']==1,'actual full native result restored original priority, no spurious zones')
      result.update(status='PASS_BOUNDED',checks=checks,requests=len(seen),actualPaidOperations=1,manaPaid=4,originalCommander=command,spellID=herald,choices=choices,seekAllCards=seekCards,scope='Conditional native registered commander opening only, actual3B Herald Seek optional Library→Command accepted/declined, exact returned original object governs RememberFound/Warp/SeekAll, resources/private/restore. Normal production registration has no Commander; no full-mode/client claim.');break
    elif 'PayMana' in kind:save(m,o,t,'payment-'+str(step));pick='ok'
    elif kind in ['dialog','InputConfirm']:
     save(m,o,t,'replacement-'+str(step));wr(f/('replacement-'+str(step)+'-memory.json'),mm);pick=ids[0] if accept else ids[1];choices.append(dict(request=r,pick=pick))
    elif kind=='RevealCards':pick='confirm-cards'
    elif 'ok' not in ids:raise AssertionError(('unhandled',stage,r))
    ck(pick in ids and not next(x for x in opts if x['id']==pick).get('disabled'),('original legal option',r,pick));print(step,kind,r['seat'],pick,flush=True);p.stdin.write(json.dumps(dict(type='respond',session=cfg['session'],seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
   else:raise AssertionError('step limit')
   ck(result['status']=='PASS_BOUNDED','actual final result')
 except Exception:result.update(status='FAIL',checks=checks,error=traceback.format_exc())
 finally:
  if p is not None and p.poll() is None:p.kill();p.wait()
 wr(f/'result.json',result);wr(OUT/('seek-replacement-'+('accept' if accept else 'decline')+'-result.json'),result);print(result['status'],result.get('error',''),flush=True);return result
if __name__=='__main__':raise SystemExit(run('--accept' in sys.argv)['status']!='PASS_BOUNDED')
