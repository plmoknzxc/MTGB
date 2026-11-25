import json,subprocess,threading,queue,time,traceback,hashlib
from pathlib import Path
R=Path(__file__).resolve().parents[2];RT=R/'ForgeRuntime';O=R/'.tools/stack-payment-1025';checks=[]
def check(p,label):
 checks.append({'pass':bool(p),'label':label})
 if not p:raise AssertionError(label)
def case(name,spell,lands,extra):
 d=O/name;d.mkdir(exist_ok=True);session='receipt-'+name
 deck={'cards':[{'setCode':'10E','collectorNumber':'368','count':60}],'sideboard':[]}
 spec={'session':session,'actor':1,'seed':4,'firstPlayerSeat':1,'startingLife':20,'lands':lands,'decks':[deck,deck],'own':{'Hand':[spell]+extra},'other':{'Battlefield':['Grizzly Bears'],'Hand':['Opt']}}
 (d/'start.json').write_text(json.dumps(spec),encoding='utf8');cfg=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
 cp=';'.join(map(str,[O/'classes',R/'.tools/strict-source-1025/classes',RT/'classes',RT/'lib/*']))
 log=(d/'native.log').open('w',encoding='utf8');p=subprocess.Popen([str(RT/cfg['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',cp,'mtgb.bridge.SourceAvailabilityFixture',str(RT/cfg['assets']),str(d/'start.json')],cwd=RT/cfg['assets'],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');q=queue.Queue()
 def read():
  for line in p.stdout:q.put(json.loads(line))
  q.put(None)
 threading.Thread(target=read,daemon=True).start();started=False
 try:
  with (d/'trace.jsonl').open('w',encoding='utf8') as trace:
   for step in range(160):
    m=q.get(timeout=45)
    if m is None:raise AssertionError('host ended')
    if m['type'] in ('fatal','rejected'):raise AssertionError(m)
    if m['type']!='request':continue
    trace.write(json.dumps(m,ensure_ascii=False)+'\n');trace.flush();v=m['views'][0];r=m['request'];opts=[o for o in r['options'] if not o.get('disabled')];pick=None
    entries=[e for e in v['stackItems'] if e.get('kind')=='spell' and e.get('source',{}).get('name')==spell]
    if entries:
     e=entries[0];receipt=e.get('payment',{});check(e.get('hasPaymentReceipt'),name+' receipt present on actual committed stack');check(any(c['kind']=='Evoke' for c in e.get('castingChoices',[])) if name!='gift' else any(c['kind']=='PromiseGift' for c in e.get('castingChoices',[])),name+' chosen optional cost retained')
     if name=='exile':
      check('white card' in receipt.get('cost',''),name+' coloured exile requirement retained')
      check('paidCards' not in receipt,name+' individual movement cards left to existing move notices')
     else:check(receipt.get('spentMana')=='{U}{U}' if name=='hybrid' else receipt.get('spentMana')=='{U}',name+' actual mana colour receipt')
     if name=='hybrid':check('{U/B}' in receipt.get('manaCost','') or 'U/B' in receipt.get('cost','') or 'UB' in receipt.get('cost',''),name+' hybrid evoke requirement retained')
     (d/'stack.json').write_text(json.dumps(m,ensure_ascii=False,indent=2),encoding='utf8');return
    if r['kind']=='InputPassPriority':
     if not started and r['seat']==1 and v['turnSeat']==1 and v['phase']=='MAIN1':
      card=next((c for c in v['players'][0]['hand'] if c['name']==spell),None)
      if card and any(o['id']=='card:'+str(card['id']) for o in opts):pick='card:'+str(card['id']);started=True
     if pick is None:pick=next(o['id'] for o in opts if o['id'] in ('ok','pass'))
    else:
     pick=next((o['id'] for o in opts if o['id']=='ok' and r.get('selectedCount',0)>0),None)
     if pick is None:pick=next((o['id'] for o in opts if o.get('faceKind')=='evoke'),None)
     if pick is None and name=='gift':pick=next((o['id'] for o in opts if 'gift' in (o['id']+' '+o['label']).lower() and not any(s in o['label'].lower() for s in ['without','not ','decline'])),None)
     if pick is None:pick=next((o['id'] for o in opts if o.get('card',{}).get('name')=='Swords to Plowshares'),None)
     if pick is None:pick=next((o['id'] for o in opts if o.get('id','').startswith('target:') or o.get('card',{}).get('name')=='Grizzly Bears'),None)
     if pick is None:pick=next((o['id'] for o in opts if o['id'] in ('keep','ok','auto','pay','yes')),opts[0]['id'])
    print(name,step,r['kind'],pick,flush=True)
    p.stdin.write(json.dumps({'type':'respond','session':session,'seat':r['seat'],'requestId':r['requestId'],'option':pick})+'\n');p.stdin.flush()
   raise AssertionError('request limit')
 finally:p.kill();p.wait();log.close()
try:
 case('exile','Solitude',[],['Swords to Plowshares'])
 case('hybrid','Deceit',['Island','Island'],[])
 case('gift','Into the Flood Maw',['Island'],[])
 status='PASS';error=None
except Exception:status='FAIL';error=traceback.format_exc()
(O/'result.json').write_text(json.dumps({'status':status,'checks':checks,'error':error,'scope':'Native initial-position fixtures followed by real casting/payment; no resolved-effect claims'},ensure_ascii=False,indent=2),encoding='utf8')
print(status,error or '',flush=True)
if status!='PASS':raise SystemExit(1)
