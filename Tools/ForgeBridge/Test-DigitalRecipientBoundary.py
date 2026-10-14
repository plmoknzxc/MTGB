"""R38 native multi-owner conjure/Seek and top-ten insufficient-pool branches."""
import hashlib,json,os,queue,subprocess,tempfile,threading,time,traceback,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'));OUT=ROOT/'.tools/digital-identity-1010'
FIX=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['RoyalStateFixture.java','ControlVariantFixture.java','DigitalIdentityFixture.java']]
DECK=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
def rd(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def wr(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def z(o,s=1):return o['players'][s-1]['zones']
def run(top=False):
 case='top-ten-short-pool' if top else 'multi-owner-library-and-seek';f=OUT/'native'/case/str(time.time_ns());f.mkdir(parents=True);audit=f/'audit';audit.mkdir();classes=f/'classes';classes.mkdir();checks=0;p=None
 for x in FIX+[Path(__file__)]: (f/x.name).write_bytes(x.read_bytes())
 result=dict(folder=f.relative_to(ROOT).as_posix(),case=case,runtimeHash=CFG['runtimeHash'],driver=sha(Path(__file__)),fixtures={x.name:sha(x) for x in FIX},producerClassesLib={x.relative_to(RT).as_posix():sha(x) for d in ['classes','lib'] for x in (RT/d).rglob('*') if x.is_file()},status='RUNNING');wr(f/'result.json',result)
 def ck(ok,msg):
  nonlocal checks
  assert ok,msg;checks+=1
 try:
  with (f/'compile.log').open('w') as log:subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*[str(f/x.name) for x in FIX]],check=True,stdout=log,stderr=subprocess.STDOUT)
  with tempfile.TemporaryDirectory(prefix='mtgb-digital-recipient-') as temp,(f/'trace.jsonl').open('w',encoding='utf8') as trace,(f/'stderr.log').open('w') as log:
   if top:positions=[dict(Battlefield=[],Hand=['Grow Old Together','Grizzly Bears'],Library=['Opt','Giant Spider']+['Swamp']*12+['Shivan Dragon']),dict(Battlefield=[],Hand=['Negate'],Library=['Shock','Mountain','Forest'])]
   else:positions=[dict(Battlefield=['Manor Guardian'],Hand=['Sandcloud Harbinger','Lightning Bolt','Bounty of the Deep','Bounty of the Deep'],Library=['Opt','Shock','Grizzly Bears']+['Swamp']*12+['Giant Spider']),dict(Battlefield=['Opposition Agent'],Hand=['Negate'],Library=['Shock','Mountain','Forest'])]
   cfg=dict(session=case,seed=4,fixtureSeed=11,startingLife=30,firstPlayerSeat=1,decks=[DECK,DECK],positions=positions,auditDir=str(audit));wr(f/'start.json',cfg)
   p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.DigitalIdentityFixture',str(RT/CFG['assets']),str(f/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');q=queue.Queue()
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
   initial=None;prior=None;stage=0;casts=[];made=[];moved=[];seen=set();deadline=time.monotonic()+150
   for step in range(450):
    ck(time.monotonic()<deadline,'deadline');m=q.get(timeout=35)
    if isinstance(m,Exception):raise m
    ck(m is not None,'process exited');trace.write(json.dumps(m)+'\n');trace.flush();ck(m['type'] not in ['fatal','ended','rejected'],m)
    if m['type']!='request':continue
    r=m['request'];kind=r['kind'];opts=r['options'];ids=[x['id'] for x in opts];pick='ok'
    if r['requestId'] in seen:continue
    seen.add(r['requestId']);o,t=(None,None) if 'Mulligan' in kind else obs(r);v=m['views'][0]
    ck(not m['views'][0]['players'][1]['hand'] and not m['views'][1]['players'][0]['hand'],'each recipient hidden hand not broadcast')
    if 'Mulligan' in kind:pick='keep' if 'keep' in ids else 'ok'
    elif kind=='InputPassPriority' and r['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
     if initial is None:initial=o;prior=o;save(m,o,t,'initial')
     if top and stage==1:
      save(m,o,t,'resolved');old=z(initial);now=z(o);oldIDs={c['id'] for c in old['Library']};newHand={c['id'] for c in now['Hand']}-{c['id'] for c in old['Hand']};inside=old['Library'][:10];spider=next(c for c in inside if c['name']=='Giant Spider');outside=next(c for c in old['Library'][10:] if c['name']=='Shivan Dragon')
      ck(newHand=={spider['id']},'Num2 short top-ten pool gets exact sole existing eligible creature');ck(outside['id'] in {c['id'] for c in now['Library']},'eligible outside top ten is not sought');ck({c['id'] for c in now['Library']}==oldIDs-{spider['id']},'actual shuffle preserves all unrelated exact IDs')
      for name,power,toughness in [('Giant Spider',3,5),('Grizzly Bears',3,3)]:c=next(c for c in now['Hand'] if c['name']==name);ck(c['power']==power and c['toughness']==toughness,'actual chained hand perpetual +1/+1 '+name)
      ck(next(c for c in now['Library'] if c['id']==outside['id'])['power']==5,'Shivan original power unchanged outside hand')
      ck({c['id'] for c in now['Graveyard']}==set(casts),'only actual paid Grow grave');ck(sum(c['tapped'] for c in now['Battlefield'])==3,'actual1GU exact three mana');result.update(status='PASS_BOUNDED',checks=checks,requests=len(seen),spellIDs=casts,soughtIDs=[spider['id']],excludedOutsideID=outside['id'],scope='Actual1GU Grow with sole top-ten creature from Num2, accurate original recipient ID, outside creature excluded, actual following native Shuffle/all remaining IDs and hand perpetual fields. No arbitrary all replacement/condition/modes PASS.');break
     if not top and stage>0:
      old=z(prior);now=z(o)
      if stage==1:
       for s in [1,2]:
        a=z(initial,s)['Library'];b=z(o,s)['Library'];born=[c for c in b if c['id'] not in {c['id'] for c in a}];ck(len(born)==3 and all(c['name']=='Sunscorched Desert' and c['owner']==s for c in born),'exact Amount3 new cards per recipient');ck([c for c in b if c['id'] in {x['id'] for x in a}]==a,'original relative library order retained for each recipient');ck(all(b.index(c)<=12 for c in born),'bounded native random insertion within original top-ten range including later insertions');made.extend(c['id'] for c in born)
       ck(len(set(made))==6 and all(not c['token'] for c in t if c['id'] in made),'six distinct actual non-token created IDs');save(m,o,t,'after-conjure')
      elif stage==2:
       for s in [1,2]:
        a=z(prior,s);b=z(o,s);found=[c for c in b['Hand'] if c['id'] not in {x['id'] for x in a['Hand']}];ck(len(found)==1 and found[0]['name'] in ['Shock','Grizzly Bears'],'each recipient exact nonland<=2 seek');ck(found[0]['id'] in {c['id'] for c in a['Library']},'Seek uses original recipient library ID');moved.extend(c['id'] for c in found);ck(b['Library']==[c for c in a['Library'] if c['id']!=found[0]['id']],'no-shuffle remaining exact ordered IDs');ck(not b['Exile'],'Opposition Agent only searches, actual Seek remains own hand')
       ck(any(c['name']=='Manor Guardian' for c in now['Graveyard']),'actual original Manor dies from Bolt');save(m,o,t,'after-each-seek')
      elif stage==3:
       found=[c for c in now['Hand'] if c['id'] not in {x['id'] for x in old['Hand']}];ck(len(found)==2 and sum(c['name'] in ['Mountain','Swamp','Sunscorched Desert'] for c in found)==1,'no-land hand branch seeks one land and one distinct nonland');ck(all(c['id'] in {x['id'] for x in old['Library']} for c in found),'Types uses original IDs');ck(now['Library']==[c for c in old['Library'] if c['id'] not in {x['id'] for x in found}],'multi-type original remaining order');moved.extend(c['id'] for c in found);save(m,o,t,'after-types')
      elif stage==4:
       save(m,o,t,'resolved');found=[c for c in now['Hand'] if c['id'] not in {x['id'] for x in old['Hand']}];remaining=[c for c in old['Library'] if c['name'] in ['Opt','Shock','Grizzly Bears','Giant Spider']];ck(len(remaining)==2 and len(found)==2 and {c['id'] for c in found}=={c['id'] for c in remaining},'Num2 moves exactly two distinct remaining nonlands');ck(now['Library']==[c for c in old['Library'] if c['id'] not in {x['id'] for x in found}],'Num2 no shuffle or duplicate existing ID');moved.extend(c['id'] for c in found);ck(len(set(moved))==6,'six distinct existing sought cards across recipient/types/Num')
       ck(len(casts)==4 and len(set(casts))==4,'four actual distinct paid spell IDs');ck({c['id'] for c in now['Graveyard']}==set(casts[1:])|{next(c['id'] for c in z(initial)['Battlefield'] if c['name']=='Manor Guardian')},'only actual Bolt/two sorceries plus original killed source grave; actual Harbinger creature remains battlefield');ck(sum(c['tapped'] for c in now['Battlefield'] if c['name'] in ['Plains','Island','Swamp','Mountain','Forest'])==10,'actual3+1+3+3 ten mana');ck([p['life'] for p in o['players']]==[30,30],'no player damage');ck(r['seat']==1 and v['turn']==1,'original actor restored same Main1');result.update(status='PASS_BOUNDED',checks=checks,requests=len(seen),spellIDs=casts,createdIDs=made,soughtIDs=moved,scope='Actual Sandcloud Amount3 each owner six original new IDs/native random library insertion and unchanged original order; actual Bolt kills Manor each-player Seek exact existing IDs/private views despite Opposition Agent; actual two Bounty casts Types fresh criteria then Num2 distinct pool, final original Hand/no-exile and resources. No arbitrary final replacement/memory flags/production unsupported modes/global PASS.');break
      prior=o
     cast='Grow Old Together' if top else ['Sandcloud Harbinger','Lightning Bolt','Bounty of the Deep','Bounty of the Deep'][stage];c=next(c for c in z(o)['Hand'] if c['name']==cast);casts.append(c['id']);pick='card:'+str(c['id']);stage+=1
    elif kind=='InputSelectTargets':
     if r.get('selectedCount'):pick='ok'
     else:pick=next(x['id'] for x in opts if x.get('card',{}).get('name')=='Manor Guardian')
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
 wr(f/'result.json',result);wr(OUT/('top-ten-result.json' if top else 'recipient-result.json'),result);print(result['status'],result.get('error',''),flush=True);return result
if __name__=='__main__':raise SystemExit(run('--top10' in sys.argv)['status']!='PASS_BOUNDED')
