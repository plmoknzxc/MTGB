"""Native view-projection delta, initial-position fixture, no new spell-effect claim."""
import json,subprocess,queue,threading,time,hashlib,os,traceback
from pathlib import Path
root=Path(__file__).resolve().parents[2];rt=root/'ForgeRuntime';out=root/'.tools/hand-source-tabs-1025';cfg=json.loads((rt/'runtime.json').read_text('utf-8-sig'))
deck={'cards':[{'setCode':'10E','collectorNumber':'368','count':60}],'sideboard':[]}
(out/'audit').mkdir(exist_ok=True)
start={'session':'public-graveyard','actor':1,'seed':4,'firstPlayerSeat':1,'startingLife':20,'auditDir':str(out/'audit'),'decks':[deck,deck],
 'own':{'Graveyard':['Think Twice','Reassembling Skeleton','Grizzly Bears']},'other':{'Graveyard':['Deep Analysis','Zombify']}}
(out/'start.json').write_text(json.dumps(start),encoding='utf8')
checks=[]
def check(value,label):
 checks.append({'pass':bool(value),'label':label});assert value,label
with (out/'native.log').open('w',encoding='utf8') as log,(out/'trace.jsonl').open('w',encoding='utf8') as trace:
 p=subprocess.Popen([str(rt/cfg['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[out/'classes',rt/'classes',rt/'lib/*'])),'mtgb.bridge.StateBranchFixture',str(rt/cfg['assets']),str(out/'start.json')],cwd=rt/cfg['assets'],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
 q=queue.Queue()
 def reader():
  for line in p.stdout:q.put(json.loads(line))
  q.put(None)
 threading.Thread(target=reader,daemon=True).start()
 try:
  for step in range(100):
   m=q.get(timeout=60);assert m is not None,'process ended';trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ['fatal','rejected'],m
   if m['type']!='request':continue
   r=m['request'];views=m['views']
   if views[0]['phase']=='MAIN1' and r['kind']=='InputPassPriority':
    for n,v in enumerate(views,1):
     (out/f'view-{n}.json').write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf8')
     a,b=v['players'];check({c['name'] for c in a['publicPlayableGraveyard']}=={'Think Twice','Reassembling Skeleton'},f'seat{n} sees only player1 flashback/activated graveyard permissions')
     check({c['name'] for c in b['publicPlayableGraveyard']}=={'Deep Analysis'},f'seat{n} sees only player2 flashback permission')
     check(not v['players'][2-n]['hand'],f'seat{n} opponent hand stays private')
     check(all(c['zone']=='Graveyard' and not c['faceDown'] for pl in v['players'] for c in pl['publicPlayableGraveyard']),f'seat{n} exact public zone only')
    check({c['id'] for c in views[0]['players'][0]['publicPlayableGraveyard']}=={c['id'] for c in views[1]['players'][0]['publicPlayableGraveyard']},'both recipients preserve exact identities')
    break
   ids=[o['id'] for o in r['options'] if not o.get('disabled')];pick='ok' if 'ok' in ids else ids[0]
   p.stdin.write(json.dumps({'type':'respond','session':'public-graveyard','seat':r['seat'],'requestId':r['requestId'],'option':pick})+'\n');p.stdin.flush()
  else:raise AssertionError('no main phase')
  status='PASS';error=None
 except BaseException:
  status='FAIL';error=traceback.format_exc()
 finally:
  p.kill();p.wait()
  result={'status':status,'scope':__doc__,'checks':checks,'error':error,'baseRuntimeHash':cfg['runtimeHash'],'bridgeSourceSha256':hashlib.sha256((root/'Tools/ForgeBridge/java/mtgb/bridge/ForgeBridge.java').read_bytes()).hexdigest()}
  (out/'result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8');print(json.dumps(result,ensure_ascii=False))
  if status!='PASS':raise SystemExit(1)
