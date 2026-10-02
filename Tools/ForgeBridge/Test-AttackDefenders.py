"""Pinned Forge acceptance: per-creature player/planeswalker/battle combat destinations.
Uses a separate Java test entry to arrange the board, not a production config switch.
Never simulates declarations or combat damage. Writes actual wire snapshots for UI replay.
"""
import json, os, queue, subprocess, tempfile, threading, time
from pathlib import Path

root=Path(__file__).resolve().parents[2]
runtime=root/'ForgeRuntime'
cfg=json.loads((runtime/'runtime.json').read_text(encoding='utf-8-sig'))
out=root/'Tools/Validation/Reports/AttackDefenders';out.mkdir(parents=True,exist_ok=True)
result=out/'engine-result.txt';result.write_text('RUNNING',encoding='utf-8')
deck={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}], 'sideboard':[]}
with tempfile.TemporaryDirectory(prefix='mtgb-attack-') as temp, (out/'engine.log').open('w',encoding='utf-8') as log, (out/'trace.jsonl').open('w',encoding='utf-8') as trace:
    start=Path(temp)/'start.json'
    start.write_text(json.dumps({'session':'attack-test','seed':4,'startingLife':20,'firstPlayerSeat':1,'decks':[deck,deck]}),encoding='utf-8')
    cp=str(root/'.tools/attack-defenders/classes')+';'+str(runtime/'classes')+';'+str(runtime/'lib/*')
    p=subprocess.Popen([str(runtime/cfg['java']),'-Djava.awt.headless=true','-cp',cp,'mtgb.bridge.AttackDefenderFixture',str(runtime/cfg['assets']),str(start)],cwd=runtime/cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf-8')
    messages=queue.Queue();threading.Thread(target=lambda:[messages.put(json.loads(line)) for line in p.stdout],daemon=True).start()
    def read():
        m=messages.get(timeout=25);trace.write(json.dumps(m)+'\n');trace.flush()
        assert m['type']!='fatal',m
        return m
    def request():
        while True:
            m=read()
            if m['type']=='request':return m
            assert m['type'] not in ('rejected','ended'),m
    def send(m,option,seat=None):
        r=m['request'];p.stdin.write(json.dumps({'type':'respond','session':'attack-test','seat':r['seat'] if seat is None else seat,'requestId':r['requestId'],'option':option})+'\n');p.stdin.flush()
    def respond(m,option):
        assert option in [o['id'] for o in m['request']['options']],option
        send(m,option);return request()
    def reject(m,option,error,seat=None):
        send(m,option,seat);reply=read()
        assert reply['type']=='rejected' and reply['error']==error,reply
    def snapshot(m,name):
        v=m['views'][0];v['request']=m['request'];v['awaitingInput']=m['request']['seat']==1
        (out/(name+'.json')).write_text(json.dumps(v),encoding='utf-8')
    def field(m,seat):return m['views'][0]['players'][seat-1]['battlefield']
    def assigned(m,attacker,kind=None,defender=None):
        c=next(c for c in field(m,1) if c['id']==attacker)
        assert c['attacking']==(kind is not None),c
        if kind:assert (c['defenderKind'],c['defenderId'])==(kind,defender),c
    try:
        m=request();deadline=time.monotonic()+90
        while m['request']['kind']!='InputAttack':
            assert time.monotonic()<deadline,'No declaration phase'
            m=respond(m,'ok')
        assert m['request']['seat']==1
        one,two=field(m,1),field(m,2)
        attackers=[c['id'] for c in one if c['name']=='Goblin Piker']
        a,b,c=attackers
        walker=next(c['id'] for c in two if c['planeswalker'])
        battle=next(c['id'] for c in one if c['battle'])
        player=m['views'][0]['players'][1]['targetId']
        allowed={('player',player),('card',walker),('card',battle)}
        pairs=[o for o in m['request']['options'] if o['id'].startswith('attack:')]
        assert len(pairs)==9,pairs
        assert {(o['defenderKind'],o['defenderId']) for o in pairs}==allowed
        assert {o['attackerId'] for o in pairs}==set(attackers)
        snapshot(m,'initial')
        reject(m,'attack:'+str(a)+':card:999999','invalid-option')
        valid=f'attack:{a}:player:{player}'
        reject(m,valid,'wrong-seat',2)
        previous=m;m=respond(m,valid);assigned(m,a,'player',player)
        reject(previous,valid,'stale-request');republished=read()
        assert republished['type']=='request' and republished['views']==m['views'],'Duplicate changed state'
        m=respond(m,f'attack:{a}:card:{walker}');assigned(m,a,'card',walker)
        m=respond(m,f'attack:{a}:card:{walker}');assigned(m,a,'card',walker)
        m=respond(m,f'attack-remove:{a}');assigned(m,a)
        m=respond(m,valid);assigned(m,a,'player',player)
        snapshot(m,'one-assigned')
        m=respond(m,f'attack:{b}:card:{walker}');assigned(m,b,'card',walker)
        m=respond(m,f'attack:{c}:card:{battle}');assigned(m,c,'card',battle)
        assigned(m,a,'player',player);assigned(m,b,'card',walker)
        snapshot(m,'all-assigned')
        m=respond(m,'ok');passes=set()
        while True:
            v=m['views'][0]
            if v['players'][1]['life']==18:
                assert next(c for c in field(m,2) if c['id']==walker)['loyalty']==3
                assert next(c for c in field(m,1) if c['id']==battle)['defense']==3
                assert next(c for c in field(m,1) if c['planeswalker'])['loyalty']==5
                assert next(c for c in field(m,2) if c['battle'])['defense']==5
                assert passes=={1},passes
                assert '[auto-pass] seat=2' in (out/'engine.log').read_text(encoding='utf-8')
                snapshot(m,'resolved');break
            if m['request']['kind']=='InputPassPriority':passes.add(m['request']['seat'])
            assert time.monotonic()<deadline,'Combat damage timeout'
            m=respond(m,'ok')
        text='PASS: 3 attackers independently assigned to player/planeswalker/own battle protected by opponent; reassign, same-destination idempotency, recall; invalid/duplicate/stale/wrong-seat rejected; defender creature/friendly planeswalker/self-protected battle excluded; both seats pass; real combat life 20->18, loyalty 5->3, defense 5->3, other counters unchanged.'
        result.write_text(text,encoding='utf-8');print(text)
    except Exception as error:
        result.write_text('FAIL: '+repr(error),encoding='utf-8');raise
    finally:
        if p.poll() is None:
            p.stdin.write('{"type":"shutdown"}\n');p.stdin.flush()
            try:p.wait(timeout=3)
            except subprocess.TimeoutExpired:p.kill();p.wait()
        (out/'process-result.txt').write_text(f'Child exited: pid={p.pid}, exit={p.returncode}',encoding='utf-8')
