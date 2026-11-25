import json,subprocess,threading,queue,traceback
from pathlib import Path
R=Path(__file__).resolve().parents[2];RT=R/'ForgeRuntime';O=R/'.tools/hand-dock-empty-1025';checks=[]
def check(p,n):
 checks.append({'pass':bool(p),'label':n})
 if not p:raise AssertionError(n)
def run(name,lands,wish=False):
 d=O/name;d.mkdir(exist_ok=True);session='dock-'+name
 deck={'cards':[{'setCode':'10E','collectorNumber':'368','count':60}],'sideboard':[]}
 spec={'session':session,'seed':4,'firstPlayerSeat':1,'startingLife':20,'lands':lands,'decks':[deck,deck],'own':{'Hand':['Cunning Wish']},'other':{'Hand':['Opt']}}
 (d/'start.json').write_text(json.dumps(spec),encoding='utf8')
 cfg=json.loads((RT/'runtime.json').read_text('utf-8-sig'));cp=';'.join(map(str,[O/'classes',R/'.tools/strict-source-1025/classes',RT/'classes',RT/'lib/*']))
 log=(d/'native.log').open('w');p=subprocess.Popen([str(RT/cfg['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',cp,'mtgb.bridge.HandDockSourcesFixture',str(RT/cfg['assets']),str(d/'start.json')],cwd=RT/cfg['assets'],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');q=queue.Queue()
 def reader():
  for line in p.stdout:q.put(json.loads(line))
  q.put(None)
 threading.Thread(target=reader,daemon=True).start();started=False;initial=False
 try:
  for step in range(110):
   m=q.get(timeout=45)
   if m is None:raise AssertionError('host ended')
   if m['type'] in ['fatal','rejected']:raise AssertionError(m)
   if m['type']!='request':continue
   views=m['views'];v=views[0];r=m['request'];opts=[o for o in r['options'] if not o.get('disabled')];pick=None
   if r['kind']=='InputPassPriority' and r['seat']==1 and v['turnSeat']==1 and v['phase']=='MAIN1' and not initial:
    initial=True
    for i in [0,1]:
     a=views[i]['players'][i];b=views[i]['players'][1-i]
     check(len(a['sideboardCards'])==(2 if i==0 else 1),name+' recipient '+str(i+1)+' own actual sideboard')
     check(not b['sideboardCards'],name+' recipient '+str(i+1)+' no opposing sideboard')
    c=next(c for c in v['players'][0]['commandCards'] if c['commander'])
    option=next((o for o in opts if o['id']=='card:'+str(c['id'])),None)
    payable=option is not None and not option.get('requiresPaymentCheck')
    check(payable==bool(lands),name+' native command payable matches mana')
    for i in [0,1]:check((c['id'] in views[i]['players'][0]['usableSourceIds'])==payable,name+' command eligibility recipient '+str(i+1))
    (d/'priority.json').write_text(json.dumps(m,ensure_ascii=False,indent=2),encoding='utf8')
    if not wish:return
    cw=next(c for c in v['players'][0]['hand'] if c['name']=='Cunning Wish');pick='card:'+str(cw['id']);started=True
   if started and len(v['players'][0]['sideboardCards'])==1 and any(c['name']=='Opt' for c in v['players'][0]['hand']):
    check(v['players'][0]['sideboardCards'][0]['name']=='Negate',name+' sideboard shrinks after native wish')
    check(not views[1]['players'][0]['sideboardCards'],name+' updated sideboard remains private')
    (d/'after.json').write_text(json.dumps(m,ensure_ascii=False,indent=2),encoding='utf8');return
   if pick is None:
    if r['kind']=='InputPassPriority':pick=next(o['id'] for o in opts if o['id'] in ['ok','pass'])
    else:pick=next((o['id'] for o in opts if o['id']=='ok' and r.get('selectedCount',0)>0),None) or next((o['id'] for o in opts if o.get('card',{}).get('name')=='Opt'),None) or next((o['id'] for o in opts if o['id'] in ['keep','ok','auto','pay','yes']),opts[0]['id'])
   print(name,step,r['kind'],pick,flush=True);p.stdin.write(json.dumps({'type':'respond','session':session,'seat':r['seat'],'requestId':r['requestId'],'option':pick})+'\n');p.stdin.flush()
  raise AssertionError('request limit')
 finally:p.kill();p.wait();log.close()
try:
 run('unavailable',[])
 run('available',['Island','Island','Island','Forest'],True)
 status='PASS';error=None
except Exception:status='FAIL';error=traceback.format_exc()
(O/'native-result.json').write_text(json.dumps({'status':status,'checks':checks,'error':error},ensure_ascii=False,indent=2),encoding='utf8')
print(status,error or '',flush=True)
if error:raise SystemExit(1)

