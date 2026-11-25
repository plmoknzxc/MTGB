import json,subprocess,threading,queue,traceback
from pathlib import Path
R=Path(__file__).resolve().parents[2];RT=R/'ForgeRuntime';O=R/'.tools/redesign-lands-1026';checks=[]
def check(value,label):
 checks.append({'pass':bool(value),'label':label})
 if not value:raise AssertionError(label)
def save(name,m):(O/(name+'.json')).write_text(json.dumps(m,ensure_ascii=False,indent=2),encoding='utf8')
def run():
 deck={'cards':[{'setCode':'10E','collectorNumber':'368','count':60}],'sideboard':[]}
 spec={'session':'land-presentation','seed':4,'firstPlayerSeat':1,'startingLife':20,'decks':[deck,deck],'own':{'Hand':['Blood Moon','Disenchant']},'other':{'Hand':['Opt']}}
 (O/'start.json').write_text(json.dumps(spec),encoding='utf8')
 cfg=json.loads((RT/'runtime.json').read_text('utf-8-sig'));cp=';'.join(map(str,[O/'classes',R/'.tools/strict-source-1025/classes',RT/'classes',RT/'lib/*']))
 log=(O/'native.log').open('w');p=subprocess.Popen([str(RT/cfg['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',cp,'mtgb.bridge.LandPresentationFixture',str(RT/cfg['assets']),str(O/'start.json')],cwd=RT/cfg['assets'],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');q=queue.Queue()
 def reader():
  for line in p.stdout:q.put(json.loads(line))
  q.put(None)
 threading.Thread(target=reader,daemon=True).start();initial=False;activated=False;moonCast=False;moonSeen=False;disenchantCast=False;last=None;repeats=0
 try:
  for step in range(140):
   m=q.get(timeout=45)
   if m is None:raise AssertionError('host ended')
   if m['type'] in ['fatal','rejected']:raise AssertionError(m)
   if m['type']!='request':continue
   v=m['views'][0];r=m['request'];me=v['players'][0];lands=[c for c in me['battlefield'] if c.get('land')];opts=[o for o in r['options'] if not o.get('disabled')];pick=None
   ownPriority=r['kind']=='InputPassPriority' and r['seat']==1 and v['turnSeat']==1 and v['phase']=='MAIN1'
   if ownPriority and not initial:
    initial=True
    expected={'Island':2,'Mountain':8,'Wastes':0,'Hallowed Fountain':3,'Steam Vents':10,'Shivan Reef':10,"Raffine's Tower":7,'City of Brass':31,'Flooded Strand':0}
    for name,mask in expected.items():
     cards=[c for c in lands if c['name']==name];check(bool(cards),name+' native board present')
     check(all(c.get('landPresentationKnown') and c['landManaColors']==mask for c in cards),name+' native producible color mask')
    shrines=[c for c in lands if c['name']=='Hallowed Fountain'];check(sum(c['landStackable'] for c in shrines)==3,'linked remembered land stays separate')
    save('before',m)
   normal=[c for c in lands if c['name']=='Hallowed Fountain' and c.get('landStackable')]
   tapped=[c for c in normal if c.get('tapped')]
   if ownPriority and not activated and len(tapped)==2:
    activated=True
    check(all(not c['stackablePermanent'] and c['landStackable'] for c in tapped),'real used mana lands retain redesign grouping eligibility')
    check(me['manaPool'][1]==2,'two actual blue mana in pool')
    save('after-mana',m)
   moon=next((c for c in me['battlefield'] if c['name']=='Blood Moon'),None)
   if ownPriority and moon and not moonSeen:
    moonSeen=True
    check(all(c['landManaColors']==8 for c in lands if c['name'] not in ['Island','Mountain','Plains','Wastes']),'resolved Blood Moon recolors nonbasic production red')
    check(all(c['landManaColors']==2 for c in lands if c['name']=='Island'),'Blood Moon preserves basic island blue')
    save('moon',m)
   if ownPriority and moonSeen and not moon:
    check(all(c['landManaColors']==3 for c in lands if c['name']=='Hallowed Fountain'),'resolved Disenchant restores white blue production')
    save('restored',m);return
   if ownPriority:
    if not activated:
     c=next(c for c in normal if not c.get('tapped'));pick='card:'+str(c['id'])
    elif not moonCast:
     c=next(c for c in me['hand'] if c['name']=='Blood Moon');pick='card:'+str(c['id']);moonCast=True
    elif moonSeen and not disenchantCast:
     c=next(c for c in me['hand'] if c['name']=='Disenchant');pick='card:'+str(c['id']);disenchantCast=True
   if pick is None:
    if r['kind']=='InputPassPriority':pick=next(o['id'] for o in opts if o['id'] in ['ok','pass'])
    else:
     pick=next((o['id'] for o in opts if o['id']=='ok' and r.get('selectedCount',0)>0),None)
     if not pick:pick=next((o['id'] for o in opts if o.get('card',{}).get('name')=='Blood Moon'),None)
     if not pick:pick=next((o['id'] for o in opts if '{U}' in o.get('label','') or o.get('label','')=='Blue'),None)
     if not pick:pick=next((o['id'] for o in opts if o['id'] in ['keep','ok','auto','pay','yes']),opts[0]['id'])
   progress=(r['kind'],r.get('selectedCount'),pick,json.dumps(me.get('manaPool')))
   repeats=repeats+1 if progress==last else 0;last=progress
   if repeats>4:save('stalled',m);raise AssertionError('No progress '+str(progress))
   print(step,r['kind'],pick,flush=True)
   p.stdin.write(json.dumps({'type':'respond','session':spec['session'],'seat':r['seat'],'requestId':r['requestId'],'option':pick})+'\n');p.stdin.flush()
  raise AssertionError('request limit')
 finally:p.kill();p.wait();log.close()
try:run();status='PASS';error=None
except Exception:status='FAIL';error=traceback.format_exc()
(O/'result.json').write_text(json.dumps({'status':status,'checks':checks,'error':error,'scope':'Native mana activation and Moon/Disenchant resolution; linked-card initial fixture is projection boundary, no new card rules'},ensure_ascii=False,indent=2),encoding='utf8')
print(status,error or '',flush=True)
if error:raise SystemExit(1)

