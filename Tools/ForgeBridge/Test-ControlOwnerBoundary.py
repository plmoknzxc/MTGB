"""R03 owner-return filter/source lifecycle: actual Raise/Donate/Bolt/Shock and native end trigger."""
import hashlib,json,os,queue,subprocess,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'));OUT=ROOT/'.tools/r03-payload-1010';FIX=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['RoyalStateFixture.java','ControlVariantFixture.java','ControlOwnerBoundaryFixture.java']];DECK=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
def rd(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def wr(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def field(o):return [c for p in o['players'] for c in p['zones']['Battlefield']]
def find(o,n):return next(c for c in field(o) if c['name']==n)
def run():
 f=OUT/'native/owner-filter-source-left'/str(time.time_ns());f.mkdir(parents=True);audit=f/'audit';audit.mkdir();classes=f/'classes';classes.mkdir();checks=0;p=None
 for x in FIX+[Path(__file__)]: (f/x.name).write_bytes(x.read_bytes())
 result=dict(folder=f.relative_to(ROOT).as_posix(),case='owner-filter-source-left',runtimeHash=CFG['runtimeHash'],driver=sha(Path(__file__)),fixtures={x.name:sha(x) for x in FIX},producerClassesLib={x.relative_to(RT).as_posix():sha(x) for d in ['classes','lib'] for x in (RT/d).rglob('*') if x.is_file()},status='RUNNING');wr(f/'result.json',result)
 def ck(ok,msg):
  nonlocal checks
  assert ok,msg;checks+=1
 try:
  with (f/'compile.log').open('w') as log:subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*[str(f/x.name) for x in FIX]],check=True,stdout=log,stderr=subprocess.STDOUT)
  with tempfile.TemporaryDirectory(prefix='mtgb-owner-boundary-') as temp,(f/'trace.jsonl').open('w',encoding='utf8') as trace,(f/'stderr.log').open('w') as log:
   positions=[dict(Battlefield=['Brooding Saurian','Sol Ring','Grizzly Bears'],Hand=['Raise the Alarm','Donate','Donate','Lightning Bolt','Shock'],Library=['Island','Plains','Forest','Mountain']),dict(Battlefield=['Giant Spider','Glorious Anthem'],Hand=['Negate'],Library=['Island','Plains','Forest','Mountain'])]
   cfg=dict(session='owner-filter-source-left',seed=4,fixtureSeed=11,startingLife=30,firstPlayerSeat=1,decks=[DECK,DECK],positions=positions,auditDir=str(audit));wr(f/'start.json',cfg)
   p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.ControlOwnerBoundaryFixture',str(RT/CFG['assets']),str(f/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');q=queue.Queue()
   def reader():
    try:
     for line in p.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start()
   def obs(r):
    end=time.monotonic()+4
    while time.monotonic()<end:
     try:return rd(audit/(str(r['requestId'])+'.json')),rd(audit/(str(r['requestId'])+'-tokens.json'))
     except (FileNotFoundError,json.JSONDecodeError,PermissionError):time.sleep(.01)
    raise AssertionError('native/token read-only observer missing')
   def save(m,o,t,n):wr(f/(n+'-wire.json'),m);wr(f/(n+'-native.json'),o);wr(f/(n+'-tokens.json'),t)
   initial=None;raised=None;donated=None;stage=0;cast='';token=0;source=0;sol=0;seen=set();castIDs=[];deadline=time.monotonic()+180
   for step in range(500):
    ck(time.monotonic()<deadline,'deadline');m=q.get(timeout=35)
    if isinstance(m,Exception):raise m
    ck(m is not None,'process exited');trace.write(json.dumps(m)+'\n');trace.flush();ck(m['type'] not in ['fatal','ended','rejected'],m)
    if m['type']!='request':continue
    r=m['request'];kind=r['kind'];opts=r['options'];ids=[x['id'] for x in opts];pick='ok'
    if r['requestId'] in seen:continue
    seen.add(r['requestId']);o,t=(None,None) if 'Mulligan' in kind else obs(r);v=m['views'][0]
    ck(not m['views'][0]['players'][1]['hand'] and not m['views'][1]['players'][0]['hand'],'non-actor private hands concealed')
    if 'Mulligan' in kind:pick='keep' if 'keep' in ids else 'ok'
    elif kind=='InputPassPriority':
     if initial is None and r['seat']==1 and o['players'][0]['zones']['Hand']:initial=o;source=find(o,'Brooding Saurian')['id'];sol=find(o,'Sol Ring')['id'];save(m,o,t,'initial')
     if r['seat']==1 and v['phase']=='MAIN1' and not v['stackItems'] and stage<3:
      if stage==1:
       ck(len(t)==2 and all(x['owner']==x['controller']==1 for x in t),'real Raise generated exactly two own tokens');token=t[0]['id'];raised=o;save(m,o,t,'after-raise')
      if stage==2:ck(next(c for c in field(o) if c['id']==sol)['controller']==2,'real Donate foreign original artifact');save(m,o,t,'after-artifact-donate')
      cast=['Raise the Alarm','Donate','Donate'][stage];c=next(c for c in o['players'][0]['zones']['Hand'] if c['name']==cast);castIDs.append(c['id']);pick='card:'+str(c['id']);stage+=1
     elif stage==3 and v['phase']=='MAIN1' and not v['stackItems']:
      ck(next(c for c in field(o) if c['id']==token)['controller']==2,'real Donate foreign original token');donated=o;save(m,o,t,'after-token-donate');stage=4
     elif stage in [4,5] and r['seat']==1 and v['phase']=='END_OF_TURN' and v['stackItems']:
      if stage==4:
       ck(any(x.get('source',{}).get('id')==source for x in v['stackItems']),'natural original Saurian end trigger');save(m,o,t,'end-trigger');cast='Lightning Bolt'
      elif any(c['id']==source and c['damage']==3 for c in field(o)):
       save(m,o,t,'after-bolt');cast='Shock'
      else:cast=''
      if cast:c=next(c for c in o['players'][0]['zones']['Hand'] if c['name']==cast);castIDs.append(c['id']);pick='card:'+str(c['id']);stage+=1
     elif stage==6 and not v['stackItems']:
      save(m,o,t,'resolved');ck(initial is not None and raised is not None and donated is not None,'all real setup operations resolved');ck(len(castIDs)==5 and len(set(castIDs))==5,'five distinct actual spell IDs');ck(r['seat']==1 and v['turn']==1,'original end-step actor restored')
      old={c['id']:c for c in field(donated)};now={c['id']:c for c in field(o)};ck(set(now)==set(old)-{source},'only killed original source left battlefield')
      for id,c in now.items():
       ck(c['controller']==(1 if id==sol else old[id]['controller']),'exact owner-return excludes actual token and unrelated permanents')
       for k in ['owner','timestamp','damage','counters']:ck(c[k]==old[id][k],'unchanged original field '+k)
      ck(len(t)==2 and {x['id'] for x in t}=={c['id'] for c in field(raised) if c['id'] not in {x['id'] for x in field(initial)}},'same actual token identities retained')
      ck(next(x for x in t if x['id']==token)['controller']==2,'donated token remains foreign despite owner1')
      ck([pv['life'] for pv in o['players']]==[30,30],'only source receives damage, no life change')
      for s in [1,2]:
       oz=initial['players'][s-1]['zones'];nz=o['players'][s-1]['zones'];ck(nz['Library']==oz['Library'] and nz['Exile']==oz['Exile'],'untouched ordered library/exile');ck({c['id'] for c in nz['Hand']}=={c['id'] for c in oz['Hand']}-set(castIDs),'exact original unspent hand');ck({c['id'] for c in nz['Graveyard']}==(set(castIDs)|{source} if s==1 else set()),'only original five spells and killed source grave')
       for view in m['views']:ck(sum(view['players'][s-1]['manaPool'])==sum(view['players'][s-1]['snowManaPool'])==0,'zero residual mana')
      paid=sum(c['tapped'] for c in field(o) if c['name'] in ['Plains','Island','Swamp','Mountain','Forest'])+2*next(c['tapped'] for c in field(o) if c['id']==sol);ck(paid==10,'exact2+3+3+1+1 mana sourced from lands/ring')
      result.update(status='PASS_BOUNDED',checks=checks,requests=len(seen),spellIDs=castIDs,sourceID=source,artifactID=sol,tokenID=token,scope='Actual five paid native casts, non-token owner return and source gone before triggered ability resolves; exact resources/original IDs/restored actor. No global payload/four-human/full locale PASS.');break
    elif kind=='InputSelectTargets':
     save(m,o,t,'targets-'+str(step))
     if r.get('selectedCount'):pick='ok'
     elif cast=='Donate':
      target=sol if stage==2 else token;want=next((x for x in opts if x.get('card',{}).get('id',0)==target),None);pick=want['id'] if want else next(x['id'] for x in opts if x.get('label')=='Player 2')
     elif cast in ['Lightning Bolt','Shock']:pick=next(x['id'] for x in opts if x.get('card',{}).get('id',0)==source)
     else:raise AssertionError(('unexpected target',r))
    elif 'PayMana' in kind:save(m,o,t,'payment-'+str(step));pick='ok'
    elif kind=='dialog':save(m,o,t,'dialog-'+str(step));pick=ids[0]
    elif kind=='OrderAbilities':pick='confirm-order' if 'confirm-order' in ids else next(x for x in ids if x.startswith('order:'))
    elif kind=='RevealCards':pick='confirm-cards'
    elif 'ok' not in ids:raise AssertionError(('unhandled',r))
    ck(pick in ids and not next(x for x in opts if x['id']==pick).get('disabled'),('original legal option',r,pick));print(step,kind,r['seat'],pick,flush=True);p.stdin.write(json.dumps(dict(type='respond',session=cfg['session'],seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
   else:raise AssertionError('step limit')
   ck(result['status']=='PASS_BOUNDED','actual final result')
 except Exception:result.update(status='FAIL',checks=checks,error=traceback.format_exc())
 finally:
  if p is not None and p.poll() is None:p.kill();p.wait()
 wr(f/'result.json',result);wr(OUT/'owner-filter-result.json',result);print(result['status'],result.get('error',''),flush=True);return result
if __name__=='__main__':raise SystemExit(run()['status']!='PASS_BOUNDED')
