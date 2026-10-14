"""Actual Plotter exchange followed by Herald departure: Land.YouCtrl uses original source controller."""
import hashlib,json,os,queue,subprocess,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'));OUT=ROOT/'.tools/r03-payload-1010';FIX=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['RoyalStateFixture.java','ControlVariantFixture.java']];DECK=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
def rd(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def wr(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def field(o):return [c for p in o['players'] for c in p['zones']['Battlefield']]
def run():
 f=OUT/'native/control-land-source'/str(time.time_ns());f.mkdir(parents=True);audit=f/'audit';audit.mkdir();classes=f/'classes';classes.mkdir();checks=0;p=None
 for x in FIX+[Path(__file__)]: (f/x.name).write_bytes(x.read_bytes())
 result=dict(folder=f.relative_to(ROOT).as_posix(),case='control-land-source',runtimeHash=CFG['runtimeHash'],driver=sha(Path(__file__)),fixtures={x.name:sha(x) for x in FIX},producerClassesLib={x.relative_to(RT).as_posix():sha(x) for d in ['classes','lib'] for x in (RT/d).rglob('*') if x.is_file()},status='RUNNING');wr(f/'result.json',result)
 def ck(ok,msg):
  nonlocal checks
  assert ok,msg;checks+=1
 try:
  with (f/'compile.log').open('w') as log:subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*[str(f/x.name) for x in FIX]],check=True,stdout=log,stderr=subprocess.STDOUT)
  with tempfile.TemporaryDirectory(prefix='mtgb-land-source-') as temp,(f/'trace.jsonl').open('w',encoding='utf8') as trace,(f/'stderr.log').open('w') as log:
   positions=[dict(Battlefield=[],Hand=['Vedalken Plotter','Herald of Leshrac','Lightning Bolt','Shock'],Library=['Island','Plains','Forest','Mountain']),dict(Battlefield=['Forest','Grizzly Bears'],Hand=['Negate'],Library=['Island','Plains','Forest','Mountain'])]
   cfg=dict(session='control-land-source',seed=4,fixtureSeed=11,startingLife=30,firstPlayerSeat=1,decks=[DECK,DECK],positions=positions,auditDir=str(audit));wr(f/'start.json',cfg)
   p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.ControlVariantFixture',str(RT/CFG['assets']),str(f/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');q=queue.Queue()
   def reader():
    try:
     for line in p.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start()
   def obs(r):
    end=time.monotonic()+4
    while time.monotonic()<end:
     try:return rd(audit/(str(r['requestId'])+'.json'))
     except (FileNotFoundError,json.JSONDecodeError,PermissionError):time.sleep(.01)
    raise AssertionError('native observer missing')
   def save(m,o,n):wr(f/(n+'-wire.json'),m);wr(f/(n+'-native.json'),o)
   initial=None;exchanged=None;stage=0;cast='';seen=set();castIDs=[];targets=[];deadline=time.monotonic()+150
   for step in range(400):
    ck(time.monotonic()<deadline,'deadline');m=q.get(timeout=35)
    if isinstance(m,Exception):raise m
    ck(m is not None,'process exited');trace.write(json.dumps(m)+'\n');trace.flush();ck(m['type'] not in ['fatal','ended','rejected'],m)
    if m['type']!='request':continue
    r=m['request'];kind=r['kind'];opts=r['options'];ids=[x['id'] for x in opts];pick='ok';v=m['views'][0]
    if r['requestId'] in seen:continue
    seen.add(r['requestId']);o=None if 'Mulligan' in kind else obs(r)
    ck(not m['views'][0]['players'][1]['hand'] and not m['views'][1]['players'][0]['hand'],'private hands concealed')
    if 'Mulligan' in kind:pick='keep' if 'keep' in ids else 'ok'
    elif kind=='InputPassPriority':
     if initial is None and r['seat']==1 and v['phase']=='MAIN1':
      initial=o;save(m,o,'initial');source=next(c['id'] for c in o['players'][0]['zones']['Hand'] if c['name']=='Herald of Leshrac');own=next(c['id'] for c in o['players'][0]['zones']['Battlefield'] if c['name']=='Island');foreign=next(c['id'] for c in o['players'][1]['zones']['Battlefield'] if c['name']=='Forest')
     if initial is not None and r['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
      if stage==0:cast='Vedalken Plotter';stage=1
      elif stage==1:
       ck(targets==[own],'own land selected; sole foreign target is natively auto-selected');byid={c['id']:c for c in field(o)};ck(byid[own]['controller']==2 and byid[foreign]['controller']==1,'actual native land exchange');save(m,o,'exchanged-lands');cast='Herald of Leshrac';stage=2
      elif stage==2:
       ck(next(c for c in field(o) if c['id']==source)['power']==3 and next(c for c in field(o) if c['id']==source)['toughness']==5,'native foreign-controlled land changes Herald stat');exchanged=o;save(m,o,'exchanged');cast='Lightning Bolt';stage=3
      elif stage==3:
       ck(next(c for c in field(o) if c['id']==source)['damage']==3,'actual first source damage3');save(m,o,'after-bolt');cast='Shock';stage=4
      elif stage==4:
       save(m,o,'resolved');byid={c['id']:c for c in field(o)};prior={c['id']:c for c in field(exchanged)};ck(source not in byid,'real source killed before owner return trigger');ck(byid[own]['controller']==2 and byid[own]['owner']==1,'own land outside original source-controller pool remains foreign');ck(byid[foreign]['controller']==byid[foreign]['owner']==2,'only currently own-controlled foreign land returns to owner');ck(set(byid)==set(prior)-{source},'only killed source leaves battlefield')
       for id,c in byid.items():
        ck(c['controller']==(2 if id==foreign else prior[id]['controller']),'exact controlled land scope')
        for key in ['id','name','owner','timestamp','power','toughness','damage','counters']:ck(c[key]==prior[id][key],'unchanged original field '+key)
       ck([pv['life'] for pv in o['players']]==[30,30],'only original source marked/killed, no life changes');ck(r['seat']==r['playerSeat']==1 and v['turn']==1,'original actor restored')
       for s in [1,2]:
        old=initial['players'][s-1]['zones'];now=o['players'][s-1]['zones'];ck(now['Library']==old['Library'] and now['Exile']==old['Exile'],'ordered zones unchanged');ck({c['id'] for c in now['Hand']}=={c['id'] for c in old['Hand']}-set(castIDs),'only actual original paid hand cards leave');ck({c['id'] for c in now['Graveyard']}==({source,*castIDs[2:]} if s==1 else set()),'only killed source/Bolt/Shock grave, Plotter remains');
        for view in m['views']:ck(sum(view['players'][s-1]['manaPool'])==sum(view['players'][s-1]['snowManaPool'])==0,'zero residual pools')
       ck(sum(c['tapped'] for c in field(o))==12,'actual2U/6B/R/R paid12');result.update(status='PASS_BOUNDED',checks=checks,requests=len(seen),sourceID=source,ownLandID=own,foreignLandID=foreign,spellIDs=castIDs,scope='Actual paid Plotter exchange/Herald, native foreign-land stat change, Bolt/Shock kill source and natural return trigger; Land.YouCtrl uses original departed source controller, outside-pool own land remains foreign, exact IDs/owners/resources/restored actor. No global filter/factory/multiplayer PASS.');break
      else:cast=''
      if cast:c=next(c for c in o['players'][0]['zones']['Hand'] if c['name']==cast);castIDs.append(c['id']);pick='card:'+str(c['id'])
    elif kind=='InputSelectTargets':
     save(m,o,'targets-'+str(step))
     if r.get('selectedCount'):pick='ok'
     else:
      candidates=[x for x in opts if x.get('card',{}).get('id',0)>0]
      desired=(own if any(x['card']['id']==own for x in candidates) else foreign) if stage==1 else source
      pick=next(x['id'] for x in candidates if x['card']['id']==desired)
      if stage==1:targets.append(desired)
    elif 'PayMana' in kind:save(m,o,'payment-'+str(step));pick='ok'
    elif kind=='OrderAbilities':pick='confirm-order' if 'confirm-order' in ids else next(x for x in ids if x.startswith('order:'))
    elif 'ok' not in ids:save(m,o,'unhandled-'+str(step));raise AssertionError(('unhandled',r))
    ck(pick in ids and not next(x for x in opts if x['id']==pick).get('disabled'),('native legal option',r,pick));print(step,kind,r['seat'],pick,flush=True);p.stdin.write(json.dumps(dict(type='respond',session=cfg['session'],seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
   else:raise AssertionError('step limit')
 except Exception:result.update(status='FAIL',checks=checks,error=traceback.format_exc())
 finally:
  if p is not None and p.poll() is None:p.kill();p.wait()
 wr(f/'result.json',result);wr(OUT/'control-land-result.json',result);print(result['status'],result.get('error',''),flush=True);return result
if __name__=='__main__':raise SystemExit(run()['status']!='PASS_BOUNDED')
