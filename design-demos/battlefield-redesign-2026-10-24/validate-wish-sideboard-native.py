import json,subprocess,threading,queue,traceback,hashlib,sys
from pathlib import Path
R=Path(__file__).resolve().parents[2];RT=R/'ForgeRuntime';baseline='--baseline' in sys.argv;noBlue='--no-blue' in sys.argv;landMode='--land' in sys.argv;O=R/('.tools/wish-sideboard-1026-baseline' if baseline else '.tools/wish-sideboard-1026/no-blue' if noBlue else '.tools/wish-sideboard-1026/land' if landMode else '.tools/wish-sideboard-1026');O.mkdir(exist_ok=True);checks=[]
def check(value,label):
 checks.append({'pass':bool(value),'label':label})
 if not value:raise AssertionError(label)
def save(name,m):(O/(name+'.json')).write_text(json.dumps(m,ensure_ascii=False,indent=2),encoding='utf8')
def run():
 deck={'cards':[{'setCode':'10E','collectorNumber':'368','count':60}],'sideboard':[]}
 spec={'session':'wish-sideboard','seed':4,'firstPlayerSeat':1,'startingLife':20,'decks':[deck,deck],'own':{'Hand':['Wish','Wish']},'other':{'Hand':[]}}
 if noBlue:spec['noBlue']=True
 save('start',spec);cfg=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
 cp=';'.join(map(str,[O/'classes',R/'.tools/wish-sideboard-1026/classes',R/'.tools/strict-source-1025/classes',RT/'classes',RT/'lib/*']))
 log=(O/'native.log').open('w');p=subprocess.Popen([str(RT/cfg['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',cp,'mtgb.bridge.WishSideboardFixture',str(RT/cfg['assets']),str(O/'start.json')],cwd=RT/cfg['assets'],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');q=queue.Queue()
 def reader():
  for line in p.stdout:
   try:q.put(json.loads(line))
   except Exception:pass
  q.put(None)
 threading.Thread(target=reader,daemon=True).start()
 initial=False;wishCasts=0;wishReady=False;tome=False;stifled=False;done=False;secondReady=False;drawBefore=None;sb={};last=None;repeats=0
 try:
  for step in range(280):
   m=q.get(timeout=40)
   if m is None:raise AssertionError('native host ended')
   if m['type'] in ['fatal','rejected']:save('failure',m);raise AssertionError(m)
   if m['type']!='request':continue
   r=m['request'];v=next(v for v in m['views'] if v['seat']==1);me=v['players'][0];op=v['players'][1]
   opts=[o for o in r['options'] if not o.get('disabled')];offered={o['id'] for o in opts};pick=None
   ownPriority=r['kind']=='InputPassPriority' and r['seat']==1
   ownMain=ownPriority and v['turnSeat']==1 and v['phase']=='MAIN1' and not v['stackCount']
   side=me.get('sideboardCards',[]);hand=me.get('hand',[]);gy=me.get('graveyardCards',[])
   if ownMain and not initial:
    initial=True;sb={c['name']:c['id'] for c in side};check(len(sb)==4,'fixture has four physical sideboard cards')
    check(not any('card:'+str(i) in offered for i in sb.values()),'no native sideboard action before Wish permission')
    check(not next(w for w in m['views'] if w['seat']==2)['players'][0].get('sideboardCards'),'opponent receives no private sideboard')
    save('before-permission',m);pick='card:'+str(next(c for c in hand if c['name']=='Wish')['id']);wishCasts=1
   if ownMain and wishCasts==1 and any(c['name']=='Wish' for c in gy) and not wishReady:
    wishReady=True
    check('card:'+str(sb['Mountain'] if noBlue else sb['Divination']) in offered,'Wish grants later native main-phase sideboard action')
    check(any(c['id']==sb['Stifle'] for c in side),'Wish resolution does not preselect or move Stifle to hand')
    save('after-wish',m)
    if landMode:pick='card:'+str(sb['Mountain'])
   if ownMain and landMode and wishReady and any(c['id']==sb['Mountain'] for c in me['battlefield']):
    check(not any('card:'+str(c['id']) in offered for c in side),'native sideboard land play consumes one Wish permission')
    check(all(c['id']!=sb['Mountain'] for c in side),'same physical sideboard land enters battlefield')
    save('land-played',m);return
   if wishReady and not tome and r['kind']=='InputPassPriority' and r['seat']==2 and not v['stackCount']:
    tome=True;drawBefore=op['handCount'];t=next(c for c in op['battlefield'] if c['name']=='Jayemdae Tome');pick='card:'+str(t['id'])
   if ownPriority and tome and not stifled and any(e.get('source',{}).get('name')=='Jayemdae Tome' for e in v.get('stackItems',[])):
    if noBlue:
     check('card:'+str(sb['Stifle']) not in offered,'permission without blue mana does not offer sideboard Stifle')
     check(not v['canRespond'],'unpayable sideboard does not falsely stop automatic priority')
     save('unpayable-response',m);return
    check('card:'+str(sb['Stifle']) in offered,'deferred Wish allows sideboard Stifle in response to opposing activated ability')
    check('card:'+str(sb['Divination']) not in offered,'sorcery timing not bypassed while ability on stack')
    check('card:'+str(sb['Counterspell']) not in offered,'spell-only Counterspell not offered against ability')
    check(v['canRespond'],'sideboard action contributes to native response availability')
    save('response-available',m);pick='card:'+str(sb['Stifle']);stifled=True
   if ownMain and stifled and not done and any(c['id']==sb['Stifle'] for c in gy) and not v['stackCount']:
    done=True
    check(op['handCount']==drawBefore,'native Stifle counters draw ability without opponent drawing')
    check(all(c['id']!=sb['Stifle'] for c in side),'cast card leaves sideboard for stack then graveyard')
    check(not any('card:'+str(i) in offered for i in sb.values()),'one card consumes Wish permission for all remaining sideboard cards')
    check(me['manaPool'][1]==0,'blue mana paid for actual sideboard Stifle')
    save('permission-used',m);pick='card:'+str(next(c for c in hand if c['name']=='Wish')['id']);wishCasts=2
   if ownMain and done and wishCasts==2 and sum(c['name']=='Wish' for c in gy)==2 and not secondReady:
    secondReady=True;check('card:'+str(sb['Divination']) in offered,'second Wish independently restores one permission');save('second-wish',m)
   if secondReady and ownPriority and v['turnSeat']==2:
    check(not any('card:'+str(i) in offered for i in sb.values()),'unused Wish permission expires at end of turn')
    save('expired',m);return
   if pick is None:
    if r['kind']=='InputPassPriority':pick=next(o['id'] for o in opts if o['id'] in ['ok','pass'])
    else:
     pick=next((o['id'] for o in opts if o['id']=='ok' and r.get('selectedCount',0)>0),None)
     if pick is None and r['kind'].startswith('InputSelect'):
      pick=next((o['id'] for o in opts if 'Jayemdae Tome' in o.get('label','') or o.get('card',{}).get('name')=='Jayemdae Tome'),None)
     if pick is None:pick=next((o['id'] for o in opts if '{U}' in o.get('label','') or o.get('label')=='Blue'),None)
     if pick is None:pick=next((o['id'] for o in opts if o['id'] in ['keep','ok','auto','pay','yes']),opts[0]['id'])
   progress=(r['seat'],r['kind'],r.get('selectedCount'),pick,json.dumps(me.get('manaPool')),json.dumps(op.get('manaPool')))
   repeats=repeats+1 if progress==last else 0;last=progress
   if repeats>4:save('stalled',m);raise AssertionError('No progress '+str(progress))
   print(step,r['seat'],r['kind'],pick,flush=True)
   p.stdin.write(json.dumps({'type':'respond','session':spec['session'],'seat':r['seat'],'requestId':r['requestId'],'option':pick})+'\n');p.stdin.flush()
  raise AssertionError('request limit')
 finally:p.kill();p.wait();log.close()
try:run();status='PASS';error=None
except Exception:status='FAIL';error=traceback.format_exc()
sourceHashes={str(p.relative_to(R)):hashlib.sha256(p.read_bytes()).hexdigest() for p in [R/'Tools/ForgeBridge/java/mtgb/bridge/ForgeBridge.java',R/'Tools/ForgeBridge/tests/WishSideboardFixture.java',R/('.tools/wish-sideboard-1026-baseline/classes/mtgb/bridge/ForgeBridge.class' if baseline else '.tools/wish-sideboard-1026/classes/mtgb/bridge/ForgeBridge.class'),R/'.tools/wish-sideboard-1026/classes/mtgb/bridge/ForgeStackPayment.class']}
save('result',{'status':status,'checks':checks,'error':error,'baseRuntimeHash':json.loads((RT/'runtime.json').read_text('utf-8-sig'))['runtimeHash'],'candidateHashes':sourceHashes,'scope':'Actual Wish cast and resolution, deferred sideboard Stifle against opposing activated ability, consumption and end-turn expiry. Isolated candidate classes, not deployed.'})
print(status,error or '',flush=True)
if error:raise SystemExit(1)

