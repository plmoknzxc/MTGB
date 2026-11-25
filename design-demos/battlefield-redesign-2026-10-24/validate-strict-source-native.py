"""Native priority/affordability eligibility projection and real payment/timing transitions."""
import json, subprocess, queue, threading, time, hashlib, traceback, sys
from pathlib import Path
root=Path(__file__).resolve().parents[2]
rt=root/'ForgeRuntime'; out=root/'.tools/strict-source-1025'; cfg=json.loads((rt/'runtime.json').read_text('utf-8-sig'))
checks=[]
def check(value,label):
    checks.append({'pass':bool(value),'label':label})
    if not value: raise AssertionError(label)
def run_case(name,lands,payment=False):
    folder=out/name;folder.mkdir(exist_ok=True)
    deck={'cards':[{'setCode':'10E','collectorNumber':'368','count':60}],'sideboard':[]}
    spec={'session':name,'actor':1,'seed':4,'firstPlayerSeat':1,'startingLife':20,'lands':lands,'decks':[deck,deck],
          'own':{'Graveyard':['Think Twice','Deep Analysis','Reassembling Skeleton'],'Hand':['Negate']},
          'other':{'Graveyard':['Think Twice','Deep Analysis'],'Hand':['Opt']}}
    if name=='exile':spec['own']['Exile']=['Misthollow Griffin']
    (folder/'start.json').write_text(json.dumps(spec),encoding='utf8')
    log=(folder/'native.log').open('w',encoding='utf8');trace=(folder/'requests.jsonl').open('w',encoding='utf8')
    p=subprocess.Popen([str(rt/cfg['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[out/'classes',rt/'classes',rt/'lib/*'])),
                        'mtgb.bridge.SourceAvailabilityFixture',str(rt/cfg['assets']),str(folder/'start.json')],cwd=rt/cfg['assets'],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
    q=queue.Queue()
    def reader():
        try:
            for line in p.stdout:q.put(json.loads(line))
        finally:q.put(None)
    threading.Thread(target=reader,daemon=True).start()
    first=False;activated=False;steps=0
    try:
        while steps<250:
            m=q.get(timeout=60)
            if m is None:raise AssertionError('native host ended')
            if m['type'] in ('fatal','rejected'):raise AssertionError(m)
            if m['type']!='request':continue
            steps+=1;r=m['request'];views=m['views'];v=views[0]
            trace.write(json.dumps({'request':r,'views':views},ensure_ascii=False)+'\n');trace.flush()
            print(name,steps,r['kind'],r['seat'],v['turn'],v['phase'],[o['id'] for o in r['options']],flush=True)
            options=[o for o in r['options'] if not o.get('disabled')];ids=[o['id'] for o in options]
            pick=None
            priority=r['kind']=='InputPassPriority' and r['seat']==1 and v['phase']=='MAIN1'
            if priority:
                own=v['players'][0];cards={c['name']:c for c in own['graveyardCards']};usable=set(own['usableSourceIds'])
                names={c['name'] for c in own['graveyardCards'] if c['id'] in usable}
                for n,view in enumerate(views):
                    for pl in view['players']:
                        public_ids={c['id'] for owner in view['players'] for z in ('graveyardCards','exileCards') for c in owner.get(z,[]) if not c.get('faceDown')}
                        check(set(pl['usableSourceIds'])<=public_ids,f'{name} view{n+1} eligibility contains outside-hand public IDs only')
                check(set(views[0]['players'][0]['usableSourceIds'])==set(views[1]['players'][0]['usableSourceIds']),name+' exact public identities agree between recipients')
                check(not views[1]['players'][0]['hand'] and not views[0]['players'][1]['hand'],name+' eligibility does not send private hand faces')
                if not first and v['turnSeat']==1:
                    first=True;(folder/'before.json').write_text(json.dumps(m,ensure_ascii=False,indent=2),encoding='utf8')
                    if not lands:
                        check(not usable,'no-mana: having flashback permission is insufficient');return
                    if payment:
                        check(cards['Reassembling Skeleton']['id'] in usable,'payment: native confirms Skeleton activation affordable')
                        check(cards['Deep Analysis']['id'] in usable,'payment: native confirms flashback affordable before spending mana')
                        pick='card:'+str(cards['Reassembling Skeleton']['id']);activated=True
                    else:
                        check({'Think Twice','Deep Analysis','Reassembling Skeleton'}<=names,'own main phase has affordable instant, sorcery and activated graveyard actions')
                        if name=='exile':check(own['exileCards'][0]['id'] in usable,'native cast-from-exile creature affordable in own main phase')
                elif payment and activated:
                    (folder/'after.json').write_text(json.dumps(m,ensure_ascii=False,indent=2),encoding='utf8')
                    check('Deep Analysis' in cards,'payment: unpaid comparison card remains in graveyard')
                    check(cards['Deep Analysis']['id'] not in usable,'payment: spending mana removes other still-permitted graveyard action')
                    return
                elif not payment and first and v['turnSeat']==2:
                    (folder/'after.json').write_text(json.dumps(m,ensure_ascii=False,indent=2),encoding='utf8')
                    check('Think Twice' in names and 'Reassembling Skeleton' in names,'opponent turn still permits affordable instant/activated actions')
                    check('Deep Analysis' not in names,'opponent turn removes sorcery-speed flashback despite permission and mana')
                    if name=='exile':check(own['exileCards'][0]['id'] not in usable,'exile creature disappears when sorcery timing no longer permits casting')
                    return
            if pick is None:
                if r['kind']=='InputPassPriority':pick=next(x for x in ('ok','pass') if x in ids)
                else:pick=next((x for x in ('keep','ok','auto','pay','yes') if x in ids),ids[0])
            assert pick in ids,(pick,ids)
            p.stdin.write(json.dumps({'type':'respond','session':name,'seat':r['seat'],'requestId':r['requestId'],'option':pick})+'\n');p.stdin.flush()
        raise AssertionError('request limit')
    finally:
        p.kill();p.wait();trace.close();log.close()
try:
    if '--exile-only' in sys.argv:run_case('exile',['Island']*3+['Swamp']*2)
    else:
        if '--delta' not in sys.argv:run_case('no-mana',[])
        run_case('payment',['Island','Swamp'],True)
        run_case('timing',['Island']*3+['Swamp']*2)
    status='PASS';error=None
except BaseException:
    status='FAIL';error=traceback.format_exc()
result={'status':status,'scope':__doc__,'checks':checks,'error':error,'baseRuntimeHash':cfg['runtimeHash'],'bridgeSourceSha256':hashlib.sha256((root/'Tools/ForgeBridge/java/mtgb/bridge/ForgeBridge.java').read_bytes()).hexdigest()}
(out/('result-exile.json' if '--exile-only' in sys.argv else 'result-delta.json' if '--delta' in sys.argv else 'result.json')).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps({'status':status,'checks':len(checks),'failed':sum(not c['pass'] for c in checks),'error':error},ensure_ascii=False))
if status!='PASS':raise SystemExit(1)
