"""R39 actual base-power-only exchange, types/keywords and granted cost-static rezone/recast."""
import hashlib,json,os,queue,subprocess,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'));OUT=ROOT/'.tools/perpetual-retention-1010'
FIX=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['RoyalStateFixture.java','ControlVariantFixture.java','DigitalIdentityFixture.java','DigitalMemoryFixture.java','PerpetualRemovalFixture.java','PerpetualFieldFixture.java','PerpetualRetentionFixture.java']]
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
   positions=[dict(Battlefield=lands,Hand=['Sarkhan, Wanderer to Shiv','Naktamun Shines Again','Second Little Pig','Shivan Dragon','High Fae Prankster','Unsummon','Unsummon','Unsummon'],Library=['Swamp','Forest','Plains']),dict(Battlefield=lands,Hand=[],Library=['Mountain','Forest','Plains'])]
   cfg=dict(session='perpetual-retention',seed=4,fixtureSeed=11,startingLife=40,firstPlayerSeat=1,decks=[DECK,DECK],positions=positions,auditDir=str(audit));wr(f/'start.json',cfg)
   p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.PerpetualRetentionFixture',str(RT/CFG['assets']),str(f/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');q=queue.Queue()
   def reader():
    try:
     for line in p.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start()
   def obs(r):
    end=time.monotonic()+4
    while time.monotonic()<end:
     try:return rd(audit/(str(r['requestId'])+'.json')),rd(audit/(str(r['requestId'])+'-retention.json'))
     except (FileNotFoundError,json.JSONDecodeError,PermissionError):time.sleep(.01)
    raise AssertionError('native read-only fields missing')
   def save(m,o,t,n):wr(f/(n+'-wire.json'),m);wr(f/(n+'-native.json'),o);wr(f/(n+'-retention.json'),t)
   stage=0;seen=set();paid=[];snapshots={};originals={};deadline=time.monotonic()+180
   def native(o,name,zone='Hand'):return next(c for c in o['players'][0]['zones'][zone] if c['name']==name)
   def field(t,cid):return next(c for c in t if c['id']==cid)
   def record(m,o,t,label):save(m,o,t,label);snapshots[label]=r['requestId']
   def cast(o,name):
    cid=native(o,name)['id'];paid.append(dict(id=cid,name=name,seat=1,turn=v['turn'],operation='cast'));return 'card:'+str(cid)
   def traits(t,name):return field(t,originals[name])
   for step in range(500):
    ck(time.monotonic()<deadline,'deadline');m=q.get(timeout=35)
    if isinstance(m,Exception):raise m
    ck(m is not None,'process exited');trace.write(json.dumps(m)+'\n');trace.flush();ck(m['type'] not in ['fatal','ended','rejected'],m)
    if m['type']!='request':continue
    r=m['request'];kind=r['kind'];opts=r['options'];ids=[x['id'] for x in opts];pick='ok'
    if r['requestId'] in seen:continue
    seen.add(r['requestId']);o,t=(None,None) if 'Mulligan' in kind else obs(r);v=m['views'][0]
    if 'Mulligan' in kind:pick='keep' if 'keep' in ids else 'ok'
    elif kind=='InputPassPriority' and r['seat']==1 and v['turn']==1 and v['phase']=='MAIN1' and not v['stackItems']:
     if stage==0:
      originals={n:native(o,n)['id'] for n in ['Second Little Pig','Shivan Dragon','High Fae Prankster']};record(m,o,t,'initial');pick=cast(o,'Naktamun Shines Again');stage=1
     elif stage==1:
      ck(len(traits(t,'Second Little Pig')['journal'])==1 and traits(t,'Second Little Pig')['journal'][0]['kind']=='PerpetualPTBoost' and not traits(t,'Shivan Dragon')['journal'] and not traits(t,'High Fae Prankster')['journal'],'actual Saga independent +1 power record');record(m,o,t,'boosted');pick=cast(o,'Sarkhan, Wanderer to Shiv');stage=100
     elif stage==100:
      record(m,o,t,'sarkhan-cast');pick='card:'+str(native(o,'Sarkhan, Wanderer to Shiv','Battlefield')['id']);stage=2
     elif stage==2:
      c=traits(t,'Shivan Dragon');ck(len(c['journal'])==1 and len([a for a in c['statics'] if a['params'].get('Mode')=='ReduceCost' or a['params'].get('MayPlay')=='True'])==2,'actual two granted cost-static abilities on original Dragon while original Pig boost remains independent');record(m,o,t,'cost-static-hand');pick=cast(o,'Second Little Pig');stage=3
     elif stage==3:
      paid.append(dict(id=originals['Second Little Pig'],name='Second Little Pig',seat=1,turn=v['turn'],operation='activate'));pick='card:'+str(originals['Second Little Pig']);stage=4
     elif stage==4:
      c=traits(t,'Second Little Pig');ck((c['basePower'],c['baseToughness'],c['power'],c['toughness'])==(4,4,5,4),'actual animate changes base4/4 plus independent +1');ck('Spirit' in c['type'] and 'Flying' in c['keywords'] and len(c['journal'])==4,'actual type/newPT/keyword records');record(m,o,t,'animated');pick=cast(o,'Shivan Dragon');stage=5
     elif stage==5:
      c=traits(t,'Shivan Dragon');ck(c['zone']=='Battlefield' and len([a for a in c['statics'] if a['params'].get('Mode')=='ReduceCost' or a['params'].get('MayPlay')=='True'])==2,'actual granted reduce normal dragon cast retains traits');record(m,o,t,'dragon-normal');pick=cast(o,'High Fae Prankster');stage=6
     elif stage==6:
      pig=traits(t,'Second Little Pig');dragon=traits(t,'Shivan Dragon');ck((pig['basePower'],pig['baseToughness'],pig['power'],pig['toughness'])==(5,4,6,4),'actual power exchange uses other BASE5 not net6 and preserves toughness4/boost');ck((dragon['basePower'],dragon['baseToughness'],dragon['power'],dragon['toughness'])==(4,5,4,5),'reciprocal base4 only, toughness5; does not use Pig net5');ck(pig['payloads'][-1]['kind']=='PerpetualNewPT' and pig['payloads'][-1].get('toughness') is None,'native power-only record null toughness');record(m,o,t,'exchanged');pick=cast(o,'Unsummon');stage=7
     elif stage==7:
      c=traits(t,'Second Little Pig');ck(c['zone']=='Hand' and (c['power'],c['toughness'])==(6,4) and 'Spirit' in c['type'] and 'Flying' in c['keywords'] and len(c['journal'])==5,'all Pig original field journals retained in new hand object');record(m,o,t,'pig-hand');pick=cast(o,'Second Little Pig');stage=8
     elif stage==8:
      c=traits(t,'Second Little Pig');ck(c['zone']=='Battlefield' and c['power']==6 and c['toughness']==4 and len(c['journal'])==5,'actual Pig recast preserves power/type/keyword without doubling');record(m,o,t,'pig-recast');pick=cast(o,'Unsummon');stage=9
     elif stage==9:
      c=traits(t,'Shivan Dragon');ck(c['zone']=='Hand' and c['power']==4 and c['toughness']==5 and len(c['journal'])==2 and len([a for a in c['statics'] if a['params'].get('Mode')=='ReduceCost' or a['params'].get('MayPlay')=='True'])==2,'exact dragon original base-only/boost/cost-static hand retention');record(m,o,t,'dragon-hand');pick=cast(o,'Shivan Dragon');stage=10
     elif stage==10:
      c=traits(t,'Shivan Dragon');ck(c['zone']=='Battlefield' and c['power']==4 and c['toughness']==5 and len(c['journal'])==2 and len([a for a in c['statics'] if a['params'].get('Mode')=='ReduceCost' or a['params'].get('MayPlay')=='True'])==2,'real dragon alternative cost recast after zone preserved all records');record(m,o,t,'dragon-alt-recast');pick=cast(o,'Unsummon');stage=11
     elif stage==11:
      c=traits(t,'High Fae Prankster');ck(c['zone']=='Hand' and len(c['journal'])==0,'source left while target independent permanent records remain');record(m,o,t,'source-hand');record(m,o,t,'resolved');ck(len(traits(t,'Second Little Pig')['journal'])==5 and len(traits(t,'Shivan Dragon')['journal'])==2,'source departure did not remove or replay independent exchange');result.update(status='PASS_BOUNDED',nativeFlows=1,actualPaidOperations=len(paid),actualPaidCasts=sum(x['operation']=='cast' for x in paid),checks=checks,requests=len(seen),paid=paid,originals=originals,snapshots=snapshots,scope='One actual shared flow: NewPT whole/only power with null toughness, original unrelated Pig +1 boost, types/Flying hand/BF/recast, granted Sarkhan reduce and actual generic alternative casting after original returns hand, source leaves while independent target records remain. No additional source recast or CopyPermanent/Moved/other payload certification.');break
    elif kind=='InputSelectTargets':
     record(m,o,t,'target-'+str(step))
     wanted=originals['Second Little Pig'] if stage==7 else originals['Shivan Dragon'] if stage==9 else originals['High Fae Prankster'] if stage==11 else None
     if wanted is not None:
      pick='ok' if r.get('selectedCount') else next(x['id'] for x in opts if x.get('card',{}).get('id')==wanted)
     else:
      op=next((x for x in opts if x.get('card',{}).get('id') in [originals['Second Little Pig'],originals['Shivan Dragon']] and not x.get('selected')),None)
      pick=op['id'] if op else 'ok'
    elif kind=='ChooseModes':
     record(m,o,t,'mode-'+str(step));pick='confirm-modes' if any(x.get('selected') for x in opts) else next(x['id'] for x in opts if ('exchange' in x['label'].lower() if stage==6 else '4/1' in x['label']))
    elif kind=='dialog':
     record(m,o,t,'dialog-'+str(step))
     if stage==2:pick=next(x['id'] for x in opts if 'perpetually gain' in x['label'].lower())
     elif stage==6:pick=next(x['id'] for x in opts if 'exchange' in x['label'].lower())
     elif stage==12:pick=next(x['id'] for x in opts if '4/1' in x['label'])
     elif stage==10:pick=next(x['id'] for x in opts if 'by paying' in x['label'].lower())
     elif stage==5:pick=ids[0]
     else:pick=ids[0]
    elif 'SelectCards' in kind or 'SelectEntities' in kind or kind=='SelectCards':
     record(m,o,t,'choice-'+str(step));pick='confirm-cards' if 'confirm-cards' in ids else 'ok' if 'ok' in ids else ids[0]
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
